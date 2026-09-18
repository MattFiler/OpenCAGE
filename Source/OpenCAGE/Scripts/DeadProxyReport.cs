using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenCAGE
{
    /// <summary>
    /// The dead proxies a port or import left behind in its destination (see CommandsUtils.IsDeadProxy),
    /// and the words for telling the user about them. A mission script ported to another level keeps
    /// every proxy it had, but the doors and triggers they pointed at are not there; this is what the
    /// completion message says about that.
    /// </summary>
    public class DeadProxyReport
    {
        public class Entry
        {
            public string Composite;
            public List<string> Proxies = new List<string>();
        }

        public List<Entry> Entries = new List<Entry>();

        public int ProxyCount => Entries.Sum(o => o.Proxies.Count);
        public bool Any => Entries.Count != 0;

        /// <summary>The dead proxies in <paramref name="composites"/>, resolved against <paramref name="commands"/>.</summary>
        public static DeadProxyReport Of(Commands commands, IEnumerable<Composite> composites)
        {
            DeadProxyReport report = new DeadProxyReport();
            if (commands?.Utils == null || composites == null)
                return report;
            foreach (KeyValuePair<Composite, List<ProxyEntity>> dead in commands.Utils.GetDeadProxies(composites))
            {
                Entry entry = new Entry() { Composite = Path.GetFileName(dead.Key.name) };
                foreach (ProxyEntity proxy in dead.Value)
                    entry.Proxies.Add(commands.Utils.GetEntityName(dead.Key, proxy));
                report.Entries.Add(entry);
            }
            return report;
        }

        /// <summary>
        /// A paragraph for a completion message, or an empty string when there is nothing to say.
        /// <paramref name="where"/> names the destination ("this level", "SCI_HospitalLower").
        /// </summary>
        public string Describe(string where)
        {
            if (!Any)
                return "";

            int proxies = ProxyCount;
            StringBuilder text = new StringBuilder();
            text.Append(proxies + (proxies == 1 ? " unresolvable proxy" : " unresolvable proxies") + " in " + Entries.Count + (Entries.Count == 1 ? " composite" : " composites")
                + ": what they point at is not in " + where + ". They have been kept, with the links through them, and are shown in red in the flowgraph. "
                + "Each needs re-pointing at an entity that exists in " + where + " (Change Target in the entity inspector, or right-click the node) before that part of the script will work.");

            const int perComposite = 4, compositesShown = 6;
            foreach (Entry entry in Entries.Take(compositesShown))
            {
                text.Append("\n  " + entry.Composite + ": " + string.Join(", ", entry.Proxies.Take(perComposite)));
                if (entry.Proxies.Count > perComposite)
                    text.Append(" (+" + (entry.Proxies.Count - perComposite) + " more)");
            }
            if (Entries.Count > compositesShown)
                text.Append("\n  ... and " + (Entries.Count - compositesShown) + " more composites");
            return text.ToString();
        }

        /// <summary>The reports of several destinations as one paragraph, each named; empty when none has anything to say.</summary>
        public static string Describe(IEnumerable<KeyValuePair<string, DeadProxyReport>> perLevel)
        {
            if (perLevel == null)
                return "";
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, DeadProxyReport> level in perLevel)
            {
                if (level.Value != null && level.Value.Any)
                    parts.Add(level.Value.Describe(level.Key));
            }
            return string.Join("\n\n", parts);
        }
    }
}
