using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using System;
using System.Collections.Generic;

namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Applies ENTITY_ADDED packets sent from the Godot Level Viewer (deep-select alias creation).
    /// </summary>
    public static class ViewerEntitySync
    {
        public static bool TryApply(Packet packet)
        {
            if (packet?.packet_event != PacketEvent.ENTITY_ADDED)
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
                    Debug.Log("Websocket", "Failed to queue viewer entity add on UI thread: " + ex.Message);
                    return false;
                }
            }

            return ApplyCore(packet);
        }

        private static bool ApplyCore(Packet packet)
        {
            CommandsDisplay commands = Singleton.Editor?.CommandsDisplay;
            if (commands?.Content?.Level == null)
                return false;

            Composite composite = commands.Content.Level.Commands.GetComposite(new ShortGuid(packet.composite));
            if (composite == null)
                return false;

            ShortGuid entityId = new ShortGuid(packet.entity);
            if (composite.GetEntityByID(entityId) != null)
                return true;

            Entity entity = null;
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

            bool hasSelectionPath = packet.path_entities != null
                && packet.path_composites != null
                && packet.path_entities.Count > 0
                && packet.path_entities.Count == packet.path_composites.Count;
            if (hasSelectionPath)
                ViewerSelectionSync.TryApply(packet);

            ViewerSelectionSync.SuppressSyncBroadcastDepth++;
            try
            {
                Singleton.OnEntityAdded?.Invoke(entity);
            }
            finally
            {
                ViewerSelectionSync.SuppressSyncBroadcastDepth--;
            }

            return true;
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
