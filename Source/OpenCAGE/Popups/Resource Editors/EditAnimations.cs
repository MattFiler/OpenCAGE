using AlienPAK;
using CATHODE;
using CathodeLib;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Browse everything in ANIMATION.PAK the way the game asks for it: pick the half of the system
    /// you're after, then a skeleton, then the context it's playing in, and the clips that leaves.
    /// Clips can be previewed on a mesh and exported.
    ///
    /// The same window doubles as the picker for animation parameters, which otherwise get the plain
    /// enum-string list - here you can see what a clip is called, how long it runs and what it fires
    /// before committing to it.
    /// </summary>
    public partial class EditAnimations : BaseWindow
    {
        /// <summary>What the window is being used to choose, if anything.</summary>
        public enum PickMode
        {
            /// <summary>Just browsing.</summary>
            None,

            /// <summary>Choosing one animation, handing back the name a set plays it by.</summary>
            Animation,

            /// <summary>Choosing an animation set, handing back its name.</summary>
            AnimationSet,
        }

        /// <summary>Raised when the user picks something, with the value the parameter wants.</summary>
        public Action<string> OnPicked;

        private CathodeLib.Animation _animations;
        private CathodeLib.Animation.AnimationSet _set;
        private CathodeLib.Animation.AnimationContext _context;
        private AnimationPreview _preview;

        private readonly PickMode _picking;
        private readonly string _startingSet;
        private readonly string _startingAnimation;

        //refilling either list on every keystroke is enough to feel it over 400 sets, so it waits
        private readonly Timer _searchDelay = new Timer { Interval = 250 };
        private bool _filling;

        private readonly ColumnOrder _setOrder = new ColumnOrder { GroupingLast = true };
        private readonly ColumnOrder _clipOrder = new ColumnOrder();

        /// <summary>
        /// Open the browser, or a picker for an animation parameter. Pass what the entity is already
        /// set to and the window opens on it, with the animation itself scrolled to and selected.
        /// </summary>
        public EditAnimations(PickMode picking = PickMode.None, string startingSet = null, string startingAnimation = null) : base()
        {
            _picking = picking;
            _startingSet = startingSet;
            _startingAnimation = startingAnimation;
            InitializeComponent();
            Icon = SharedFormIcon.Icon;

            if (_picking != PickMode.None)
            {
                Text = _picking == PickMode.AnimationSet ? "Choose an animation set" : "Choose an animation";
                pickBtn.Visible = true;
                pickBtn.Text = _picking == PickMode.AnimationSet ? "Use This Set" : "Use This Animation";
                pickBtn.Click += PickBtn_Click;
                StayAboveEditor = true;
            }

            setList.Columns.Add("Animation set", 300);
            setList.Columns.Add("Skeleton", 260);
            setList.Columns.Add("Contexts", 70, HorizontalAlignment.Right);
            setList.Columns.Add("Animations", 90, HorizontalAlignment.Right);

            clipList.Columns.Add("Animation", 230);
            clipList.Columns.Add("File", 200);
            clipList.Columns.Add("Authored on", 140);
            clipList.Columns.Add("Frames", 60, HorizontalAlignment.Right);
            clipList.Columns.Add("Length", 65, HorizontalAlignment.Right);
            clipList.Columns.Add("Properties", 110);
            clipList.Columns.Add("Notes", 110);
            clipList.MultiSelect = true;

            Sortable(setList, _setOrder);
            Sortable(clipList, _clipOrder);

            tabKinds.SelectedIndexChanged += (s, e) => TabChanged();
            setList.SelectedIndexChanged += (s, e) => SetChanged();
            contextBox.SelectedIndexChanged += (s, e) => { if (!_filling) { _context = SelectedContext(); FillClips(); } };
            clipList.SelectedIndexChanged += (s, e) => ShowSelection();

            //double click does whatever the window is for: pick it, or open the preview
            clipList.DoubleClick += (s, e) =>
            {
                if (_picking == PickMode.Animation) { if (pickBtn.Enabled) pickBtn.PerformClick(); }
                else if (previewBtn.Enabled) previewBtn.PerformClick();
            };
            previewBtn.Click += PreviewBtn_Click;
            exportBtn.Click += ExportBtn_Click;
            importBtn.Click += ImportBtn_Click;

            _searchDelay.Tick += (s, e) => { _searchDelay.Stop(); FillSets(); };
            setSearchBox.TextChanged += (s, e) => { _searchDelay.Stop(); _searchDelay.Start(); };
            clipSearchBox.TextChanged += (s, e) => FillClips();

            splitLists.SplitterMoved += (s, e) => SettingsManager.SetInteger(Settings.AnimationEditorSplitter, splitLists.SplitterDistance);

            Load += EditAnimations_Load;
            FormClosed += (s, e) => _searchDelay.Dispose();
        }

        private void EditAnimations_Load(object sender, EventArgs e)
        {
            statusLabel.Text = "Loading ANIMATION.PAK...";
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                _animations = Singleton.Animations;
            }
            catch (Exception ex)
            {
                statusLabel.Text = "ANIMATION.PAK could not be read.";
                MessageBox.Show(ex.Message, "Failed to load animations", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally { Cursor.Current = Cursors.Default; }

            if (_animations == null || !_animations.Loaded && _animations.Sets.Count == 0)
            {
                statusLabel.Text = "ANIMATION.PAK could not be read.";
                return;
            }

            _filling = true;
            tabKinds.SelectedIndex = StartingTab();
            _filling = false;
            ShowContent();
            RestoreSplitter();

            FillSets();
            SelectStartingSet();

            statusLabel.Text = _animations.Sets.Count + " animation sets, "
                + _animations.Sets.Sum(x => x.ClipCount).ToString("N0") + " clips"
                + (_animations.Failures.Count == 0 ? "" : "  (" + _animations.Failures.Count + " files could not be read)");
        }

        /* Open on the half of the system the starting set belongs to, or wherever we were left. */
        private int StartingTab()
        {
            CathodeLib.Animation.AnimationSet set = string.IsNullOrEmpty(_startingSet) ? null
                : _animations.Sets.FirstOrDefault(x => string.Equals(x.Name, _startingSet, StringComparison.OrdinalIgnoreCase));
            if (set != null) return set.Kind == CathodeLib.Animation.AnimationKind.Character ? 0 : 1;

            return SettingsManager.GetInteger(Settings.AnimationEditorTab, 0) == 1 ? 1 : 0;
        }

        private void RestoreSplitter()
        {
            int saved = SettingsManager.GetInteger(Settings.AnimationEditorSplitter, -1);
            if (saved >= splitLists.Panel1MinSize && saved <= splitLists.Height - splitLists.Panel2MinSize)
                splitLists.SplitterDistance = saved;
        }

        #region SORTING
        /* Click a heading to sort by it, click again to turn it round. Both lists have columns
         * people will want to order by - longest animation, most events, which rig it came from. */
        private static void Sortable(ListView list, ColumnOrder order)
        {
            list.ColumnClick += (s, e) =>
            {
                order.Descending = order.Column == e.Column && !order.Descending;
                order.Column = e.Column;
                Sort(list, order);
            };
        }

        private static void Sort(ListView list, ColumnOrder order)
        {
            /* A live sorter re-sorts on every insert, which turns filling the list into a quadratic
             * crawl - so it only goes on once everything is in. */
            list.ListViewItemSorter = order;
            list.Sort();
            list.ListViewItemSorter = null;
        }

        /// <summary>
        /// Sorts names, keeping anything beginning with '#' at the bottom.
        ///
        /// Those aren't characters - they're the game's own grouping entries - and sorted on
        /// punctuation they land at the very top, pushing every set anyone actually wants off screen.
        /// </summary>
        private class SetNameOrder : IComparer<string>
        {
            public static readonly SetNameOrder Instance = new SetNameOrder();

            public int Compare(string x, string y)
            {
                return CompareNames(x, y);
            }
        }

        private static int CompareNames(string left, string right)
        {
            bool leftGrouping = left != null && left.StartsWith("#", StringComparison.Ordinal);
            bool rightGrouping = right != null && right.StartsWith("#", StringComparison.Ordinal);
            if (leftGrouping != rightGrouping)
                return leftGrouping ? 1 : -1;

            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Orders a list by one column, treating a column of numbers as numbers.</summary>
        private class ColumnOrder : System.Collections.IComparer
        {
            public int Column = -1;
            public bool Descending;

            /// <summary>Keep the '#' grouping entries at the bottom whichever way the column is sorted.</summary>
            public bool GroupingLast;

            public int Compare(object x, object y)
            {
                string left = Text(x), right = Text(y);

                if (GroupingLast)
                {
                    bool leftGrouping = left.StartsWith("#", StringComparison.Ordinal);
                    bool rightGrouping = right.StartsWith("#", StringComparison.Ordinal);
                    if (leftGrouping != rightGrouping)
                        return leftGrouping ? 1 : -1;
                }

                int order = Number(left, out double a) && Number(right, out double b)
                    ? a.CompareTo(b)
                    : string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
                return Descending ? -order : order;
            }

            private string Text(object item)
            {
                ListViewItem row = item as ListViewItem;
                if (row == null || Column < 0 || Column >= row.SubItems.Count) return "";
                return row.SubItems[Column].Text;
            }

            /* "1.27s", "4,641" and "12 events" all sort as the number in them. */
            private static bool Number(string text, out double value)
            {
                value = 0;
                if (text.Length == 0) return false;

                int end = 0;
                while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '.' || text[end] == ',' || text[end] == '-')) end++;
                if (end == 0) return false;

                return double.TryParse(text.Substring(0, end).Replace(",", ""),
                    System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value);
            }
        }
        #endregion

        #region TABS
        /* The two halves of the animation system are the same window with a different set list, so
         * one set of controls moves between the pages rather than being built twice. */
        private void ShowContent()
        {
            TabPage page = tabKinds.SelectedTab ?? tabCharacters;
            if (contentPanel.Parent == page) return;

            page.Controls.Add(contentPanel);
            contentPanel.BringToFront();
        }

        private void TabChanged()
        {
            ShowContent();
            if (_filling || _animations == null) return;

            SettingsManager.SetInteger(Settings.AnimationEditorTab, tabKinds.SelectedIndex);
            FillSets();
        }

        /// <summary>Whether the environment tab is the one showing.</summary>
        private bool ShowingEnvironment { get { return tabKinds.SelectedIndex == 1; } }

        /* Anything that isn't a character is a prop as far as this window is concerned - the handful
         * of sets the PAK leaves unclassified are all props with no clips left in them. */
        private bool BelongsHere(CathodeLib.Animation.AnimationSet set)
        {
            bool character = set.Kind == CathodeLib.Animation.AnimationKind.Character;
            return character != ShowingEnvironment;
        }
        #endregion

        #region SETS
        /* One row per set. Search matches the set name, its rig, and the names of the clips inside -
         * so looking for "reload" finds the sets that have one even though no set is called that. */
        private void FillSets()
        {
            if (_animations == null) return;

            string search = setSearchBox.Text.Trim();
            CathodeLib.Animation.AnimationSet previous = _set;

            _filling = true;
            setList.BeginUpdate();
            setList.Items.Clear();

            List<CathodeLib.Animation.AnimationSet> candidates = _animations.Sets
                .Where(BelongsHere)
                .OrderBy(x => x.Name, SetNameOrder.Instance)
                .ToList();

            int total = candidates.Count;

            /* Name and rig first. Nearly every character set holds a clip called "reload" or "walk",
             * so matching the clips inside by default returns a third of the list for almost any
             * word typed - which reads as the box not filtering at all. Widen to the contents only
             * when nothing is actually named that, and say so, so a search can still find the sets
             * that have a "reload" in them without burying the one called "FEMALE". */
            List<CathodeLib.Animation.AnimationSet> listed = candidates;
            bool widened = false;
            if (search.Length != 0)
            {
                listed = candidates.Where(x => NameMatches(x, search)).ToList();
                if (listed.Count == 0)
                {
                    listed = candidates.Where(x => SetMatches(x, search)).ToList();
                    widened = listed.Count != 0;
                }
            }

            bool ownRig = true;
            foreach (CathodeLib.Animation.AnimationSet set in listed)
            {
                ListViewItem item = new ListViewItem(set.Name) { Tag = set };
                item.SubItems.Add(set.Skeleton.Length == 0 ? "-" : set.Skeleton);
                item.SubItems.Add(set.Contexts.Count(x => x.Clips.Count != 0).ToString());
                item.SubItems.Add(set.ClipCount.ToString("N0"));
                if (set.ClipCount == 0) item.ForeColor = Color.Gray;
                setList.Items.Add(item);

                if (set.Skeleton.Length != 0 && !string.Equals(set.Name, set.Skeleton, StringComparison.OrdinalIgnoreCase)) ownRig = false;
            }

            /* Most sets are named after their own rig. If every one listed is, the column says
             * nothing twice - hand its width to the names, which are long enough to need it. */
            setList.Columns[0].Width = ownRig ? 560 : 300;
            setList.Columns[1].Width = ownRig ? 0 : 260;

            if (_setOrder.Column >= 0) Sort(setList, _setOrder);
            setList.EndUpdate();
            _filling = false;

            setSearchLabel.Text = widened
                ? setList.Items.Count + " with a matching animation:"
                : setList.Items.Count == total ? "Find:" : setList.Items.Count + " of " + total + ":";

            if (previous != null) Reselect(previous);
            if (setList.SelectedItems.Count == 0 && setList.Items.Count != 0) Select(setList.Items[0]);
            else SetChanged();
        }

        /// <summary>What the set itself is called, and what it's rigged to.</summary>
        private static bool NameMatches(CathodeLib.Animation.AnimationSet set, string search)
        {
            return set.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || set.Skeleton.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool SetMatches(CathodeLib.Animation.AnimationSet set, string search)
        {
            return set.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || set.Skeleton.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || set.Contexts.Any(x => x.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                                      || x.Clips.Any(c => ClipMatches(c, search)));
        }

        private static bool ClipMatches(CathodeLib.Animation.ClipReference clip, string search)
        {
            return clip.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || clip.Path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Reselect(CathodeLib.Animation.AnimationSet set)
        {
            foreach (ListViewItem item in setList.Items)
                if (item.Tag == set) { Select(item); return; }
        }

        private static void Select(ListViewItem item)
        {
            item.Selected = true;
            item.Focused = true;
            item.EnsureVisible();
        }

        private void SetChanged()
        {
            if (_filling) return;

            CathodeLib.Animation.AnimationSet set = setList.SelectedItems.Count == 0 ? null
                : setList.SelectedItems[0].Tag as CathodeLib.Animation.AnimationSet;
            if (_picking == PickMode.AnimationSet) pickBtn.Enabled = set != null;

            //an import needs a set to go into, and nothing else
            importBtn.Enabled = set != null;

            /* Refiltering the list reselects the same set, and rebuilding the contexts then would
             * throw away whichever one was being looked at. */
            if (set == _set && _context != null) { FillClips(); return; }

            _set = set;
            FillContexts();
        }
        #endregion

        #region CONTEXTS
        /* A set's clips are grouped by the state the character is in - unnamed for the ones that
         * always apply, then one per state that overrides them. */
        private void FillContexts()
        {
            _filling = true;
            contextBox.BeginUpdate();
            contextBox.Items.Clear();

            if (_set != null)
                foreach (CathodeLib.Animation.AnimationContext context in _set.Contexts)
                    if (context.Clips.Count != 0) contextBox.Items.Add(new ContextEntry(context));

            contextBox.Enabled = contextBox.Items.Count != 0;
            if (contextBox.Items.Count != 0) contextBox.SelectedIndex = 0;
            contextBox.EndUpdate();
            _filling = false;

            _context = SelectedContext();
            FillClips();
        }

        private CathodeLib.Animation.AnimationContext SelectedContext()
        {
            return (contextBox.SelectedItem as ContextEntry)?.Context;
        }

        /// <summary>A context in the dropdown, named the way a person would ask for it.</summary>
        private class ContextEntry
        {
            public readonly CathodeLib.Animation.AnimationContext Context;

            public ContextEntry(CathodeLib.Animation.AnimationContext context) { Context = context; }

            public override string ToString()
            {
                return (Context.Name.Trim().Length == 0 ? "(always available)" : Context.Name)
                     + "   -   " + Context.Clips.Count + " animation" + (Context.Clips.Count == 1 ? "" : "s");
            }
        }
        #endregion

        #region CLIPS
        private void FillClips()
        {
            string search = clipSearchBox.Text.Trim();
            clipList.BeginUpdate();
            clipList.Items.Clear();

            /* A search looks through the whole set, not just the context the dropdown happens to be
             * showing. 116 of the game's 403 sets have more than one context and some have 25, so
             * searching inside one hides most of what the set holds - and a set that the list above
             * found *by* a clip name would then show nothing at all, which looks like a broken box. */
            bool acrossSet = search.Length != 0 && _set != null && _set.Contexts.Count > 1;

            if (acrossSet)
            {
                foreach (CathodeLib.Animation.AnimationContext context in _set.Contexts)
                    foreach (CathodeLib.Animation.ClipReference clip in context.Clips.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                        if (ClipMatches(clip, search)) clipList.Items.Add(Row(clip));
            }
            else if (_context != null)
            {
                foreach (CathodeLib.Animation.ClipReference clip in _context.Clips.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (search.Length != 0 && !ClipMatches(clip, search)) continue;
                    clipList.Items.Add(Row(clip));
                }
            }
            if (_clipOrder.Column >= 0) Sort(clipList, _clipOrder);
            clipList.EndUpdate();

            if (acrossSet)
                clipSearchLabel.Text = clipList.Items.Count + " across the set:";
            else
                clipSearchLabel.Text = _context == null || clipList.Items.Count == _context.Clips.Count
                    ? "Find:" : clipList.Items.Count + " of " + _context.Clips.Count + ":";

            ShowSelection();
        }

        private ListViewItem Row(CathodeLib.Animation.ClipReference clip)
        {
            ListViewItem item = new ListViewItem(clip.Name.Length == 0 ? Path.GetFileName(clip.Path) : clip.Name) { Tag = clip };
            item.SubItems.Add(Path.GetFileName(clip.Path));
            item.ToolTipText = clip.Path + DescribeSettings(clip);

            if (!clip.Playable)
            {
                item.SubItems.Add("-");
                item.SubItems.Add("-");
                item.SubItems.Add("-");
                item.SubItems.Add("-");
                item.SubItems.Add(clip.Section == null ? "section not found" : "unreadable");
                item.ForeColor = Color.Gray;
                return item;
            }

            HavokPackfile.AnimationClip animation = clip.Animation;

            /* Nearly every clip is authored on a shared rig and moved onto the character's own at
             * runtime, so the rig it came from is worth a column of its own - it is what a preview
             * has to retarget from, and the reason a clip can show up under a set it was never
             * built for. */
            item.SubItems.Add(animation.SkeletonName.Length == 0 ? "-" : animation.SkeletonName);
            item.SubItems.Add(animation.FrameCount.ToString());
            item.SubItems.Add(animation.Duration.ToString("0.00") + "s");
            item.SubItems.Add(Properties(clip));

            /* Nothing about being authored elsewhere goes here - almost every clip is, and the
             * column above already names the rig. This is for the things that are unusual. */
            item.SubItems.Add(clip.Additive ? "additive" : "");
            return item;
        }


        /* The six values every clip carries say nothing about this one, and the columns already show
         * what they amount to, so the tooltip skips them. */
        private static readonly HashSet<string> Boilerplate = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "anim_label", "meta_label", "duration", "numberOfFrames", "mirror", "has_motion", "0",
        };

        /// <summary>
        /// What the clip is tagged with, for the row's tooltip. The "settings" in the Properties
        /// column are metadata arguments - most clips carry only the standard six, but a locomotion
        /// clip also records how far and how fast it travels, and an aim clip records where it points,
        /// which is worth being able to read without opening anything.
        /// </summary>
        private static string DescribeSettings(CathodeLib.Animation.ClipReference clip)
        {
            AnimClipDBSec.MetadataSet metadata = clip.Metadata;
            if (metadata == null) return "";

            List<AnimClipDBSec.MetadataArgument> arguments = new List<AnimClipDBSec.MetadataArgument>(metadata.Common.Arguments);
            foreach (AnimClipDBSec.MetadataBlock block in metadata.Instances) arguments.AddRange(block.Arguments);

            List<string> lines = new List<string>();
            int hidden = 0;
            foreach (AnimClipDBSec.MetadataArgument argument in arguments)
            {
                if (Boilerplate.Contains(argument.Name ?? "")) continue;
                if (lines.Count >= 18) { hidden++; continue; }

                //an audio setting's value is a whole record; the sound and the bone are the readable part
                string value = argument.Type == CATHODE.Animations.MetadataValueType.AUDIO
                    ? DescribeAudio(argument) : argument.Value?.ToString() ?? "";
                if (value.Length > 52) value = value.Substring(0, 51) + "…";
                lines.Add("   " + argument.Name + (value.Length == 0 ? "" : " = " + value));
            }

            if (lines.Count == 0) return "";
            if (hidden != 0) lines.Add("   ... and " + hidden + " more");
            return "\r\n\r\nSettings:\r\n" + string.Join("\r\n", lines);
        }

        private static string DescribeAudio(AnimClipDBSec.MetadataArgument argument)
        {
            CathodeLib.Animation.AudioEvent audio = CathodeLib.Animation.ParseAudioEvent(argument.Value as string);
            if (audio == null) return argument.Value?.ToString() ?? "";
            return audio.Event + (string.IsNullOrEmpty(audio.Bone) ? "" : " from " + audio.Bone);
        }

        /* What the clip has tagged on it: the moments it fires things at, and any settings hung off
         * it. Both live in the metadata, and plenty of clips have neither. */
        private static string Properties(CathodeLib.Animation.ClipReference clip)
        {
            AnimClipDBSec.MetadataSet metadata = clip.Metadata;
            if (metadata == null) return "";

            int events = clip.Markers.Count;
            int arguments = metadata.Common.Arguments.Count + metadata.Instances.Sum(x => x.Arguments.Count);

            List<string> parts = new List<string>();
            if (events != 0) parts.Add(events + " event" + (events == 1 ? "" : "s"));
            if (arguments != 0) parts.Add(arguments + " setting" + (arguments == 1 ? "" : "s"));
            return string.Join(", ", parts);
        }

        /// <summary>The clip the buttons act on - the first one selected.</summary>
        private CathodeLib.Animation.ClipReference Selected()
        {
            return clipList.SelectedItems.Count == 0 ? null : clipList.SelectedItems[0].Tag as CathodeLib.Animation.ClipReference;
        }

        /// <summary>The clips an export would write: whatever is selected, or everything listed.</summary>
        private List<CathodeLib.Animation.ClipReference> ToExport()
        {
            IEnumerable<ListViewItem> rows = clipList.SelectedItems.Count > 1
                ? clipList.SelectedItems.Cast<ListViewItem>()
                : clipList.Items.Cast<ListViewItem>();

            return rows.Select(x => x.Tag as CathodeLib.Animation.ClipReference).Where(x => x != null && x.Playable).ToList();
        }

        private void ShowSelection()
        {
            CathodeLib.Animation.ClipReference clip = Selected();
            previewBtn.Enabled = clip != null && clip.Playable;
            if (_picking == PickMode.Animation) pickBtn.Enabled = clip != null;

            bool some = clipList.SelectedItems.Count > 1;
            exportBtn.Text = some ? "Export Selected..." : "Export All...";
            exportBtn.Enabled = clipList.Items.Count != 0;

            summaryLabel.Text = Summary(clip);
        }

        /* One line about where we are and what is selected, since there is no detail pane any more. */
        private string Summary(CathodeLib.Animation.ClipReference clip)
        {
            if (_set == null) return "Choose a skeleton above.";
            if (_context == null) return _set.Name + " has no animations in it.";

            if (clipList.SelectedItems.Count > 1)
                return clipList.SelectedItems.Count + " of " + clipList.Items.Count + " animations selected.";

            if (clip == null)
                return clipList.Items.Count == 0
                    ? "Nothing in " + _set.Name + " matches that."
                    : "Select an animation to preview or export it.";

            if (!clip.Playable)
                return clip.Name + " could not be read out of "
                     + (clip.Section == null ? "the PAK - its section is missing." : Path.GetFileName(clip.Section.Filepath) + ".");

            HavokPackfile.AnimationClip animation = clip.Animation;
            List<string> parts = new List<string>
            {
                animation.FrameCount + " frames at "
                    + (animation.FrameDuration > 0 ? (1 / animation.FrameDuration).ToString("0.#") : "?") + " fps",
                animation.TransformTrackCount + " bones",
            };
            if (animation.FloatTrackCount != 0) parts.Add(animation.FloatTrackCount + " float tracks");
            parts.Add("authored on " + animation.SkeletonName + BoneCount(animation.SkeletonName));

            return clip.Name + "  -  " + string.Join(", ", parts);
        }

        private string BoneCount(string skeleton)
        {
            List<Skeleton.Bone> bones = _animations?.GetSkeleton(skeleton)?.Bones;
            return bones == null ? " (not in this PAK)" : " (" + bones.Count + " bones)";
        }
        #endregion

        #region PICKING
        /* Open on whatever the entity is already set to, right down to the animation itself, so the
         * window comes up showing the current value rather than making you go and find it. */
        private void SelectStartingSet()
        {
            if (string.IsNullOrEmpty(_startingSet)) return;

            foreach (ListViewItem item in setList.Items)
            {
                if (!(item.Tag is CathodeLib.Animation.AnimationSet set)) continue;
                if (!string.Equals(set.Name, _startingSet, StringComparison.OrdinalIgnoreCase)) continue;

                Select(item);
                SetChanged();
                SelectStartingContext();
                SelectStartingClip();
                return;
            }
        }

        /* Which context to land on - the one holding the animation we started with, since a set
         * keeps the same clip name in several of them. */
        private void SelectStartingContext()
        {
            if (string.IsNullOrEmpty(_startingAnimation) || _picking == PickMode.AnimationSet) return;

            for (int i = 0; i < contextBox.Items.Count; i++)
            {
                CathodeLib.Animation.AnimationContext context = (contextBox.Items[i] as ContextEntry)?.Context;
                if (context == null) continue;
                if (!context.Clips.Any(x => string.Equals(x.Name, _startingAnimation, StringComparison.OrdinalIgnoreCase))) continue;

                if (contextBox.SelectedIndex != i) contextBox.SelectedIndex = i;
                return;
            }
        }

        private void SelectStartingClip()
        {
            if (string.IsNullOrEmpty(_startingAnimation)) return;

            foreach (ListViewItem item in clipList.Items)
            {
                if (!(item.Tag is CathodeLib.Animation.ClipReference clip)) continue;
                if (!string.Equals(clip.Name, _startingAnimation, StringComparison.OrdinalIgnoreCase)) continue;

                Select(item);

                //focus the list so the arrow keys move through the animations straight away
                clipList.Select();
                return;
            }
        }

        private void PickBtn_Click(object sender, EventArgs e)
        {
            /* An animation parameter holds the name the set plays a clip by, and an animation set
             * parameter holds the set's own name - both plain strings, no path. */
            string value = _picking == PickMode.AnimationSet ? _set?.Name : Selected()?.Name;
            if (string.IsNullOrEmpty(value)) return;

            OnPicked?.Invoke(value);
            Close();
        }
        #endregion

        #region ACTIONS
        private void PreviewBtn_Click(object sender, EventArgs e)
        {
            CathodeLib.Animation.ClipReference clip = Selected();
            if (clip == null || !clip.Playable) return;

            if (_preview != null && !_preview.IsDisposed)
            {
                _preview.Show(clip);
                _preview.BringToFront();
                return;
            }

            _preview = new AnimationPreview(_animations);
            _preview.FormClosed += (s, args) => _preview = null;
            _preview.Show();
            _preview.Show(clip);
        }


        /* Bring an animation in from a model file and add it to the set being browsed. */
        private void ImportBtn_Click(object sender, EventArgs e)
        {
            if (_set == null || _animations == null) return;

            string file;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Import an animation for " + _set.Name;
                dialog.Filter = AnimationImport.FileFilter;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                file = dialog.FileName;
            }

            string imported;
            using (ImportAnimation import = new ImportAnimation(_animations, _set, file))
            {
                if (import.ShowDialog(this) != DialogResult.OK) return;
                imported = import.ImportedName;
            }

            //rebuild the lists so the new clip is there, and put the cursor on it
            FillContexts();
            foreach (ListViewItem item in clipList.Items)
            {
                if (!(item.Tag is CathodeLib.Animation.ClipReference clip)) continue;
                if (!string.Equals(clip.Name, imported, StringComparison.OrdinalIgnoreCase)) continue;
                Select(item);
                break;
            }

            /* Written out straight away rather than offered as a choice. An import that isn't saved
             * has done nothing at all, and the write takes about a second and a half - it used to be
             * ten, which is what the question was really for. */
            Save("'" + imported + "' added to " + _set.Name);
        }

        /* Write ANIMATION.PAK back, saying so in the status bar rather than in a dialog. Only a
         * failure is worth interrupting anyone for. */
        private bool Save(string done)
        {
            Cursor.Current = Cursors.WaitCursor;
            statusLabel.Text = "Writing ANIMATION.PAK...";
            statusStrip.Refresh();
            try
            {
                if (!_animations.Save())
                {
                    statusLabel.Text = "ANIMATION.PAK could not be written.";
                    MessageBox.Show("ANIMATION.PAK could not be written.", "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                statusLabel.Text = done + ", and ANIMATION.PAK written.";
                return true;
            }
            catch (Exception ex)
            {
                statusLabel.Text = "ANIMATION.PAK could not be written.";
                MessageBox.Show(ex.ToString(), "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            finally { Cursor.Current = Cursors.Default; }
        }

        /* The clips selected, or every clip listed, written out against a rig with no mesh attached. */
        private void ExportBtn_Click(object sender, EventArgs e)
        {
            if (_context == null) return;

            List<CathodeLib.Animation.ClipReference> clips = ToExport();
            int listed = clipList.SelectedItems.Count > 1 ? clipList.SelectedItems.Count : clipList.Items.Count;
            if (clips.Count == 0)
            {
                MessageBox.Show("None of the animations listed could be read, so there's nothing to export.",
                    "Export", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Skeleton skeleton;
            CathodeLib.Animation.RootMotion rootMotion;
            CathodeLib.Animation.UntrackedChannels untracked;
            using (AnimationSkeletonPicker picker = new AnimationSkeletonPicker(_animations, _context.Set, clips))
            {
                if (picker.ShowDialog(this) != DialogResult.OK || picker.Result == null) return;
                skeleton = picker.Result;
                rootMotion = picker.RootMotion;
                untracked = picker.Untracked;
            }

            string suggested = clips.Count == 1 ? clips[0].Name
                : _context.Set.Name + (_context.Name.Trim().Length == 0 ? "" : "_" + _context.Name);
            string filename = AnimationExport.AskWhereToSave(this, suggested);
            if (filename == null) return;

            //ask about the size after the format is known - the formats differ by an order of magnitude
            if (!AnimationExport.ConfirmLargeExport(this, clips.Count, skeleton.Bones.Count,
                    clips.Max(x => x.Animation.FrameCount), filename))
                return;

            Cursor.Current = Cursors.WaitCursor;
            try
            {
                CathodeLibExtensions.ExportAnimations(skeleton, clips, filename, rootMotion, untracked);
                MessageBox.Show(clips.Count + " animation(s) exported against the '" + skeleton.Name + "' skeleton."
                    + (clips.Count == listed ? "" : "\n\n" + (listed - clips.Count) + " could not be read and were left out."),
                    "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor.Current = Cursors.Default; }
        }
        #endregion
    }
}
