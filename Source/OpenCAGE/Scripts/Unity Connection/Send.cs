using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using Newtonsoft.Json;
using OpenCAGE;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Animation;
using WebSocketSharp.Server;

namespace OpenCAGE.UnityConnection
{
    public static class Send
    {
        public const int DefaultPort = 1702;
        public const int MaxPortAttempts = 20;

        private static WebSocketServer _server;
        private static Client _serverLogic;

        public static int Port { get; private set; } = DefaultPort;
        public static bool Started => _server != null;
        public static bool Connected => _server != null && _server.WebSocketServices["/commands_editor"].Sessions.Count != 0;

        private static bool _isDirty = false;
        private static string _pendingLevelLoadName;

        static Send()
        {
            Singleton.OnLevelLoaded += LevelLoaded;
            Singleton.OnSaved += LevelSaved;
            Singleton.OnCompositeAdded += CompositeAdded;
            Singleton.OnCompositeReloaded += CompositeReloaded;
            Singleton.OnCompositeSelected += CompositeSelected;
            Singleton.OnCompositeDeleted += CompositeDeleted;
            Singleton.OnEntityReloaded += EntitySelected;
            Singleton.OnEntitiesReloaded += EntitiesSelected;
            Singleton.OnSelectionCleared += SelectionCleared;
            Singleton.OnEntityMoved += EntityMoved;
            Singleton.OnEntityAdded += EntityAdded;
            Singleton.OnEntityDeletePending += EntityDeletePending;
            Singleton.OnEntityDeleted += EntityDeleted;
            Singleton.OnResourceModified += ResourceModified;
            Singleton.OnEntityParameterModified += EntityParameterModified;
            EntityClipboard.Changed += EntityClipboardChanged;

            ViewerResourceSync.Initialise();
            ViewerZoneSync.Initialise();
        }

        public static bool Start()
        {
            Stop();

            for (int attempt = 0; attempt < MaxPortAttempts; attempt++)
            {
                int port = DefaultPort + attempt;

                try
                {
                    WebSocketServer server = new WebSocketServer("ws://localhost:" + port);
                    /* websocket-sharp pings every session once a minute and drops any that doesn't answer
                     * within WaitTime (1 s). The viewer answers from its IO thread while a receive is pending,
                     * but a packet that lands while its main thread is busy (a populate, a big refresh) completes
                     * that receive and the next one isn't issued until the main thread frees up - so the sweep
                     * dropped the session mid-load and the reconnect triggered a full resync and repopulate.
                     * One local client that reconnects on its own needs no sweeping. */
                    server.KeepClean = false;
                    server.AddWebSocketService<Client>("/commands_editor", (service) =>
                    {
                        _serverLogic = service;
                        _serverLogic.OnConnect += SyncClient;
                        _serverLogic.OnDisconnect += OnViewerDisconnected;
                    });
                    server.Start();
                    _server = server;
                    Port = port;
                    return true;
                }
                catch
                {
                }
            }

            _server = null;
            Port = DefaultPort;
            return false;
        }

        public static void Stop()
        {
            if (_server != null)
                _server.Stop();
            _server = null;
        }

        /* Force-send a new generic packet to re-sync (ideal for settings changes) */
        public static void SendReSyncPacket()
        {
            SendData(GeneratePacket());
        }

        /* Send only render filter state (fast path on the Unity client) */
        public static void SendRenderFilterPacket()
        {
            Packet packet = new Packet(PacketEvent.RENDER_FILTERS_CHANGED);
            packet.box_render_filters = RenderFilters.GetPacketFilters();
            packet.scene_render_filters = RenderFilters.GetScenePacketFilters();
            SendData(packet);
        }

        /* Send the level's zones and what each one covers. Only ViewerZoneSync calls this - it owns
           when the table is worth recalculating. */
        internal static void SendZonesPacket(List<SyncedZone> zones)
        {
            Packet packet = new Packet(PacketEvent.ZONES_CHANGED);
            packet.zones = zones ?? new List<SyncedZone>();
            packet.show_zones = SettingsManager.GetBool(Settings.ShowZones);
            SendData(packet);
        }

