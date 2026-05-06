using CATHODE;
using CATHODE.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Xml;
using System.Xml.XPath;
using System.Windows.Media.Animation;
using System.IO;

namespace CommandsEditor
{
    public static class EnumStringListViewItems
    {
        private static Dictionary<EnumStringType, Tuple<ListViewItem[], bool>> _levelSpecificEntries = new Dictionary<EnumStringType, Tuple<ListViewItem[], bool>>();
        private static Dictionary<EnumStringType, Tuple<ListViewItem[], bool>> _globalEntries = new Dictionary<EnumStringType, Tuple<ListViewItem[], bool>>();

        private static string _loadedLevel = "";

        /* Populate all enum strings for the global game */
        public static void PopulateGlobalEntries()
        {
            //Only populate global entries once
            if (_globalEntries.Count > 0)
                return;

            Debug.Log("Asset Loader - Global", "Starting to populate");
            foreach (EnumStringType type in Enum.GetValues(typeof(EnumStringType)))
            {
                if (!IsTypeGlobal(type)) continue;
                AddItems(type, _globalEntries);
            }
            Debug.Log("Asset Loader - Global", "Finished populating");
        }

        /* Populate all enum strings for the loaded level */
        public static void PopulateLevelSpecificEntries()
        {
            if (Singleton.Editor?.CommandsDisplay?.Content == null)
            {
                _levelSpecificEntries.Clear();
                return;
            }

            Debug.Log("Asset Loader - Level", "Starting to populate");

            //Populate DisplayModel every time, as it may change during editor runtime
            AddItems(EnumStringType.DISPLAY_MODEL, _levelSpecificEntries);

            //Only populate other entries if this is a new level
            if (_loadedLevel == Singleton.Editor.CommandsDisplay.Content.Level.Name)
                return;
            _loadedLevel = Singleton.Editor.CommandsDisplay.Content.Level.Name;

            _levelSpecificEntries.Clear();
            foreach (EnumStringType type in Enum.GetValues(typeof(EnumStringType)))
            {
                if (IsTypeGlobal(type)) continue;
                AddItems(type, _levelSpecificEntries);
            }
            Debug.Log("Asset Loader - Level", "Finished populating");
        }

        /* Get the items for a given type (the bool is if the desc column should show) */
        public static Tuple<ListViewItem[], bool> GetItems(EnumStringType type)
        {
            if (_levelSpecificEntries.TryGetValue(type, out Tuple<ListViewItem[], bool> fromLevel))
                return fromLevel;
            if (_globalEntries.TryGetValue(type, out Tuple<ListViewItem[], bool> fromGlobal))
                return fromGlobal;
            return null;
        }

        /* Should this string type be loaded globally or per level */
        private static bool IsTypeGlobal(EnumStringType type)
        {
            switch (type)
            {
                case EnumStringType.DISPLAY_MODEL:
                case EnumStringType.MATERIAL:
                case EnumStringType.SOUND_BANK:
                case EnumStringType.SOUND_EVENT:
                case EnumStringType.SOUND_REVERB:
                case EnumStringType.STRING_OBJECTIVES:
                case EnumStringType.STRING_TERMINAL:
                case EnumStringType.STRING_UI:
                    return false;
            }
            return true;
        }

