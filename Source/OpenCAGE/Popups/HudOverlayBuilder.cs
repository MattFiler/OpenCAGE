using CATHODE;
using CATHODE.Scripting;
using OpenCAGE.Hud;
using OpenCAGE.MCP;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Design;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace OpenCAGE
{
    /// <summary>
    /// The HUD Overlay Builder: a level's own HUD elements laid out on the overlay's 1280x720 design space. Save to Level checks the
    /// movie, writes it to the level's UI/OVERLAYS/&lt;NAME&gt;.GFX and makes the composite HUD\&lt;Name&gt; that loads and drives it
    /// (HudScript).
    /// </summary>
    public class HudOverlayBuilder : BaseWindow
    {
        private readonly string _level;
        private HudDesign _design = HudDesign.Example();
        private bool _dirty;

        private readonly ComboBox overlays = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        private readonly ListBox slots = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        private readonly PropertyGrid grid = new PropertyGrid { Dock = DockStyle.Fill, ToolbarVisible = false, PropertySort = PropertySort.Categorized };
        private readonly PreviewPanel preview = new PreviewPanel { Dock = DockStyle.Fill };
        private readonly TextBox status = new TextBox { Dock = DockStyle.Bottom, Height = 96, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        private readonly ToolTip tips = new ToolTip() { AutoPopDelay = 20000 };

        /// <summary>Show the builder for a level (one window per level: an open one comes to the front).</summary>
        public static void ShowFor(IWin32Window owner, string level)
        {
            if (string.IsNullOrWhiteSpace(level))
            {
                MessageBox.Show("Open a level to give it a HUD overlay.", "HUD Overlay Builder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            HudOverlayBuilder open = Application.OpenForms.OfType<HudOverlayBuilder>().FirstOrDefault(o => string.Equals(o._level, level, StringComparison.OrdinalIgnoreCase));
            if (open != null)
            {
                open.Activate();
                return;
            }
            HudOverlayBuilder builder = new HudOverlayBuilder(level);
            if (owner is Form form && form.TopMost)
                builder.TopMost = true;
            builder.Show(owner);
        }

        public HudOverlayBuilder(string level)
        {
            _level = level.Replace('\\', '/');
            Text = "HUD Overlay Builder - " + _level;
            Icon = SharedFormIcon.Icon;
            ClientSize = new Size(1320, 780);
            MinimumSize = new Size(980, 560);
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            MinimizeBox = false;
            KeyPreview = true;
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/hud-overlay-builder/");

            //Top: which overlay, and starting points
            FlowLayoutPanel top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(4, 4, 4, 0), WrapContents = false };
            Button newBtn = Btn("New", 70, "Start a new overlay from the example (objective, timer, health bar, counter).", () => { if (Discard()) LoadDesign(HudDesign.Example(FreeOverlayName())); });
            Button importBtn = Btn("Import Movie...", 110, "Use a movie made elsewhere (.gfx / .swf) as one of the level's overlays: it is checked first (a movie the game can't make its root object from would crash it) and refused if it fails.", ImportMovie);
            Button folderBtn = Btn("Open Folder", 95, "Open the level's overlay folder (DATA\\ENV\\<level>\\UI\\OVERLAYS).", OpenFolder);
            overlays.SelectedIndexChanged += (s, e) => OpenSelected();
            tips.SetToolTip(overlays, "The level's overlays (each a movie in its folder). Pick one to edit it.");
            top.Controls.AddRange(new Control[] { new Label { Text = "Overlay:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, overlays, newBtn, importBtn, folderBtn });

            //Left: the slots
            Panel left = new Panel { Dock = DockStyle.Left, Width = 230, Padding = new Padding(4) };
            FlowLayoutPanel slotButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 64 };
            Button addBtn = Btn("Add", 66, "Add a slot: a text, number, timer, bar, reticle or panel.", null);
            ContextMenuStrip kinds = new ContextMenuStrip();
            foreach (HudSlotKind kind in Enum.GetValues(typeof(HudSlotKind)))
            {
                HudSlotKind k = kind;
                kinds.Items.Add(kind.ToString(), null, (s, e) => AddSlot(k));
            }
            addBtn.Click += (s, e) => kinds.Show(addBtn, new Point(0, addBtn.Height));
            slotButtons.Controls.AddRange(new Control[] { addBtn, Btn("Copy", 66, "Duplicate the selected slot.", DuplicateSlot), Btn("Remove", 66, "Remove the selected slot.", RemoveSlot),
                Btn("Up", 66, "Earlier in the list: drawn under the slots after it.", () => MoveSlot(-1)), Btn("Down", 66, "Later in the list: drawn over the slots before it.", () => MoveSlot(1)) });
            slots.SelectedIndexChanged += (s, e) => SelectionChanged();
            left.Controls.Add(slots);
            left.Controls.Add(slotButtons);

            //Right: properties
            Panel right = new Panel { Dock = DockStyle.Right, Width = 340, Padding = new Padding(4) };
            right.Controls.Add(grid);
            grid.PropertyValueChanged += (s, e) => { Changed(); if (e.ChangedItem?.PropertyDescriptor?.Name == nameof(HudSlot.ImageFile)) grid.Refresh(); };

            //Bottom: the check's result and the actions
            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(4) };
            Button closeBtn = Btn("Close", 90, null, Close);
            buttons.Controls.AddRange(new Control[] {
                closeBtn,
                Btn("Save to Level", 120, "Check the overlay's movie, write it into the level's folder and make (or remake) the composite HUD\\<Name> that loads and drives it, as one undo step. Save the level afterwards.", SaveToLevel),
                Btn("Check", 90, "Build the movie and check it the way the game will use it (nothing is written).", CheckOnly),
                Btn("Export .gfx...", 110, "Save the checked movie somewhere else.", ExportMovie),
                Btn("Remove from Level", 140, "Take this overlay's movie and its composite's contents out of the level.", RemoveFromLevel),
            });

            preview.Builder = this;
            Controls.Add(preview);
            Controls.Add(right);
            Controls.Add(left);
            Controls.Add(status);
            Controls.Add(buttons);
            Controls.Add(top);
            CancelButton = closeBtn;

            KeyDown += OnKey;
            Shown += (s, e) => { FillOverlays(null); };
            FormClosing += (s, e) => { if (!Discard()) e.Cancel = true; };
            FormClosed += (s, e) => tips.Dispose();
        }

        private Button Btn(string text, int width, string tip, Action click)
        {
            Button b = new Button { Text = text, Width = width, Height = 26 };
            if (tip != null) tips.SetToolTip(b, tip);
            if (click != null) b.Click += (s, e) => click();
            return b;
        }

        #region The design
        internal HudDesign Design => _design;
        internal HudSlot Selected => slots.SelectedIndex > 0 && slots.SelectedIndex - 1 < _design.Slots.Count ? _design.Slots[slots.SelectedIndex - 1] : null;

        private string FreeOverlayName()
        {
            List<LevelOverlay> have = HudOverlays.Of(Singleton.PathToAI, _level);
            if (!have.Any(o => o.Name.Equals("HUD", StringComparison.OrdinalIgnoreCase))) return "Hud";
            for (int i = 2; ; i++)
                if (!have.Any(o => o.Name.Equals("HUD" + i, StringComparison.OrdinalIgnoreCase))) return "Hud" + i;
        }

        private void LoadDesign(HudDesign design)
        {
            _design = design;
            _dirty = false;
            FillSlots(0);
            string text = "Editing '" + design.Name + "' (" + design.Slots.Count + " slots). Drag slots on the preview (the bottom-right corner resizes; arrow keys nudge, Shift for 10).";
            //A level copied or renamed keeps FlashScripts naming the old level's folder: saving the overlay again puts it right
            if (string.Equals(EditorLevel, _level, StringComparison.OrdinalIgnoreCase))
                foreach (OverlayLoader loader in HudOverlays.Loaders(Commands, Singleton.PathToAI, _level).Where(o => !o.PathRight))
                    text += "\nNote: " + loader.Composite.name + "\\" + loader.EntityName + " loads " + loader.Filename + " - another level's folder (was the level copied?). Save to Level points it at " + loader.Expected + ".";
            Say(text);
        }

        private void FillSlots(int select)
        {
            slots.BeginUpdate();
            slots.Items.Clear();
            slots.Items.Add("[Overlay: " + _design.Name + "]");
            foreach (HudSlot slot in _design.Slots)
                slots.Items.Add(slot.ToString() + (slot.Visible ? "" : " (hidden)"));
            slots.EndUpdate();
            slots.SelectedIndex = Math.Max(0, Math.Min(select, slots.Items.Count - 1));
            preview.Invalidate();
        }

        private void SelectionChanged()
        {
            grid.SelectedObject = slots.SelectedIndex <= 0 ? (object)_design : Selected;
            preview.Invalidate();
        }

        internal void Changed()
        {
            _dirty = true;
            int at = slots.SelectedIndex;
            if (at >= 0 && at < slots.Items.Count)
            {
                string text = at == 0 ? "[Overlay: " + _design.Name + "]" : Selected.ToString() + (Selected.Visible ? "" : " (hidden)");
                if ((string)slots.Items[at] != text) slots.Items[at] = text;
            }
            preview.Invalidate();
        }

        internal void SelectSlot(HudSlot slot)
        {
            int index = slot == null ? 0 : _design.Slots.IndexOf(slot) + 1;
            if (slots.SelectedIndex != index) slots.SelectedIndex = index;
        }

        internal void RefreshGrid() => grid.Refresh();

        private void AddSlot(HudSlotKind kind)
        {
            HudSlot slot = _design.NewSlot(kind);
            _design.Slots.Add(slot);
            _dirty = true;
            FillSlots(_design.Slots.Count);
        }

        private void DuplicateSlot()
        {
            if (Selected == null) return;
            HudSlot copy = Selected.Clone();
            copy.Name = _design.FreeName(Selected.Name);
            copy.X += 20; copy.Y += 20;
            _design.Slots.Insert(_design.Slots.IndexOf(Selected) + 1, copy);
            _dirty = true;
            FillSlots(_design.Slots.IndexOf(copy) + 1);
        }

        private void RemoveSlot()
        {
            if (Selected == null) return;
            int at = _design.Slots.IndexOf(Selected);
            _design.Slots.RemoveAt(at);
            _dirty = true;
            FillSlots(Math.Min(at + 1, _design.Slots.Count));
        }

        private void MoveSlot(int by)
        {
            if (Selected == null) return;
            int at = _design.Slots.IndexOf(Selected), to = at + by;
            if (to < 0 || to >= _design.Slots.Count) return;
            HudSlot slot = _design.Slots[at];
            _design.Slots.RemoveAt(at);
            _design.Slots.Insert(to, slot);
            _dirty = true;
            FillSlots(to + 1);
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            if (Selected == null || !preview.Focused) return;
            double step = e.Shift ? 10 : 1;
            switch (e.KeyCode)
            {
                case Keys.Left: Selected.X -= step; break;
                case Keys.Right: Selected.X += step; break;
                case Keys.Up: Selected.Y -= step; break;
                case Keys.Down: Selected.Y += step; break;
                case Keys.Delete: RemoveSlot(); e.Handled = true; return;
                default: return;
            }
            e.Handled = true;
            grid.Refresh();
            Changed();
        }

        private bool Discard()
        {
            if (!_dirty) return true;
            return MessageBox.Show("The overlay '" + _design.Name + "' has changes not saved to the level. Discard them?", "HUD Overlay Builder", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        private void Say(string text) => status.Text = text.Replace("\n", "\r\n");
        #endregion

        #region The level's overlays
        private void FillOverlays(string select)
        {
            List<LevelOverlay> have = HudOverlays.Of(Singleton.PathToAI, _level);
            overlays.Tag = "filling";
            overlays.Items.Clear();
            overlays.Items.Add("(not saved yet)");
            foreach (LevelOverlay overlay in have)
                overlays.Items.Add(overlay.Name + (overlay.Design == null ? " (made elsewhere)" : ""));
            int at = select == null ? (have.Count != 0 ? 1 : 0) : 1 + have.FindIndex(o => o.Name.Equals(select, StringComparison.OrdinalIgnoreCase));
            overlays.SelectedIndex = Math.Max(0, at);
            overlays.Tag = null;
            if (select == null)
                OpenSelected(force: true);
        }

        private void OpenSelected(bool force = false)
        {
            if (overlays.Tag != null && !force) return;
            List<LevelOverlay> have = HudOverlays.Of(Singleton.PathToAI, _level);
            int at = overlays.SelectedIndex - 1;
            if (at < 0 || at >= have.Count)
            {
                if (force) LoadDesign(HudDesign.Example(FreeOverlayName()));
                return;
            }
            if (!force && !Discard()) return;
            LevelOverlay overlay = have[at];
            if (overlay.Design == null)
            {
                LoadDesign(new HudDesign() { Name = overlay.Name, Slots = new List<HudSlot>() });
                Say(overlay.Name + " is a movie made elsewhere (imported): it has no design to edit here. Its composite HUD\\" + overlay.Name + " loads it; call its own methods with FlashInvoke on layer " + HudOverlays.LayerName(overlay.Name) + ".");
                return;
            }
            LoadDesign(overlay.Design);
        }

        private string EditorLevel => Singleton.Editor?.CompositeBrowser?.Content?.Level?.Name?.Replace('\\', '/');

        private bool LevelOpen()
        {
            if (string.Equals(EditorLevel, _level, StringComparison.OrdinalIgnoreCase)) return true;
            MessageBox.Show("Open " + _level + " in the editor first: the overlay's script goes into the open level.", "HUD Overlay Builder", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        private Commands Commands => Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;

        /* Build and check (against the level's FlashInvokes on the layer when the level is open) */
        private byte[] BuildChecked(out MovieCheckResult check)
        {
            check = null;
            List<string> problems = _design.Problems();
            if (problems.Count != 0)
            {
                Say("The design can't be built yet:\n" + string.Join("\n", problems));
                return null;
            }
            byte[] movie = HudMovieBuilder.Build(_design, out check);
            if (string.Equals(EditorLevel, _level, StringComparison.OrdinalIgnoreCase))
            {
                string own = " in " + HudScript.CompositePath(_design.Name);
                check = HudOverlays.Recheck(movie, _design, HudOverlays.CallsOnLayer(Commands, _design.LayerName).Where(o => !o.From.EndsWith(own, StringComparison.OrdinalIgnoreCase)));
            }
            return movie;
        }

        private static string Describe(MovieCheckResult check)
        {
            List<string> lines = new List<string>() { "Check: " + check.Summary() };
            lines.AddRange(check.Errors.Take(12).Select(o => "  FAULT " + o));
            lines.AddRange(check.Warnings.Take(12).Select(o => "  note: " + o));
            return string.Join("\n", lines);
        }

        private void CheckOnly()
        {
            byte[] movie = BuildChecked(out MovieCheckResult check);
            if (movie == null) return;
            Say(Describe(check) + "\nThe game calls Init on it as the layer loads; the HUD's script calls hudSet(slot, value) every 0.1 s and hudShow(slot, on) from the Show/Hide pins. " + movie.Length + " bytes.");
        }

        private void SaveToLevel()
        {
            if (!LevelOpen()) return;
            byte[] movie = BuildChecked(out MovieCheckResult check);
            if (movie == null) return;
            if (!check.Ok)
            {
                Say("REFUSED - nothing was written.\n" + Describe(check));
                return;
            }
            string clash = HudScript.Clash(Commands, _design.Name);
            if (clash != null)
            {
                Say("Not saved - nothing was written. " + clash);
                return;
            }
            string problem = HudOverlays.Write(Singleton.PathToAI, _level, _design, out check);
            if (problem != null)
            {
                Say("Not saved: " + problem);
                return;
            }
            HudScript.Made made = null;
            try
            {
                McpScriptEdit.Run("HUD overlay " + _design.Name, Commands.EntryPoints[0], edit => { made = HudScript.Apply(edit, _design, _level); HudScript.FixPaths(edit, Singleton.PathToAI, _level); },
                    new McpScriptEdit.RunOptions() { Show = false, LabelPrefix = "" });
            }
            catch (Exception e)
            {
                Say("The movie was written, but its script could not be made: " + e.Message);
                return;
            }
            _dirty = false;
            List<string> lines = new List<string>()
            {
                "Saved '" + _design.Name + "': " + HudOverlays.GamePath(_level, _design.Name) + " (" + movie.Length + " bytes, checked: OK).",
                "Composite " + made.Composite.name + (made.CreatedComposite ? " made" : " remade") + " (" + made.Entities + " entities)" + (made.PlacedInstance ? ", an instance placed in the root" : "") +
                    ". Pins: " + string.Join(", ", HudScript.Pins(_design).Select(o => o.Key)) + ".",
                "Link values to the instance's pins, then save the level (a plain save is enough) and launch it.",
            };
            lines.AddRange(made.Notes);
            lines.AddRange(check.Warnings.Select(o => "note: " + o));
            Say(string.Join("\n", lines));
            FillOverlays(_design.Name);
        }

        private void ExportMovie()
        {
            byte[] movie = BuildChecked(out MovieCheckResult check);
            if (movie == null) return;
            if (!check.Ok) { Say("REFUSED - nothing was written.\n" + Describe(check)); return; }
            using (SaveFileDialog save = new SaveFileDialog() { Filter = "Scaleform movie (*.gfx)|*.gfx", FileName = _design.FileName })
            {
                if (save.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllBytes(save.FileName, movie);
                Say("Exported " + save.FileName + " (" + movie.Length + " bytes, checked: OK).");
            }
        }

        private void ImportMovie()
        {
            if (!LevelOpen()) return;
            using (OpenFileDialog open = new OpenFileDialog() { Filter = "Scaleform / Flash movie (*.gfx;*.swf)|*.gfx;*.swf" })
            {
                if (open.ShowDialog(this) != DialogResult.OK) return;
                byte[] bytes = File.ReadAllBytes(open.FileName);
                HudDesign design = HudMovieBuilder.ReadDesign(bytes);
                if (design != null)
                {
                    //Built by this window: edit its design (Save to Level writes it here)
                    if (!Discard()) return;
                    LoadDesign(design);
                    _dirty = true;
                    Say("Opened the design kept in " + Path.GetFileName(open.FileName) + ". Save to Level puts it in this level.");
                    return;
                }
                string name = Path.GetFileNameWithoutExtension(open.FileName).ToUpperInvariant().Replace(' ', '_');
                name = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
                if (name.Length == 0 || !char.IsLetter(name[0])) name = "IMPORTED";
                if (name.Length > HudDesign.MaxNameLength) name = name.Substring(0, HudDesign.MaxNameLength);
                //The movie first (one that would crash the game is the thing to hear about), then the level's one overlay layer
                MovieCheckResult first = HudMovieCheck.Check(bytes, new[] { new MovieCall() { Method = HudMovieBuilder.InitMethod, Arguments = 1, From = "The game" } }, HudOverlays.LayerName(name));
                if (!first.Ok)
                {
                    Say("REFUSED - " + Path.GetFileName(open.FileName) + " was not imported (the level's folder is unchanged).\n" + Describe(first));
                    return;
                }
                string clash = HudScript.Clash(Commands, name);
                if (clash != null)
                {
                    Say("Not imported - nothing was written. " + clash);
                    return;
                }
                string problem = HudOverlays.Import(Singleton.PathToAI, _level, name, bytes, HudOverlays.CallsOnLayer(Commands, HudOverlays.LayerName(name)), out MovieCheckResult check);
                if (problem != null)
                {
                    Say("REFUSED - " + Path.GetFileName(open.FileName) + " was not imported (the level's folder is unchanged).\n" + Describe(check));
                    return;
                }
                McpScriptEdit.Run("HUD overlay " + name, Commands.EntryPoints[0], edit => HudScript.Apply(edit, new HudDesign() { Name = name }, _level),
                    new McpScriptEdit.RunOptions() { Show = false, LabelPrefix = "" });
                Say("Imported as " + name + ": " + HudOverlays.GamePath(_level, name) + ", loaded by HUD\\" + name + " on layer " + HudOverlays.LayerName(name) + ".\n" + Describe(check));
                FillOverlays(name);
            }
        }

        private void RemoveFromLevel()
        {
            if (!LevelOpen()) return;
            if (MessageBox.Show("Take the overlay '" + _design.Name + "' out of " + _level + "? Its movie is deleted from the level's folder and its composite's contents and instances are removed (the script part can be undone).", "HUD Overlay Builder", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            int instances = 0;
            McpScriptEdit.Run("Remove HUD overlay " + _design.Name, Commands.EntryPoints[0], edit => instances = HudScript.Remove(edit, _design.Name), new McpScriptEdit.RunOptions() { Show = false, LabelPrefix = "" });
            string problem = HudOverlays.Remove(Singleton.PathToAI, _level, _design.Name);
            Say("Removed '" + _design.Name + "' (" + instances + " instance(s))" + (problem != null ? "; the movie could not be deleted: " + problem : "") + ". Save the level.");
            _dirty = false;
            FillOverlays(null);
        }

        private void OpenFolder()
        {
            string folder = HudOverlays.Folder(Singleton.PathToAI, _level);
            Directory.CreateDirectory(folder);
            Process.Start("explorer.exe", "\"" + folder + "\"");
        }
        #endregion

        /// <summary>The preview: the 1280x720 design space scaled into the panel, every slot drawn as it will look (text in the game's own fonts, read from its files by <see cref="HudFonts"/>), drag to move, the corner handle to resize.</summary>
        private sealed class PreviewPanel : Panel
        {
            public HudOverlayBuilder Builder;
            private HudSlot _dragging;
            private bool _resizing;
            private PointF _grab;
            private double _startX, _startY, _startW, _startH;

            public PreviewPanel()
            {
                DoubleBuffered = true;
                SetStyle(ControlStyles.Selectable, true);
                TabStop = true;
                BackColor = Color.FromArgb(24, 26, 28);
            }

            private RectangleF Stage(out float scale)
            {
                scale = Math.Min((Width - 16) / (float)HudDesign.StageWidth, (Height - 16) / (float)HudDesign.StageHeight);
                float w = (float)HudDesign.StageWidth * scale, h = (float)HudDesign.StageHeight * scale;
                return new RectangleF((Width - w) / 2, (Height - h) / 2, w, h);
            }

            private static RectangleF Box(HudSlot slot, RectangleF stage, float scale) =>
                new RectangleF(stage.X + (float)slot.X * scale, stage.Y + (float)slot.Y * scale, (float)slot.Width * scale, (float)slot.Height * scale);

            private static Color Col(string colour, double alpha)
            {
                int rgb = HudDesign.Rgb(colour);
                return Color.FromArgb((int)(Math.Max(0, Math.Min(1, alpha)) * 255), (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (Builder == null) return;
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                RectangleF stage = Stage(out float scale);
                using (LinearGradientBrush back = new LinearGradientBrush(stage, Color.FromArgb(58, 64, 60), Color.FromArgb(18, 20, 22), 90f))
                    g.FillRectangle(back, stage);
                using (Pen frame = new Pen(Color.FromArgb(90, 90, 90)))
                    g.DrawRectangle(frame, stage.X, stage.Y, stage.Width, stage.Height);
                using (Font small = new Font("Segoe UI", 8f))
                using (Brush faint = new SolidBrush(Color.FromArgb(110, 255, 255, 255)))
                    g.DrawString("1280 x 720 design space (scaled to the game's screen)", small, faint, stage.X + 4, stage.Bottom - 16);

                foreach (HudSlot slot in Builder.Design.Slots)
                {
                    RectangleF r = Box(slot, stage, scale);
                    double alpha = slot.Visible ? slot.Alpha : slot.Alpha * 0.3;
                    try { Draw(g, slot, r, scale, alpha); } catch { }
                }
                HudSlot selected = Builder.Selected;
                if (selected != null)
                {
                    RectangleF r = Box(selected, stage, scale);
                    using (Pen dash = new Pen(Color.FromArgb(255, 255, 200, 0), 1.5f) { DashStyle = DashStyle.Dash })
                        g.DrawRectangle(dash, r.X, r.Y, r.Width, r.Height);
                    using (Brush handle = new SolidBrush(Color.FromArgb(255, 255, 200, 0)))
                        g.FillRectangle(handle, r.Right - 4, r.Bottom - 4, 8, 8);
                }
            }

            //Pictures decoded once per PNG (the design keeps them as base64)
            private static readonly Dictionary<string, Image> _pictures = new Dictionary<string, Image>();
            private static Image Picture(string base64)
            {
                if (string.IsNullOrEmpty(base64)) return null;
                if (_pictures.TryGetValue(base64, out Image known)) return known;
                Image picture = null;
                try { picture = Image.FromStream(new MemoryStream(Convert.FromBase64String(base64))); } catch { }
                if (_pictures.Count > 64) _pictures.Clear();
                _pictures[base64] = picture;
                return picture;
            }

            private static string Sample(HudSlot slot)
            {
                switch (slot.Kind)
                {
                    case HudSlotKind.Text:
                        return slot.Lines.Count == 0 ? "" : slot.Lines[Math.Max(0, Math.Min(slot.Lines.Count - 1, (int)slot.Initial))];
                    case HudSlotKind.Number:
                        return (slot.Prefix ?? "") + slot.Initial.ToString("F" + Math.Max(0, Math.Min(3, slot.Decimals))) + (slot.Suffix ?? "");
                    default:
                        {
                            double t = Math.Max(0, slot.Initial);
                            int m = (int)(t / 60), s = (int)(t - m * 60);
                            return (slot.Prefix ?? "") + m + ":" + s.ToString("00") + (slot.Tenths ? "." + ((int)(t * 10) % 10) : "") + (slot.Suffix ?? "");
                        }
                }
            }

            private static void Draw(Graphics g, HudSlot slot, RectangleF r, float scale, double alpha)
            {
                switch (slot.Kind)
                {
                    case HudSlotKind.Text:
                    case HudSlotKind.Number:
                    case HudSlotKind.Timer:
                        {
                            string text = Sample(slot);
                            HudFont gameFont = HudFonts.Get(Singleton.PathToAI, slot.Font);
                            if (gameFont != null)
                            {
                                using (GraphicsPath path = HudFonts.Layout(gameFont, slot.Font, text, slot.Size, r, scale, slot.Align, out bool _))
                                    HudFonts.DrawTextField(g, path, r, Col(slot.Colour, 1), slot.Shadow, scale, alpha);
                                break;
                            }
                            //The game's fonts can't be read: a Windows face stands in
                            HudFonts.StandIn(slot.Font, out string family, out bool bold);
                            using (Font font = new Font(family, Math.Max(1f, (float)slot.Size * scale * 0.75f), bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point))
                            using (StringFormat format = new StringFormat() { Alignment = slot.Align == "center" ? StringAlignment.Center : slot.Align == "right" ? StringAlignment.Far : StringAlignment.Near, LineAlignment = StringAlignment.Near, FormatFlags = StringFormatFlags.NoWrap })
                            {
                                if (slot.Shadow)
                                    using (Brush shadow = new SolidBrush(Color.FromArgb((int)(alpha * 200), 0, 0, 0)))
                                        g.DrawString(text, font, shadow, new RectangleF(r.X + 1.5f, r.Y + 1.5f, r.Width, r.Height), format);
                                using (Brush brush = new SolidBrush(Col(slot.Colour, alpha)))
                                    g.DrawString(text, font, brush, r, format);
                            }
                            break;
                        }
                    case HudSlotKind.Bar:
                        {
                            using (Brush back = new SolidBrush(Col(slot.BackColour, slot.BackAlpha * alpha)))
                                g.FillRectangle(back, r);
                            double f = slot.Max > 0 ? slot.Initial / slot.Max : 1;
                            f = Math.Max(0, Math.Min(1, f));
                            bool low = f <= slot.LowAt && !string.IsNullOrEmpty(slot.LowColour);
                            RectangleF fill = r;
                            switch (slot.Direction)
                            {
                                case "left": fill = new RectangleF(r.Right - r.Width * (float)f, r.Y, r.Width * (float)f, r.Height); break;
                                case "up": fill = new RectangleF(r.X, r.Bottom - r.Height * (float)f, r.Width, r.Height * (float)f); break;
                                case "down": fill = new RectangleF(r.X, r.Y, r.Width, r.Height * (float)f); break;
                                default: fill = new RectangleF(r.X, r.Y, r.Width * (float)f, r.Height); break;
                            }
                            using (Brush brush = new SolidBrush(Col(low ? slot.LowColour : slot.Colour, alpha)))
                                g.FillRectangle(brush, fill);
                            if (!string.IsNullOrEmpty(slot.BorderColour))
                                using (Pen pen = new Pen(Col(slot.BorderColour, alpha), Math.Max(1f, (float)slot.BorderWidth * scale)))
                                    g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                            break;
                        }
                    case HudSlotKind.Reticle:
                        {
                            using (Pen pen = new Pen(Col(slot.Colour, alpha), Math.Max(1f, (float)slot.Thickness * scale)))
                            using (Brush brush = new SolidBrush(Col(slot.Colour, alpha)))
                            {
                                float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, rad = Math.Min(r.Width, r.Height) / 2, gap = (float)slot.Gap * scale;
                                switch (slot.Style)
                                {
                                    case "circle": g.DrawEllipse(pen, cx - rad, cy - rad, rad * 2, rad * 2); break;
                                    case "dot": g.FillEllipse(brush, cx - rad, cy - rad, rad * 2, rad * 2); break;
                                    case "brackets":
                                        {
                                            float arm = Math.Min(r.Width, r.Height) / 4;
                                            g.DrawLines(pen, new[] { new PointF(r.X, r.Y + arm), new PointF(r.X, r.Y), new PointF(r.X + arm, r.Y) });
                                            g.DrawLines(pen, new[] { new PointF(r.Right - arm, r.Y), new PointF(r.Right, r.Y), new PointF(r.Right, r.Y + arm) });
                                            g.DrawLines(pen, new[] { new PointF(r.Right, r.Bottom - arm), new PointF(r.Right, r.Bottom), new PointF(r.Right - arm, r.Bottom) });
                                            g.DrawLines(pen, new[] { new PointF(r.X + arm, r.Bottom), new PointF(r.X, r.Bottom), new PointF(r.X, r.Bottom - arm) });
                                            break;
                                        }
                                    default:
                                        g.DrawLine(pen, r.X, cy, Math.Max(r.X, cx - gap), cy); g.DrawLine(pen, Math.Min(r.Right, cx + gap), cy, r.Right, cy);
                                        g.DrawLine(pen, cx, r.Y, cx, Math.Max(r.Y, cy - gap)); g.DrawLine(pen, cx, Math.Min(r.Bottom, cy + gap), cx, r.Bottom);
                                        break;
                                }
                            }
                            break;
                        }
                    case HudSlotKind.Image:
                        {
                            Image picture = Picture(slot.ImagePng);
                            if (picture == null)
                            {
                                using (Pen pen = new Pen(Color.FromArgb((int)(alpha * 255), 200, 200, 200)) { DashStyle = DashStyle.Dot })
                                    g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                                break;
                            }
                            using (System.Drawing.Imaging.ImageAttributes faded = new System.Drawing.Imaging.ImageAttributes())
                            {
                                faded.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix() { Matrix33 = (float)Math.Max(0, Math.Min(1, alpha)) });
                                g.DrawImage(picture, Rectangle.Round(r), 0, 0, picture.Width, picture.Height, GraphicsUnit.Pixel, faded);
                            }
                            break;
                        }
                    default:
                        using (Brush brush = new SolidBrush(Col(slot.Colour, alpha)))
                            g.FillRectangle(brush, r);
                        if (!string.IsNullOrEmpty(slot.BorderColour))
                            using (Pen pen = new Pen(Col(slot.BorderColour, alpha), Math.Max(1f, (float)slot.BorderWidth * scale)))
                                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                        break;
                }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                Focus();
                if (Builder == null || e.Button != MouseButtons.Left) return;
                RectangleF stage = Stage(out float scale);
                HudSlot selected = Builder.Selected;
                if (selected != null)
                {
                    RectangleF r = Box(selected, stage, scale);
                    if (new RectangleF(r.Right - 7, r.Bottom - 7, 14, 14).Contains(e.Location))
                    {
                        Begin(selected, e.Location, true);
                        return;
                    }
                }
                //The topmost slot under the pointer (drawn last)
                HudSlot hit = Builder.Design.Slots.AsEnumerable().Reverse().FirstOrDefault(o => RectangleF.Inflate(Box(o, stage, scale), 3, 3).Contains(e.Location));
                Builder.SelectSlot(hit);
                if (hit != null) Begin(hit, e.Location, false);
            }

            private void Begin(HudSlot slot, Point at, bool resize)
            {
                _dragging = slot;
                _resizing = resize;
                _grab = at;
                _startX = slot.X; _startY = slot.Y; _startW = slot.Width; _startH = slot.Height;
                Capture = true;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (_dragging == null) return;
                Stage(out float scale);
                double dx = Math.Round((e.X - _grab.X) / scale), dy = Math.Round((e.Y - _grab.Y) / scale);
                if (_resizing)
                {
                    _dragging.Width = Math.Max(4, _startW + dx);
                    _dragging.Height = Math.Max(4, _startH + dy);
                }
                else
                {
                    _dragging.X = _startX + dx;
                    _dragging.Y = _startY + dy;
                }
                Builder.Changed();
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (_dragging != null) Builder.RefreshGrid();
                _dragging = null;
                Capture = false;
            }

            protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) == Keys.Left || (keyData & Keys.KeyCode) == Keys.Right || (keyData & Keys.KeyCode) == Keys.Up || (keyData & Keys.KeyCode) == Keys.Down || base.IsInputKey(keyData);
            protected override void OnResize(EventArgs eventargs) { base.OnResize(eventargs); Invalidate(); }
        }
    }
}

namespace OpenCAGE.Hud
{
    /// <summary>An image slot's PNG, picked with a file dialog (the slot reads the file when it is set).</summary>
    public sealed class HudPngFileEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            using (OpenFileDialog dialog = new OpenFileDialog() { Filter = "PNG image (*.png)|*.png", Title = "Image for the slot" })
                return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : value;
        }
    }

    /// <summary>A #RRGGBB text property picked with the colour dialog, with a swatch in the property grid.</summary>
    public sealed class HudColourEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;
        public override bool GetPaintValueSupported(ITypeDescriptorContext context) => true;

        public override void PaintValue(PaintValueEventArgs e)
        {
            string text = e.Value as string;
            if (string.IsNullOrEmpty(text)) return;
            int rgb = HudDesign.Rgb(text);
            using (Brush brush = new SolidBrush(Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255)))
                e.Graphics.FillRectangle(brush, e.Bounds);
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            string text = value as string;
            using (ColorDialog dialog = new ColorDialog() { FullOpen = true })
            {
                if (!string.IsNullOrEmpty(text))
                {
                    int rgb = HudDesign.Rgb(text);
                    dialog.Color = Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
                }
                if (dialog.ShowDialog() != DialogResult.OK) return value;
                return "#" + dialog.Color.R.ToString("X2") + dialog.Color.G.ToString("X2") + dialog.Color.B.ToString("X2");
            }
        }
    }
}
