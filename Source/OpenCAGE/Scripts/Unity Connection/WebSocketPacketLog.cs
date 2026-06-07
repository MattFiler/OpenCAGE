using System;
using System.Text;

namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Logs websocket packet traffic. Uses Console so output appears in release builds too.
    /// </summary>
    public static class WebSocketPacketLog
    {
        public const bool Enabled = true;

        public static void LogSent(Packet packet, int jsonLength = -1)
            => Log(">>", packet, jsonLength);

        public static void LogReceived(Packet packet, int jsonLength = -1)
            => Log("<<", packet, jsonLength);

        public static void LogReceiveFailed(int jsonLength, string reason)
        {
            if (!Enabled)
                return;

            Console.WriteLine("[WS] << FAILED | " + jsonLength + "b | " + reason);
        }

        private static void Log(string direction, Packet packet, int jsonLength)
        {
            if (!Enabled)
                return;

            string size = jsonLength >= 0 ? jsonLength + "b | " : string.Empty;
            Console.WriteLine("[WS] " + direction + " " + size + FormatSummary(packet));
        }

        public static string FormatSummary(Packet packet)
        {
            if (packet == null)
                return "null packet";

            StringBuilder sb = new StringBuilder();
            sb.Append(packet.packet_event);

            if (!string.IsNullOrEmpty(packet.level_name))
                sb.Append(" | level=").Append(packet.level_name);

            if (packet.composite != 0)
                sb.Append(" | composite=").Append(packet.composite);

            if (packet.entity != 0)
                sb.Append(" | entity=").Append(packet.entity);

            if (packet.path_entities != null && packet.path_entities.Count > 0)
            {
                sb.Append(" | pathEntities=").Append(packet.path_entities.Count);
                if (packet.path_entities.Count <= 4)
                    sb.Append('[').Append(string.Join(",", packet.path_entities)).Append(']');
            }

            if (packet.path_composites != null && packet.path_composites.Count > 0)
            {
                sb.Append(" | pathComposites=").Append(packet.path_composites.Count);
                if (packet.path_composites.Count <= 4)
                    sb.Append('[').Append(string.Join(",", packet.path_composites)).Append(']');
            }

            if (packet.has_transform)
            {
                sb.Append(" | transform=(")
                    .Append(packet.position.X.ToString("0.###")).Append(',')
                    .Append(packet.position.Y.ToString("0.###")).Append(',')
                    .Append(packet.position.Z.ToString("0.###")).Append(')');
            }

            if (packet.parameters != null && packet.parameters.Count > 0)
            {
                sb.Append(" | params=").Append(packet.parameters.Count);
                if (packet.parameters.Count <= 3)
                {
                    sb.Append('[');
                    for (int i = 0; i < packet.parameters.Count; i++)
                    {
                        if (i > 0)
                            sb.Append(',');
                        SyncedParameter param = packet.parameters[i];
                        sb.Append(param?.data_type.ToString() ?? "?");
                        if (param != null && param.removed)
                            sb.Append(":removed");
                    }
                    sb.Append(']');
                }
            }

            if (packet.renderable != null && packet.renderable.Count > 0)
                sb.Append(" | renderables=").Append(packet.renderable.Count);

            if (packet.dirty)
                sb.Append(" | dirty");

            return sb.ToString();
        }
    }
}
