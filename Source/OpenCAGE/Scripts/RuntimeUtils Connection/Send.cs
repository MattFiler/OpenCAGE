using OpenCAGE;
using Newtonsoft.Json;
using System;

namespace OpenCAGE.RuntimeUtilsConnection
{
    public static class Send
    {
        private static Client _client;
        private const string ServerUrl = "ws://localhost:8765";

        public static bool Connected => _client != null && _client.Connected;
        public static bool Started => _client != null;

        public static bool Start()
        {
            Stop();

            try
            {
                _client = new Client(ServerUrl);
                _client.OnConnected += () =>
                {
                    Debug.Log("RuntimeUtils", "Successfully connected to RuntimeUtils");
                };
                _client.OnDisconnected += () =>
                {
                    Debug.Log("RuntimeUtils", "Disconnected from RuntimeUtils");
                };
                _client.OnMessage += (message) =>
                {
                    Debug.Log("RuntimeUtils", "Received: " + message);
                };

                return _client.Connect();
            }
            catch
            {
                _client = null;
                return false;
            }
        }

        public static void Stop()
        {
            if (_client != null)
            {
                _client.Disconnect();
                _client = null;
            }
        }

        public static void SendData(Packet content)
        {
            _client?.Send(JsonConvert.SerializeObject(content));
        }
    }
}

