using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;

namespace OpenCAGE
{
    /// <summary>
    /// Applies selection packets sent from the Godot Level Viewer.
    /// Equal path_entities/path_composites counts select an entity; extra composite entry steps into an instance.
    /// </summary>
    public static class ViewerSelectionSync
    {
        /// <summary>
        /// While &gt; 0, suppress websocket echo packets triggered by applying viewer selection in OpenCAGE.
        /// </summary>
        public static int SuppressSyncBroadcastDepth { get; internal set; }

        /// <summary>
        /// While &gt; 0, OpenCAGE is applying a packet that originated from the embedded level viewer.
        /// Editor UI should update selection without stealing Win32 focus from the viewer process.
        /// </summary>
        public static int ApplyingViewerSelectionDepth { get; private set; }

        public static bool IsApplyingViewerSelection => ApplyingViewerSelectionDepth > 0;

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
                    Debug.Log("Websocket", "Failed to queue viewer selection on UI thread: " + ex.Message);
                    return false;
                }
            }

            return ApplyCore(packet);
        }

        private static bool ApplyCore(Packet packet)
        {
            if (packet?.path_composites == null || packet.path_composites.Count == 0
                || packet.path_entities == null)
            {
                return false;
            }

            CompositeBrowser commands = Singleton.Editor?.CompositeBrowser;
            if (commands?.Content?.Level == null)
                return false;

            Composite entryComposite = commands.Content.Level.Commands.GetComposite(new ShortGuid(packet.path_composites[0]));
            if (entryComposite == null)
                return false;

            bool entitySelected = packet.path_composites.Count == packet.path_entities.Count;

            CompositeDisplay display = commands.CompositeDisplay;
            if (display == null || !display.Populated)
                display = commands.LoadComposite(entryComposite);
            else if (!IsEntryCompositeReachable(display, entryComposite))
                display = commands.LoadComposite(entryComposite);

            if (display == null)
                return false;

            SuppressSyncBroadcastDepth++;
            ApplyingViewerSelectionDepth++;
            try
            {
                if (entitySelected && packet.entity != 0 && packet.composite != 0)
                {
                    Composite ownerComposite = commands.Content.Level.Commands.GetComposite(new ShortGuid(packet.composite));
                    Entity directEntity = ownerComposite?.GetEntityByID(new ShortGuid(packet.entity));
                    if (directEntity != null
                        && display.Composite?.shortGUID == ownerComposite.shortGUID
                        && display.TrySelectAddedAlias(ownerComposite, directEntity))
                    {
                        return true;
                    }
                }

                bool applied = display.ApplyViewerSelectionPath(
                    entryComposite,
                    packet.path_entities,
                    entitySelected,
                    entity => TryGetChildComposite(entity, commands));

                if (!applied && entitySelected && packet.path_entities.Count > 0)
                    applied = display.TrySelectLeafEntityInCurrentComposite(packet.path_entities);

                if (!applied && entitySelected && packet.entity != 0)
                {
                    Composite ownerComposite = packet.composite != 0
                        ? commands.Content.Level.Commands.GetComposite(new ShortGuid(packet.composite))
                        : display.Composite;
                    Entity leaf = ownerComposite?.GetEntityByID(new ShortGuid(packet.entity));
                    if (leaf != null)
                    {
                        commands.LoadCompositeAndEntity(ownerComposite, leaf);
                        applied = true;
                    }
                }

                return applied;
            }
            finally
            {
                ApplyingViewerSelectionDepth--;
                SuppressSyncBroadcastDepth--;
                ScheduleLevelViewerFocusRestore();
            }
        }

        private static void ScheduleLevelViewerFocusRestore()
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return;

            editor.BeginInvoke(new Action(() => TryRestoreLevelViewerFocus(editor)));
        }

        private static void TryRestoreLevelViewerFocus(CommandsEditor editor)
        {
            if (editor == null || editor.IsDisposed)
                return;

            if (NativeMouseInput.IsAnyMouseButtonPressed)
            {
                editor.BeginInvoke(new Action(() => TryRestoreLevelViewerFocus(editor)));
                return;
            }

            editor.LevelViewerPanel?.RestoreInputFocus();
        }

        private static bool IsEntryCompositeReachable(CompositeDisplay display, Composite entryComposite)
        {
            if (display.Composite?.shortGUID == entryComposite.shortGUID)
                return true;

            foreach (Composite composite in display.Path.AllComposites)
            {
                if (composite.shortGUID == entryComposite.shortGUID)
                    return true;
            }

            return false;
        }

        private static Composite TryGetChildComposite(Entity entity, CompositeBrowser commands)
        {
            if (entity == null || entity.variant != EntityVariant.FUNCTION)
                return null;

            FunctionEntity function = (FunctionEntity)entity;
            if (function.function.IsFunctionType)
                return null;

            return commands.Content.Level.Commands.GetComposite(function.function);
        }
    }
}
