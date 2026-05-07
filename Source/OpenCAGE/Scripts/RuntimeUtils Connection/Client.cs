using OpenCAGE;
using Newtonsoft.Json;
using System;
using WebSocketSharp;

namespace OpenCAGE.RuntimeUtilsConnection
{
    public class Client
    {
        private WebSocket _ws;
        private string _url;

        public bool Connected => _ws != null && _ws.ReadyState == WebSocketState.Open;
        public Action OnConnected;
        public Action OnDisconnected;
        public Action<string> OnMessage;

        public Client(string url)
        {
            _url = url;
        }

        public bool Connect()
        {
            if (Connected)
                return true;

            try
            {
                _ws = new WebSocket(_url);
                _ws.OnOpen += (sender, e) =>
                {
                    Debug.Log("RuntimeUtils", "Connected to RuntimeUtils server");
                    OnConnected?.Invoke();
                };
                _ws.OnClose += (sender, e) =>
                {
                    Debug.Log("RuntimeUtils", "Disconnected from RuntimeUtils server");
                    OnDisconnected?.Invoke();
                };
                _ws.OnMessage += (sender, e) =>
                {
                    Debug.Log("RuntimeUtils", "Message received: " + e.Data);
                    OnMessage?.Invoke(e.Data);
                };
                _ws.OnError += (sender, e) =>
                {
                    Debug.Log("RuntimeUtils", "Error: " + e.Message);
                };
                _ws.Connect();
                return Connected;
            }
            catch (Exception ex)
            {
                Debug.Log("RuntimeUtils", "Failed to connect: " + ex.Message);
                return false;
            }
        }

        public void Disconnect()
        {
            if (_ws != null)
            {
                _ws.Close();
                _ws = null;
            }
        }

        public void Send(string message)
        {
            if (!Connected)
            {
                Debug.Log("RuntimeUtils", "Cannot send message - not connected");
                return;
            }

            try
            {
                _ws.Send(message);
            }
            catch (Exception ex)
            {
                Debug.Log("RuntimeUtils", "Failed to send message: " + ex.Message);
            }
        }
    }
}

