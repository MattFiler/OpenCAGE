#if ENABLE_MOD_PACKAGES
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.Modding.Merging
{
    /// <summary>One mod's finished version of a file: what the file holds with that mod alone installed.</summary>
    public class Contribution
    {
        public string ModId;
        public string ModName;
        public byte[] Bytes;
    }

    public enum ConflictKind
    {
        /// <summary>Both mods changed the same value: the one lower in the list is kept.</summary>
        Overridden,
        /// <summary>One mod removed something the other changed: the one lower in the list decides.</summary>
        RemovedVsChanged,
        /// <summary>The file could not be merged at all, so one mod's whole version was used.</summary>
        WholeFile,
    }

    /// <summary>
    /// Two mods that want different things for the same part of a file. The later mod in the list is
    /// always the one kept - this records what the user should know about the other.
    /// </summary>
    public class MergeConflict
    {
        /// <summary>The name the user's own changes go by when they're combined with mods (they come first, so mods win clashes with them).</summary>
        public const string OwnChanges = "Your own changes";

        public string Target;
        public string Where;
        public ConflictKind Kind;
        public string Kept;
        public string Lost;
        public string Detail;

        private static string Name(string mod) { return mod == OwnChanges ? "your own changes" : "'" + mod + "'"; }

        /// <summary>A level or game file as a player would call it: "the level BSP_TORRENS", "the alien's settings (DEFAULT.BML)".</summary>
        public static string Place(string target)
        {
            if (string.IsNullOrEmpty(target)) return "a game file";
            string t = target.ToUpperInvariant();
            if (!t.StartsWith("DATA/")) return "the level " + target.Replace("DLC/", "");
            string file = target.Substring(target.LastIndexOf('/') + 1);
            if (t.StartsWith("DATA/ALIENCONFIGS/")) return "the alien's settings (" + file + ")";
            if (t.StartsWith("DATA/BINARY_BEHAVIOR/")) return "the AI's behaviour trees";
            if (t.StartsWith("DATA/CHR_INFO/")) return "the character settings (" + file + ")";
            if (t.StartsWith("DATA/DIFFICULTYSETTINGS/")) return "the difficulty settings (" + file + ")";
            if (t.StartsWith("DATA/WEAPON_INFO/")) return "the weapon and ammo settings (" + file + ")";
            if (t.StartsWith("DATA/VIEW_CONE_SETS/")) return "what characters can see (" + file + ")";
            if (t == "DATA/GBL_ITEM.BML") return "the item settings";
            if (t == "DATA/GLOBALCONSTANTS.BML") return "the game-wide settings";
            if (t.StartsWith("DATA/TEXT/")) return "the game's text (" + file + ")";
            if (t == "DATA/UI.PAK") return "the user interface";
            int production = t.IndexOf("/ENV/PRODUCTION/");
            if (production >= 0)
            {
                string rest = target.Substring(production + "/ENV/PRODUCTION/".Length);
                int slash = rest.IndexOf('/');
                if (slash > 0) return file + " in the level " + rest.Substring(0, slash);
            }
            return file;
        }

        public string Describe()
        {
            string text;
            switch (Kind)
            {
                case ConflictKind.WholeFile:
                    text = Name(Kept) + " and " + Name(Lost) + " both replace " + Place(Target) + " and it can't be combined: " + Name(Kept) + " is used" + (string.IsNullOrEmpty(Detail) ? "." : " (" + Detail + ")."); break;
                case ConflictKind.RemovedVsChanged:
                    text = Name(Kept) + " wins over " + Name(Lost) + " at " + Where + " in " + Place(Target) + (string.IsNullOrEmpty(Detail) ? "." : ": " + Detail + "."); break;
                default:
                    text = Name(Kept) + " and " + Name(Lost) + " both change " + Where + " in " + Place(Target) + ": " + Name(Kept) + " wins" + (string.IsNullOrEmpty(Detail) ? "." : " (" + Detail + ")."); break;
            }
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        public override string ToString() { return Describe(); }
    }

    /// <summary>A merged file, and what the user should know about how it was put together.</summary>
    public class MergeOutcome
    {
        public byte[] Bytes;
        public List<MergeConflict> Conflicts = new List<MergeConflict>();
        /// <summary>Something went wrong merging, so the last mod's whole file was used instead (see Conflicts).</summary>
        public bool FellBack;
    }

    /// <summary>
    /// Who last changed each part of a file while mods are merged into it one after another, so a later
    /// mod overriding an earlier one can say which earlier one it was.
    /// </summary>
    public class ClaimLedger
    {
        private readonly Dictionary<string, string> _claims = new Dictionary<string, string>();
        public string ClaimedBy(string where)
        {
            string mod;
            return _claims.TryGetValue(where, out mod) ? mod : null;
        }
        public void Claim(string where, string mod) { _claims[where] = mod; }
        /// <summary>The first earlier claim at or under this location (for a removal that sweeps away earlier changes inside it).</summary>
        public string ClaimedAtOrUnder(string where)
        {
            string mod = ClaimedBy(where);
            if (mod != null)
                return mod;
            foreach (KeyValuePair<string, string> claim in _claims)
                if (claim.Key.StartsWith(where + " › ", StringComparison.Ordinal))
                    return claim.Value;
            return null;
        }
    }
}
#endif