        /* Push one edited material-mapping set to the viewer (in-memory; disk save happens on level save). */
        public static void NotifyMaterialMappingModified(MaterialMappings.MaterialMapping mapping)
        {
            if (!Connected || mapping == null)
                return;

            SyncedMaterialMappingSet payload = MaterialMappingSync.Pack(mapping);
            if (payload == null)
                return;

            Packet packet = new Packet(PacketEvent.MATERIAL_MAPPING_MODIFIED)
            {
                material_mapping = payload,
            };
            SendData(packet);
        }

        /* Animation Mode in the CAGEAnimation editor: what its tracks hold at the playhead, for the
           viewer to paint on top of the scene. The whole set goes each time - a target dropping out of
           it is how the viewer learns to put that one back. */
        internal static void SendAnimationPreviewPacket(float time, List<SyncedAnimationTarget> targets)
        {
            if (!Connected)
                return;

            Packet packet = new Packet(PacketEvent.ANIMATION_PREVIEW);
            packet.animation_preview_active = true;
            packet.animation_preview_time = time;
            packet.animation_preview = targets ?? new List<SyncedAnimationTarget>();
            SendData(packet);
        }

        /* Animation Mode is over - everything the preview was holding goes back to where it rests. */
        internal static void SendAnimationPreviewCleared()
        {
            if (!Connected)
                return;

            Packet packet = new Packet(PacketEvent.ANIMATION_PREVIEW);
            packet.animation_preview_active = false;
            packet.animation_preview = new List<SyncedAnimationTarget>();
            SendData(packet);
        }

        /* Send viewer settings (focus, hide nested previews, etc.) */
        public static void SendSettingsPacket()
        {
            Packet packet = new Packet(PacketEvent.SETTINGS_CHANGED);
            packet.focus_object = SettingsManager.GetBool(Settings.FocusOnSelected);
            packet.fix_camera_to_selected = SettingsManager.GetBool(Settings.FixCameraToSelected);
            packet.show_camera_position = SettingsManager.GetBool(Settings.ShowCameraPosition);
            packet.model_reference_wireframe = SettingsManager.GetBool(Settings.RenderWireframe);
            packet.hide_nested_script_entities = SettingsManager.GetBool(Settings.HideNestedScriptEntities);
            packet.highlight_aliases = SettingsManager.GetBool(Settings.HighlightAliases);
            packet.highlight_proxies = SettingsManager.GetBool(Settings.HighlightProxies);
            packet.transform_grid_snap = TransformSnapDefinitions.NormalizeGridSnap(SettingsManager.GetFloat(Settings.TransformGridSnap));
            packet.rotation_snap_degrees = TransformSnapDefinitions.NormalizeRotationSnap(SettingsManager.GetFloat(Settings.RotationSnapDegrees));
            packet.transform_vertex_snap = SettingsManager.GetBool(Settings.TransformVertexSnap);
            packet.deep_select_mode = (int)LevelViewerViewportDefinitions.NormalizeDeepSelectMode(
                SettingsManager.GetInteger(Settings.LevelViewerDeepSelectMode));
            packet.gizmo_mode = (int)LevelViewerViewportDefinitions.NormalizeGizmoMode(
                SettingsManager.GetInteger(Settings.LevelViewerGizmoMode));
            packet.create_function_type = ViewerCreateMode.ActiveFunctionType;
            packet.show_navmesh_state = ViewerStateInfoMode.NavMeshState;
            packet.show_cover_state = ViewerStateInfoMode.CoverState;
            packet.show_zones = SettingsManager.GetBool(Settings.ShowZones);
            packet.selection_highlight_mode = (int)LevelViewerViewportDefinitions.NormalizeHighlightMode(
                SettingsManager.GetInteger(Settings.LevelViewerHighlightMode));
            packet.render_galaxy = SettingsManager.GetBool(Settings.RenderGalaxy);
            packet.scene_render_filters = RenderFilters.GetScenePacketFilters();
            SendData(packet);
        }

        /* A composite was dropped on the viewport -> ask the viewer to raycast the spot it landed on.
           It answers with ENTITY_CREATE_REQUEST, which is where the instance actually gets created. */
        public static void SendCompositeDropPacket(Composite compositeToInstance, float viewportX, float viewportY)
        {
            if (!Connected || compositeToInstance == null)
                return;

            Packet packet = GeneratePacket(PacketEvent.VIEWPORT_DROP_REQUEST);
            packet.create_composite_instance = compositeToInstance.shortGUID.AsUInt32;
            packet.drop_viewport_x = viewportX;
            packet.drop_viewport_y = viewportY;
            SendData(packet);
        }

