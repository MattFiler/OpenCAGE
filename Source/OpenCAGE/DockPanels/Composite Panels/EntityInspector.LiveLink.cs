using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.RuntimeUtilsConnection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.DockPanels
{
    /* Live link: while the game is connected, the selected entity's methods can be called in it from here - "start"
       on a DebugTextStacking without wiring a ThinkOnce to it. The main method gets a button of its own; every method
       pin is in the dropdown. A call goes to the instance the composite was reached through from the level's root, or
       to every instance of it when it was opened some other way. */
    public partial class EntityInspector
    {
        //Tried in order for the one-click button
        private static readonly string[] _primaryMethods = { "trigger", "start", "show", "enable", "play" };

        private ToolStripButton _liveLinkPrimary;
        private ToolStripDropDownButton _liveLinkCall;

        private void SetupLiveLinkButtons()
        {
            _liveLinkPrimary = new ToolStripButton()
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Visible = false,
            };
            _liveLinkPrimary.Click += (s, e) => CallInGame(_liveLinkPrimary.Tag as ShortGuid?);
            _liveLinkCall = new ToolStripDropDownButton()
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Text = "Call in Game",
                ToolTipText = "Call one of this entity's methods in the running game (live link)",
                Visible = false,
            };
            toolStrip1.Items.Insert(0, _liveLinkCall);
            toolStrip1.Items.Insert(0, _liveLinkPrimary);

            LiveLink.ConnectionChanged += OnLiveLinkConnectionChanged;
            this.Disposed += (s, e) => LiveLink.ConnectionChanged -= OnLiveLinkConnectionChanged;
        }

        private void OnLiveLinkConnectionChanged()
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
                return;
            BeginInvoke(new Action(RefreshLiveLinkButtons));
        }

        private void RefreshLiveLinkButtons()
        {
            if (_liveLinkCall == null || IsDisposed)
                return;
            Commands commands = Content?.Level?.Commands;
            List<ShortGuid> methods = LiveLink.Connected && _entity != null && !IsMultiEditing
                ? LiveLink.Methods(commands, _entity, Composite)
                : new List<ShortGuid>();

            _liveLinkCall.DropDownItems.Clear();
            foreach (ShortGuid method in methods.OrderBy(o => o.ToString()))
            {
                ShortGuid id = method;
                ToolStripMenuItem item = new ToolStripMenuItem(method.ToString());
                item.Click += (s, e) => CallInGame(id);
                _liveLinkCall.DropDownItems.Add(item);
            }
            _liveLinkCall.Visible = methods.Count != 0;

            ShortGuid? primary = null;
            foreach (string name in _primaryMethods)
            {
                ShortGuid candidate = ShortGuidUtils.Generate(name);
                if (methods.Contains(candidate))
                {
                    primary = candidate;
                    break;
                }
            }
            _liveLinkPrimary.Tag = primary;
            _liveLinkPrimary.Visible = primary != null;
            if (primary != null)
            {
                _liveLinkPrimary.Text = "▶ " + primary.Value.ToString();
                _liveLinkPrimary.ToolTipText = "Call \"" + primary.Value.ToString() + "\" on this entity in the running game (live link)";
            }
        }

        //A call the game has not answered yet: it holds calls while a level starts, so clicks could pile up behind it
        private static bool _liveLinkCalling = false;

        private async void CallInGame(ShortGuid? method)
        {
            if (method == null || _entity == null || Composite == null)
                return;
            Entity entity = _entity;
            Composite composite = Composite;
            Commands commands = Content?.Level?.Commands;
            List<ShortGuid> path = LiveLink.InstancePath(_compositeDisplay, commands);
            string name = commands?.Utils.GetEntityName(composite, entity) ?? entity.shortGUID.ToByteString();
            string what = "\"" + method.Value.ToString() + "\" on " + name;
            if (_liveLinkCalling)
            {
                Singleton.Editor?.ShowLiveLinkActivity("Live link: still waiting for the game to take the last call");
                return;
            }

            //Edits made before it go first; the level's root goes with it, so the game refuses it if running another level
            _liveLinkCalling = true;
            Singleton.Editor?.ShowLiveLinkActivity("Live link: calling " + what + "...");
            LiveLink.Reply reply;
            try
            {
                reply = await LiveLink.CallAfterEdits(commands, composite, entity, method.Value, path);
            }
            finally
            {
                _liveLinkCalling = false;
            }
            Singleton.Editor?.ShowLiveLinkActivity(reply.Ok
                ? "Live link: called " + what + (path == null ? " (every instance)" : "")
                : "Live link: could not call " + what + " - " + reply.Message);
        }
    }
}
