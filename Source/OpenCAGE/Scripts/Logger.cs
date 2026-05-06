using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAGE
{
    public static class Debug
    {
        private static Dictionary<string, long> _lastTick = new Dictionary<string, long>();

        public static void Log(string system, string message)
        {
#if DEBUG
            if (!_lastTick.ContainsKey(system))
                _lastTick.Add(system, DateTime.Now.Ticks);

            Console.WriteLine($"[{system.ToUpper()}] {message} [{((DateTime.Now.Ticks - _lastTick[system]) / 10000)}MS SINCE PREV]");

            _lastTick[system] = DateTime.Now.Ticks;
#endif
        }
    }
}