        /* A function type was dropped on the viewport out of the entity palette (ViewerFunctionDrop) -> the same
           request as a composite drop, and the same ENTITY_CREATE_REQUEST answer, which creates the entity. */
        public static void SendFunctionDropPacket(FunctionType function, float viewportX, float viewportY)
        {
            if (!Connected)
                return;

            Packet packet = GeneratePacket(PacketEvent.VIEWPORT_DROP_REQUEST);
            packet.drop_function_type = (uint)function;
            packet.drop_viewport_x = viewportX;
            packet.drop_viewport_y = viewportY;
            SendData(packet);
        }

        /* The viewport's context menu (ViewerContextMenu): an entry whose action lives in the viewer, or word
           that the menu has opened or closed. A full packet, so a viewer that doesn't know this event takes
           it as nothing more than a re-sync of the selection it already has. */
        public static void SendViewportAction(ViewportAction action)
        {
            if (!Connected)
                return;

            Packet packet = GeneratePacket(PacketEvent.VIEWPORT_ACTION);
            packet.viewport_action = (int)action;
            SendData(packet);
        }

        public static void NotifyLevelLoadStarting(string levelName)
        {
            _pendingLevelLoadName = levelName;
            if (Connected)
                SendLevelLoadedPacket(levelName);
        }

        public static void NotifyLevelLoadAborted()
        {
            _pendingLevelLoadName = null;
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return;

            editor.BeginInvoke(new System.Action(() => editor.EndViewerPopulateProgress(0, forceClose: true)));
        }

        private static void OnViewerDisconnected()
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return;

            editor.BeginInvoke(new System.Action(() => editor.OnViewerDisconnected()));
        }

        /* A level has just been loaded -> load its data in Unity */
        private static void LevelLoaded(LevelContent content)
        {
            _isDirty = false;
            _pendingLevelLoadName = null;
            SendLevelLoadedPacket();
        }

        private static void SendLevelLoadedPacket(string levelNameOverride = null)
        {
            ViewerResourceSync.NotifyViewerReloading();
            SendLevelLoadedPacketCore(levelNameOverride);
        }

        private static void SendLevelLoadedPacketCore(string levelNameOverride)
        {
            //A batch cannot outlive the level it was for; the viewer forgets its side on LEVEL_LOADED too
            _sceneBatchDepth = 0;
            Packet packet = GeneratePacket(PacketEvent.LEVEL_LOADED);
            //A real load, so the viewer reads the level again even when it is the one it already has - loading the
            //same level again is how unsaved changes get thrown away. The LEVEL_LOADED a save sends does not say this.
            packet.level_reload = true;
            if (!string.IsNullOrEmpty(levelNameOverride))
                packet.level_name = levelNameOverride;
            SendData(packet);
        }

        /* The level has been saved -> clear our dirty flag */
        private static void LevelSaved()
        {
            _isDirty = false;
            SendData(GeneratePacket(PacketEvent.LEVEL_LOADED)); //NEW: Fire another loaded event to reload write indexes on the Unity side.

            /* The viewer keeps the level it read from disk, and that LEVEL_LOADED does not make it read
               it again (it is the same level, already loaded). The generated navigation data has just
               been rewritten though - regenerated outright by an instanced save - so the state overlays
               would go on drawing the navmesh and cover from before the build. */
            SendData(GeneratePacket(PacketEvent.LEVEL_STATE_RESOURCES_MODIFIED));
        }

        /* A composite has been loaded -> open it in the Unity scene */
        private static void CompositeSelected(Composite composite)
        {
            Packet p = GeneratePacket(PacketEvent.COMPOSITE_SELECTED);
            p.composite = composite.shortGUID.AsUInt32;
            p.composite_name = composite.name ?? "";
            SendData(p);
        }
        /* Have the viewer build the composite it is showing again, from the script as it has it now.
           For after an import: the composite is opened (and so populated in the viewer, empty) before
           its contents can be sent - those wait on the resource sync - and the entities that then land
           one at a time do not put a nested instance's contents on screen the way a populate does. Queue
           this behind the same sync as the contents and the viewer rebuilds once everything is there;
           the viewer treats a plain re-selection of the composite it already holds as nothing to do. */
        internal static void RefreshCompositeInViewer(Composite composite)
        {
            if (composite == null || !Connected)
                return;
            Packet p = GeneratePacket(PacketEvent.COMPOSITE_RELOADED);
            p.composite = composite.shortGUID.AsUInt32;
            p.composite_name = composite.name ?? "";
            SendData(p);
        }
        private static void CompositeReloaded(Composite composite)
        {
            // Hierarchy drill / in-place composite reload — sync path only. COMPOSITE_SELECTED is reserved
            // for browser/root composite switches that should rebuild the viewer scene.
            SendData(GeneratePacket(PacketEvent.GENERIC_DATA_SYNC));
        }

