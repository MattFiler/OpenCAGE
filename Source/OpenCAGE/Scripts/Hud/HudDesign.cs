using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenCAGE.Hud
{
    /// <summary>What a slot of a HUD overlay shows.</summary>
    public enum HudSlotKind
    {
        /// <summary>Fixed text; with several lines, the value picks which one shows (0 = the first).</summary>
        Text,
        /// <summary>A number: prefix + value + suffix.</summary>
        Number,
        /// <summary>A time: the value in seconds as m:ss (or m:ss.t).</summary>
        Timer,
        /// <summary>A bar filled by value / max.</summary>
        Bar,
        /// <summary>A crosshair at a point.</summary>
        Reticle,
        /// <summary>A plain panel (a backing for other slots).</summary>
        Box,
        /// <summary>A picture (a PNG kept in the design), stretched to the slot's box.</summary>
        Image,
    }

    /// <summary>Where a slot's value comes from in the level's script.</summary>
    public enum HudSource
    {
        /// <summary>An input pin on the HUD's composite instance: link any number to it.</summary>
        Pin,
        /// <summary>The player's health (CHR_GetHealth on the player, every tenth of a second).</summary>
        PlayerHealth,
        /// <summary>Seconds counted up from the level start (paused with the game).</summary>
        CountUp,
        /// <summary>No value: the slot shows as designed.</summary>
        None,
    }

    /// <summary>A text property picked from a fixed list in the property grid.</summary>
    public abstract class HudChoices : StringConverter
    {
        protected abstract string[] Choices { get; }
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => true;
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context) => new StandardValuesCollection(Choices);
    }
    public sealed class HudFontChoices : HudChoices { protected override string[] Choices => HudDesign.Fonts; }
    public sealed class HudAlignChoices : HudChoices { protected override string[] Choices => HudDesign.Aligns; }
    public sealed class HudDirectionChoices : HudChoices { protected override string[] Choices => HudDesign.Directions; }
    public sealed class HudStyleChoices : HudChoices { protected override string[] Choices => HudDesign.Styles; }

    /// <summary>One element of a HUD overlay. Positions and sizes are in the overlay's 1280x720 design space (scaled to the screen).</summary>
    public sealed class HudSlot
    {
        private const string MultilineEditor = "System.ComponentModel.Design.MultilineStringEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
        private const string TypeEditor = "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
        private const string ColourEditor = "OpenCAGE.Hud.HudColourEditor, OpenCAGE";
        private const string FileEditor = "OpenCAGE.Hud.HudPngFileEditor, OpenCAGE";
        public const int MaxImageBytes = 2 * 1024 * 1024;

        [Category("1 Slot"), Description("Names the slot and its pins on the HUD's composite (letters, digits, spaces, _).")]
        public string Name { get; set; } = "Slot";
        [Category("1 Slot"), JsonConverter(typeof(StringEnumConverter)), Description("Text (lines to pick from), Number, Timer (seconds as m:ss), Bar, Reticle or Box.")]
        public HudSlotKind Kind { get; set; } = HudSlotKind.Text;
        [Category("1 Slot"), Description("Shown when the overlay starts.")]
        public bool Visible { get; set; } = true;
        [Category("1 Slot"), DisplayName("Show/hide pins"), Description("The HUD's composite gets 'Show <slot>' and 'Hide <slot>' pins for it.")]
        public bool ShowHidePins { get; set; }
        [Category("1 Slot"), JsonConverter(typeof(StringEnumConverter)), Description("Where its value comes from: Pin (link a number to the instance's pin), PlayerHealth (bars), CountUp (seconds since the level started), None.")]
        public HudSource Source { get; set; } = HudSource.Pin;
        [Category("1 Slot"), Description("The value shown before the script sends one (a text slot: which line, from 0).")]
        public double Initial { get; set; }

        [Category("2 Place"), Description("Left edge, in the 1280x720 design space.")]
        public double X { get; set; } = 40;
        [Category("2 Place"), Description("Top edge, in the 1280x720 design space.")]
        public double Y { get; set; } = 40;
        [Category("2 Place")]
        public double Width { get; set; } = 300;
        [Category("2 Place")]
        public double Height { get; set; } = 40;
        [Category("2 Place"), Description("#RRGGBB: the text, fill or line colour."), Editor(ColourEditor, TypeEditor)]
        public string Colour { get; set; } = "#E8E0C8";
        [Category("2 Place"), Description("0 (invisible) to 1.")]
        public double Alpha { get; set; } = 1;

        [Category("3 Text"), TypeConverter(typeof(HudFontChoices)), Description("One of the game's fonts.")]
        public string Font { get; set; } = "$JixellationBold";
        [Category("3 Text"), Description("Text size in design pixels.")]
        public double Size { get; set; } = 24;
        [Category("3 Text"), TypeConverter(typeof(HudAlignChoices)), Description("left, center or right, within the slot's width.")]
        public string Align { get; set; } = "left";
        [Category("3 Text"), Description("A dark outline behind the text, so it reads over anything.")]
        public bool Shadow { get; set; } = true;
        [Browsable(false)]
        public List<string> Lines { get; set; } = new List<string>();
        [JsonIgnore, Category("3 Text"), DisplayName("Lines"), Description("Text: one line per row; the slot's value picks the line shown (0 = the first)."), Editor(MultilineEditor, TypeEditor)]
        public string LinesText
        {
            get => string.Join("\r\n", Lines);
            set => Lines = (value ?? "").Replace("\r\n", "\n").Split('\n').Where(o => o.Length != 0).ToList();
        }
        [Category("3 Text"), Description("Number / Timer: text before the value.")]
        public string Prefix { get; set; } = "";
        [Category("3 Text"), Description("Number / Timer: text after the value.")]
        public string Suffix { get; set; } = "";
        [Category("3 Text"), Description("Number: digits after the point (0-3).")]
        public int Decimals { get; set; }
        [Category("3 Text"), Description("Timer: show tenths of a second.")]
        public bool Tenths { get; set; }

        [Category("4 Bar"), Description("The value that fills the bar; 0 = the largest value seen so far (health starts full).")]
        public double Max { get; set; } = 100;
        [Category("4 Bar"), TypeConverter(typeof(HudDirectionChoices)), Description("Which way the bar fills: right, left, up or down.")]
        public string Direction { get; set; } = "right";
        [Category("4 Bar"), DisplayName("Back colour"), Editor(ColourEditor, TypeEditor)]
        public string BackColour { get; set; } = "#000000";
        [Category("4 Bar"), DisplayName("Back alpha")]
        public double BackAlpha { get; set; } = 0.5;
        [Category("4 Bar"), DisplayName("Low colour"), Description("The fill's colour at or below 'Low at' (empty: no change)."), Editor(ColourEditor, TypeEditor)]
        public string LowColour { get; set; } = "#D03020";
        [Category("4 Bar"), DisplayName("Low at"), Description("A fraction of the bar, 0 to 1.")]
        public double LowAt { get; set; } = 0.25;
        [Category("4 Bar"), DisplayName("Border colour"), Description("An outline for a bar or a box (empty: none)."), Editor(ColourEditor, TypeEditor)]
        public string BorderColour { get; set; } = "";
        [Category("4 Bar"), DisplayName("Border width")]
        public double BorderWidth { get; set; } = 2;

        [Category("5 Reticle"), TypeConverter(typeof(HudStyleChoices)), Description("cross, circle, dot or brackets, filling the slot's box.")]
        public string Style { get; set; } = "cross";
        [Category("5 Reticle"), Description("Line thickness.")]
        public double Thickness { get; set; } = 2;
        [Category("5 Reticle"), Description("Cross: the gap at its centre.")]
        public double Gap { get; set; } = 6;

        /// <summary>Image: the PNG's bytes (base64), kept in the design and so in the movie.</summary>
        [Browsable(false)]
        public string ImagePng { get; set; } = "";
        [Category("6 Image"), DisplayName("Image"), ReadOnly(true), Description("The picture's file name when it was picked.")]
        public string ImageName { get; set; } = "";
        /// <summary>Image: a PNG file to read into <see cref="ImagePng"/> (read when set; never written out).</summary>
        [Category("6 Image"), DisplayName("Image file"), Description("A PNG (up to 1024x1024, 2 MB) to show, stretched to the slot's box; its pixels are kept in the design and the movie."), Editor(FileEditor, TypeEditor)]
        public string ImageFile
        {
            get => "";
            set
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                byte[] bytes = System.IO.File.ReadAllBytes(value);
                if (bytes.Length > MaxImageBytes) throw new ArgumentException(System.IO.Path.GetFileName(value) + " is over 2 MB.");
                HudPng.Decode(bytes, out int _, out int _);
                ImagePng = Convert.ToBase64String(bytes);
                ImageName = System.IO.Path.GetFileName(value);
            }
        }
        public bool ShouldSerializeImageFile() => false;

        /// <summary>Whether the slot shows a value the script sends (not a reticle or a box, nor a one-line text).</summary>
        [JsonIgnore, Browsable(false)]
        public bool TakesValue => Source != HudSource.None && Kind != HudSlotKind.Reticle && Kind != HudSlotKind.Box && Kind != HudSlotKind.Image && !(Kind == HudSlotKind.Text && Lines.Count < 2);

        public HudSlot Clone() => JsonConvert.DeserializeObject<HudSlot>(JsonConvert.SerializeObject(this), new JsonSerializerSettings() { ObjectCreationHandling = ObjectCreationHandling.Replace });

        public override string ToString() => Name + " (" + Kind + ")";
    }

    /// <summary>
    /// A HUD overlay as the HUD Overlay Builder edits it: slots, plus what the level's script should do around them. Built into a
    /// movie by <see cref="HudMovieBuilder"/>, which keeps this design inside the movie (a Metadata tag) so it can be opened again.
    /// </summary>
    public sealed class HudDesign
    {
        /// <summary>Starts the Metadata tag that holds the design in an overlay movie.</summary>
        public const string MetadataPrefix = "OPENCAGE_HUD_OVERLAY:";
        public const double StageWidth = 1280, StageHeight = 720;
        public const int MaxSlots = 40, MaxNameLength = 24;

        [Browsable(false)]
        public int Version { get; set; } = 1;
        [Category("Overlay"), Description("Names the movie (UI/OVERLAYS/<NAME>.GFX in the level's folder) and its composite (HUD\\<Name>): letters, digits and _.")]
        public string Name { get; set; } = "Hud";
        [Category("Overlay"), DisplayName("Show at start"), Description("Shown as the level starts; else the HUD composite's 'show' pin brings it up.")]
        public bool ShowAtStart { get; set; } = true;
        [Category("Game HUD"), DisplayName("Hide ammo counter"), Description("Take the game's ammo counter off the weapon HUD (re-applied every half second: the game re-shows it after every shot).")]
        public bool HideAmmoCounter { get; set; }
        [Category("Game HUD"), DisplayName("Hide health bar"), Description("Take the game's health bar off the weapon HUD (the torch battery meter is part of it).")]
        public bool HideHealthBar { get; set; }
        [Browsable(false)]
        public List<HudSlot> Slots { get; set; } = new List<HudSlot>();

        public static readonly string[] Fonts = { "$JixellationBold", "$JixellationReg", "$Isolation", "$JectRexBold", "$NostromoBoldCond" };
        public static readonly string[] Aligns = { "left", "center", "right" };
        public static readonly string[] Directions = { "right", "left", "up", "down" };
        public static readonly string[] Styles = { "cross", "circle", "dot", "brackets" };

        [JsonIgnore, Browsable(false)] public string FileName => Name.ToUpperInvariant() + ".GFX";
        /// <summary>
        /// The layer the level's FlashScript loads the overlay on: always the game's ChallengeMapOverlay layer, the only one whose
        /// FlashScript movie the game loads straight away (a layer of any other name is created but its movie never loads). It is hidden
        /// while paused, and a level has one, so a challenge map's own overlay and a HUD overlay can't share it. The overlay answers the
        /// calls the game makes on that layer's movie.
        /// </summary>
        [JsonIgnore, Browsable(false)] public string LayerName => HudMovieCheck.ChallengeMapLayer;

        public string ToJson(bool indented = true) => JsonConvert.SerializeObject(this, indented ? Formatting.Indented : Formatting.None);

        public static HudDesign FromJson(string json)
        {
            HudDesign design = JsonConvert.DeserializeObject<HudDesign>(json, new JsonSerializerSettings() { ObjectCreationHandling = ObjectCreationHandling.Replace });
            if (design == null) throw new FormatException("no design");
            if (design.Slots == null) design.Slots = new List<HudSlot>();
            design.Slots.RemoveAll(o => o == null);
            foreach (HudSlot slot in design.Slots)
                if (slot.Lines == null) slot.Lines = new List<string>();
            return design;
        }

        public HudDesign Clone() => FromJson(ToJson(false));

        /// <summary>A new slot's name: "Timer", "Timer 2"...</summary>
        public string FreeName(string stem)
        {
            if (!Slots.Any(o => string.Equals(o.Name, stem, StringComparison.OrdinalIgnoreCase))) return stem;
            for (int i = 2; ; i++)
                if (!Slots.Any(o => string.Equals(o.Name, stem + " " + i, StringComparison.OrdinalIgnoreCase))) return stem + " " + i;
        }

        /// <summary>A new slot of a kind, with sensible settings, placed so it shows.</summary>
        public HudSlot NewSlot(HudSlotKind kind)
        {
            HudSlot slot = new HudSlot() { Kind = kind, Name = FreeName(kind.ToString()), X = 80 + 20 * (Slots.Count % 10), Y = 120 + 30 * (Slots.Count % 10) };
            switch (kind)
            {
                case HudSlotKind.Text: slot.Lines = new List<string>() { "OBJECTIVE" }; slot.Source = HudSource.None; break;
                case HudSlotKind.Number: slot.Prefix = "COUNT "; break;
                case HudSlotKind.Timer: slot.Width = 200; slot.Height = 44; slot.Size = 32; slot.Align = "center"; slot.Source = HudSource.CountUp; break;
                case HudSlotKind.Bar: slot.Width = 300; slot.Height = 16; slot.Colour = "#7CC8E8"; slot.Max = 100; slot.Initial = 100; break;
                case HudSlotKind.Reticle: slot.X = 620; slot.Y = 340; slot.Width = 40; slot.Height = 40; slot.Source = HudSource.None; break;
                case HudSlotKind.Box: slot.Width = 360; slot.Height = 60; slot.Colour = "#000000"; slot.Alpha = 0.45; slot.Source = HudSource.None; break;
                case HudSlotKind.Image: slot.Width = 128; slot.Height = 128; slot.Source = HudSource.None; break;
            }
            return slot;
        }

        private static readonly Regex _nameRule = new Regex("^[A-Za-z][A-Za-z0-9_]*$");
        private static readonly Regex _slotRule = new Regex("^[A-Za-z0-9][A-Za-z0-9 _]*$");
        private static readonly Regex _colourRule = new Regex("^#?[0-9A-Fa-f]{6}$");

        /// <summary>What is wrong with the design (empty when it can be built).</summary>
        public List<string> Problems()
        {
            List<string> problems = new List<string>();
            if (string.IsNullOrWhiteSpace(Name) || !_nameRule.IsMatch(Name) || Name.Length > MaxNameLength)
                problems.Add("The overlay's name must be 1-" + MaxNameLength + " letters, digits or _, starting with a letter (it names its movie, layer and composite).");
            if (Slots.Count == 0)
                problems.Add("The overlay has no slots.");
            if (Slots.Count > MaxSlots)
                problems.Add("The overlay has " + Slots.Count + " slots; " + MaxSlots + " at most.");
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "show", "hide" };
            foreach (HudSlot slot in Slots)
            {
                string s = "Slot '" + slot.Name + "'";
                if (string.IsNullOrWhiteSpace(slot.Name) || !_slotRule.IsMatch(slot.Name) || slot.Name.Length > 32 || slot.Name != slot.Name.Trim())
                    problems.Add(s + ": a name is 1-32 letters, digits, spaces or _ (it names the slot's pins).");
                else if (!names.Add(slot.Name))
                    problems.Add(s + ": two slots have this name (or it is 'show' / 'hide', the overlay's own pins).");
                if (new[] { slot.X, slot.Y, slot.Width, slot.Height, slot.Alpha, slot.Size, slot.Max, slot.LowAt, slot.BackAlpha, slot.BorderWidth, slot.Thickness, slot.Gap, slot.Initial }.Any(o => double.IsNaN(o) || double.IsInfinity(o)))
                    problems.Add(s + ": a number is not a number.");
                if (slot.X < -StageWidth || slot.X > StageWidth * 2 || slot.Y < -StageHeight || slot.Y > StageHeight * 2)
                    problems.Add(s + ": its position is far off the screen (the design space is " + StageWidth + "x" + StageHeight + ").");
                if (slot.Width <= 0 || slot.Height <= 0 || slot.Width > StageWidth * 2 || slot.Height > StageHeight * 2)
                    problems.Add(s + ": its width and height must be above 0 and at most twice the screen.");
                if (slot.Alpha < 0 || slot.Alpha > 1 || slot.BackAlpha < 0 || slot.BackAlpha > 1)
                    problems.Add(s + ": alpha is from 0 to 1.");
                foreach (string colour in new[] { slot.Colour, slot.BackColour })
                    if (!_colourRule.IsMatch(colour ?? "")) problems.Add(s + ": '" + colour + "' is not a colour (#RRGGBB).");
                foreach (string colour in new[] { slot.LowColour, slot.BorderColour })
                    if (!string.IsNullOrEmpty(colour) && !_colourRule.IsMatch(colour)) problems.Add(s + ": '" + colour + "' is not a colour (#RRGGBB, or empty for none).");
                if (!Fonts.Contains(slot.Font)) problems.Add(s + ": the font must be one the game has (" + string.Join(", ", Fonts) + ").");
                if (slot.Size < 4 || slot.Size > 200) problems.Add(s + ": text size is 4 to 200.");
                if (!Aligns.Contains(slot.Align)) problems.Add(s + ": align is left, center or right.");
                if (!Directions.Contains(slot.Direction)) problems.Add(s + ": a bar fills right, left, up or down.");
                if (!Styles.Contains(slot.Style)) problems.Add(s + ": a reticle is cross, circle, dot or brackets.");
                if (slot.Decimals < 0 || slot.Decimals > 3) problems.Add(s + ": 0 to 3 decimals.");
                if (slot.Thickness <= 0 || slot.Thickness > 50 || slot.BorderWidth < 0 || slot.BorderWidth > 50 || slot.Gap < 0) problems.Add(s + ": line widths are above 0 and at most 50.");
                if (slot.Lines.Count > 32) problems.Add(s + ": 32 lines at most.");
                if (slot.Lines.Concat(new[] { slot.Prefix ?? "", slot.Suffix ?? "" }).Any(o => o == null || o.Length > 200 || o.Any(c => c < 0x20)))
                    problems.Add(s + ": texts are one line of at most 200 characters.");
                if (slot.Kind == HudSlotKind.Text && slot.Lines.Count == 0)
                    problems.Add(s + ": a text slot needs a line of text.");
                if (slot.Max < 0) problems.Add(s + ": a bar's max is 0 (the largest value seen) or more.");
                if (slot.LowAt < 0 || slot.LowAt > 1) problems.Add(s + ": the low mark is a fraction of the bar, 0 to 1.");
                if (slot.Source == HudSource.PlayerHealth && slot.Kind != HudSlotKind.Bar)
                    problems.Add(s + ": the player's health goes to a bar (its max 0: the full health it starts with).");
                if (slot.Kind == HudSlotKind.Image)
                {
                    try
                    {
                        byte[] png = Convert.FromBase64String(slot.ImagePng ?? "");
                        if (png.Length == 0) problems.Add(s + ": pick a PNG for the image (Image file).");
                        else if (png.Length > HudSlot.MaxImageBytes) problems.Add(s + ": its PNG is over 2 MB.");
                        else HudPng.Decode(png, out int _, out int _);
                    }
                    catch (Exception e) when (e is FormatException || e is System.IO.InvalidDataException)
                    {
                        problems.Add(s + ": its image can't be read (" + e.Message + ").");
                    }
                }
                if (slot.Source == HudSource.CountUp && slot.Kind != HudSlotKind.Timer && slot.Kind != HudSlotKind.Number)
                    problems.Add(s + ": counting up goes to a timer or a number.");
            }
            return problems;
        }

        /// <summary>An example to start from: an objective with two lines, a timer, a health bar and a counter.</summary>
        public static HudDesign Example(string name = "Hud")
        {
            HudDesign design = new HudDesign() { Name = name };
            design.Slots.Add(new HudSlot() { Name = "Objective", Kind = HudSlotKind.Text, X = 40, Y = 36, Width = 600, Height = 36, Size = 24, Lines = new List<string>() { "REACH THE TRANSIT STATION", "SURVIVE" }, ShowHidePins = true });
            design.Slots.Add(new HudSlot() { Name = "Time", Kind = HudSlotKind.Timer, X = 540, Y = 30, Width = 200, Height = 44, Size = 34, Align = "center", Source = HudSource.CountUp });
            design.Slots.Add(new HudSlot() { Name = "Health", Kind = HudSlotKind.Bar, X = 900, Y = 44, Width = 320, Height = 18, Colour = "#7CC8E8", Max = 0, Source = HudSource.PlayerHealth, BorderColour = "#E8E0C8", BorderWidth = 1 });
            design.Slots.Add(new HudSlot() { Name = "Kills", Kind = HudSlotKind.Number, X = 900, Y = 70, Width = 320, Height = 30, Size = 20, Align = "right", Prefix = "KILLS ", Suffix = " / 10" });
            return design;
        }

        /// <summary>A colour as 0xRRGGBB.</summary>
        public static int Rgb(string colour)
        {
            string hex = (colour ?? "").Trim().TrimStart('#');
            return int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value) ? value & 0xFFFFFF : 0xFFFFFF;
        }
    }
}
