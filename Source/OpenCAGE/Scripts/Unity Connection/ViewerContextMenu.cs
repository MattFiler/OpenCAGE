using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using OpenCAGE.Theming;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// The viewport's right-click menu (issue 704), drawn here rather than in the viewer: a ContextMenuStrip
    /// themed like the editor's other menus, put up where the viewer says a right click landed
    /// (VIEWPORT_CONTEXT_MENU). Its entries are what the viewport's shortcuts do to the selection, greyed out
    /// as the viewer says they have nothing to act on. Copy, Paste, Duplicate and Delete are this side's:
    /// they go through the very handlers the viewport's Ctrl+C, Ctrl+V, Ctrl+D and Delete land in, so a
    /// paste from the menu is placed exactly as one from the key. The rest are the viewer's, asked for with
    /// VIEWPORT_ACTION and run there through the same methods their shortcuts run.
    /// </summary>
    /// <remarks>
    /// The viewer's window belongs to another process, so a click or a key that lands in it while the menu
    /// is up never reaches this side's message loop, and the menu would stay. The viewer is told when the
    /// menu opens and closes (ContextMenuOpened / ContextMenuClosed), swallows its next click or key while
    /// it is up, and sends VIEWPORT_CONTEXT_MENU_DISMISS instead - which closes the menu here, and is also
    /// how Escape in the viewport closes it. A click anywhere else in the editor closes it as any menu
    /// closes. It also goes when the viewer disconnects or exits, and when a level loads.
    /// </remarks>
    public static class ViewerContextMenu
    {
        private static ContextMenuStrip _menu;
        private static ToolStripMenuItem _copy;
        private static ToolStripMenuItem _paste;
        private static ToolStripMenuItem _duplicate;
        private static ToolStripMenuItem _delete;
        private static ToolStripMenuItem _focus;
        private static ToolStripMenuItem _snapToFloor;
        private static ToolStripMenuItem _hide;
        private static ToolStripMenuItem _unhideAll;
        private static ToolStripMenuItem _stepInto;
        private static ToolStripMenuItem _selectParent;
        private static ToolStripMenuItem _deselectAll;

        //The viewer has been told the menu is up, and not yet that it has gone
        private static bool _open;

        public static bool IsOpen => _open && _menu != null && !_menu.IsDisposed && _menu.Visible;

        static ViewerContextMenu()
        {
            //Nothing the menu names survives a load; raised off the loader's thread
            Singleton.OnLevelLoaded += content => CloseFromAnyThread();
        }

        public static bool TryApply(Packet packet)
        {
            if (packet == null)
                return false;

            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return false;

            if (editor.InvokeRequired)
            {
                try
                {
                    editor.BeginInvoke(new Action(() => ApplyCore(packet)));
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.Log("Websocket", "Failed to queue viewer context menu on UI thread: " + ex.Message);
                    return false;
                }
            }

            return ApplyCore(packet);
        }

        private static bool ApplyCore(Packet packet)
        {
            switch (packet.packet_event)
            {
                case PacketEvent.VIEWPORT_CONTEXT_MENU:
                    return Show(packet);
                case PacketEvent.VIEWPORT_CONTEXT_MENU_DISMISS:
                    Close();
                    return true;
                default:
                    return false;
            }
        }

        /* Put the menu up for a right click in the viewport. Nothing without a populated composite to act
           on: the viewer only asks with one loaded, but a level may have gone since it asked. */
        private static bool Show(Packet packet)
        {
            CompositeBrowser commands = Singleton.Editor?.CompositeBrowser;
            //Level is there from the moment a load begins; its commands only once it is done
            if (commands?.Content == null || !commands.Content.IsLevelDataLoaded)
                return false;

            CompositeDisplay display = commands.CompositeDisplay;
            if (display == null || display.IsDisposed || !display.Populated)
                return false;

            Close();
            EnsureMenu();

            //Greyed as the viewer's own menu greyed them; Paste needs the clipboard too, which only this side knows
            _copy.Enabled = packet.context_menu_has_selection;
            _paste.Enabled = packet.context_menu_can_paste && EntityClipboard.HasContent;
            _duplicate.Enabled = packet.context_menu_has_selection;
            _delete.Enabled = packet.context_menu_has_selection;
            _focus.Enabled = packet.context_menu_can_focus;
            _snapToFloor.Enabled = packet.context_menu_can_snap_to_floor;
            _hide.Enabled = packet.context_menu_can_hide;
            _unhideAll.Enabled = packet.context_menu_can_unhide_all;
            _stepInto.Enabled = packet.context_menu_can_step_into;
            _selectParent.Enabled = packet.context_menu_can_select_parent;
            _deselectAll.Enabled = packet.context_menu_has_selection;

            /* Where the click landed, through the panel the viewer is embedded in - the viewer reports it as
               a fraction of its window, and that panel's client area IS that window. The cursor otherwise,
               which is where the click was a moment ago. */
            LevelViewerPanel panel = Singleton.Editor?.LevelViewerPanel;
            Point at;
            if (panel == null || panel.IsDisposed
                || !panel.TryGetViewportScreenPoint(packet.context_menu_viewport_x, packet.context_menu_viewport_y, out at))
            {
                at = Cursor.Position;
            }

            /* Themed on the way up, every time. The other context menus hang off a control and are walked by
               the theme with their form; this one hangs off nothing, so a theme switched since it was last up
               would never reach it. In light mode this only puts back what dark mode set. */
            ThemeEngine.Apply(_menu, ThemeManager.IsDark);

            _menu.Show(at);
            if (!_menu.Visible)
                return false;

            _open = true;
            Send.SendViewportAction(ViewportAction.ContextMenuOpened);
            return true;
        }

        /// <summary>Take the menu down, if it is up. The viewer hears through the menu's own Closed.</summary>
        public static void Close()
        {
            if (_menu == null || _menu.IsDisposed)
                return;

            if (_menu.Visible)
                _menu.Close();
            else if (_open)
                Menu_Closed(_menu, null);
        }

        private static void CloseFromAnyThread()
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return;

            if (editor.InvokeRequired)
            {
                try { editor.BeginInvoke(new Action(Close)); }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            Close();
        }

        private static void Menu_Closed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            if (!_open)
                return;

            _open = false;
            Send.SendViewportAction(ViewportAction.ContextMenuClosed);
        }

        /* The same entries, order and shortcut text as the viewer's own menu had. Built once; the enabled
           states are set each time it opens. */
        private static void EnsureMenu()
        {
            if (_menu != null && !_menu.IsDisposed)
                return;

            _menu = new ContextMenuStrip() { Name = "ViewportContextMenu" };
            _menu.Closed += Menu_Closed;

            _copy = Item("Copy", "Ctrl+C", (sender, e) => RequestAsViewport(PacketEvent.ENTITY_CLIPBOARD_COPY));
            _paste = Item("Paste", "Ctrl+V", (sender, e) => RequestAsViewport(PacketEvent.ENTITY_CLIPBOARD_PASTE));
            _duplicate = Item("Duplicate", "Ctrl+D", (sender, e) => RequestAsViewport(PacketEvent.ENTITY_DUPLICATE_REQUEST));
            _delete = Item("Delete", "Delete", (sender, e) => RequestAsViewport(PacketEvent.ENTITY_DELETE_REQUEST));
            _focus = Item("Focus on Selection", "Z", (sender, e) => Send.SendViewportAction(ViewportAction.FocusOnSelection));
            _snapToFloor = Item("Snap to Floor", "Shift+End", (sender, e) => Send.SendViewportAction(ViewportAction.SnapToFloor));
            _hide = Item("Hide", "H", (sender, e) => Send.SendViewportAction(ViewportAction.Hide));
            _unhideAll = Item("Unhide All", "Shift+H", (sender, e) => Send.SendViewportAction(ViewportAction.UnhideAll));
            //Ctrl+middle click and '-', which the viewer's menu showed no text for either
            _stepInto = Item("Step Into Composite", null, (sender, e) => Send.SendViewportAction(ViewportAction.StepIntoComposite));
            _selectParent = Item("Select Parent Composite", null, (sender, e) => Send.SendViewportAction(ViewportAction.SelectParentComposite));
            _deselectAll = Item("Deselect All", "Escape", (sender, e) => Send.SendViewportAction(ViewportAction.DeselectAll));

            _menu.Items.AddRange(new ToolStripItem[]
            {
                _copy,
                _paste,
                _duplicate,
                _delete,
                new ToolStripSeparator(),
                _focus,
                _snapToFloor,
                _hide,
                _unhideAll,
                new ToolStripSeparator(),
                _stepInto,
                _selectParent,
                _deselectAll,
            });
        }

        private static ToolStripMenuItem Item(string text, string shortcut, EventHandler onClick)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text) { ShortcutKeyDisplayString = shortcut };
            item.Click += onClick;
            return item;
        }

        /* An entry this side owns, done exactly as the viewport's key does it: the packet that key sends,
           built here from the selection as this side holds it - which is what the viewer's selection mirrors -
           and handed to the handler that key's packet lands in. So a paste from the menu is placed and
           selected as Ctrl+V's is, a duplicate makes the same undo step as Ctrl+D's, and a delete asks the
           same question Delete does. */
        private static void RequestAsViewport(PacketEvent request)
        {
            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            if (display == null || display.IsDisposed || !display.Populated || display.Composite == null)
                return;

            Packet packet = new Packet(request) { composite = display.Composite.shortGUID.AsUInt32 };

            if (request != PacketEvent.ENTITY_CLIPBOARD_PASTE)
            {
                /* The primary entity is the packet's entity; several selected at once travel in
                   selection_entities, primary first, as Send.GeneratePacket sends them to the viewer. */
                List<Entity> multiSelection = display.EntityDisplay?.MultiSelectedEntities;
                Entity primary = display.EntityDisplay?.Entity
                    ?? (multiSelection != null && multiSelection.Count > 0 ? multiSelection[0] : null);
                if (primary == null)
                    return;

                packet.entity = primary.shortGUID.AsUInt32;
                if (multiSelection != null && multiSelection.Count > 1)
                {
                    foreach (Entity entity in multiSelection)
                        packet.selection_entities.Add(entity.shortGUID.AsUInt32);
                }
            }

            switch (request)
            {
                case PacketEvent.ENTITY_CLIPBOARD_COPY:
                case PacketEvent.ENTITY_CLIPBOARD_PASTE:
                    ViewerClipboardSync.TryApply(packet);
                    break;
                case PacketEvent.ENTITY_DUPLICATE_REQUEST:
                    ViewerEntityDuplicateSync.TryApply(packet);
                    break;
                case PacketEvent.ENTITY_DELETE_REQUEST:
                    ViewerEntitySync.TryApplyDeleteRequest(packet);
                    break;
            }
        }
    }
}
