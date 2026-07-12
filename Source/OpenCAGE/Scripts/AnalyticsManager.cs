using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAGE
{
    static class AnalyticsManager
    {
        /* Make sure we have our own unique ID before we handle any analytics */
        static AnalyticsManager()
        {
            if (SettingsManager.GetString(Settings.UniqueId) == "") SettingsManager.SetString(Settings.UniqueId, BitConverter.ToString(new SHA1Managed().ComputeHash(Encoding.UTF8.GetBytes(DateTime.Now.ToString()))).Replace("-", string.Empty));
        }

        /* Log an open of the OpenCAGE app */
        static public void LogAppStartup()
        {
#if DEBUG
            return;
#else
            Task.Factory.StartNew(() => LogAppStartupLogic());
#endif
        }
        static private void LogAppStartupLogic()
        {
            try
            {
                Stream webStream = new WebClient().OpenRead("http://opencage.mattfiler.co.uk/analytics.php?uid=" + SettingsManager.GetString(Settings.UniqueId) + "&version=" + Singleton.Version + "&staging=" + Singleton.BetaName + "&r=" + new Random().Next(5000).ToString());
                string result = new StreamReader(webStream).ReadToEnd();
                switch (result)
                {
                    case "SUCCESS_UPDATED_ENTRY":
                    case "SUCCESS_NEW_ENTRY":
                        return;
                    case "LOGIC_ERROR":
                        return;
                    default:
                        Console.WriteLine("AnalyticsManager::LogAppStartup: " + result);
                        return;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("AnalyticsManager::LogAppStartup: Logging launch FAILED!\n" + e.ToString());
            }
            return;
        }
    }
}
