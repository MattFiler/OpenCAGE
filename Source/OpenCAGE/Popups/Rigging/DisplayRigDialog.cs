using AlienPAK;
using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.MCP;
using OpenCAGE.Rigging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;

namespace OpenCAGE.Popups.Rigging
{
    /// <summary>
    /// "Create display rig": a smaller, bigger or differently proportioned copy of a gameplay rig for a character to be drawn on
    /// while it plays the gameplay rig's animations (<see cref="DisplayRigBuilder"/>), and optionally a DisplayModel of the open
    /// level resized onto it and the NPC archetypes that should use it (<see cref="DisplayRigFit"/>).
    /// </summary>
    public partial class DisplayRigDialog : Form
    {
        private List<Skeleton> _rigs;

        public DisplayRigDialog(string gameplay)
        {
            InitializeComponent();
            Icon = SharedFormIcon.Icon;
            _rigs = RebindImport.CharacterRigs(Singleton.Animations);
            foreach (Skeleton s in _rigs) _gameplay.Items.Add(s.Name);
            int pick = _rigs.FindIndex(o => string.Equals(o.Name, gameplay ?? "MALE", StringComparison.OrdinalIgnoreCase));
            if (_rigs.Count != 0) _gameplay.SelectedIndex = Math.Max(0, pick);
            FillSources();
            _name.Text = (Gameplay?.Name ?? "MALE") + "_SMALL";
            //attached once the lists and name are filled in, so filling them doesn't redraw or suggest a display model name
            _gameplay.SelectedIndexChanged += Gameplay_SelectedIndexChanged;
            _source.SelectedIndexChanged += SuggestDisplayName_Changed;
            _name.TextChanged += SuggestDisplayName_Changed;
            OpenCAGE.Theming.ThemeManager.ApplyToForm(this);
            Redraw();
        }

        private Skeleton Gameplay => _gameplay.SelectedIndex >= 0 && _gameplay.SelectedIndex < _rigs.Count ? _rigs[_gameplay.SelectedIndex] : null;

        private void Gameplay_SelectedIndexChanged(object sender, EventArgs e)
        {
            FillSources();
            Changed();
        }

        private void SuggestDisplayName_Changed(object sender, EventArgs e)
        {
            SuggestDisplayName();
        }

        private void Scale_ValueChanged(object sender, EventArgs e)
        {
            Changed();
        }

        private void Bones_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            //a column's header text being set is not an edit
            if (e.RowIndex < 0) return;
            Changed();
        }