        /* Add all items for the given type to the given db */
        private static void AddItems(EnumStringType type, Dictionary<EnumStringType, Tuple<ListViewItem[], bool>> dictionary)
        {
            List<ListViewItem> items = new List<ListViewItem>();
            bool useDescColumn = false;
            switch (type)
            {
                case EnumStringType.ACHIEVEMENT_ID:
                    foreach (string str in ParseXML("AWARDS/MAIN_AWARD_LIST.BML", "awards/award", "game_id"))
                        items.Add(new ListViewItem() { Text = str });
                    break;
                case EnumStringType.ACHIEVEMENT_STAT_ID:
                    foreach (string str in ParseXML("AWARDS/MAIN_AWARD_LIST.BML", "awards/stat", "stat_id"))
                        items.Add(new ListViewItem() { Text = str });
                    break;
                case EnumStringType.ANIMATION:
                    foreach (KeyValuePair<string, HashSet<string>> animSets in Singleton.AllAnimations)
                    {
                        foreach (string anim in animSets.Value)
                        {
                            ListViewItem item = new ListViewItem() { Text = anim };
                            item.SubItems.Add(animSets.Key);
                            items.Add(item);
                        }
                    }
                    useDescColumn = true;
                    break;
                case EnumStringType.ANIMATION_SET:
                    foreach (KeyValuePair<string, HashSet<string>> animSets in Singleton.AllAnimations)
                        items.Add(new ListViewItem() { Text = animSets.Key });
                    break;
                case EnumStringType.ANIMATION_TREE_SET:
                    foreach (string str in Singleton.AllAnimTrees)
                        items.Add(new ListViewItem() { Text = str });
                    break;
                case EnumStringType.ATTRIBUTE_SET:
                    foreach (string str in ParseXML("CHR_INFO/ATTRIBUTES/ATTRIBUTES.BML", "Attributes/Attribute", "Name", true))
                        items.Add(new ListViewItem() { Text = str });
                    break;
                case EnumStringType.BLUEPRINT_TYPE:
                    foreach (string str in ParseXML("GBL_ITEM.BML", "item_database/recipes/recipe", "name"))
                        items.Add(new ListViewItem() { Text = str });
                    break;
                case EnumStringType.DISPLAY_MODEL:
                    foreach (Composite composite in Singleton.Editor.CommandsDisplay.Content.Level.Commands.Entries)
                        if (Singleton.Editor.CommandsDisplay.Content.EditorUtils.GetCompositeType(composite) == EditorUtils.CompositeType.IS_DISPLAY_MODEL)
                            items.Add(new ListViewItem() { Text = composite.name.Substring(("DisplayModel:").Length) });
                    break;
                case EnumStringType.GAME_VARIABLE:
                    foreach (string str in ParseXML("SCRIPT_READABLE_VARIABLES.XML", "game_variables/variable", "id"))
                        items.Add(new ListViewItem() { Text = str });
                    break;
                case EnumStringType.GAMEPLAY_TIP_STRING_ID:
                    foreach (string str in ParseXML("GBL_ITEM.BML", "item_database/custom_gameplay_tips/custom_gameplay_tip", "string_id"))
                        items.Add(new ListViewItem() { Text = str });
                    break;
                case EnumStringType.IDTAG_ID:
                    {
                        List<string> uids = ParseXML("GBL_ITEM.BML", "item_database/journal_idtag_entries/idtag_entry", "uid");
                        List<string> names = ParseXML("GBL_ITEM.BML", "item_database/journal_idtag_entries/idtag_entry", "name");
                        for (int i = 0; i < uids.Count; i++)
                        {
                            ListViewItem item = new ListViewItem() { Text = uids[i] };
                            item.SubItems.Add(names[i]);
                            items.Add(item);
                        }
                        useDescColumn = true;
                    }
                    break;
                case EnumStringType.MAP_KEYFRAME_ID:
                    foreach (string str in ParseXML("GBL_ITEM.BML", "item_database/map_available_keyframes/map_keyframe", "name"))
                        items.Add(new ListViewItem() { Text = str });
                    break;
                case EnumStringType.MATERIAL:
                    foreach (Materials.Material entry in Singleton.Editor.CommandsDisplay.Content.Level.Materials.Entries)
                    {
                        if (items.FirstOrDefault(o => o.Text == entry.Name) == null)
                            items.Add(new ListViewItem() { Text = entry.Name });
                    }
                    break;
                case EnumStringType.NOSTROMO_LOG_ID:
                    {
                        List<string> uids = ParseXML("GBL_ITEM.BML", "item_database/journal_nostromo_entries/log_entry", "uid");
                        List<string> headers = ParseXML("GBL_ITEM.BML", "item_database/journal_nostromo_entries/log_entry", "heading_text");
                        for (int i = 0; i < uids.Count; i++)
                        {
                            ListViewItem item = new ListViewItem() { Text = uids[i] };
                            item.SubItems.Add(headers[i].TryLocalise());
                            items.Add(item);
                        }
                        useDescColumn = true;
                    }
                    break;
                case EnumStringType.OBJECTIVE_ENTRY_ID:
                    foreach (string str in ParseXML("OBJECTIVE_ENTRIES.XML", "localisation_entries/localisation_entry", "id"))
                        items.Add(new ListViewItem() { Text = str });
                    break;
                case EnumStringType.PRESENCE_ID:
                    foreach (string str in ParseXML("AWARDS/MAIN_AWARD_LIST.BML", "awards/presence", "game_id"))
                        items.Add(new ListViewItem() { Text = str });
                    break;
                case EnumStringType.SEVASTOPOL_LOG_ID:
                    {
                        List<string> uids = ParseXML("GBL_ITEM.BML", "item_database/journal_sevastopol_entries/log_entry", "uid");
                        List<string> headers = ParseXML("GBL_ITEM.BML", "item_database/journal_sevastopol_entries/log_entry", "heading_text");
                        for (int i = 0; i < uids.Count; i++)
                        {
                            ListViewItem item = new ListViewItem() { Text = uids[i] };
                            item.SubItems.Add(headers[i].TryLocalise());
                            items.Add(item);
                        }
                        useDescColumn = true;
                    }
                    break;
                case EnumStringType.SKELETON_SET:
                    foreach (string str in Singleton.AllSkeletons)
                        items.Add(new ListViewItem() { Text = str });
                    break;
                case EnumStringType.SOUND_BANK:
                    foreach (SoundBankData.SoundBank entry in Singleton.Editor.CommandsDisplay.Content.Level.SoundBankData.Entries)
                        if (items.FirstOrDefault(o => o.Text == entry.Name) == null)
                            items.Add(new ListViewItem() { Text = entry.Name });
                    break;
                case EnumStringType.SOUND_EVENT:
                    foreach (SoundEventData.Soundbank entry in Singleton.Editor.CommandsDisplay.Content.Level.SoundEventData.Entries)
                    {
                        foreach (SoundEventData.Soundbank.Event e in entry.events)
                        {
                            if (items.FirstOrDefault(o => o.Text == e.name) == null)
                            {
                                items.Add(new ListViewItem() { Text = e.name });
                                string localised = e.name.TryLocalise();
                                if (localised != e.name)
                                    items[items.Count - 1].SubItems.Add(localised);
                            }
                        }
                    }
                    useDescColumn = true;
                    break;
                case EnumStringType.SOUND_FOOTWEAR_GROUP:
                    foreach (string str in ParseXML("FOLEY_MATERIALS.XML", "foley_materials/feet", "id"))
                        items.Add(new ListViewItem() { Text = str });
                    break;
                case EnumStringType.SOUND_LEG_GROUP:
                    foreach (string str in ParseXML("FOLEY_MATERIALS.XML", "foley_materials/leg", "id"))
                        items.Add(new ListViewItem() { Text = str });
                    break;
                case EnumStringType.SOUND_REVERB:
                    foreach (string entry in Singleton.Editor.CommandsDisplay.Content.Level.SoundEnvironmentData.Entries)
                        if (items.FirstOrDefault(o => o.Text == entry) == null)
                            items.Add(new ListViewItem() { Text = entry });
                    break;
                case EnumStringType.SOUND_TORSO_GROUP:
                    foreach (string str in ParseXML("FOLEY_MATERIALS.XML", "foley_materials/torso", "id"))
                        items.Add(new ListViewItem() { Text = str });
                    break;
                case EnumStringType.STRING_OBJECTIVES:
                    foreach (KeyValuePair<string, TextDB> entries in Singleton.Editor.CommandsDisplay.Content.Level.Strings["ENGLISH"])
                    {
                        if (!(entries.Key.Length > 3 && entries.Key.Substring(0, 3).ToUpper() == "DLC"))
                            continue;

                        foreach (KeyValuePair<string, string> entry in entries.Value.Entries)
                        {
                            if (items.FirstOrDefault(o => o.Text == entry.Key) == null)
                            {
                                ListViewItem item = new ListViewItem() { Text = entry.Key };
                                item.SubItems.Add(entry.Value);
                                items.Add(item);
                            }
                        }
                    }
                    foreach (KeyValuePair<string, string> entry in Singleton.GlobalTextDBs["OBJECTIVES"].Entries)
                    {
                        if (items.FirstOrDefault(o => o.Text == entry.Key) == null)
                        {
                            ListViewItem item = new ListViewItem() { Text = entry.Key };
                            item.SubItems.Add(entry.Value);
                            items.Add(item);
                        }
                    }
                    useDescColumn = true;
                    break;
                case EnumStringType.STRING_TERMINAL:
                    foreach (KeyValuePair<string, TextDB> entries in Singleton.Editor.CommandsDisplay.Content.Level.Strings["ENGLISH"])
                    {
                        if (!(entries.Key.Length > 3 && entries.Key.Substring(0, 3).ToUpper() == "DLC") &&
                            !(entries.Key.Length > 2 && entries.Key.Substring(0, 2).ToUpper() == "T0") && entries.Key != "UI")
                            continue;

                        foreach (KeyValuePair<string, string> entry in entries.Value.Entries)
                        {
                            if (items.FirstOrDefault(o => o.Text == entry.Key) == null)
                            {
                                ListViewItem item = new ListViewItem() { Text = entry.Key };
                                item.SubItems.Add(entry.Value);
                                items.Add(item);
                            }
                        }
                    }
                    foreach (KeyValuePair<string, TextDB> entries in Singleton.GlobalTextDBs)
                    {
                        if (!(entries.Key.Length > 2 && entries.Key.Substring(0, 2).ToUpper() == "T0") && entries.Key != "UI")
                            continue;

                        foreach (KeyValuePair<string, string> entry in entries.Value.Entries)
                        {
                            if (items.FirstOrDefault(o => o.Text == entry.Key) == null)
                            {
                                ListViewItem item = new ListViewItem() { Text = entry.Key };
                                item.SubItems.Add(entry.Value);
                                items.Add(item);
                            }
                        }
                    }
                    useDescColumn = true;
                    break;
                case EnumStringType.STRING_UI:
                    foreach (KeyValuePair<string, TextDB> entries in Singleton.Editor.CommandsDisplay.Content.Level.Strings["ENGLISH"])
                    {
                        if (!(entries.Key.Length > 3 && entries.Key.Substring(0, 3).ToUpper() == "DLC") && entries.Key != "UI")
                            continue;

                        foreach (KeyValuePair<string, string> entry in entries.Value.Entries)
                        {
                            if (items.FirstOrDefault(o => o.Text == entry.Key) == null)
                            {
                                ListViewItem item = new ListViewItem() { Text = entry.Key };
                                item.SubItems.Add(entry.Value);
                                items.Add(item);
                            }
                        }
                    }
                    foreach (KeyValuePair<string, TextDB> entries in Singleton.GlobalTextDBs)
                    {
                        if (entries.Key != "UI")
                            continue;

                        foreach (KeyValuePair<string, string> entry in entries.Value.Entries)
                        {
                            if (items.FirstOrDefault(o => o.Text == entry.Key) == null)
                            {
                                ListViewItem item = new ListViewItem() { Text = entry.Key };
                                item.SubItems.Add(entry.Value);
                                items.Add(item);
                            }
                        }
                    }
                    useDescColumn = true;
                    break;
                case EnumStringType.TEXTURE:
                    foreach (Textures.TEX4 entry in Singleton.Editor.CommandsDisplay.Content.Level.Textures.Entries)
                    {
                        if (items.FirstOrDefault(o => o.Text == entry.Name) == null)
                            items.Add(new ListViewItem() { Text = entry.Name });
                    }
                    foreach (Textures.TEX4 entry in Singleton.Global.Textures.Entries)
                    {
                        if (items.FirstOrDefault(o => o.Text == entry.Name) == null)
                            items.Add(new ListViewItem() { Text = entry.Name });
                    }
                    break;
                case EnumStringType.TUTORIAL_ENTRY_ID:
                    foreach (string str in ParseXML("TUTORIAL_ENTRIES.XML", "localisation_entries/localisation_entry", "id"))
                        items.Add(new ListViewItem() { Text = str });
                    break;

                //case EnumStringType.SOUND_DIALOGUE:
                //    foreach (SoundDialogueLookups.Sound entry in Content.resource.sound_dialoguelookups.Entries)
                //    {
                //        if (strings.FirstOrDefault(o => o.Text == entry.ToString()) == null)
                //        {
                //            strings.Add(new ListViewItem() { Text = entry.ToString() });
                //            strings[strings.Count - 1].SubItems.Add(Singleton.TryLocalise(entry.ToString()));
                //        }
                //    }
                //    useDescColumn = true;
                //    break;
            }
            items.OrderBy(o => o.Text);

            var output = new Tuple<ListViewItem[], bool>(items.ToArray(), useDescColumn);
            if (dictionary.ContainsKey(type))
                dictionary[type] = output;
            else
                dictionary.Add(type, output);
        }

        /* Parse an XML to retrieve the enum string values */
        private static List<string> ParseXML(string file, string path, string attribute, bool isNode = false)
        {
            file = Singleton.PathToAI + "/DATA/" + file;
            if (!File.Exists(file))
            {
                Debug.Log("EnumStringListViewItems", $"Could not find {file} to parse enum strings for {attribute}");
                return new List<string>();
            }

            XDocument xml = System.IO.Path.GetExtension(file) == ".BML" ? XDocument.Load(new XmlNodeReader(new BML(file).Content)) : XDocument.Load(file);
            foreach (var elem in xml.Descendants())
                elem.Name = elem.Name.LocalName;
            List<XElement> entries = xml.XPathSelectElements(path).ToList();
            List<string> strings = new List<string>();
            foreach (XElement entry in entries)
                strings.Add(isNode ? entry.Element(attribute).Value : entry.Attribute(attribute).Value);
            return strings;
        }
    }
}