        /* Composite lifetime events -> sync them to Unity */
        private static void CompositeAdded(Composite composite)
        {
            Packet p = GeneratePacket(PacketEvent.COMPOSITE_ADDED);
            p.composite = composite.shortGUID.AsUInt32;
            p.composite_name = composite.name ?? "";
            SendData(p);

            /* The viewer only ever gets an empty composite from that, and learns of entities one at a
             * time as they are added to the OPEN composite. A composite that arrives already populated
             * (an import's placing composite) has to have its contents sent explicitly, addressed to it.
             * That waits for the resource sync, because the renderables name model and material indexes
             * the viewer only holds once the snapshot carrying a just-imported model has landed. */
            if (composite.functions.Count + composite.variables.Count + composite.aliases.Count + composite.proxies.Count != 0)
                ViewerResourceSync.AfterNextSync(() => SendCompositeContents(composite));
        }

        /* A run of composite changes the viewer should only follow in its script copy, because a
           rebuild of the scene comes at the end of it (an import). Nested: only the outermost pair is
           sent. Always paired - a batch left open would leave the viewer never touching its scene. */
        private static int _sceneBatchDepth;
        internal static void BeginSceneBatch()
        {
            if (_sceneBatchDepth++ == 0)
                SendData(GeneratePacket(PacketEvent.SCENE_BATCH_BEGIN));
        }
        internal static void EndSceneBatch()
        {
            if (_sceneBatchDepth <= 0)
                return;
            if (--_sceneBatchDepth == 0)
                SendData(GeneratePacket(PacketEvent.SCENE_BATCH_END));
        }

        /* Every entity of a composite, in one COMPOSITE_CONTENTS packet addressed to that composite */
        internal static void SendCompositeContents(Composite composite)
        {
            if (composite == null)
                return;
            Stopwatch timer = Stopwatch.StartNew();
            Packet p = GeneratePacket(PacketEvent.COMPOSITE_CONTENTS);
            p.composite = composite.shortGUID.AsUInt32;
            p.composite_name = composite.name ?? "";
            LevelContent content = Singleton.Editor?.CompositeBrowser?.Content;
            foreach (VariableEntity entity in composite.variables) p.composite_entities.Add(EntityRecordOf(entity, content));
            foreach (FunctionEntity entity in composite.functions) p.composite_entities.Add(EntityRecordOf(entity, content));
            foreach (AliasEntity entity in composite.aliases) p.composite_entities.Add(EntityRecordOf(entity, content));
            foreach (ProxyEntity entity in composite.proxies) p.composite_entities.Add(EntityRecordOf(entity, content));
            long built = timer.ElapsedMilliseconds;
            SendData(p);
            Debug.Log("Composite Sync", "Sent the contents of " + composite.name + ": " + p.composite_entities.Count + " entities, built in " + built + " ms, sent in " + (timer.ElapsedMilliseconds - built) + " ms");
        }

        /* What ENTITY_ADDED says about an entity, as a record */
        private static EntityRecord EntityRecordOf(Entity entity, LevelContent content)
        {
            EntityRecord record = new EntityRecord()
            {
                entity = entity.shortGUID.AsUInt32,
                entity_variant = entity.variant,
            };
            switch (entity.variant)
            {
                case EntityVariant.FUNCTION:
                    record.entity_function = ((FunctionEntity)entity).function.AsUInt32;
                    break;
                case EntityVariant.PROXY:
                    record.entity_pointed = ((ProxyEntity)entity).proxy.pathUint;
                    break;
                case EntityVariant.ALIAS:
                    record.entity_pointed = ((AliasEntity)entity).alias.pathUint;
                    break;
            }
            foreach (Parameter parameter in entity.parameters)
            {
                SyncedParameter sync = ParameterSync.Pack(parameter, content);
                if (sync != null)
                    record.parameters.Add(sync);
            }
            return record;
        }
        private static void CompositeDeleted(Composite composite)
        {
            Packet p = GeneratePacket(PacketEvent.COMPOSITE_DELETED);
            p.composite = composite.shortGUID.AsUInt32;
            SendData(p);
        }

