using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenCAGE.LevelDiff;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenCAGE.Recipes
{
    /// <summary>
    /// A recorded set of edits, replayable onto another copy of the level or another level. A JSONL file: the first
    /// line is the header ({"recipe":1, "name", "level", "root", "recorded", "steps": [the undo steps recorded], "not_carried": [...]}),
    /// then one step per line ({"op":"set_param", "composite":ID, "entity":ID, "param":"position", "before":{...}, "after":{...}} and
    /// the like). Values are in <see cref="CathodeJson"/> form. Lines starting with # and blank lines are skipped, so a recipe can be
    /// annotated by hand.
    /// </summary>
    public sealed class Recipe
    {
        public const string Extension = ".ocrecipe";
        public const int Version = 1;

        public JObject Header = new JObject() { ["recipe"] = Version };
        public readonly List<JObject> Ops = new List<JObject>();

        public string Name { get => (string)Header["name"]; set => Header["name"] = value; }
        /// <summary>The level it was recorded in (list_levels' name).</summary>
        public string Level { get => (string)Header["level"]; set => Header["level"] = value; }
        /// <summary>That level's root composite id: a copy of the same level has the same one.</summary>
        public string Root { get => (string)Header["root"]; set => Header["root"] = value; }
        public List<string> Steps => (Header["steps"] as JArray)?.Select(o => (string)o).ToList() ?? new List<string>();
        public List<string> NotCarried => (Header["not_carried"] as JArray)?.Select(o => (string)o).ToList() ?? new List<string>();

        public static Recipe Parse(string text)
        {
            Recipe recipe = new Recipe();
            bool header = false;
            int line = 0;
            using (StringReader reader = new StringReader(text ?? ""))
            {
                string row;
                while ((row = reader.ReadLine()) != null)
                {
                    line++;
                    string trimmed = row.Trim().TrimStart('﻿');
                    if (trimmed.Length == 0 || trimmed.StartsWith("#") || trimmed.StartsWith("//")) continue;
                    JObject o;
                    try { o = CathodeJson.Parse(trimmed) as JObject; }
                    catch (JsonException e) { throw new RecipeException("line " + line + " is not JSON: " + e.Message); }
                    if (o == null) throw new RecipeException("line " + line + " is not a JSON object");
                    if (!header && o["recipe"] != null)
                    {
                        recipe.Header = o;
                        header = true;
                        if ((int)o["recipe"] > Version) throw new RecipeException("this recipe was written by a newer OpenCAGE (format " + (int)o["recipe"] + "; this one reads " + Version + ")");
                        continue;
                    }
                    if (o["op"] == null) throw new RecipeException("line " + line + " has no \"op\"");
                    recipe.Ops.Add(o);
                }
            }
            if (!header) throw new RecipeException("there is no header line ({\"recipe\":1, ...}): not a recipe");
            return recipe;
        }

        public static Recipe Read(string path) => Parse(File.ReadAllText(path));

        public string ToJsonl()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Header.ToString(Formatting.None)).Append('\n');
            foreach (JObject op in Ops) sb.Append(op.ToString(Formatting.None)).Append('\n');
            return sb.ToString();
        }

        public void Save(string path)
        {
            Header["ops"] = Ops.Count;
            File.WriteAllText(path, ToJsonl(), new UTF8Encoding(false));
        }

        /// <summary>"12 steps: 3 entities added, 7 parameters set ..." for a header line or a status bar.</summary>
        public string Summary()
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (JObject op in Ops)
            {
                string kind = (string)op["op"];
                counts[kind] = (counts.TryGetValue(kind, out int n) ? n : 0) + 1;
            }
            if (counts.Count == 0) return "no changes";
            return Ops.Count + " steps: " + string.Join(", ", counts.OrderByDescending(o => o.Value).Select(o => o.Value + " " + RecipeText.OpTitle(o.Key, o.Value)));
        }
    }

    /// <summary>Readable text for recipe steps.</summary>
    public static class RecipeText
    {
        public static string OpTitle(string op, int count)
        {
            bool many = count != 1;
            switch (op)
            {
                case "add_composite": return many ? "composites added" : "composite added";
                case "rename_composite": return many ? "composites renamed" : "composite renamed";
                case "remove_composite": return many ? "composites removed" : "composite removed";
                case "set_pins": return many ? "pin type changes" : "pin type change";
                case "set_colours": return many ? "colour pin changes" : "colour pin change";
                case "add_entity": return many ? "entities added" : "entity added";
                case "remove_entity": return many ? "entities removed" : "entity removed";
                case "set_spec": return many ? "entities retyped or repointed" : "entity retyped or repointed";
                case "set_param": return many ? "parameters set" : "parameter set";
                case "remove_param": return many ? "parameters removed" : "parameter removed";
                case "links": return many ? "link changes" : "link change";
                case "set_resources": return many ? "resource changes" : "resource change";
                case "set_special": return many ? "animation / sequence changes" : "animation / sequence change";
                case "set_brush": return many ? "brush changes" : "brush change";
                case "set_material": return many ? "material values set" : "material value set";
                default: return op;
            }
        }

        /// <summary>One line for a step: "set position on Door_1 in AYZ\...\Doors: (1, 2, 3)".</summary>
        public static string Describe(JObject op)
        {
            string composite = (string)op["composite_name"] ?? (string)op["composite"];
            string entity = (string)op["entity_name"] ?? (string)op["entity"];
            switch ((string)op["op"])
            {
                case "add_composite": return "add composite " + (string)op["name"];
                case "rename_composite": return "rename composite " + (string)op["from"] + " to " + (string)op["to"];
                case "remove_composite": return "remove composite " + (string)op["name"];
                case "set_pins": return "pin types of " + composite;
                case "set_colours": return "colour pins of " + composite;
                case "add_entity": return "add " + Kind(op["spec"] as JObject) + " " + entity + " to " + composite;
                case "remove_entity": return "remove " + entity + " from " + composite;
                case "set_spec": return "retype / repoint " + entity + " in " + composite;
                case "set_param": return "set " + (string)op["param"] + " on " + entity + " in " + composite + ": " + CathodeJson.Show(op["after"], 80);
                case "remove_param": return "remove " + (string)op["param"] + " from " + entity + " in " + composite;
                case "links": return "links of " + entity + " in " + composite + ((op["add"] as JArray)?.Count > 0 ? ": +" + ((JArray)op["add"]).Count : "") + ((op["remove"] as JArray)?.Count > 0 ? " -" + ((JArray)op["remove"]).Count : "");
                case "set_resources": return "resources of " + entity + " in " + composite;
                case "set_special": return "animation / sequence of " + entity + " in " + composite;
                case "set_brush": return "brush shape of " + entity + " in " + composite;
                case "set_material": return "material " + (string)op["material"] + " " + (string)op["field"] + ": " + (string)op["before"] + " -> " + (string)op["after"];
                default: return (string)op["op"];
            }
        }

        private static string Kind(JObject spec)
        {
            if (spec == null) return "entity";
            switch ((string)spec["kind"])
            {
                case "function": return (string)spec["function"];
                case "instance": return "instance";
                default: return (string)spec["kind"];
            }
        }
    }
}
