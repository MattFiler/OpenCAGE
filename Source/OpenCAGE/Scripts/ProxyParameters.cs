using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// A proxy's parameters are of two kinds (issue 742). Its own: the ProxyInterface it is (proxy_enable_on_reset,
    /// proxy_filter_targets, its enable/disable pins and the events they fire), ScriptInterface's reference and delete_me, and
    /// its name. And the rest, which belong to the entity it stands for: an enabled proxy applies every one of those it
    /// carries to that entity, so each is an override, as on an alias - a door proxy carrying zonelink 0 cut the door off
    /// from its ZoneLink the moment it was enabled. Retail proxies carry their own (all 2,783 of them) and at most seven
    /// overrides; 2,489 carry none.
    /// </summary>
    public static class ProxyParameters
    {
        //ProxyInterface and ScriptInterface as CathodeLib's definition gives them, the events every retail proxy stores, and the name
        private static readonly HashSet<ShortGuid> _own = new HashSet<ShortGuid>(new[]
        {
            "proxy_filter_targets", "proxy_enable_on_reset", "proxy_enable", "proxy_disable", "proxy_enabled", "proxy_disabled",
            "reference", "delete_me", "name",
        }.Select(o => ShortGuidUtils.Generate(o)));

        /// <summary>Whether a parameter is the proxy's own rather than one it overrides on the entity it stands for.</summary>
        public static bool IsOwn(ShortGuid parameter) => _own.Contains(parameter);

        /// <summary>Whether this parameter on this entity is a proxy's override of the entity it stands for.</summary>
        public static bool IsOverride(Entity entity, ShortGuid parameter) => entity is ProxyEntity && !IsOwn(parameter);

        /// <summary>
        /// Take off the overrides the inspector filled in on proxies before issue 742 - every default of the entity a proxy
        /// stands for, the first time the proxy was inspected - which an enabled proxy applies to that entity. Only what it
        /// filled in itself goes: the proxy carries the defaults-applied mark and the parameter was never edited (a level's own
        /// values, and everything set by hand, count as edited). Returns how many came off, and from how many proxies.
        /// </summary>
        public static int StripFilledInOverrides(Commands commands, out int proxies)
        {
            proxies = 0;
            int removed = 0;
            if (commands?.Entries == null)
                return 0;
            foreach (Composite composite in commands.Entries)
            {
                if (composite == null)
                    continue;
                foreach (ProxyEntity proxy in composite.proxies)
                {
                    if (!ParameterModificationTracker.IsDefaultsApplied(composite.shortGUID, proxy.shortGUID))
                        continue;
                    int before = proxy.parameters.Count;
                    proxy.parameters.RemoveAll(o => o != null && !IsOwn(o.name)
                        && !ParameterModificationTracker.IsParameterModified(composite.shortGUID, proxy.shortGUID, o.name));
                    if (proxy.parameters.Count != before)
                    {
                        proxies++;
                        removed += before - proxy.parameters.Count;
                    }
                }
            }
            return removed;
        }
    }
}