        /* Entity lifetime events -> sync them to Unity */
        private static void EntitySelected(Entity entity)
        {
            SendData(GeneratePacket(PacketEvent.ENTITY_SELECTED, entity));
        }
        /* Several entities selected together. The plain packet already describes the whole selection
           from the inspector, so there is nothing to add to it here. */
        private static void EntitiesSelected(List<Entity> entities)
        {
            if (entities == null || entities.Count == 0)
                return;

            SendData(GeneratePacket(PacketEvent.ENTITY_SELECTED));
        }
        /* The inspector was cleared, so nothing is selected any more. The plain packet carries no entity
           when nothing is selected, which is exactly the deselect the viewer needs to drop its highlight
           and gizmo - without this it kept showing the last selection (issue 675). */
        private static void SelectionCleared()
        {
            SendData(GeneratePacket(PacketEvent.ENTITY_SELECTED));
        }
        /* The entity clipboard was set or emptied -> tell the viewer whether there is anything to paste; its
           context menu greys Paste out when there isn't. A full packet, so a viewer that doesn't know this
           event takes it as nothing more than a re-sync of the selection it already has. */
        private static bool _clipboardHadContent;
        private static void EntityClipboardChanged()
        {
            //Only when the answer changes: a duplicate or a level load sets and clears it in one go, and the state
            //rides every packet anyway, so a viewer that connects later still gets it
            bool hasContent = EntityClipboard.HasContent;
            if (hasContent == _clipboardHadContent)
                return;
            _clipboardHadContent = hasContent;
            if (!Connected)
                return;
            SendData(GeneratePacket(PacketEvent.ENTITY_CLIPBOARD_CHANGED));
        }
        /* Deliberately never writes to the entity. This is a notification, and the caller is often
           part-way through its own edit - resetting an alias override raises it and then removes the
           parameter. Nulling the live parameter's content behind the caller's back left the grid
           holding a transform row with nothing in it, which threw on the next repaint. */
        private static void EntityMoved(cTransform transform, Entity entity)
        {
            _isDirty = true;

            Parameter position = entity?.GetParameter("position");
            if (transform != null)
            {
                //Send what we were handed, not whatever is stored - callers raise this either side of the write
                SendParameterPacket(
                    entity,
                    position == null
                        ? new Parameter("position", transform)
                        : new Parameter(position.name, transform, position.variant),
                    false);
                return;
            }

            //No transform means the position is going away; Pack keys off the removed flag, not the content
            if (position != null)
                SendParameterPacket(entity, position, true);
        }
        /* The viewer removes by composite and entity, and a packet names the open composite - which
           is not always the deleted entity's (the references window deletes in any composite). Only
           the pending event says which composite it is, so that is kept for the deletion that follows. */
        private static Entity _pendingDeletion = null;
        private static Composite _pendingDeletionComposite = null;
        private static void EntityDeletePending(Entity entity, Composite composite)
        {
            _pendingDeletion = entity;
            _pendingDeletionComposite = composite;
        }

        /* A run of deletions that goes as one ENTITY_DELETED (batch_entities) rather than one each - a
           box's worth of deep-select aliases let go of, where a packet per alias was 6 ms to build and
           send each, and hundreds for the viewer to take one at a time. Sent when the scope ends, with the
           selection of that moment; the viewer removes every entity it names. */
        private static List<Entity> _deletedBatch = null;
        private static Composite _deletedBatchComposite = null;
        internal static IDisposable BeginDeletedBatch(Composite composite)
        {
            //Nested: the outer scope sends
            if (_deletedBatch != null)
                return new DeletedBatchScope(null);
            _deletedBatch = new List<Entity>();
            _deletedBatchComposite = composite;
            return new DeletedBatchScope(EndDeletedBatch);
        }
        private static void EndDeletedBatch()
        {
            List<Entity> batch = _deletedBatch;
            Composite composite = _deletedBatchComposite;
            _deletedBatch = null;
            _deletedBatchComposite = null;
            if (batch == null || batch.Count == 0)
                return;
            Packet removed = GeneratePacket(PacketEvent.ENTITY_DELETED, batch[0]);
            if (composite != null)
                removed.composite = composite.shortGUID.AsUInt32;
            foreach (Entity entity in batch)
                removed.batch_entities.Add(entity.shortGUID.AsUInt32);
            SendData(removed);
        }
        private sealed class DeletedBatchScope : IDisposable
        {
            private Action _end;
            public DeletedBatchScope(Action end) { _end = end; }
            public void Dispose()
            {
                Action end = _end;
                _end = null;
                end?.Invoke();
            }
        }
        private static void EntityDeleted(Entity entity)
        {
            _isDirty = true;
            if (_deletedBatch != null)
            {
                //Goes with the rest when the batch ends
                _deletedBatch.Add(entity);
                _pendingDeletion = null;
                _pendingDeletionComposite = null;
                return;
            }
            Packet removed = GeneratePacket(PacketEvent.ENTITY_DELETED, entity);
            if (entity != null && entity == _pendingDeletion && _pendingDeletionComposite != null)
                removed.composite = _pendingDeletionComposite.shortGUID.AsUInt32;
            _pendingDeletion = null;
            _pendingDeletionComposite = null;
            SendData(removed);
        }
        private static void EntityAdded(Entity entity)
        {
            _isDirty = true;
            SendData(EntityAddedPacket(entity, null));
        }

