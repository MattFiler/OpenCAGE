using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using System;
using System.Collections.Generic;

namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Applies ENTITY_ADDED / ENTITY_DELETED / ENTITY_ALIAS_RELEASED packets sent from the Godot Level Viewer.
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
                case PacketEvent.ENTITY_ALIAS_RELEASED:
                    return TryApplyAliasReleased(packet);
                default:
                    return false;
            }
        }

        /* The viewer's deep-select makes an alias for whatever was picked, and lets go of it again when
           the pick moves on without the alias having been used. Whether it was used is something only
           this side can say in full - it may have been edited here, or given a node on a flowgraph (live,
           or saved when the composite was left) - so the viewer asks rather than deletes. A deletion goes
           back to it through the ordinary ENTITY_DELETED broadcast. */
        private static bool TryApplyAliasReleased(Packet packet)
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return false;

            if (editor.InvokeRequired)
            {
                try
                {
                    editor.BeginInvoke(new Action(() => ApplyAliasReleasedCore(packet)));
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.Log("Websocket", "Failed to queue viewer alias release on UI thread: " + ex.Message);
                    return false;
                }
            }

            return ApplyAliasReleasedCore(packet);
        }

        private static bool ApplyAliasReleasedCore(Packet packet)
        {
            CompositeBrowser commands = Singleton.Editor?.CompositeBrowser;
            if (commands?.Content?.Level == null)
                return false;

            Composite composite = commands.Content.Level.Commands.GetComposite(new ShortGuid(packet.composite));
            if (composite == null)
                return false;

            ShortGuid entityId = new ShortGuid(packet.entity);
            AliasEntity alias = composite.GetEntityByID(entityId) as AliasEntity;

            /* The selection that replaced the alias goes first, so that what is deleted below is no longer
               selected: the ENTITY_DELETED that deletion broadcasts carries the selection of the moment, and
               the viewer reads an empty one as its own new selection having been abandoned as well. */
            if (HasSelectionPath(packet))
            {
                ViewerSelectionSync.SuppressSyncBroadcastDepth++;
                try
                {
                    ViewerSelectionSync.TryApply(packet);
                }
                finally
                {
                    ViewerSelectionSync.SuppressSyncBroadcastDepth--;
                }
            }

            if (alias == null || AliasHasReasonToStay(commands, composite, alias))
                return true;

            /* Not suppressed: the viewer still holds the alias, and this broadcast is what takes it away.
               (The inspector takes the replacing selection up a hop later, so the broadcast's path may say
               nothing is selected yet; the viewer knows to read the answer to its release as only that.) */
            CompositeDisplay display = commands.CompositeDisplay;
            if (display != null && !display.IsDisposed && display.Populated
                && display.Composite?.shortGUID == composite.shortGUID)
            {
                //Letting go of an alias the viewer made for a pick is housekeeping, not an edit
                using (OpenCAGE.Undo.UndoStack.Current.Suspend())
                    display.DeleteEntity(alias, ask: false, reloadUI: false);
            }
            else
            {
                composite.RemoveAlias(entityId);
                Singleton.OnEntityDeleted?.Invoke(alias);
            }
            return true;
        }

        /* Used from this side: linked, or on a flowgraph - the live pages when its composite is the one open
           (they aren't saved until the level is, or the composite is left), the saved pages otherwise.
           Parameters are deliberately not consulted. The inspector applies defaults the moment an alias is
           selected (it gains "position"), which is no edit; and a real parameter edit, from either side,
           reaches the viewer as a sync that commits the alias there, so it is never offered back at all. */
        private static bool AliasHasReasonToStay(CompositeBrowser commands, Composite composite, AliasEntity alias)
        {
            if (alias.childLinks != null && alias.childLinks.Count > 0)
                return true;
            foreach (Entity entity in composite.GetEntities())
            {
                if (entity == alias || entity.childLinks == null)
                    continue;
                foreach (EntityConnector link in entity.childLinks)
                {
                    if (link.linkedEntityID == alias.shortGUID)
                        return true;
                }
            }

            CompositeDisplay display = commands.CompositeDisplay;
            if (display != null && !display.IsDisposed && display.Composite?.shortGUID == composite.shortGUID)
                return display.AnyFlowgraphsContainEntity(alias);

            foreach (CathodeLib.CompositeFlowgraphTable.FlowgraphMeta layout in FlowgraphLayoutManager.GetLayouts(composite))
            {
                foreach (CathodeLib.CompositeFlowgraphTable.FlowgraphMeta.NodeMeta node in layout.Nodes)
                {
                    if (node.EntityGUID == alias.shortGUID)
                        return true;
                }
            }
            return false;
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
                    //Viewer-originated: select without stealing Win32 focus from the viewer
                    ViewerSelectionSync.RunAsViewerOriginated(
                        () => SelectAddedViewerAlias(commands, ownerComposite, entity));
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
