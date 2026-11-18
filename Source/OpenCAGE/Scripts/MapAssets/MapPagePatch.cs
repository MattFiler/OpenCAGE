using OpenCAGE.Hud;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenCAGE.MapAssets
{
    /// <summary>
    /// The game's own hub and map page, changed as little as a level's map needs, from the user's own UI.PAK: the hub loads the map page
    /// from another URL; the map page loads the floors from another folder and shows the level's map and floor names for the keyframes the
    /// level borrows. Only string constants and one prologue in the page's SetMapKeyframe change; everything else is the game's.
    /// </summary>
    public static class MapPagePatch
    {
        public const string HubPageUrl = "Hub_Map.swf", PageFloorFolder = "maps/";

        /// <summary>The hub (DATA/UI/HUB.GFX) loading its map page from pageUrl instead of the game's.</summary>
        public static byte[] Hub(byte[] hub, string pageUrl)
        {
            return EditAbc(hub, a =>
            {
                int s = FindString(a, HubPageUrl);
                if (s < 0) throw new InvalidDataException("this hub movie doesn't load " + HubPageUrl);
                a.Strings[s] = Encoding.UTF8.GetBytes(pageUrl);
            });
        }

        /// <summary>
        /// The map page (DATA/UI/HUB_MAP.GFX) loading floors from floorFolder ("LV/": floor = folder + keyframe + ".swf") and, for each
        /// borrowed keyframe, showing the level's map name and floor name instead of the game's (names: keyframe -> {map, floor}).
        /// fontLibrary: where its font import points once it no longer sits beside the game's font library (null: as it was).
        /// </summary>
        public static byte[] Page(byte[] page, string floorFolder, IDictionary<string, KeyValuePair<string, string>> names, string fontLibrary = null)
        {
            if (fontLibrary != null)
                page = ImportsFrom(page, fontLibrary);
            return EditAbc(page, a =>
            {
                int s = FindString(a, PageFloorFolder);
                if (s < 0) throw new InvalidDataException("this map page doesn't load its floors from " + PageFloorFolder);
                a.Strings[s] = Encoding.UTF8.GetBytes(floorFolder);
                if (names == null || names.Count == 0) return;

                // SetMapKeyframe(keyframe, map_group, map_floor): a prologue sets map_group / map_floor for the borrowed keyframes
                int method = -1;
                foreach (AbcInstance inst in a.Instances)
                    foreach (AbcTrait t in inst.Traits)
                        if ((t.Type == AbcTrait.Method) && a.MnName(t.Name) == "SetMapKeyframe") method = t.B;
                if (method < 0) throw new InvalidDataException("this map page has no SetMapKeyframe");
                AbcBody body = a.BodyOf(method);
                AbcCode c = new AbcCode(a);
                int k = 0;
                foreach (KeyValuePair<string, KeyValuePair<string, string>> n in names)
                {
                    string next = "next" + k++;
                    c.GetLocal(1).Str(n.Key).IfNe(next).Str(n.Value.Key).SetLocal(2).Str(n.Value.Value).SetLocal(3).Target(next);
                }
                byte[] prologue = c.ToArray();
                body.Code = prologue.Concat(body.Code).ToArray();
                foreach (AbcException e in body.Exceptions) { e.From += prologue.Length; e.To += prologue.Length; e.Target += prologue.Length; }
                body.MaxStack = Math.Max(body.MaxStack, 2);
                body.LocalCount = Math.Max(body.LocalCount, 4);
            });
        }

        /// <summary>A movie whose asset imports (ImportAssets / ImportAssets2) all come from url.</summary>
        public static byte[] ImportsFrom(byte[] movie, string url)
        {
            SwfFile swf = SwfFile.Read(movie);
            foreach (SwfTag t in swf.Tags.Where(o => o.Type == 57 || o.Type == 71))
            {
                int end = Array.IndexOf(t.Body, (byte)0);
                if (end < 0) continue;
                t.Body = Encoding.UTF8.GetBytes(url).Concat(t.Body.Skip(end)).ToArray();
            }
            return swf.Write();
        }

        private static int FindString(AbcFile a, string s)
        {
            byte[] want = Encoding.UTF8.GetBytes(s);
            for (int i = 1; i < a.Strings.Count; i++) if (a.Strings[i] != null && a.Strings[i].SequenceEqual(want)) return i;
            return -1;
        }

        /* Every DoABC block of a movie read, edited, written back (the movie is written uncompressed) */
        private static byte[] EditAbc(byte[] movie, Action<AbcFile> edit)
        {
            SwfFile swf = SwfFile.Read(movie);
            bool any = false;
            foreach (SwfTag t in swf.Tags.Where(o => o.Type == SwfFile.TagDoAbc))
            {
                SwfFile.SplitDoAbc(t.Body, out uint flags, out string name, out byte[] abc);
                AbcFile a = AbcFile.Read(abc);
                edit(a);
                t.Body = SwfFile.JoinDoAbc(flags, name, a.Write());
                t.LongForm = true;
                any = true;
            }
            if (!any) throw new InvalidDataException("the movie has no ActionScript block");
            return swf.Write();
        }
    }
}
