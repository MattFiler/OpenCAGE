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
    /// What a port or import left unresolved in its destination, and the words for telling the user about it: dead proxies
    /// (see CommandsUtils.IsDeadProxy) - a mission script ported to another level keeps every proxy it had, but the doors and
    /// triggers they pointed at are not there - and instances of composites the destination doesn't have (see
    /// CommandsUtils.IsMissingCompositeInstance), when a script instanced a composite that didn't come with it. This is what
    /// the completion message says about them.
    /// </summary>
    public class DeadProxyReport
    {
        public class Entry
        {
            public string Composite;
            public List<string> Proxies = new List<string>();
        }

        public List<Entry> Entries = new List<Entry>();

        /// <summary>Per composite, its instances of composites the destination doesn't have ("name (ID)").</summary>
        public List<Entry> MissingComposites = new List<Entry>();

        public int ProxyCount => Entries.Sum(o => o.Proxies.Count);
        public int MissingInstanceCount => MissingComposites.Sum(o => o.Proxies.Count);
        public bool Any => Entries.Count != 0 || MissingComposites.Count != 0;

        /// <summary>The dead proxies and missing-composite instances in <paramref name="composites"/>, resolved against <paramref name="commands"/>.</summary>
        public static DeadProxyReport Of(Commands commands, IEnumerable<Composite> composites)
        {
            DeadProxyReport report = new DeadProxyReport();
            if (commands?.Utils == null || composites == null)
                return report;
            List<Composite> list = composites.ToList();
            foreach (KeyValuePair<Composite, List<ProxyEntity>> dead in commands.Utils.GetDeadProxies(list))
            {
                Entry entry = new Entry() { Composite = EditorUtils.GetCompositeName(dead.Key) };
                foreach (ProxyEntity proxy in dead.Value)
                    entry.Proxies.Add(commands.Utils.GetEntityName(dead.Key, proxy));
                report.Entries.Add(entry);
            }
            foreach (KeyValuePair<Composite, List<FunctionEntity>> missing in commands.Utils.GetMissingCompositeInstances(list))
            {
                //One the script itself switches off (retail's cut content) is never made by the game, so there is nothing to say
                List<FunctionEntity> live = missing.Value.Where(o => !(o.GetParameter(CathodeLib.ShortGuids.deleted)?.content is cBool deleted && deleted.value)).ToList();
                if (live.Count == 0)
                    continue;
                Entry entry = new Entry() { Composite = EditorUtils.GetCompositeName(missing.Key) };
                foreach (FunctionEntity instance in live)
                    entry.Proxies.Add(commands.Utils.GetEntityName(missing.Key, instance) + " (" + instance.function.ToByteString() + ")");
                report.MissingComposites.Add(entry);
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

            StringBuilder text = new StringBuilder();
            if (Entries.Count != 0)
            {
                int proxies = ProxyCount;
                text.Append(proxies + (proxies == 1 ? " unresolvable proxy" : " unresolvable proxies") + " in " + Entries.Count + (Entries.Count == 1 ? " composite" : " composites")
                    + ": what they point at is not in " + where + ". They have been kept, with the links through them, and are shown in red in the flowgraph. "
                    + "Each needs re-pointing at an entity that exists in " + where + " (Change Target in the entity inspector, or right-click the node) before that part of the script will work.");
                AppendList(text, Entries);
            }
            if (MissingComposites.Count != 0)
            {
                int instances = MissingInstanceCount;
                if (text.Length != 0)
                    text.Append("\n\n");
                text.Append(instances + (instances == 1 ? " instance of a composite" : " instances of composites") + " that " + where + " doesn't have, in " + MissingComposites.Count + (MissingComposites.Count == 1 ? " composite" : " composites")
                    + ": the composite each one instances (by the ID shown) came with neither this import nor " + where + ". They have been kept, with their links, and are shown in red in the flowgraph; "
                    + "the game skips them, so that part of the script does nothing. Import the missing composites (from the package or level they were made in) and these become instances of them again.");
                AppendList(text, MissingComposites);
            }
            return text.ToString();
        }

        private static void AppendList(StringBuilder text, List<Entry> entries)
        {
            const int perComposite = 4, compositesShown = 6;
            foreach (Entry entry in entries.Take(compositesShown))
            {
                text.Append("\n  " + entry.Composite + ": " + string.Join(", ", entry.Proxies.Take(perComposite)));
                if (entry.Proxies.Count > perComposite)
                    text.Append(" (+" + (entry.Proxies.Count - perComposite) + " more)");
            }
            if (entries.Count > compositesShown)
                text.Append("\n  ... and " + (entries.Count - compositesShown) + " more composites");
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
