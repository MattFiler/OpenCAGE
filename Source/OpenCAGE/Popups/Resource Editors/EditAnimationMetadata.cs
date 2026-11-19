using CATHODE;
using CATHODE.Animations;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// An animation's metadata as the game reads it, to see and edit. It comes in blocks: one for the animation itself, and
    /// one per use of it. A block holds settings (named values, each of a type) and tracks (named moments on the animation's
    /// timeline - a foot striking the floor, a sound starting - each entry of which may fire one of the block's settings).
    /// Everything shows as it is stored, names and all, whether or not what it means is known. The edits are made to a copy,
    /// and only Save puts them into the animation (every stored copy of it at once); the Animation Editor then writes
    /// ANIMATION.PAK.
    /// </summary>
    public partial class EditAnimationMetadata : BaseWindow
    {
        private readonly CathodeLib.Animation _animations;
        private readonly CathodeLib.Animation.ClipReference _clip;

        //What is being edited: a copy of the animation's metadata, until Save
        private readonly AnimClipDBSec.MetadataSet _working;
        //Why it can only be looked at (null when it can be edited)
        private readonly string _readOnly;
        private bool _dirty;
        //Close without asking: Save went through, or Cancel was pressed
        private bool _closing;
        private int _filling;

        private AnimClipDBSec.MetadataBlock _block;
        private AnimClipDBSec.MetadataProperty _track;

        /* The types a setting can be given here: those whose values are read and written. The others (64-bit numbers, script
         * interfaces) show as they are and can only be removed. */
        private static readonly MetadataValueType[] EditableTypes =
        {
            MetadataValueType.BOOL, MetadataValueType.UINT32, MetadataValueType.INT32, MetadataValueType.FLOAT32,
            MetadataValueType.STRING, MetadataValueType.AUDIO, MetadataValueType.PROPERTY_REFERENCE, MetadataValueType.VECTOR,
        };

        //What a sound setting holds, in the form every one the game ships with is in
        private const string AudioTemplate = "[ArgumentList={},Bone={},Event={},Offset={0,0,0},UseArguments={No}]";

        public EditAnimationMetadata(CathodeLib.Animation animations, CathodeLib.Animation.ClipReference clip)
            : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            InitializeComponent();

            _animations = animations;
            _clip = clip;
            _working = Copy(clip.Metadata);
            _readOnly = WhyReadOnly();

            Text = "Metadata of '" + ClipName + "' (" + (clip.Context?.Set?.Name ?? "") + ")";
            Icon = SharedFormIcon.Icon;

            /* The times read right-aligned under a dark theme's header. A first column can only be given that before it is
             * one: set on it there, it goes back to the left */
            entryList.Columns.Remove(entryTimeColumn);
            entryTimeColumn.TextAlign = HorizontalAlignment.Right;
            entryList.Columns.Insert(0, entryTimeColumn);

            foreach (MetadataValueType type in EditableTypes) typeBox.Items.Add(type);
            if (_readOnly != null)
            {
                foreach (Control control in new Control[] { trackNameBox, addTrackBtn, renameTrackBtn, removeTrackBtn, timeBox, firesBox, addEntryBtn, applyEntryBtn, removeEntryBtn,
                                                            settingNameBox, typeBox, mirrorCheck, speedCheck, valueBox, addSettingBtn, applySettingBtn, removeSettingBtn })
                    control.Enabled = false;
                saveBtn.Visible = false;
                cancelBtn.Text = "Close";
            }

            infoLabel.Text = Describe();
            FillBlocks();
            UpdateButtons();
        }

        private string ClipName => string.IsNullOrEmpty(_clip.Name) ? Path.GetFileName(_clip.Path ?? "") : _clip.Name;

        #region EVENTS
        private void blockBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_filling == 0) UseBlock((blockBox.SelectedItem as BlockEntry)?.Block);
        }

        private void trackList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_filling == 0) UseTrack(trackList.SelectedItems.Count == 0 ? null : trackList.SelectedItems[0].Tag as AnimClipDBSec.MetadataProperty);
        }

        private void entryList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_filling == 0) ShowEntry();
        }

        private void settingList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_filling == 0) ShowSetting();
        }

        private void typeBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_filling != 0) return;
            if (typeBox.SelectedItem is MetadataValueType type && type == MetadataValueType.AUDIO && valueBox.Text.Trim().Length == 0)
                valueBox.Text = AudioTemplate;
        }

        private void addTrackBtn_Click(object sender, EventArgs e)
        {
            AddTrack();
        }

        private void renameTrackBtn_Click(object sender, EventArgs e)
        {
            RenameTrack();
        }

        private void removeTrackBtn_Click(object sender, EventArgs e)
        {
            RemoveTrack();
        }

        private void addEntryBtn_Click(object sender, EventArgs e)
        {
            PutEntry(false);
        }

        private void applyEntryBtn_Click(object sender, EventArgs e)
        {
            PutEntry(true);
        }

        private void removeEntryBtn_Click(object sender, EventArgs e)
        {
            RemoveEntry();
        }

        private void addSettingBtn_Click(object sender, EventArgs e)
        {
            PutSetting(false);
        }

        private void applySettingBtn_Click(object sender, EventArgs e)
        {
            PutSetting(true);
        }

        private void removeSettingBtn_Click(object sender, EventArgs e)
        {
            RemoveSetting();
        }

        private void saveBtn_Click(object sender, EventArgs e)
        {
            Save();
        }

        private void cancelBtn_Click(object sender, EventArgs e)
        {
            _closing = true;
            DialogResult = DialogResult.Cancel;
            Close();
        }
        #endregion

        #region DESCRIPTION

        private string Describe()
        {
            List<string> lines = new List<string>();
            string length = _clip.Playable ? _clip.Duration.ToString("0.###", CultureInfo.InvariantCulture) + " s, " + _clip.Animation.FrameCount + " frames" : "length unknown (it can't be read)";
            lines.Add("'" + ClipName + "' in " + (_clip.Context?.Set?.Name ?? "?") + " - " + length + ". Stored as " + _clip.Path + ".");
            if (_readOnly != null)
                lines.Add("Shown only: " + _readOnly + ".");
            else if (!(_clip.Path ?? "").StartsWith(@"ANIMATION\OPENCAGE\", StringComparison.OrdinalIgnoreCase))
                lines.Add("This animation ships with the game: a change here is to it everywhere it plays, for every character and in every level.");
            else
                lines.Add("Changes go into the animation when saved, and ANIMATION.PAK is written straight away.");
            return string.Join("\r\n", lines);
        }

        /* Edited only where every stored copy of the animation can take the same change and be written back exactly */
        private string WhyReadOnly()
        {
            AnimClipDBSec section = _clip.Section;
            if (section == null || _clip.Metadata == null)
                return "its metadata isn't in ANIMATION.PAK";
            if (section.Metadata.Count != 1)
                return "it is stored together with " + (section.Metadata.Count - 1) + " other animations, and only the metadata of an animation stored on its own can be edited here";
            foreach (AnimClipDBSec copy in _animations.CopiesOf(section))
            {
                if (!copy.MetadataParsed)
                    return Path.GetFileName(copy.Filepath) + "'s metadata is kept byte for byte, as it doesn't read back exactly, so it can't be changed";
                if (copy.Metadata.Count != 1)
                    return "its copy " + Path.GetFileName(copy.Filepath) + " holds a different number of animations";
                if (!Same(copy.Metadata[0], section.Metadata[0]))
                    return "its stored copies (one for each build of the game) don't hold the same metadata, so one change can't be made to them all";
            }
            return null;
        }
        #endregion

        #region SHOWING
        private class BlockEntry
        {
            public AnimClipDBSec.MetadataBlock Block;
            public string Label;
            public override string ToString() => Label;
        }

        private void FillBlocks()
        {
            _filling++;
            try
            {
                AnimClipDBSec.MetadataBlock was = _block;
                blockBox.Items.Clear();
                blockBox.Items.Add(new BlockEntry { Block = _working.Common, Label = "The animation itself" });
                for (int i = 0; i < _working.Instances.Count; i++)
                    blockBox.Items.Add(new BlockEntry { Block = _working.Instances[i], Label = "Use " + i });

                //Open on the block it was on, else the first with tracks - where the events are - else the first use
                BlockEntry pick = blockBox.Items.Cast<BlockEntry>().FirstOrDefault(o => o.Block == was)
                    ?? blockBox.Items.Cast<BlockEntry>().FirstOrDefault(o => o.Block.Properties.Count != 0)
                    ?? blockBox.Items.Cast<BlockEntry>().ElementAtOrDefault(blockBox.Items.Count > 1 ? 1 : 0);
                blockBox.SelectedItem = pick;
            }
            finally { _filling--; }
            UseBlock((blockBox.SelectedItem as BlockEntry)?.Block);
        }

        private void UseBlock(AnimClipDBSec.MetadataBlock block)
        {
            _block = block;
            FillTracks(null);
            FillSettings(null);
        }

        private void FillTracks(AnimClipDBSec.MetadataProperty select)
        {
            _filling++;
            try
            {
                trackList.BeginUpdate();
                trackList.Items.Clear();
                if (_block != null)
                    foreach (AnimClipDBSec.MetadataProperty track in _block.Properties)
                    {
                        ListViewItem item = new ListViewItem(track.Name) { Tag = track };
                        item.SubItems.Add(track.Times.Count.ToString());
                        item.SubItems.Add(track.HasEvents ? "yes" : "");
                        trackList.Items.Add(item);
                        if (track == select) item.Selected = true;
                    }
                if (trackList.SelectedItems.Count == 0 && trackList.Items.Count != 0) trackList.Items[0].Selected = true;
                trackList.EndUpdate();
                trackList.SelectedItems.Cast<ListViewItem>().FirstOrDefault()?.EnsureVisible();
                tracksBox.Text = "Tracks (" + trackList.Items.Count + ")";
            }
            finally { _filling--; }
            UseTrack(trackList.SelectedItems.Count == 0 ? null : trackList.SelectedItems[0].Tag as AnimClipDBSec.MetadataProperty);
        }

        private void UseTrack(AnimClipDBSec.MetadataProperty track)
        {
            _track = track;
            if (track != null) trackNameBox.Text = track.Name;
            FillEntries(-1);
        }

        private void FillEntries(int select)
        {
            _filling++;
            try
            {
                entryList.BeginUpdate();
                entryList.Items.Clear();
                if (_track != null)
                    for (int i = 0; i < _track.Times.Count; i++)
                    {
                        AnimClipDBSec.MetadataEvent fired = i < _track.Events.Count ? _track.Events[i] : null;
                        ListViewItem item = new ListViewItem(_track.Times[i].ToString("0.####", CultureInfo.InvariantCulture)) { Tag = i };
                        item.SubItems.Add(fired?.Name ?? "");
                        item.SubItems.Add(WhatFires(fired));
                        entryList.Items.Add(item);
                        if (i == select) item.Selected = true;
                    }
                entryList.EndUpdate();
                entryList.SelectedItems.Cast<ListViewItem>().FirstOrDefault()?.EnsureVisible();

                //What an entry can fire: the block's settings by name
                string typed = firesBox.Text;
                firesBox.Items.Clear();
                firesBox.Items.Add("");
                if (_block != null)
                    foreach (AnimClipDBSec.MetadataArgument argument in _block.Arguments)
                        if (!string.IsNullOrEmpty(argument.Name) && !firesBox.Items.Contains(argument.Name)) firesBox.Items.Add(argument.Name);
                firesBox.Text = typed;
                entriesBox.Text = _track == null ? "Entries of the track" : "Entries of " + _track.Name + " (" + _track.Times.Count + ")";
            }
            finally { _filling--; }
            ShowEntry();
            UpdateButtons();
        }

        /* What an entry fires, in words: the setting it names (a sound, for most), or the raw value it carries */
        private string WhatFires(AnimClipDBSec.MetadataEvent fired)
        {
            if (fired == null) return "";
            if (fired.Type != MetadataValueType.PROPERTY_REFERENCE)
                return "a " + fired.Type + " value, as stored";
            if (string.IsNullOrEmpty(fired.Name)) return "";
            AnimClipDBSec.MetadataArgument argument = _block?.Arguments.FirstOrDefault(o => o.Name == fired.Name);
            if (argument == null) return "no setting of that name in this block";
            if (argument.Type == MetadataValueType.AUDIO)
            {
                CathodeLib.Animation.AudioEvent audio = CathodeLib.Animation.ParseAudioEvent(argument.Value as string);
                if (audio != null) return "sound " + audio.Event + (string.IsNullOrEmpty(audio.Bone) ? "" : " from " + audio.Bone);
            }
            return argument.Type + " " + ValueText(argument);
        }

        private int SelectedEntry => entryList.SelectedItems.Count == 0 ? -1 : (int)entryList.SelectedItems[0].Tag;

        private void ShowEntry()
        {
            int at = SelectedEntry;
            if (_track == null || at < 0) { UpdateButtons(); return; }
            _filling++;
            try
            {
                timeBox.Text = _track.Times[at].ToString("R", CultureInfo.InvariantCulture);
                firesBox.Text = at < _track.Events.Count ? _track.Events[at].Name ?? "" : "";
            }
            finally { _filling--; }
            UpdateButtons();
        }

        private void FillSettings(AnimClipDBSec.MetadataArgument select)
        {
            _filling++;
            try
            {
                settingList.BeginUpdate();
                settingList.Items.Clear();
                if (_block != null)
                    foreach (AnimClipDBSec.MetadataArgument argument in _block.Arguments)
                    {
                        ListViewItem item = new ListViewItem(argument.Name) { Tag = argument };
                        item.SubItems.Add(argument.Type.ToString());
                        item.SubItems.Add(ValueText(argument));
                        item.SubItems.Add(argument.CanMirror ? "yes" : "");
                        item.SubItems.Add(argument.CanModulateByPlayspeed ? "yes" : "");
                        item.SubItems.Add(argument.RequiresConvert.ToString());
                        if (Protected(argument)) item.ForeColor = Color.Gray;
                        settingList.Items.Add(item);
                        if (argument == select) item.Selected = true;
                    }
                settingList.EndUpdate();
                settingList.SelectedItems.Cast<ListViewItem>().FirstOrDefault()?.EnsureVisible();
                settingsBox.Text = "Settings in this block (" + settingList.Items.Count + ")";
            }
            finally { _filling--; }
            ShowSetting();
        }

        private AnimClipDBSec.MetadataArgument SelectedSetting => settingList.SelectedItems.Count == 0 ? null : settingList.SelectedItems[0].Tag as AnimClipDBSec.MetadataArgument;

        private void ShowSetting()
        {
            AnimClipDBSec.MetadataArgument argument = SelectedSetting;
            if (argument != null)
            {
                _filling++;
                try
                {
                    settingNameBox.Text = argument.Name;
                    typeBox.SelectedItem = EditableTypes.Contains(argument.Type) ? (object)argument.Type : null;
                    valueBox.Text = ValueText(argument);
                    mirrorCheck.Checked = argument.CanMirror;
                    speedCheck.Checked = argument.CanModulateByPlayspeed;
                }
                finally { _filling--; }
            }
            UpdateButtons();
        }

        /* A setting's value as it is typed back in: numbers the same in every language (1.5, not 1,5) and exact */
        private static string ValueText(AnimClipDBSec.MetadataArgument argument)
        {
            switch (argument.Value)
            {
                case null: return EditableTypes.Contains(argument.Type) ? "" : "(not read)";
                case bool flag: return flag ? "true" : "false";
                case float number: return number.ToString("R", CultureInfo.InvariantCulture);
                case System.Numerics.Vector3 vector:
                    return vector.X.ToString("R", CultureInfo.InvariantCulture) + ", " + vector.Y.ToString("R", CultureInfo.InvariantCulture) + ", " + vector.Z.ToString("R", CultureInfo.InvariantCulture);
                case IFormattable formattable: return formattable.ToString(null, CultureInfo.InvariantCulture);
                default: return argument.Value.ToString();
            }
        }

        /* Settings that are how the game finds the animation's metadata at all, kept as they are */
        private static bool Protected(AnimClipDBSec.MetadataArgument argument)
        {
            return argument.Name == "anim_label" || argument.Name == "meta_label" || argument.Name == "0";
        }

        private void UpdateButtons()
        {
            bool editable = _readOnly == null;
            AnimClipDBSec.MetadataArgument setting = SelectedSetting;
            renameTrackBtn.Enabled = removeTrackBtn.Enabled = editable && _track != null;
            addTrackBtn.Enabled = editable && _block != null;
            addEntryBtn.Enabled = editable && _track != null;
            applyEntryBtn.Enabled = removeEntryBtn.Enabled = editable && _track != null && SelectedEntry >= 0;
            addSettingBtn.Enabled = editable && _block != null;
            applySettingBtn.Enabled = editable && setting != null && !Protected(setting) && EditableTypes.Contains(setting.Type);
            removeSettingBtn.Enabled = editable && setting != null && !Protected(setting);
            saveBtn.Enabled = editable && _dirty;
            if (setting != null && Protected(setting))
                statusLabel.Text = "'" + setting.Name + "' is how the game finds this animation's metadata, so it is kept as it is.";
        }

        private void Changed(string what)
        {
            _dirty = true;
            statusLabel.Text = what;
            UpdateButtons();
        }

        private void Refuse(string why)
        {
            statusLabel.Text = why;
            MessageBox.Show(why, "Can't do that", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        #endregion

        #region TRACKS
        private void AddTrack()
        {
            if (_block == null) return;
            string name = trackNameBox.Text.Trim();
            if (name.Length == 0) { Refuse("Type the new track's name first."); return; }
            if (_block.Properties.Any(o => o.Name == name)) { Refuse("This block has a track called '" + name + "' already."); return; }

            AnimClipDBSec.MetadataProperty track = new AnimClipDBSec.MetadataProperty { Name = name };
            _block.Properties.Add(track);
            _block.HasProperties = true;
            FillTracks(track);
            Changed("Track '" + name + "' added: give it its first entry on the right.");
            timeBox.Focus();
        }

        private void RenameTrack()
        {
            if (_track == null) return;
            string name = trackNameBox.Text.Trim();
            if (name.Length == 0 || name == _track.Name) return;
            if (_block.Properties.Any(o => o != _track && o.Name == name)) { Refuse("This block has a track called '" + name + "' already."); return; }
            string was = _track.Name;
            _track.Name = name;
            FillTracks(_track);
            Changed("Track '" + was + "' renamed '" + name + "'.");
        }

        private void RemoveTrack()
        {
            if (_track == null) return;
            string name = _track.Name;
            _block.Properties.Remove(_track);
            _block.HasProperties = _block.Properties.Count != 0;
            FillTracks(null);
            Changed("Track '" + name + "' removed.");
        }
        #endregion

        #region ENTRIES
        /* An entry added, or the selected one changed. Entries stay in time order, each with what it fires when the track fires
         * anything - one per time, as the game stores them */
        private void PutEntry(bool replacing)
        {
            if (_track == null) return;
            int at = SelectedEntry;
            if (replacing && at < 0) return;

            if (!Number(timeBox.Text, out float time)) { Refuse("The time is a number of seconds, e.g. 0.5."); return; }
            float length = _clip.Playable ? _clip.Duration : float.MaxValue;
            bool unmoved = replacing && _track.Times[at] == time;
            if (!unmoved && (time < 0 || time > length + 0.0005f))
            {
                Refuse("The time must be within the animation, 0 to " + length.ToString("0.###", CultureInfo.InvariantCulture) + " s.");
                return;
            }
            string fires = firesBox.Text.Trim();
            if (fires.Length != 0 && !uint.TryParse(fires, out _) && !_block.Arguments.Any(o => o.Name == fires))
            {
                Refuse("This block has no setting called '" + fires + "' for the entry to fire. Add it below first.");
                return;
            }

            AnimClipDBSec.MetadataEvent fired = null;
            if (replacing)
            {
                fired = at < _track.Events.Count ? _track.Events[at] : null;
                _track.Times.RemoveAt(at);
                if (at < _track.Events.Count) _track.Events.RemoveAt(at);
            }
            //A track that fires nothing yet starts firing: every entry gets a slot for what it fires
            if (fires.Length != 0 && !_track.HasEvents)
            {
                _track.Events = _track.Times.Select(o => new AnimClipDBSec.MetadataEvent { Name = "", Type = MetadataValueType.PROPERTY_REFERENCE }).ToList();
                _track.HasEvents = true;
            }

            int into = _track.Times.FindIndex(o => o > time);
            if (into < 0) into = _track.Times.Count;
            _track.Times.Insert(into, time);
            if (_track.HasEvents)
            {
                if (fired == null) fired = new AnimClipDBSec.MetadataEvent { Type = MetadataValueType.PROPERTY_REFERENCE };
                fired.Name = fires;
                _track.Events.Insert(Math.Min(into, _track.Events.Count), fired);
            }
            Tidy(_track);

            FillTracks(_track);
            FillEntries(into);
            Changed((replacing ? "Entry changed: " : "Entry added: ") + _track.Name + " at " + time.ToString("0.###", CultureInfo.InvariantCulture) + " s" + (fires.Length == 0 ? "" : ", firing " + fires) + ".");
        }

        private void RemoveEntry()
        {
            int at = SelectedEntry;
            if (_track == null || at < 0) return;
            _track.Times.RemoveAt(at);
            if (at < _track.Events.Count) _track.Events.RemoveAt(at);
            Tidy(_track);

            //A track with no entries left is no track: it goes, as it would when the animation is next read
            if (_track.Times.Count == 0)
            {
                string name = _track.Name;
                _block.Properties.Remove(_track);
                _block.HasProperties = _block.Properties.Count != 0;
                FillTracks(null);
                Changed("The last entry of '" + name + "' removed, and the track with it.");
                return;
            }
            FillTracks(_track);
            FillEntries(Math.Min(at, _track.Times.Count - 1));
            Changed("Entry removed from " + _track.Name + ".");
        }

        /* One event per time; a track whose entries fire nothing at all is stored as one that never does */
        private static void Tidy(AnimClipDBSec.MetadataProperty track)
        {
            if (!track.HasEvents) { track.Events.Clear(); return; }
            while (track.Events.Count < track.Times.Count) track.Events.Add(new AnimClipDBSec.MetadataEvent { Name = "", Type = MetadataValueType.PROPERTY_REFERENCE });
            while (track.Events.Count > track.Times.Count) track.Events.RemoveAt(track.Events.Count - 1);
            if (track.Events.All(o => string.IsNullOrEmpty(o.Name) && o.Type == MetadataValueType.PROPERTY_REFERENCE))
            {
                track.Events.Clear();
                track.HasEvents = false;
            }
        }
        #endregion

        #region SETTINGS
        private void PutSetting(bool replacing)
        {
            if (_block == null) return;
            AnimClipDBSec.MetadataArgument setting = replacing ? SelectedSetting : null;
            if (replacing && (setting == null || Protected(setting))) return;

            string name = settingNameBox.Text.Trim();
            if (name.Length == 0) { Refuse("Type the setting's name first."); return; }
            if (_block.Arguments.Any(o => o != setting && o.Name == name)) { Refuse("This block has a setting called '" + name + "' already."); return; }
            if (name == "anim_label" || name == "meta_label" || name == "0") { Refuse("'" + name + "' is how the game finds an animation's metadata, so it can't be made here."); return; }
            if (!(typeBox.SelectedItem is MetadataValueType type)) { Refuse("Choose the setting's type."); return; }
            if (!Parse(type, valueBox.Text.Trim(), out object value, out string problem)) { Refuse(problem); return; }

            string was = setting?.Name;
            if (setting == null)
            {
                setting = new AnimClipDBSec.MetadataArgument();
                _block.Arguments.Add(setting);
            }
            setting.Name = name;
            setting.Type = type;
            setting.Value = value;
            setting.CanMirror = mirrorCheck.Checked;
            setting.CanModulateByPlayspeed = speedCheck.Checked;

            //Entries firing it by its old name fire it by its new one
            int repointed = 0;
            if (was != null && was != name)
                foreach (AnimClipDBSec.MetadataProperty track in _block.Properties)
                    foreach (AnimClipDBSec.MetadataEvent fired in track.Events)
                        if (fired.Type == MetadataValueType.PROPERTY_REFERENCE && fired.Name == was) { fired.Name = name; repointed++; }

            FillSettings(setting);
            FillEntries(SelectedEntry);
            Changed((replacing ? "Setting '" + name + "' changed" : "Setting '" + name + "' added") + (repointed == 0 ? "." : ", and the " + repointed + " entries firing it follow its new name."));
        }

        private void RemoveSetting()
        {
            AnimClipDBSec.MetadataArgument setting = SelectedSetting;
            if (_block == null || setting == null || Protected(setting)) return;
            List<string> firing = _block.Properties.Where(o => o.Events.Any(e => e.Type == MetadataValueType.PROPERTY_REFERENCE && e.Name == setting.Name)).Select(o => o.Name).ToList();
            if (firing.Count != 0)
            {
                Refuse("'" + setting.Name + "' is fired by entries of " + string.Join(", ", firing) + ": remove those entries, or make them fire something else, first.");
                return;
            }
            _block.Arguments.Remove(setting);
            FillSettings(null);
            FillEntries(SelectedEntry);
            Changed("Setting '" + setting.Name + "' removed.");
        }

        /* A typed value as the type stores it. Numbers are read as 1.5 in any language, or as this one writes them */
        private static bool Parse(MetadataValueType type, string text, out object value, out string problem)
        {
            value = null;
            problem = null;
            switch (type)
            {
                case MetadataValueType.BOOL:
                    string flag = text.ToLowerInvariant();
                    if (flag == "true" || flag == "yes" || flag == "1") { value = true; return true; }
                    if (flag == "false" || flag == "no" || flag == "0") { value = false; return true; }
                    problem = "A BOOL setting is true or false.";
                    return false;
                case MetadataValueType.UINT32:
                    if (uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint whole)) { value = whole; return true; }
                    problem = "A UINT32 setting is a whole number from 0 to " + uint.MaxValue + ".";
                    return false;
                case MetadataValueType.INT32:
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int signed)) { value = signed; return true; }
                    problem = "An INT32 setting is a whole number.";
                    return false;
                case MetadataValueType.FLOAT32:
                    if (Number(text, out float number)) { value = number; return true; }
                    problem = "A FLOAT32 setting is a number, e.g. 1.5.";
                    return false;
                case MetadataValueType.VECTOR:
                    string[] parts = text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 3 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                        && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                        && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                    { value = new System.Numerics.Vector3(x, y, z); return true; }
                    problem = "A VECTOR setting is three numbers: x, y, z (e.g. 0, 1.5, 0).";
                    return false;
                case MetadataValueType.AUDIO:
                    if (CathodeLib.Animation.ParseAudioEvent(text) == null)
                    {
                        problem = "A sound (AUDIO) setting is in the form " + AudioTemplate + ", with the sound event and the bone it plays from filled in.";
                        return false;
                    }
                    value = text;
                    return true;
                default:
                    value = text;
                    return true;
            }
        }

        private static bool Number(string text, out float number)
        {
            text = (text ?? "").Trim();
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
                || float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out number);
        }
        #endregion

        #region SAVING
        /* Into the animation: every stored copy of it takes the copy edited here (CathodeLib's EditMetadata tries it on each
         * first, and changes nothing if any would not read back), and the names it uses go into the string table */
        private bool Save()
        {
            if (_readOnly != null || !_dirty) return false;

            foreach (AnimClipDBSec.MetadataBlock block in new[] { _working.Common }.Concat(_working.Instances))
            {
                AnimClipDBSec.MetadataProperty empty = block.Properties.FirstOrDefault(o => o.Times.Count == 0);
                if (empty != null) { Refuse("Track '" + empty.Name + "' has no entries yet: give it one, or remove it."); return false; }
            }
            string stale = AnimationPakWrite.Stale(_animations);
            if (stale != null)
            {
                MessageBox.Show(stale, "Can't save", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!_animations.EditMetadata(_clip, metadata =>
            {
                Put(_working.Common, metadata.Common);
                for (int i = 0; i < metadata.Instances.Count && i < _working.Instances.Count; i++)
                    Put(_working.Instances[i], metadata.Instances[i]);
            }, out string problem))
            {
                MessageBox.Show("The changes couldn't be made, so nothing was changed: " + problem, "Can't save", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            //Every name is stored as a hash: a new one goes in the debug string table, or it reads back as a number
            foreach (AnimClipDBSec.MetadataBlock block in new[] { _working.Common }.Concat(_working.Instances))
            {
                foreach (AnimClipDBSec.MetadataArgument argument in block.Arguments)
                {
                    Register(argument.Name);
                    if (argument.Type == MetadataValueType.STRING || argument.Type == MetadataValueType.AUDIO || argument.Type == MetadataValueType.PROPERTY_REFERENCE)
                        Register(argument.Value as string);
                }
                foreach (AnimClipDBSec.MetadataProperty track in block.Properties)
                {
                    Register(track.Name);
                    foreach (AnimClipDBSec.MetadataEvent fired in track.Events) Register(fired.Name);
                }
            }

            _dirty = false;
            _closing = true;
            DialogResult = DialogResult.OK;
            Close();
            return true;
        }

        private static void Put(AnimClipDBSec.MetadataBlock from, AnimClipDBSec.MetadataBlock into)
        {
            into.Arguments = from.Arguments.Select(Copy).ToList();
            into.Properties = from.Properties.Select(Copy).ToList();
            into.HasProperties = from.HasProperties;
        }

        /* A name the string tables don't hold yet goes into the debug one. A number stands for a hash no table knows, and stays */
        private void Register(string name)
        {
            if (string.IsNullOrEmpty(name) || uint.TryParse(name, out _)) return;
            uint id = CathodeLib.Utilities.AnimationHashedString(name);
            if ((_animations.Strings?.Entries.ContainsKey(id) ?? false) || (_animations.StringsDebug?.Entries.ContainsKey(id) ?? false)) return;
            _animations.AddName(name, true);
        }

        private void EditAnimationMetadata_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_closing || !_dirty || e.CloseReason != CloseReason.UserClosing) return;
            DialogResult answer = MessageBox.Show("There are changes to the metadata of '" + ClipName + "' that haven't been saved.\n\nSave them before closing?",
                "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (answer == DialogResult.Cancel) { e.Cancel = true; return; }
            if (answer == DialogResult.Yes && !Save()) e.Cancel = true;
        }
        #endregion

        #region COPIES
        private static AnimClipDBSec.MetadataSet Copy(AnimClipDBSec.MetadataSet set)
        {
            AnimClipDBSec.MetadataSet copy = new AnimClipDBSec.MetadataSet { Common = Copy(set?.Common ?? new AnimClipDBSec.MetadataBlock()) };
            if (set != null) copy.Instances.AddRange(set.Instances.Select(Copy));
            return copy;
        }

        private static AnimClipDBSec.MetadataBlock Copy(AnimClipDBSec.MetadataBlock block)
        {
            return new AnimClipDBSec.MetadataBlock
            {
                Arguments = block.Arguments.Select(Copy).ToList(),
                Properties = block.Properties.Select(Copy).ToList(),
                HasProperties = block.HasProperties,
            };
        }

        private static AnimClipDBSec.MetadataArgument Copy(AnimClipDBSec.MetadataArgument argument)
        {
            return new AnimClipDBSec.MetadataArgument
            {
                Name = argument.Name,
                Type = argument.Type,
                Value = argument.Value,
                RequiresConvert = argument.RequiresConvert,
                CanMirror = argument.CanMirror,
                CanModulateByPlayspeed = argument.CanModulateByPlayspeed,
            };
        }

        private static AnimClipDBSec.MetadataProperty Copy(AnimClipDBSec.MetadataProperty track)
        {
            return new AnimClipDBSec.MetadataProperty
            {
                Name = track.Name,
                Times = new List<float>(track.Times),
                Events = track.Events.Select(o => new AnimClipDBSec.MetadataEvent { Name = o.Name, Type = o.Type }).ToList(),
                HasEvents = track.HasEvents,
            };
        }

        /* Whether two copies of an animation's metadata say the same thing (what the game reads; not where it lies) */
        private static bool Same(AnimClipDBSec.MetadataSet a, AnimClipDBSec.MetadataSet b)
        {
            if (!Same(a.Common, b.Common) || a.Instances.Count != b.Instances.Count) return false;
            for (int i = 0; i < a.Instances.Count; i++)
                if (!Same(a.Instances[i], b.Instances[i])) return false;
            return true;
        }

        private static bool Same(AnimClipDBSec.MetadataBlock a, AnimClipDBSec.MetadataBlock b)
        {
            if (a.Arguments.Count != b.Arguments.Count || a.Properties.Count != b.Properties.Count || a.HasProperties != b.HasProperties) return false;
            for (int i = 0; i < a.Arguments.Count; i++)
            {
                AnimClipDBSec.MetadataArgument x = a.Arguments[i], y = b.Arguments[i];
                if (x.Name != y.Name || x.Type != y.Type || !Equals(x.Value, y.Value) || x.RequiresConvert != y.RequiresConvert
                    || x.CanMirror != y.CanMirror || x.CanModulateByPlayspeed != y.CanModulateByPlayspeed) return false;
            }
            for (int i = 0; i < a.Properties.Count; i++)
            {
                AnimClipDBSec.MetadataProperty x = a.Properties[i], y = b.Properties[i];
                if (x.Name != y.Name || x.HasEvents != y.HasEvents || !x.Times.SequenceEqual(y.Times) || x.Events.Count != y.Events.Count) return false;
                for (int k = 0; k < x.Events.Count; k++)
                    if (x.Events[k].Name != y.Events[k].Name || x.Events[k].Type != y.Events[k].Type) return false;
            }
            return true;
        }
        #endregion
    }
}