        private void Bones_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            Changed();
        }

        private void Bones_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
        }

        private void Fit_CheckedChanged(object sender, EventArgs e)
        {
            _source.Enabled = _newDisplay.Enabled = _fit.Checked;
        }

        private void Redraw_Tick(object sender, EventArgs e)
        {
            _redraw.Stop();
            Redraw();
        }

        private void Create_Click(object sender, EventArgs e)
        {
            CreateRig();
        }

        private void DisplayRigDialog_FormClosed(object sender, FormClosedEventArgs e)
        {
            _redraw.Dispose();
            _picture.Image?.Dispose();
        }

        private Level OpenLevel => Singleton.Editor?.CompositeBrowser?.Content?.Level;

        private void FillSources()
        {
            Skeleton g = Gameplay;
            DataGridViewComboBoxColumn bones = (DataGridViewComboBoxColumn)_bones.Columns[0];
            bones.Items.Clear();
            if (g != null) bones.Items.AddRange(g.Bones.Select(b => (object)RigRoles.Short(b.Name)).ToArray());
            _source.Items.Clear();
            Level level = OpenLevel;
            if (level?.Commands == null || g == null) { _fit.Enabled = false; return; }
            foreach (Composite c in level.Commands.Entries.Where(o => o != null && o.name.StartsWith(ModelCompositeBuilder.DisplayModelPrefix)).OrderBy(o => o.name))
            {
                EnvironmentAnimations.EnvironmentAnimation entry = DisplayRigFit.AnimatedModelOf(c, out _);
                if (entry != null && string.Equals(entry.SkeletonName, g.Name, StringComparison.OrdinalIgnoreCase)) _source.Items.Add(c.name);
            }
            _fit.Enabled = _source.Items.Count != 0;
            if (_source.Items.Count != 0) _source.SelectedIndex = 0;
        }

        private void SuggestDisplayName()
        {
            if (_source.SelectedItem is string name)
                _newDisplay.Text = name + "_" + _name.Text.Trim();
        }

        private void Changed() { _redraw.Stop(); _redraw.Start(); }

        private DisplayRigBuilder.Options Options(out string problem)
        {
            problem = null;
            DisplayRigBuilder.Options o = new DisplayRigBuilder.Options() { Gameplay = Gameplay?.Name ?? "MALE", Name = _name.Text.Trim().ToUpperInvariant(), Scale = (float)_scale.Value };
            float F(object v, float fallback) => float.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : fallback;
            foreach (DataGridViewRow row in _bones.Rows)
            {
                if (row.IsNewRow || !(row.Cells[0].Value is string bone) || bone.Length == 0) continue;
                float scale = F(row.Cells[1].Value, 1);
                if (scale <= 0) { problem = bone + "'s scale has to be above 0."; continue; }
                if (Math.Abs(scale - 1) > 1e-6f) o.BoneScale[bone] = scale;
                Vector3 offset = new Vector3(F(row.Cells[2].Value, 0), F(row.Cells[3].Value, 0), F(row.Cells[4].Value, 0));
                if (offset != Vector3.Zero) o.BoneOffset[bone] = offset;
            }
            return o;
        }

        /* The gameplay rig in grey and the new one in white, front and side, at rest */
        private void Redraw()
        {
            Skeleton g = Gameplay;
            if (g == null) return;
            DisplayRigBuilder.Options o = Options(out _);
            float[] scales = DisplayRigBuilder.BoneScales(g, o);
            List<Matrix4x4> gameplay = g.GetBindPose(), local = new List<Matrix4x4>();
            for (int i = 0; i < g.Bones.Count; i++)
            {
                Skeleton.Bone b = g.Bones[i];
                Vector3 t = b.Position * scales[i] + (o.BoneOffset.TryGetValue(RigRoles.Short(b.Name), out Vector3 off) ? off : Vector3.Zero);
                Matrix4x4 m = Matrix4x4.CreateScale(b.ScaleXYZ) * Matrix4x4.CreateFromQuaternion(b.Rotation) * Matrix4x4.CreateTranslation(t);
                local.Add(b.ParentIndex >= 0 ? m * local[b.ParentIndex] : m);
            }
            List<Matrix4x4> made = local.Select(m => m * Skeleton.ToMeshSpace).ToList();
            float top = Math.Max(gameplay.Max(m => m.Translation.Y), made.Max(m => m.Translation.Y));
            float ppm = 280f / Math.Max(0.5f, top);
            Vector3 centre = new Vector3(0, top / 2, 0);
            RigPicture picture = new RigPicture(520, 300);
            RigPicture.View front = RigPicture.View.Character(true, centre, ppm, 0, 0, 260, 300, "front"), side = RigPicture.View.Character(false, centre, ppm, 260, 0, 260, 300, "side");
            foreach (RigPicture.View v in new[] { front, side })
                for (int b = 0; b < g.Bones.Count; b++)
                {
                    int p = g.Bones[b].ParentIndex;
                    if (p <= 0) continue;
                    picture.Line(v, gameplay[p].Translation, gameplay[b].Translation, Color.DimGray);
                    picture.Line(v, made[p].Translation, made[b].Translation, Color.White, 1);
                }
            Image old = _picture.Image;
            _picture.Image = picture.ToBitmap(new[] { front, side });
            old?.Dispose();
            int head = RigRoles.Bone(g, "HEAD");
            _status.ForeColor = SystemColors.ControlText;
            _status.Text = head >= 0 ? "Head joint " + made[head].Translation.Y.ToString("0.00") + " m up (" + g.Name + ": " + gameplay[head].Translation.Y.ToString("0.00") + " m)." : "";
        }

        private void CreateRig()
        {
            CathodeLib.Animation animations = Singleton.Animations;
            DisplayRigBuilder.Options o = Options(out string problem);
            if (problem == null) problem = DisplayRigBuilder.NameProblem(animations, o.Name, out _);
            string stale = problem == null ? AnimationPakWrite.Stale(animations) : null;
            if (problem == null && stale != null) problem = stale;
            if (problem == null && EditorUtils.ThisInstallsGameRunning()) problem = "Alien: Isolation is running and holds ANIMATION.PAK open: close it first.";
            Level level = OpenLevel;
            string newDisplay = _fit.Checked && _source.SelectedItem is string ? ModelCompositeBuilder.Normalise(_newDisplay.Text) : null;
            if (problem == null && newDisplay != null)
            {
                if (!newDisplay.StartsWith(ModelCompositeBuilder.DisplayModelPrefix)) newDisplay = ModelCompositeBuilder.DisplayModelPrefix + newDisplay;
                problem = ModelCompositeBuilder.Problem(newDisplay, true, level?.Commands);
            }
            List<string> archetypes = _archetypes.Lines.Select(l => l.Trim()).Where(l => l.Length != 0).ToList();
            List<Composite> archetypeComposites = new List<Composite>();
            foreach (string a in archetypes)
            {
                Composite c = level?.Commands?.GetComposite(a);
                if (c == null && problem == null) problem = "The open level has no composite '" + a + "'.";
                if (c != null) archetypeComposites.Add(c);
            }
            if (problem != null) { _status.ForeColor = Color.Firebrick; _status.Text = problem; return; }

            Cursor = Cursors.WaitCursor;
            List<string> report = new List<string>();
            try
            {
                DisplayRigBuilder.Result built = DisplayRigBuilder.Create(animations, o);
                if (!built.Ok) { _status.ForeColor = Color.Firebrick; _status.Text = built.Problem; return; }
                string saved = AnimationPakWrite.SaveWhole(animations, this);
                if (saved != null) { _status.ForeColor = Color.Firebrick; _status.Text = "The rig was built but ANIMATION.PAK was not written: " + saved; return; }
                Singleton.OnAnimationsModified?.Invoke();
                report.AddRange(built.Report);

                if (newDisplay != null)
                {
                    Singleton.OnCompositeAddPending?.Invoke();
                    DisplayRigFit.Result fitted = DisplayRigFit.Fit(level, level.Commands.GetComposite((string)_source.SelectedItem), Gameplay, built.Skeleton, newDisplay, o.Scale);
                    if (fitted.Composite != null)
                    {
                        Singleton.OnCompositeAdded?.Invoke(fitted.Composite);
                        EditHistory.CompositesChanged(level.Commands, new[] { fitted.Composite });
                        Singleton.Editor.CompositeBrowser?.RefreshList();
                    }
                    if (fitted.Model != null) EditHistory.AssetChanged(level, AssetModificationInfoTable.AssetKind.MODEL, fitted.Model.Name);
                    Singleton.OnResourceModified?.Invoke();
                    report.AddRange(fitted.Ok ? fitted.Report : new List<string>() { "The display model was not made: " + fitted.Problem });
                }

                //who wears it: the archetype's own pins and every placement that passes its own value, as one undo step
                if (archetypeComposites.Count != 0)
                {
                    int placements = 0;
                    McpScriptEdit.Run("Put " + archetypeComposites.Count + " archetype(s) on " + o.Name, archetypeComposites[0], edit =>
                    {
                        foreach (Composite a in archetypeComposites)
                        {
                            foreach (string pin in new[] { "reference_skeleton", "display_model", "custom_character_type" })
                            {
                                if (pin != "reference_skeleton" && newDisplay == null) continue;
                                string value = pin == "reference_skeleton" ? o.Name : pin == "display_model" ? newDisplay.Substring(ModelCompositeBuilder.DisplayModelPrefix.Length) : "NONE";
                                //the archetype exposes each Character setting as a variable of that name (its pin)
                                ShortGuid pinId = ShortGuidUtils.Generate(pin);
                                VariableEntity variable = a.variables.FirstOrDefault(v => v.name == pinId);
                                if (variable != null) edit.SetParameter(a, variable, pin, value);
                            }
                            foreach (var p in DisplayRigFit.PlacementsPassing(level.Commands, a, "reference_skeleton").Where(x => !string.Equals(x.was, o.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                edit.SetParameter(p.composite, p.entity, "reference_skeleton", o.Name);
                                placements++;
                            }
                        }
                    }, new McpScriptEdit.RunOptions() { Show = false, AlsoTouches = DisplayRigFit_Placers(level.Commands, archetypeComposites) });
                    report.Add(archetypeComposites.Count + " archetype(s) put on " + o.Name + "; " + placements + " placement(s) that passed their own reference_skeleton changed too.");
                }
                MessageBox.Show(this, string.Join("\r\n\r\n", report) + "\r\n\r\n" + _limits.Text, "Display rig created", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception e)
            {
                _status.ForeColor = Color.Firebrick;
                _status.Text = "Failed: " + e.Message;
            }
            finally { Cursor = Cursors.Default; }
        }

        private static List<Composite> DisplayRigFit_Placers(Commands commands, List<Composite> archetypes)
        {
            return archetypes.SelectMany(a => DisplayRigFit.PlacementsPassing(commands, a, "reference_skeleton").Select(p => p.composite)).Distinct().ToList();
        }
    }
}