        /* A proxy or alias points somewhere else now. The viewer's copy is the path it was added with,
           so it is removed and added again with the new one - the overrides it carries then land on
           the new target. */
        internal static void EntityRetargeted(Entity entity, Composite composite)
        {
            if (entity == null || (entity.variant != EntityVariant.PROXY && entity.variant != EntityVariant.ALIAS))
                return;
            _isDirty = true;
            Packet removed = GeneratePacket(PacketEvent.ENTITY_DELETED, entity);
            if (composite != null)
                removed.composite = composite.shortGUID.AsUInt32;
            SendData(removed);
            SendData(EntityAddedPacket(entity, composite));
        }

        /* An entity as the viewer needs to add it: its parameters bundled so it spawns with the correct
           position and renderables (e.g. duplicated entities). The composite is the open one unless the
           caller names another, which the viewer takes from the packet rather than its own selection. */
        private static Packet EntityAddedPacket(Entity entity, Composite composite)
        {
            Packet p = GeneratePacket(PacketEvent.ENTITY_ADDED, entity);
            if (composite != null)
                p.composite = composite.shortGUID.AsUInt32;
            switch (entity.variant)
            {
                case EntityVariant.PROXY:
                    p.entity_pointed = ((ProxyEntity)entity).proxy.pathUint;
                    break;
                case EntityVariant.ALIAS:
                    p.entity_pointed = ((AliasEntity)entity).alias.pathUint;
                    break;
            }

            LevelContent content = Singleton.Editor?.CompositeBrowser?.Content;
            foreach (Parameter parameter in entity.parameters)
            {
                SyncedParameter sync = ParameterSync.Pack(parameter, content);
                if (sync != null)
                    p.parameters.Add(sync);
            }
            return p;
        }
        private static void ResourceModified()
        {
            _isDirty = true;
            SendSelectedEntityResource();
        }
        /* The selected entity's resource as it stands now. What the viewer draws for it comes from the
           model and material write indexes in here, so it goes again whenever those may have changed. */
        internal static void SendSelectedEntityResource()
        {
            Entity entity = Singleton.Editor?.CompositeDisplay?.EntityDisplay?.Entity;
            if (entity == null)
                return;

            Parameter resource = entity.GetParameter("resource");
            if (resource != null)
                SendParameterPacket(entity, resource, false);
        }
        private static void EntityParameterModified(Entity entity, Parameter parameter, bool removed)
        {
            if (entity == null || parameter == null)
                return;

            _isDirty = true;
            SendParameterPacket(entity, parameter, removed);
        }

