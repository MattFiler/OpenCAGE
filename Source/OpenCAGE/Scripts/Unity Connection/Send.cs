using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using Newtonsoft.Json;
using OpenCAGE;
using System;
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
        private static WebSocketServer _server;
        private static Client _serverLogic;

        public static bool Started => _server != null;
        public static bool Connected => _server != null && _server.WebSocketServices["/commands_editor"].Sessions.Count != 0;

        private static bool _isDirty = false;

        static Send()
        {
            Singleton.OnLevelLoaded += LevelLoaded;
            Singleton.OnSaved += LevelSaved;
            Singleton.OnCompositeAdded += CompositeAdded;
            Singleton.OnCompositeReloaded += CompositeReloaded;
            Singleton.OnCompositeSelected += CompositeSelected;
            Singleton.OnCompositeDeleted += CompositeDeleted;
            Singleton.OnEntityReloaded += EntitySelected;
            Singleton.OnEntityMoved += EntityMoved;
            Singleton.OnEntityAdded += EntityAdded;
            Singleton.OnEntityDeleted += EntityDeleted;
            Singleton.OnResourceModified += ResourceModified;
            Singleton.OnEntityParameterModified += EntityParameterModified;
        }

        public static bool Start()
        {
            Stop();

            try
            {
                _server = new WebSocketServer("ws://localhost:1702");
                _server.AddWebSocketService<Client>("/commands_editor", (server) =>
                {
                    _serverLogic = server;
                    _serverLogic.OnConnect += SyncClient;
                });
                _server.Start();
                return true;
            }
            catch
            {
                _server = null;
                return false;
            }
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
            SendData(packet);
        }

        /* Send viewer settings (focus, hide nested previews, etc.) */
        public static void SendSettingsPacket()
        {
            Packet packet = new Packet(PacketEvent.SETTINGS_CHANGED);
            packet.focus_object = SettingsManager.GetBool(Singleton.Settings.UNITY_FocusEntity);
            packet.show_camera_position = SettingsManager.GetBool(Singleton.Settings.UNITY_ShowCameraPosition);
            packet.model_reference_wireframe = SettingsManager.GetBool(Singleton.Settings.UNITY_RenderWireframe);
            packet.hide_nested_script_entities = SettingsManager.GetBool(Singleton.Settings.UNITY_HideNestedScriptEntities);
            packet.highlight_aliases = SettingsManager.GetBool(Singleton.Settings.UNITY_HighlightAliases);
            packet.transform_grid_snap = TransformSnapDefinitions.NormalizeGridSnap(
                SettingsManager.GetFloat(Singleton.Settings.UNITY_TransformGridSnap));
            packet.rotation_snap_degrees = TransformSnapDefinitions.NormalizeRotationSnap(
                SettingsManager.GetFloat(Singleton.Settings.UNITY_RotationSnapDegrees));
            packet.box_render_filters = RenderFilters.GetPacketFilters();
            SendData(packet);
        }

        /* A level has just been loaded -> load its data in Unity */
        private static void LevelLoaded(LevelContent content)
        {
            _isDirty = false;
            SendData(GeneratePacket(PacketEvent.LEVEL_LOADED));
        }

        /* The level has been saved -> clear our dirty flag */
        private static void LevelSaved()
        {
            _isDirty = false;
            SendData(GeneratePacket(PacketEvent.LEVEL_LOADED)); //NEW: Fire another loaded event to reload write indexes on the Unity side.
        }

        /* A composite has been loaded -> open it in the Unity scene */
        private static void CompositeSelected(Composite composite)
        {
            Packet p = GeneratePacket(PacketEvent.COMPOSITE_SELECTED);
            p.composite = composite.shortGUID.AsUInt32;
            SendData(p);
        }
        private static void CompositeReloaded(Composite composite)
        {
            Packet p = GeneratePacket(PacketEvent.COMPOSITE_RELOADED);
            p.composite = composite.shortGUID.AsUInt32;
            SendData(p);
        }

        /* Composite lifetime events -> sync them to Unity */
        private static void CompositeAdded(Composite composite)
        {
            Packet p = GeneratePacket(PacketEvent.COMPOSITE_ADDED);
            p.composite = composite.shortGUID.AsUInt32;
            SendData(p);
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
        private static void EntityMoved(cTransform transform, Entity entity)
        {
            _isDirty = true;

            Parameter position = entity?.GetParameter("position");
            if (position == null && transform != null)
                position = new Parameter("position", transform);

            if (position != null)
            {
                if (transform != null)
                    position.content = transform;
                else
                    position.content = null;

                SendParameterPacket(entity, position, transform == null);
            }
        }
        private static void EntityDeleted(Entity entity)
        {
            _isDirty = true;
            SendData(GeneratePacket(PacketEvent.ENTITY_DELETED, entity));
        }
        private static void EntityAdded(Entity entity)
        {
            _isDirty = true;
            Packet p = GeneratePacket(PacketEvent.ENTITY_ADDED, entity);
            switch (entity.variant)
            {
                case EntityVariant.PROXY:
                    p.entity_pointed = ((ProxyEntity)entity).proxy.pathUint;
                    break;
                case EntityVariant.ALIAS:
                    p.entity_pointed = ((AliasEntity)entity).alias.pathUint;
                    break;
            }
            SendData(p);
        }
        private static void ResourceModified()
        {
            _isDirty = true;
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

            SendData(GeneratePacket());
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
            if (Singleton.Editor?.CompositeDisplay?.EntityDisplay?.Entity != null)
            {
                Entity entity = Singleton.Editor.CompositeDisplay.EntityDisplay.Entity;
                p.path_entities.Add(entity.shortGUID.AsUInt32);
                p.entity = entity.shortGUID.AsUInt32;
                p.entity_variant = entity.variant;
                if (entity.variant == EntityVariant.FUNCTION)
                    p.entity_function = ((FunctionEntity)entity).function.AsUInt32;
            }
            if (Singleton.Editor?.CompositeDisplay?.Composite != null)
            {
                Composite composite = Singleton.Editor.CompositeDisplay.Composite;
                p.path_composites.Add(composite.shortGUID.AsUInt32);
                p.composite = composite.shortGUID.AsUInt32;
            }
            p.dirty = _isDirty; //NOTE: Not using the DirtyTracker here as we only care about changes that will visually affect the Unity editor.
            p.focus_object = SettingsManager.GetBool(Singleton.Settings.UNITY_FocusEntity);
            p.show_camera_position = SettingsManager.GetBool(Singleton.Settings.UNITY_ShowCameraPosition);
            p.model_reference_wireframe = SettingsManager.GetBool(Singleton.Settings.UNITY_RenderWireframe);
            p.hide_nested_script_entities = SettingsManager.GetBool(Singleton.Settings.UNITY_HideNestedScriptEntities);
            p.highlight_aliases = SettingsManager.GetBool(Singleton.Settings.UNITY_HighlightAliases);
            p.transform_grid_snap = TransformSnapDefinitions.NormalizeGridSnap(
                SettingsManager.GetFloat(Singleton.Settings.UNITY_TransformGridSnap));
            p.rotation_snap_degrees = TransformSnapDefinitions.NormalizeRotationSnap(
                SettingsManager.GetFloat(Singleton.Settings.UNITY_RotationSnapDegrees));
            p.box_render_filters = RenderFilters.GetPacketFilters();
            return p;
        }

        /* Send data to all connected Unity sessions */
        private static void SendData(Packet content)
        {
            if (ViewerSelectionSync.SuppressSyncBroadcastDepth > 0
                && (content.packet_event == PacketEvent.ENTITY_SELECTED
                    || content.packet_event == PacketEvent.ENTITY_ADDED
                    || content.packet_event == PacketEvent.ENTITY_DELETED
                    || content.packet_event == PacketEvent.COMPOSITE_RELOADED
                    || content.packet_event == PacketEvent.COMPOSITE_SELECTED))
            {
                return;
            }

            string json = JsonConvert.SerializeObject(content);
            WebSocketPacketLog.LogSent(content, json.Length);
            _server?.WebSocketServices["/commands_editor"].Sessions.Broadcast(json);
        }
    }
}
