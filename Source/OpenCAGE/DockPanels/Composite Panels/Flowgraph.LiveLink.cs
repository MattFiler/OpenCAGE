using CATHODE.Scripting;
using OpenCAGE.RuntimeUtilsConnection;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace OpenCAGE
{
    /* Live link "Show Activity": the page lights up the links the running game uses, as its composite display's
       LiveLinkActivity has them (see LiveLinkTrace), and shows beside the pins how often each output fired ("name [n]")
       and each method was called ("[n] name"). The node editor draws them (ConnectionActivityProvider,
       OptionBadgeProvider); the page says how active each line is and what each pin's badge reads, and keeps the glowing
       lines moving - a timer that runs only while something on the page glows and the page is on screen, repainting just
       the glowing lines. Badges are worked out at most a couple of times a second, and only the nodes whose badges
       changed are repainted. A hidden page does nothing, and catches up when it is shown. */
    public partial class Flowgraph
    {
        private LiveLinkActivity _liveLinkActivity;
        private System.Windows.Forms.Timer _activityTimer;
        //When a link drawn on this page was last used (LiveLinkTrace.NowMs): something here glows until GlowMilliseconds after
        private double _activityPageLastMs = double.NegativeInfinity;
        //Lines here have glowed since the last full repaint: once they stop, one more (see OnActivityTick)
        private bool _activityLinesGlowed;
        //When the timer last ticked (LiveLinkTrace.NowMs)
        private double _activityTickMs;
        //What repainting the glowing lines takes, in milliseconds (an average over the last few ticks): sets the timer's pace
        private double _activityPaintMs;

        //The badges the node editor draws beside the pins, as last worked out from the display's counts
        private Dictionary<STNodeOption, OptionBadge> _activityBadges = new Dictionary<STNodeOption, OptionBadge>();
        //Counts went up since then
        private bool _activityBadgesStale;
        //A badge shows bright: it goes muted a moment after its pin was last used
        private bool _activityBadgesBright;
        private double _activityBadgesAtMs = double.NegativeInfinity;

        /// <summary>The colour a data link lights up in (logic links keep the node editor's orange).</summary>
        public static readonly Color ActivityDataColor = Color.FromArgb(0, 215, 255);

        //The glowing lines' dashes march this fast, in canvas pixels at 100% a second (the pattern repeats every 12), and
        //never further than a quarter of the pattern in one step: a bigger step reads as flicker, not movement
        private const float ActivityPhaseSpeed = 45f;
        private const float ActivityPhasePeriod = 12f;
        private const float ActivityPhaseMaxStep = 3f;
        //Asked for 50 ms, a WinForms timer ticks about every 62 ms (16 a second): a glowing page's repaints are the cost.
        //A page whose glowing lines take long to paint (many of them, long, across the view) ticks less often, so that
        //painting them takes about this share of the time, down to a tick every ActivityMaxTickMs
        private const int ActivityTickMs = 50;
        private const int ActivityMaxTickMs = 200;
        private const double ActivityPaintBudget = 0.1;
        //Badges are worked out again at most this often: each one that changes repaints its whole node
        private const double ActivityBadgeRefreshMs = 450;
        //Canvas pixels round a node repainted for its badges (a badge's chip reaches a little past its row)
        private const int ActivityBadgeMargin = 4;

        /// <summary>Show this display's activity on the page (while Activity is on). UI thread.</summary>
        internal void AttachLiveLinkActivity(LiveLinkActivity activity)
        {
            if (activity == null || activity == _liveLinkActivity)
                return;
            DetachLiveLinkActivity();
            _liveLinkActivity = activity;
            activity.Changed += OnLiveLinkActivityChanged;
            activity.Cleared += OnLiveLinkActivityCleared;
            LiveLinkTrace.ShowActivityChanged += ApplyLiveLinkActivityProvider;
            VisibleChanged += OnLiveLinkActivityVisibleChanged;
            FormClosed += OnLiveLinkActivityFormClosed;
            //A page disposed without closing must not be kept alive by the display's events (or the static one)
            Disposed += OnLiveLinkActivityFormClosed;
            ApplyLiveLinkActivityProvider();
        }

        private void DetachLiveLinkActivity()
        {
            StopActivityTimer();
            if (_liveLinkActivity != null)
            {
                _liveLinkActivity.Changed -= OnLiveLinkActivityChanged;
                _liveLinkActivity.Cleared -= OnLiveLinkActivityCleared;
                _liveLinkActivity = null;
            }
            LiveLinkTrace.ShowActivityChanged -= ApplyLiveLinkActivityProvider;
            VisibleChanged -= OnLiveLinkActivityVisibleChanged;
            FormClosed -= OnLiveLinkActivityFormClosed;
            Disposed -= OnLiveLinkActivityFormClosed;
            ForgetActivityBadges();
            if (stNodeEditor1 != null && !stNodeEditor1.IsDisposed)
            {
                stNodeEditor1.ConnectionActivityProvider = null;
                stNodeEditor1.OptionBadgeProvider = null;
            }
        }

        private void OnLiveLinkActivityFormClosed(object sender, EventArgs e)
        {
            DetachLiveLinkActivity();
            _activityTimer?.Dispose();
            _activityTimer = null;
        }

        /// <summary>The node editor asks while Activity is on, or not at all.</summary>
        private void ApplyLiveLinkActivityProvider()
        {
            if (IsDisposed || stNodeEditor1 == null)
                return;
            bool on = _liveLinkActivity != null && LiveLinkTrace.ShowActivity;
            if (on == (stNodeEditor1.ConnectionActivityProvider != null))
                return;
            stNodeEditor1.ConnectionActivityProvider = on ? (Func<STNodeOption, STNodeOption, ConnectionActivity>)ActivityOf : null;
            stNodeEditor1.OptionBadgeProvider = on ? (Func<STNodeOption, OptionBadge>)BadgeOf : null;
            if (on)
            {
                RefreshActivityBadges(false);
                RefreshActivityGlow();
            }
            else
            {
                StopActivityTimer();
                ForgetActivityBadges();
            }
            stNodeEditor1.Invalidate();
        }

        /// <summary>
        /// How active a drawn line is. Lines are drawn from both ends (data links twice): only the end CathodeLib keeps the
        /// link on answers - an output (logic link), or a top pin (data link: its value flows up from the bottom pin when
        /// the top one reads it, down from the top pin when it sends it out). Runs for every line on every paint: a lookup,
        /// nothing more.
        /// </summary>
        private ConnectionActivity ActivityOf(STNodeOption from, STNodeOption to)
        {
            LiveLinkActivity activity = _liveLinkActivity;
            if (activity == null || activity.Count == 0 || from == null || to == null)
                return ConnectionActivity.None;
            bool data = from.Location == PinLocation.Top;
            if (!data && from.Location != PinLocation.Right)
                return ConnectionActivity.None;
            if (from.Owner?.Entity == null || to.Owner?.Entity == null)
                return ConnectionActivity.None;
            LinkKey key = new LinkKey(from.Owner.Entity.shortGUID.AsUInt32, from.ShortGUID.AsUInt32, to.Owner.Entity.shortGUID.AsUInt32, to.ShortGUID.AsUInt32);
            if (!activity.TryGet(key, out LinkActivity link))
                return ConnectionActivity.None;
            return new ConnectionActivity(LiveLinkActivity.GlowOf(link, LiveLinkTrace.NowMs), true, data && !link.Write, data ? ActivityDataColor : Color.Empty);
        }

        /// <summary>
        /// The page's nodes were made afresh (the page shown again, or a node put back by an undo): the badges, kept by pin,
        /// are worked out again for the new pins. The caller repaints. UI thread.
        /// </summary>
        private void RefreshActivityBadgesForNewNodes()
        {
            if (_liveLinkActivity != null && stNodeEditor1.OptionBadgeProvider != null)
                RefreshActivityBadges(false);
        }

        /// <summary>The badge beside a left or right pin, as last worked out (RefreshActivityBadges). Runs for every such pin on every paint.</summary>
        private OptionBadge BadgeOf(STNodeOption option)
        {
            if (option != null && _activityBadges.Count != 0 && _activityBadges.TryGetValue(option, out OptionBadge badge))
                return badge;
            return OptionBadge.None;
        }

        /// <summary>
        /// Work the badges out afresh from the display's counts: an output's beside it on the right ("name [n]" - how often it
        /// fired), a method's on the left ("[n] name" - how often it was called), bright for a moment after each use. With
        /// repaint, each node whose badges changed is repainted (a node at a time: the rest of the page is left alone).
        /// </summary>
        private void RefreshActivityBadges(bool repaint)
        {
            double now = LiveLinkTrace.NowMs;
            _activityBadgesAtMs = now;
            _activityBadgesStale = false;
            bool bright = false;
            Dictionary<STNodeOption, OptionBadge> badges = new Dictionary<STNodeOption, OptionBadge>();
            LiveLinkActivity activity = _liveLinkActivity;
            if (activity != null && activity.PinCount != 0 && stNodeEditor1.OptionBadgeProvider != null)
            {
                foreach (STNode node in stNodeEditor1.Nodes)
                {
                    if (node?.Entity == null)
                        continue;
                    uint id = node.Entity.shortGUID.AsUInt32;
                    //A variable is one of the composite's own pins, with a single pin of its own: it shows how it was used on
                    //whichever side it is drawn
                    bool either = node.Entity.variant == EntityVariant.VARIABLE;
                    bright |= AddActivityBadges(activity, node.GetInputOptions(), id, PinUse.Called, either, now, badges);
                    bright |= AddActivityBadges(activity, node.GetOutputOptions(), id, PinUse.Fired, either, now, badges);
                }
            }

            HashSet<STNode> changed = null;
            if (repaint)
            {
                changed = new HashSet<STNode>();
                foreach (KeyValuePair<STNodeOption, OptionBadge> pair in badges)
                {
                    if (!_activityBadges.TryGetValue(pair.Key, out OptionBadge was) || !SameBadge(was, pair.Value))
                        changed.Add(pair.Key.Owner);
                }
                foreach (STNodeOption option in _activityBadges.Keys)
                {
                    if (!badges.ContainsKey(option))
                        changed.Add(option.Owner);
                }
            }
            _activityBadges = badges;
            _activityBadgesBright = bright;
            if (changed != null)
            {
                //Just the node itself, where its badges are drawn (STNode.Invalidate takes in a wide margin round it), and
                //only when it is on screen: one off it draws its badges afresh whenever it is scrolled into view
                Rectangle view = stNodeEditor1.ClientRectangle;
                foreach (STNode node in changed)
                {
                    if (node == null || node.Owner != stNodeEditor1)
                        continue;
                    Rectangle area = node.Rectangle;
                    area.Inflate(ActivityBadgeMargin, ActivityBadgeMargin);
                    area = stNodeEditor1.CanvasToControl(area);
                    area.Inflate(2, 2);
                    if (area.IntersectsWith(view))
                        stNodeEditor1.Invalidate(area);
                }
            }
        }

        private static bool AddActivityBadges(LiveLinkActivity activity, STNodeOption[] options, uint id, PinUse use, bool either, double now, Dictionary<STNodeOption, OptionBadge> into)
        {
            if (options == null)
                return false;
            PinUse other = use == PinUse.Fired ? PinUse.Called : PinUse.Fired;
            bool bright = false;
            foreach (STNodeOption option in options)
            {
                if (option == null || option == STNodeOption.Empty)
                    continue;
                uint pin = option.ShortGUID.AsUInt32;
                if (!activity.TryGet(new PinKey(id, pin, use), out PinActivity used) && !(either && activity.TryGet(new PinKey(id, pin, other), out used)))
                    continue;
                bool glowing = LiveLinkActivity.IsGlowing(used, now);
                bright |= glowing;
                into[option] = new OptionBadge(LiveLinkActivity.CountText(used.Count), glowing ? 1f : 0f, Color.Empty);
            }
            return bright;
        }

        private static bool SameBadge(OptionBadge a, OptionBadge b)
        {
            return a.Text == b.Text && (a.Glow > 0f) == (b.Glow > 0f) && a.Color == b.Color;
        }

        private void ForgetActivityBadges()
        {
            if (_activityBadges.Count != 0)
                _activityBadges = new Dictionary<STNodeOption, OptionBadge>();
            _activityBadgesStale = false;
            _activityBadgesBright = false;
        }

        /// <summary>Every link drawn on the page, as CathodeLib keeps it (the way SaveAndCompile reads them).</summary>
        private IEnumerable<LinkKey> PageLinks()
        {
            foreach (STNode node in stNodeEditor1.Nodes)
            {
                if (node?.Entity == null)
                    continue;
                uint owner = node.Entity.shortGUID.AsUInt32;
                foreach (STNodeOption[] options in new[] { node.GetOutputOptions(), node.GetTopOptions() })
                {
                    if (options == null)
                        continue;
                    foreach (STNodeOption option in options)
                    {
                        if (option == null || option == STNodeOption.Empty)
                            continue;
                        List<STNodeOption> connected = option.GetConnectedOption();
                        if (connected == null)
                            continue;
                        foreach (STNodeOption other in connected)
                        {
                            if (other?.Owner?.Entity != null)
                                yield return new LinkKey(owner, option.ShortGUID.AsUInt32, other.Owner.Entity.shortGUID.AsUInt32, other.ShortGUID.AsUInt32);
                        }
                    }
                }
            }
        }

        private void OnLiveLinkActivityChanged(LiveLinkActivity.Change change)
        {
            if (IsDisposed || !Visible || _liveLinkActivity == null || stNodeEditor1.ConnectionActivityProvider == null)
                return;
            bool redraw = false;
            if (change.Repaint.Count != 0 || change.Glowing.Count != 0)
            {
                double last = _activityPageLastMs;
                foreach (LinkKey key in PageLinks())
                {
                    if (change.Repaint.Contains(key))
                        redraw = true;
                    if (change.Glowing.Contains(key) && _liveLinkActivity.TryGet(key, out LinkActivity link) && link.LastMs > last)
                        last = link.LastMs;
                }
                _activityPageLastMs = last;
            }
            //A line that starts glowing, or is lit for the first time, is outside the area the timer repaints
            if (redraw)
                stNodeEditor1.Invalidate();
            if (change.Pins)
            {
                _activityBadgesStale = true;
                if (LiveLinkTrace.NowMs - _activityBadgesAtMs >= ActivityBadgeRefreshMs)
                    RefreshActivityBadges(!redraw);
            }
            if (ActivityWantsTicks())
                StartActivityTimer();
        }

        private void OnLiveLinkActivityCleared()
        {
            StopActivityTimer();
            _activityPageLastMs = double.NegativeInfinity;
            _activityLinesGlowed = false;
            ForgetActivityBadges();
            if (!IsDisposed && Visible)
                stNodeEditor1.Invalidate();
        }

        private void OnLiveLinkActivityVisibleChanged(object sender, EventArgs e)
        {
            //Shown, it paints in full anyway: only what glows on it, and its badges, are worked out again
            if (Visible)
            {
                if (stNodeEditor1.ConnectionActivityProvider != null)
                    RefreshActivityBadges(false);
                RefreshActivityGlow();
            }
            else
                StopActivityTimer();
        }

        /// <summary>Work out when a link on the page was last used, and keep the glow moving if that was a moment ago.</summary>
        private void RefreshActivityGlow()
        {
            double last = double.NegativeInfinity;
            if (_liveLinkActivity != null && _liveLinkActivity.Count != 0)
            {
                foreach (LinkKey key in PageLinks())
                {
                    if (_liveLinkActivity.TryGet(key, out LinkActivity link) && link.LastMs > last)
                        last = link.LastMs;
                }
            }
            _activityPageLastMs = last;
            if (Visible && stNodeEditor1.ConnectionActivityProvider != null && ActivityWantsTicks())
                StartActivityTimer();
        }

        //Lines glow, or badges are to be worked out again (counts went up, or a bright one is to go muted)
        private bool ActivityWantsTicks()
        {
            return LiveLinkTrace.NowMs - _activityPageLastMs < LiveLinkTrace.GlowMilliseconds || _activityBadgesStale || _activityBadgesBright;
        }

        private void StartActivityTimer()
        {
            if (_activityTimer == null)
            {
                _activityTimer = new System.Windows.Forms.Timer() { Interval = ActivityTickMs };
                _activityTimer.Tick += OnActivityTick;
            }
            if (!_activityTimer.Enabled)
            {
                _activityTickMs = LiveLinkTrace.NowMs;
                _activityTimer.Start();
            }
        }

        private void StopActivityTimer()
        {
            _activityTimer?.Stop();
        }

        private void OnActivityTick(object sender, EventArgs e)
        {
            if (IsDisposed || !Visible || stNodeEditor1.ConnectionActivityProvider == null)
            {
                StopActivityTimer();
                return;
            }
            double now = LiveLinkTrace.NowMs;
            double elapsed = Math.Max(0.0, Math.Min(250.0, now - _activityTickMs));
            _activityTickMs = now;

            bool glowing = now - _activityPageLastMs < LiveLinkTrace.GlowMilliseconds;
            if (glowing)
            {
                //The dashes march by the time gone (the timer's real rate is not its interval); only the lines that glowed at
                //the last paint are repainted (the node editor works them out as it paints)
                float step = Math.Min(ActivityPhaseMaxStep, (float)(ActivityPhaseSpeed * elapsed / 1000.0));
                stNodeEditor1.ActivityPhase = (stNodeEditor1.ActivityPhase + step) % ActivityPhasePeriod;
                if (stNodeEditor1.InvalidateActivity())
                {
                    //Painted now rather than when the message comes, to see what it costs, and the next tick waits
                    //accordingly
                    long started = System.Diagnostics.Stopwatch.GetTimestamp();
                    stNodeEditor1.Update();
                    double paintMs = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    _activityPaintMs = _activityPaintMs <= 0.0 ? paintMs : _activityPaintMs * 0.75 + paintMs * 0.25;
                    int interval = (int)Math.Max(ActivityTickMs, Math.Min(ActivityMaxTickMs, _activityPaintMs / ActivityPaintBudget));
                    if (Math.Abs(interval - _activityTimer.Interval) >= 10)
                        _activityTimer.Interval = interval;
                }
                _activityLinesGlowed = true;
            }
            else if (_activityLinesGlowed)
            {
                //The last glow here has faded: one full repaint leaves those lines lit only (a tick's repaint keeps a line's
                //last pixels at a pin, under the node's border, as they were)
                _activityLinesGlowed = false;
                stNodeEditor1.Invalidate();
            }

            if ((_activityBadgesStale || _activityBadgesBright) && now - _activityBadgesAtMs >= ActivityBadgeRefreshMs)
                RefreshActivityBadges(true);

            if (!glowing && !_activityLinesGlowed && !_activityBadgesStale && !_activityBadgesBright)
                StopActivityTimer();
        }
    }
}
