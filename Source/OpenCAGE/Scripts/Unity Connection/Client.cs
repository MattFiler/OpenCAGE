using Newtonsoft.Json;
using OpenCAGE;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace OpenCAGE.UnityConnection
{
    public class Client : WebSocketBehavior
    {
        public Action OnConnect;
        public Action OnDisconnect;

        protected override void OnMessage(MessageEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            // Level Viewer (and OpenCAGE outbound) use JSON packets — do not fall through to the legacy prefix parser.
            if (e.Data[0] == '{')
            {
                try
                {
                    Packet packet = JsonConvert.DeserializeObject<Packet>(e.Data);
                    if (packet == null || packet.version != new Packet().version)
                        return;

                    if (packet.packet_event == PacketEvent.ENTITY_SELECTED)
                        ViewerSelectionSync.TryApply(packet);
                    else if (packet.packet_event == PacketEvent.ENTITY_ADDED)
                        ViewerEntitySync.TryApply(packet);
                    else if (packet.packet_event == PacketEvent.ENTITY_PARAMETER_MODIFIED)
                        ViewerParameterSync.TryApply(packet);
                }
                catch (Exception ex)
                {
                    Debug.Log("Websocket", "Failed to parse JSON websocket message: " + ex.Message);
                }

                return;
            }

            if (e.Data.Length < 1 || !char.IsDigit(e.Data[0]))
            {
                Debug.Log("Websocket", "Unrecognized message format: " + e.Data);
                return;
            }

            PacketEvent type = (PacketEvent)Convert.ToInt32(e.Data.Substring(0, 1));
            switch (type)
            {
                default:
                    Debug.Log("Websocket", "Legacy message: " + e.Data.Substring(1));
                    break;
            }
        }

        protected override void OnOpen()
        {
            Debug.Log("Websocket", "Client connected");
            SendMessage(new Packet(PacketEvent.GENERIC_DATA_SYNC));
            OnConnect?.Invoke();
            base.OnOpen();
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Debug.Log("Websocket", "Client disconnected");
            OnDisconnect?.Invoke();
            base.OnClose(e);
        }

        public void SendMessage(Packet content)
        {
#if DEBUG
            Debug.Log("Websocket", "Sending " + content.packet_event + " data");
#endif
            base.Send(JsonConvert.SerializeObject(content));
        }
    }
}
