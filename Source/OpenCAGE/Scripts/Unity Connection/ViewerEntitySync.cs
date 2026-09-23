using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using System;
using System.Collections.Generic;
using System.Linq;

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
            //Level is there from the moment a load begins; its commands only once it is done
            if (commands?.Content == null || !commands.Content.IsLevelDataLoaded)
                return false;

            Composite composite = commands.Content.Level.Commands.GetComposite(new ShortGuid(packet.composite));
            if (composite == null)
                return false;

            /* Every alias the packet lets go of: the one on `entity`, and with it a box's worth abandoned
               together in batch_entities, which are judged and deleted as one set below */
            List<AliasEntity> aliases = new List<AliasEntity>();
            HashSet<uint> taken = new HashSet<uint>();
            void Take(uint id)
            {
                if (id != 0 && taken.Add(id) && composite.GetEntityByID(new ShortGuid(id)) is AliasEntity released)
                    aliases.Add(released);
            }
            Take(packet.entity);
            if (packet.batch_entities != null)
            {
                foreach (uint id in packet.batch_entities)
                    Take(id);
            }

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

            if (aliases.Count > 1)
            {
                /* A hop later, not now: the selection that replaced them (bundled above, or on the
                   ENTITY_ADDED just before this packet) reaches the inspector through a BeginInvoke the
                   display posts on itself, and a deletion run before that - while the inspector still
                   showed the set - cleared the inspector, which the viewer heard as nothing being selected
                   any more and let go of the new selection as well. Posted behind that populate, on the
                   same control, the set is deleted out from under nothing. (On the editor it would run
                   first: this packet is applied inside the editor's own BeginInvoke, and a callback queued
                   on the control whose callback is running is taken in the same turn.) */
                CompositeDisplay queueOn = commands.CompositeDisplay;
                System.Windows.Forms.Control host = queueOn != null && !queueOn.IsDisposed && queueOn.IsHandleCreated
                    ? (System.Windows.Forms.Control)queueOn
                    : Singleton.Editor;
                host.BeginInvoke(new Action(() => ReleaseAliasSet(commands, composite, aliases)));
                return true;
            }
            if (aliases.Count == 0)
                return true;

            AliasEntity alias = aliases[0];
            ShortGuid entityId = alias.shortGUID;
            if (AliasHasReasonToStay(commands, composite, alias))
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
                /* The pending event is what names the composite on the ENTITY_DELETED that follows
                   (Send takes the open one otherwise, and this is not it) - the viewer removes by
                   composite and entity, and with the wrong composite it found nothing to remove. */
                Singleton.OnEntityDeletePending?.Invoke(alias, composite);
                composite.RemoveAlias(entityId);
                Singleton.OnEntityDeleted?.Invoke(alias);
            }
            return true;
        }

        /* A box's worth of aliases let go of together: judged as one set, and the ones with no reason to
           stay deleted as one set. One at a time, each deletion walked every saved flowgraph layout in the
           level and every entity of the composite - about 60 ms per alias on Torrens' environment, 8.5 s
           for a box of 130 and a wedge past 80 s for one of 300. */
        private static void ReleaseAliasSet(CompositeBrowser commands, Composite composite, List<AliasEntity> aliases)
        {
            //The level, the composite or the aliases themselves may have gone in the hop
            if (commands?.Content == null || !commands.Content.IsLevelDataLoaded
                || commands.Content.Level.Commands.GetComposite(composite.shortGUID) != composite)
            {
                return;
            }

            HashSet<ShortGuid> staying = AliasesWithReasonToStay(commands, composite, aliases);
            List<AliasEntity> released = new List<AliasEntity>();
            foreach (AliasEntity alias in aliases)
            {
                if (composite.GetEntityByID(alias.shortGUID) == alias && !staying.Contains(alias.shortGUID))
                    released.Add(alias);
            }
            if (released.Count == 0)
                return;

            //Not suppressed, as for one: the ENTITY_DELETED each deletion broadcasts is what takes it out of the viewer
            CompositeDisplay display = commands.CompositeDisplay;
            bool onScreen = display != null && !display.IsDisposed && display.Populated
                && display.Composite?.shortGUID == composite.shortGUID;
            DeleteReleasedAliases(commands, composite, released, onScreen ? display : null);
        }

        /* The set removed the way EntityDeleteEdit removes one - the dictionary entry, every link into it,
           the trigger and animation references that went through it, its nodes on the saved pages - but in
           one pass over the composite and one over the saved layouts for the lot. Housekeeping, as for one:
           nothing is recorded. Each alias still raises its own OnEntityDeletePending and OnEntityDeleted,
           so the open pages and the rest follow as for any removal; the entity list takes its rows out
           together, and the viewer hears of the lot in one ENTITY_DELETED. */
        private static void DeleteReleasedAliases(CompositeBrowser commands, Composite composite, List<AliasEntity> aliases, CompositeDisplay display)
        {
            HashSet<ShortGuid> ids = new HashSet<ShortGuid>();
            foreach (AliasEntity alias in aliases)
                ids.Add(alias.shortGUID);
            bool PointsThroughUs(EntityPath path) => path?.path != null && path.path.Length >= 2 && ids.Contains(path.path[path.path.Length - 2]);

            using (OpenCAGE.Undo.UndoStack.Current.Suspend())
            using (FlowgraphLayoutManager.BeginDeletingTogether(aliases, composite))
            using (display?.EntityListPanel?.List?.BatchUpdate()) //one re-layout of the list for the lot, not one per row
            using (Send.BeginDeletedBatch(composite)) //one ENTITY_DELETED for the lot, not one per alias
            {
                foreach (AliasEntity alias in aliases)
                    composite.RemoveAlias(alias.shortGUID);

                //Into new lists, as EntityDeleteEdit does: an undo step may hold the ones the entities have
                foreach (Entity other in composite.GetEntities())
                {
                    if (other.childLinks != null && other.childLinks.Exists(o => ids.Contains(o.linkedEntityID)))
                        other.childLinks = other.childLinks.Where(o => !ids.Contains(o.linkedEntityID)).ToList();

                    if (other is TriggerSequence triggerSequence)
                    {
                        if (triggerSequence.sequence.Exists(o => PointsThroughUs(o.connectedEntity)))
                            triggerSequence.sequence = triggerSequence.sequence.Where(o => !PointsThroughUs(o.connectedEntity)).ToList();
                    }
                    else if (other is CAGEAnimation animation)
                    {
                        if (animation.connections.Exists(o => PointsThroughUs(o.connectedEntity)))
                            animation.connections = animation.connections.Where(o => !PointsThroughUs(o.connectedEntity)).ToList();
                    }
                }
                commands.Content.Level.Commands.Utils.PurgedComposites.purged.Clear(); //as EntityDeleteEdit does

                foreach (AliasEntity alias in aliases)
                {
                    /* The pending event names the composite on the ENTITY_DELETED that follows (Send takes the
                       open one otherwise, which off screen is not it); the layouts it trims were done for the
                       set above */
                    Singleton.OnEntityDeletePending?.Invoke(alias, composite);
                    Singleton.OnEntityDeleted?.Invoke(alias);
                }
            }

            display?.RefreshNodeMarkers();
        }

        /* Used from this side: linked, or on a flowgraph - the live pages when its composite is the one open
           (they aren't saved until the level is, or the composite is left), the saved pages otherwise.
           Parameters are deliberately not consulted. The inspector applies defaults the moment an alias is
           selected (it gains "position"), which is no edit; and a real parameter edit, from either side,
           reaches the viewer as a sync that commits the alias there, so it is never offered back at all. */
        private static bool AliasHasReasonToStay(CompositeBrowser commands, Composite composite, AliasEntity alias)
        {
            return AliasesWithReasonToStay(commands, composite, new List<AliasEntity>() { alias }).Count > 0;
        }

        /* The same verdict for a set, with the composite's links walked once for the lot: the ids of the
           aliases that stay */
        private static HashSet<ShortGuid> AliasesWithReasonToStay(CompositeBrowser commands, Composite composite, List<AliasEntity> aliases)
        {
            HashSet<ShortGuid> staying = new HashSet<ShortGuid>();
            HashSet<ShortGuid> candidates = new HashSet<ShortGuid>();
            foreach (AliasEntity alias in aliases)
            {
                if (alias.childLinks != null && alias.childLinks.Count > 0)
                    staying.Add(alias.shortGUID);
                else
                    candidates.Add(alias.shortGUID);
            }

            if (candidates.Count > 0)
            {
                foreach (Entity entity in composite.GetEntities())
                {
                    if (entity.childLinks == null)
                        continue;
                    foreach (EntityConnector link in entity.childLinks)
                    {
                        if (candidates.Contains(link.linkedEntityID))
                            staying.Add(link.linkedEntityID);
                    }
                }
            }

            CompositeDisplay display = commands.CompositeDisplay;
            bool onScreen = display != null && !display.IsDisposed && display.Composite?.shortGUID == composite.shortGUID;
            HashSet<ShortGuid> savedNodes = null;
            if (!onScreen)
            {
                savedNodes = new HashSet<ShortGuid>();
                foreach (CathodeLib.CompositeFlowgraphTable.FlowgraphMeta layout in FlowgraphLayoutManager.GetLayouts(composite))
                {
                    foreach (CathodeLib.CompositeFlowgraphTable.FlowgraphMeta.NodeMeta node in layout.Nodes)
                        savedNodes.Add(node.EntityGUID);
                }
            }
            foreach (AliasEntity alias in aliases)
            {
                if (staying.Contains(alias.shortGUID))
                    continue;
                if (onScreen ? display.AnyFlowgraphsContainEntity(alias) : savedNodes.Contains(alias.shortGUID))
                    staying.Add(alias.shortGUID);
            }
            return staying;
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
            //Level is there from the moment a load begins; its commands only once it is done
            if (commands?.Content == null || !commands.Content.IsLevelDataLoaded)
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
            /* One of a run of adds (a viewport box making aliases): the run's last packet selects them all.
               Selecting what had arrived at each step re-selected a growing set in the list every time -
               a box of 128 aliases kept this side busy for 36 s that way, 3 s this way. */
            if (packet.selection_follows)
                return;

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

        /* Delete was pressed in the viewport. The level data lives here, so the deletion happens
           here - everything that was selected there, asked about once - and the viewer hears about
           it through the ENTITY_DELETED packets that follow, like any other deletion. */
        public static bool TryApplyDeleteRequest(Packet packet)
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
                    editor.BeginInvoke(new Action(() => ApplyDeleteRequestCore(packet)));
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.Log("Websocket", "Failed to queue viewer delete on UI thread: " + ex.Message);
                    return false;
                }
            }

            return ApplyDeleteRequestCore(packet);
        }

        private static bool ApplyDeleteRequestCore(Packet packet)
        {
            CompositeBrowser commands = Singleton.Editor?.CompositeBrowser;
            //Level is there from the moment a load begins; its commands only once it is done
            if (commands?.Content == null || !commands.Content.IsLevelDataLoaded)
                return false;

            Composite composite = commands.Content.Level.Commands.GetComposite(new ShortGuid(packet.composite));
            CompositeDisplay display = commands.CompositeDisplay;
            if (composite == null || display == null || display.IsDisposed || !display.Populated
                || display.Composite?.shortGUID != composite.shortGUID)
            {
                return false;
            }

            List<Entity> entities = new List<Entity>();
            void Add(uint entityId)
            {
                Entity entity = entityId == 0 ? null : composite.GetEntityByID(new ShortGuid(entityId));
                if (entity != null && !entities.Contains(entity))
                    entities.Add(entity);
            }

            Add(packet.entity);
            if (packet.selection_entities != null)
                foreach (uint entityId in packet.selection_entities)
                    Add(entityId);

            if (entities.Count == 0)
                return false;

            bool deleted = false;
            ViewerSelectionSync.RunAsViewerOriginated(() => deleted = display.DeleteEntities(entities));
            return deleted;
        }

        private static bool ApplyDeletedCore(Packet packet)
        {
            CompositeBrowser commands = Singleton.Editor?.CompositeBrowser;
            //Level is there from the moment a load begins; its commands only once it is done
            if (commands?.Content == null || !commands.Content.IsLevelDataLoaded)
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
