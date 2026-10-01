using OpenCAGE;
using Newtonsoft.Json;
using System;

namespace OpenCAGE.RuntimeUtilsConnection
{
    public static class Send
    {
        private static Client _client;
        //127.0.0.1, not localhost: the game only listens on IPv4, and trying ::1 first costs seconds on every connect
        private const string ServerUrl = "ws://127.0.0.1:8765";

        public static bool Connected => _client != null && _client.Connected;
        public static bool Started => _client != null;

        //Start runs off the UI thread too (the live link's reconnect): one connect at a time
        private static readonly object _lock = new object();

        /// <summary>Connect, if not already connected. Blocks while connecting (seconds when nothing listens): call it off the UI thread.</summary>
        public static bool Start()
        {
            lock (_lock)
            {
                if (Connected)
                    return true;
                if (!Wanted)
                    return false;
                return Connect();
            }
        }

        private static bool Connect()
        {
            StopLocked();

            try
            {
                _client = new Client(ServerUrl);
                _client.OnConnected += () =>
                {
                    Debug.Log("RuntimeUtils", "Successfully connected to RuntimeUtils");
                    LiveLink.NotifyConnectionChanged();
                };
                _client.OnDisconnected += () =>
                {
                    Debug.Log("RuntimeUtils", "Disconnected from RuntimeUtils");
                    LiveLink.FailPending("The game closed the Live Link connection");
                    LiveLink.NotifyConnectionChanged();
                };
                _client.OnMessage += (message) =>
                {
                    Debug.Log("RuntimeUtils", "Received: " + message);
                };
                _client.OnBinaryMessage += LiveLink.HandleReply;

                bool connected = _client.Connect();
                LiveLink.NotifyConnectionChanged();
                return connected;
            }
            catch
            {
                _client = null;
                return false;
            }
        }

        public static void Stop()
        {
            lock (_lock)
                StopLocked();
        }

        //Whether a connect may go ahead: asked again under the lock, so one that was queued behind a Stop (the option
        //switched off meanwhile) does not connect after it
        private static bool Wanted => Singleton.Platform == CathodeLib.PatchManager.Platform.STEAM && SettingsManager.GetBool(Settings.RuntimeUtilsOpt);

        private static void StopLocked()
        {
            if (_client != null)
            {
                _client.Disconnect();
                _client = null;
                LiveLink.FailPending("Live Link was disconnected");
                LiveLink.NotifyConnectionChanged();
            }
        }

        public static void SendData(Packet content)
        {
            _client?.Send(JsonConvert.SerializeObject(content));
        }

        public static bool SendBinary(byte[] content)
        {
            return _client != null && _client.Send(content);
        }
    }
}