        private static void SendParameterPacket(Entity entity, Parameter parameter, bool removed)
        {
            LevelContent content = Singleton.Editor?.CompositeBrowser?.Content;
            SyncedParameter sync = ParameterSync.Pack(parameter, content, removed);
            if (sync == null)
                return;

            PacketEvent packetEvent = PacketEvent.ENTITY_PARAMETER_MODIFIED;
            if (sync.data_type == (uint)DataType.RESOURCE)
                packetEvent = PacketEvent.ENTITY_RESOURCE_MODIFIED;

            Packet p = GeneratePacket(packetEvent, entity);
            p.parameters.Add(sync);

            //Legacy fields for viewers that read top-level renderable/transform
            if (!removed && parameter.content != null)
            {
                switch (parameter.content.dataType)
                {
                    case DataType.TRANSFORM:
                        cTransform transform = (cTransform)parameter.content;
                        p.has_transform = true;
                        p.position = transform.position;
                        p.rotation = transform.rotation;
                        break;
                    case DataType.RESOURCE:
                        foreach (RenderableSyncElement element in sync.renderable)
                            p.renderable.Add(new Tuple<int, int>(element.model_index, element.material_index));
                        break;
                }
            }
            else if (removed && parameter.name == ShortGuidUtils.Generate("position"))
            {
                p.has_transform = false;
            }

            SendData(p);
        }

        /* Re-sync a new client with all current info */
        private static void SyncClient()
        {
            Debug.Log("Websocket", _server?.WebSocketServices["/commands_editor"].Sessions.Count + " clients connected!");

            if (_isDirty)
            {
                //TODO: Warn that there's likely going to be a mismatch between client and server.
            }

            string levelName = _pendingLevelLoadName ?? Singleton.Editor?.CompositeBrowser?.Content?.Level?.Name;
            if (!string.IsNullOrEmpty(levelName))
                SendLevelLoadedPacket(levelName);
            else
                SendData(GeneratePacket());

            //A viewer that has just connected has no zone table at all, whether or not one has changed
            ViewerZoneSync.SendNow();
        }

