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
    /* Live Link: while the game is connected, the selected entity's methods can be triggered in it from here - "start"
       on a DebugTextStacking without wiring a ThinkOnce to it - through the Trigger Method dropdown of its method pins
       (a flowgraph pin's right-click menu triggers that one pin the same way, through TriggerInGame). A call goes to the
       instance the composite was reached through from the level's root, or to every instance of it when it was opened
       some other way. */
    public partial class EntityInspector
    {
        private ToolStripDropDownButton _liveLinkCall;

        private void SetupLiveLinkButtons()
        {
            _liveLinkCall = new ToolStripDropDownButton()
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Text = "▶ Trigger Method",
                ToolTipText = "Trigger one of this entity's methods in the running game (Live Link)",
                Visible = false,
            };
            toolStrip1.Items.Insert(0, _liveLinkCall);

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
        }

        private void CallInGame(ShortGuid method)
        {
            if (_entity == null || Composite == null)
                return;
            Commands commands = Content?.Level?.Commands;
            TriggerInGame(commands, Composite, _entity, method, LiveLink.InstancePath(_compositeDisplay, commands));
        }

        //A call the game has not answered yet: it holds calls while a level starts, so clicks could pile up behind it
        private static bool _liveLinkCalling = false;

        /// <summary>
        /// Trigger one of an entity's methods in the running game, once the edits made before it are there, saying how it
        /// went in the status bar: the Trigger Method dropdown, and a flowgraph method pin's right-click Trigger in Game. A
        /// null path calls every instance of the composite. UI thread.
        /// </summary>
        internal static async void TriggerInGame(Commands commands, Composite composite, Entity entity, ShortGuid method, List<ShortGuid> path)
        {
            if (entity == null || composite == null)
                return;
            string name = commands?.Utils.GetEntityName(composite, entity) ?? entity.shortGUID.ToByteString();
            string what = "\"" + method.ToString() + "\" on " + name;
            if (_liveLinkCalling)
            {
                Singleton.Editor?.ShowLiveLinkActivity("Live Link: still waiting for the game to take the last call");
                return;
            }

            //Edits made before it go first; the level's root goes with it, so the game refuses it if running another level
            _liveLinkCalling = true;
            Singleton.Editor?.ShowLiveLinkActivity("Live Link: calling " + what + "...");
            LiveLink.Reply reply;
            try
            {
                reply = await LiveLink.CallAfterEdits(commands, composite, entity, method, path);
            }
            finally
            {
                _liveLinkCalling = false;
            }
            Singleton.Editor?.ShowLiveLinkActivity(reply.Ok
                ? "Live Link: called " + what + (path == null ? " (every instance)" : "")
                : "Live Link: could not call " + what + " - " + reply.Message);
        }
    }
}
