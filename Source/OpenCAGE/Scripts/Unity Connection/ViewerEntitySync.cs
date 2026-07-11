using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using System;
using System.Collections.Generic;

namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Applies ENTITY_ADDED / ENTITY_DELETED packets sent from the Godot Level Viewer.
    /// </summary>
    public static class ViewerEntitySync
    {
        public static bool TryApply(Packet packet)
        {
            if (packet == null)
                return false;

            switch (packet.packet_event)
            {
                case PacketEvent.ENTITY_ADDED:
                    return TryApplyAdded(packet);
                case PacketEvent.ENTITY_DELETED:
                    return TryApplyDeleted(packet);
                default:
                    return false;
            }
        }

        private static bool TryApplyAdded(Packet packet)
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return false;

            if (editor.InvokeRequired)
            {
                try
                {
                    editor.BeginInvoke(new Action(() => ApplyAddedCore(packet)));
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.Log("Websocket", "Failed to queue viewer entity add on UI thread: " + ex.Message);
                    return false;
                }
            }

            return ApplyAddedCore(packet);
        }

        private static bool TryApplyDeleted(Packet packet)
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return false;

            if (editor.InvokeRequired)
            {
                try
                {
                    editor.BeginInvoke(new Action(() => ApplyDeletedCore(packet)));
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.Log("Websocket", "Failed to queue viewer entity delete on UI thread: " + ex.Message);
                    return false;
                }
            }

            return ApplyDeletedCore(packet);
        }

        private static bool ApplyAddedCore(Packet packet)
        {
            CompositeBrowser commands = Singleton.Editor?.CompositeBrowser;
            if (commands?.Content?.Level == null)
                return false;

            Composite composite = commands.Content.Level.Commands.GetComposite(new ShortGuid(packet.composite));
            if (composite == null)
                return false;

            ShortGuid entityId = new ShortGuid(packet.entity);
            Entity entity = composite.GetEntityByID(entityId);
            if (entity != null)
            {
                ApplyAddedSelection(commands, packet, composite, entity);
                return true;
            }

            entity = null;
            switch (packet.entity_variant)
            {
                case EntityVariant.ALIAS:
                {
                    if (packet.entity_pointed == null || packet.entity_pointed.Count == 0)
                        return false;

                    EntityPath aliasPath = new EntityPath()
                    {
                        path = new ShortGuid[packet.entity_pointed.Count],
                    };
                    for (int i = 0; i < packet.entity_pointed.Count; i++)
                        aliasPath.path[i] = new ShortGuid(packet.entity_pointed[i]);

                    entity = composite.AddAlias(new AliasEntity()
                    {
                        shortGUID = entityId,
                        alias = aliasPath,
                    });
                    break;
                }
                default:
                    return false;
            }

            if (entity == null)
                return false;

            ApplyPacketParameters(entity, packet, commands.Content);

            ViewerSelectionSync.SuppressSyncBroadcastDepth++;
            try
            {
                Singleton.OnEntityAdded?.Invoke(entity);
            }
            finally
            {
                ViewerSelectionSync.SuppressSyncBroadcastDepth--;
            }

            ApplyAddedSelection(commands, packet, composite, entity);

            return true;
        }

        private static bool HasSelectionPath(Packet packet)
        {
            return packet.path_entities != null
                && packet.path_composites != null
                && packet.path_entities.Count > 0
                && packet.path_entities.Count == packet.path_composites.Count;
        }

        private static void ApplyAddedSelection(
            CompositeBrowser commands,
            Packet packet,
            Composite ownerComposite,
            Entity entity)
        {
            if (HasSelectionPath(packet))
            {
                ViewerSelectionSync.TryApply(packet);
                return;
            }

            QueueSelectAddedViewerAlias(commands, ownerComposite, entity);
        }

        private static void QueueSelectAddedViewerAlias(
            CompositeBrowser commands,
            Composite ownerComposite,
            Entity entity)
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed || commands == null || ownerComposite == null || entity == null)
                return;

            editor.BeginInvoke(new Action(() =>
            {
                ViewerSelectionSync.SuppressSyncBroadcastDepth++;
                try
                {
                    SelectAddedViewerAlias(commands, ownerComposite, entity);
                }
                finally
                {
                    ViewerSelectionSync.SuppressSyncBroadcastDepth--;
                }
            }));
        }

        private static void SelectAddedViewerAlias(
            CompositeBrowser commands,
            Composite ownerComposite,
            Entity entity)
        {
            if (commands == null || ownerComposite == null || entity == null)
                return;

            CompositeDisplay display = commands.CompositeDisplay;
            if (display != null && !display.IsDisposed && display.Populated
                && display.TrySelectAddedAlias(ownerComposite, entity))
            {
                return;
            }

            commands.LoadCompositeAndEntity(ownerComposite, entity);
        }

        private static bool ApplyDeletedCore(Packet packet)
        {
            CompositeBrowser commands = Singleton.Editor?.CompositeBrowser;
            if (commands?.Content?.Level == null)
                return false;

            Composite composite = commands.Content.Level.Commands.GetComposite(new ShortGuid(packet.composite));
            if (composite == null)
                return false;

            ShortGuid entityId = new ShortGuid(packet.entity);
            Entity entity = composite.GetEntityByID(entityId);
            if (entity == null)
            {
                RemoveEntityFromListIfShowingComposite(commands, composite, entityId);
                return true;
            }

            if (entity.variant != EntityVariant.ALIAS)
                return false;

            CompositeDisplay display = commands.CompositeDisplay;
            bool hasSelectionPath = packet.path_entities != null
                && packet.path_composites != null
                && packet.path_entities.Count > 0
                && packet.path_entities.Count == packet.path_composites.Count;

            ViewerSelectionSync.SuppressSyncBroadcastDepth++;
            try
            {
                if (display != null && !display.IsDisposed && display.Populated
                    && display.Composite?.shortGUID == composite.shortGUID)
                {
                    display.DeleteEntity(entity, ask: false, reloadUI: false);
                }
                else
                {
                    composite.RemoveAlias(entityId);
                    Singleton.OnEntityDeleted?.Invoke(entity);
                }

                if (hasSelectionPath)
                    ViewerSelectionSync.TryApply(packet);
            }
            finally
            {
                ViewerSelectionSync.SuppressSyncBroadcastDepth--;
            }

            return true;
        }

        private static void RemoveEntityFromListIfShowingComposite(
            CompositeBrowser commands,
            Composite composite,
            ShortGuid entityId)
        {
            CompositeDisplay display = commands?.CompositeDisplay;
            if (display == null || display.IsDisposed || !display.Populated || composite == null)
                return;

            if (display.Composite?.shortGUID != composite.shortGUID)
                return;

            display.RemoveEntityFromList(entityId);
        }

        private static void ApplyPacketParameters(Entity entity, Packet packet, LevelContent content)
        {
            if (packet.parameters == null || packet.parameters.Count == 0)
                return;

            foreach (SyncedParameter sync in packet.parameters)
            {
                if (sync == null)
                    continue;

                ParameterSync.ApplyToEntity(entity, sync, content);
            }
        }
    }
}