        /* Create a Packet object containing useful metadata */
        private static Packet GeneratePacket(PacketEvent packet_event, Entity entity)
        {
            Packet p = GeneratePacket(packet_event);
            p.entity_variant = entity.variant;
            p.entity = entity.shortGUID.AsUInt32;
            if (entity.variant == EntityVariant.FUNCTION)
                p.entity_function = ((FunctionEntity)entity).function.AsUInt32;
            return p;
        }
        private static Packet GeneratePacket(PacketEvent packet_event = PacketEvent.GENERIC_DATA_SYNC)
        {
            Packet p = new Packet(packet_event);
            p.level_name = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Name;
            p.system_folder = Singleton.PathToAI;
            if (Singleton.Editor?.CompositeDisplay != null)
            {
                List<CompositePath.CompAndEnt> richPath = Singleton.Editor.CompositeDisplay.Path.GetPathRich(Singleton.Editor.CompositeDisplay.Composite);
                foreach (CompositePath.CompAndEnt entry in richPath)
                {
                    if (entry.Entity != null)
                    {
                        p.path_entities.Add(entry.Entity.shortGUID.AsUInt32);
                        p.path_composites.Add(entry.Composite.shortGUID.AsUInt32);
                    }
                }
            }
            //The path down to the open composite, before the selected entity goes on the end of it
            List<uint> drillPath = new List<uint>(p.path_entities);

            /* Editing several entities at once is still one selection to the viewer: the first is the
               one it anchors on (inspector, camera, the gizmo's orientation) and the rest ride along
               in selection_entities so they are marked and moved with it. */
            List<Entity> multiSelection = Singleton.Editor?.CompositeDisplay?.EntityDisplay?.MultiSelectedEntities;
            Entity selectedEntity = Singleton.Editor?.CompositeDisplay?.EntityDisplay?.Entity
                ?? (multiSelection != null && multiSelection.Count > 0 ? multiSelection[0] : null);
            if (selectedEntity != null)
            {
                p.path_entities.Add(selectedEntity.shortGUID.AsUInt32);
                p.entity = selectedEntity.shortGUID.AsUInt32;
                p.entity_variant = selectedEntity.variant;
                if (selectedEntity.variant == EntityVariant.FUNCTION)
                    p.entity_function = ((FunctionEntity)selectedEntity).function.AsUInt32;

                if (multiSelection != null && multiSelection.Count > 1)
                    foreach (Entity entity in multiSelection)
                        p.selection_entities.Add(entity.shortGUID.AsUInt32);
            }
            if (Singleton.Editor?.CompositeDisplay?.Composite != null)
            {
                Composite composite = Singleton.Editor.CompositeDisplay.Composite;
                p.path_composites.Add(composite.shortGUID.AsUInt32);
                p.composite = composite.shortGUID.AsUInt32;

                /* Selecting a TriggerSequence marks everything it fires at, so the sequence can be
                   read off the level rather than off its entry list. Its members are named by paths
                   which can point out of this composite entirely, so they travel as instance paths
                   rather than as ids in selection_entities. */
                if (selectedEntity is TriggerSequence sequence)
                {
                    p.selection_entity_paths = EntityInstancePath.ResolveTriggerSequenceMembers(
                        Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands,
                        composite,
                        drillPath,
                        sequence);
                }
                else if (selectedEntity is FunctionEntity zone && zone.function == FunctionType.Zone)
                {
                    /* A Zone the same way: it marks everything it claims - what its sequences name, and
                       everything inside those - which is what the zone overlay draws in its colour. The
                       paths are written from where the hierarchy starts, the composite the viewer has
                       populated. */
                    Composite hierarchyRoot = Singleton.Editor.CompositeDisplay.Path?.AllComposites.FirstOrDefault() ?? composite;
                    p.selection_entity_paths = ViewerZoneSync.ResolveZoneMembers(
                        Singleton.Editor?.CompositeBrowser?.Content?.Level,
                        hierarchyRoot,
                        drillPath,
                        composite,
                        zone);
                }
            }
            p.dirty = _isDirty; //NOTE: Not using the DirtyTracker here as we only care about changes that will visually affect the Unity editor.
            p.focus_object = SettingsManager.GetBool(Settings.FocusOnSelected);
            p.fix_camera_to_selected = SettingsManager.GetBool(Settings.FixCameraToSelected);
            p.show_camera_position = SettingsManager.GetBool(Settings.ShowCameraPosition);
            p.model_reference_wireframe = SettingsManager.GetBool(Settings.RenderWireframe);
            p.hide_nested_script_entities = SettingsManager.GetBool(Settings.HideNestedScriptEntities);
            p.highlight_aliases = SettingsManager.GetBool(Settings.HighlightAliases);
            p.highlight_proxies = SettingsManager.GetBool(Settings.HighlightProxies);
            p.transform_grid_snap = TransformSnapDefinitions.NormalizeGridSnap(SettingsManager.GetFloat(Settings.TransformGridSnap));
            p.rotation_snap_degrees = TransformSnapDefinitions.NormalizeRotationSnap(SettingsManager.GetFloat(Settings.RotationSnapDegrees));
            p.transform_vertex_snap = SettingsManager.GetBool(Settings.TransformVertexSnap);
            p.deep_select_mode = (int)LevelViewerViewportDefinitions.NormalizeDeepSelectMode(
                SettingsManager.GetInteger(Settings.LevelViewerDeepSelectMode));
            p.gizmo_mode = (int)LevelViewerViewportDefinitions.NormalizeGizmoMode(
                SettingsManager.GetInteger(Settings.LevelViewerGizmoMode));
            p.create_function_type = ViewerCreateMode.ActiveFunctionType;
            p.box_render_filters = RenderFilters.GetPacketFilters();
            p.scene_render_filters = RenderFilters.GetScenePacketFilters();
            p.show_navmesh_state = ViewerStateInfoMode.NavMeshState;
            p.show_cover_state = ViewerStateInfoMode.CoverState;
            p.show_zones = SettingsManager.GetBool(Settings.ShowZones);
            p.selection_highlight_mode = (int)LevelViewerViewportDefinitions.NormalizeHighlightMode(
                SettingsManager.GetInteger(Settings.LevelViewerHighlightMode));
            p.render_galaxy = SettingsManager.GetBool(Settings.RenderGalaxy);
            p.entity_clipboard_has_content = EntityClipboard.HasContent;
            return p;
        }

        /* Send data to all connected Unity sessions */
        internal static void SendData(Packet content)
        {
            if (ViewerSelectionSync.SuppressSyncBroadcastDepth > 0
                && (content.packet_event == PacketEvent.ENTITY_SELECTED
                    || content.packet_event == PacketEvent.ENTITY_ADDED
                    || content.packet_event == PacketEvent.ENTITY_DELETED
                    || content.packet_event == PacketEvent.COMPOSITE_RELOADED
                    || content.packet_event == PacketEvent.COMPOSITE_SELECTED
                    || content.packet_event == PacketEvent.GENERIC_DATA_SYNC))
            {
                return;
            }

            string json = JsonConvert.SerializeObject(content);
            WebSocketPacketLog.LogSent(content, json.Length);
            _server?.WebSocketServices["/commands_editor"].Sessions.Broadcast(json);
        }
    }
}
