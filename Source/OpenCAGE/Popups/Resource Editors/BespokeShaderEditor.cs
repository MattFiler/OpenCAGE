using CATHODE;
using OpenCAGE.ShaderTools;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// The game's bespoke shaders (DATA/SHADERS/UBERSHADER/BESPOKESHADERS_DX11.PAK and its _BIN and _IDX_REMAP files): the
    /// ordinary, one-off shaders the engine uses for particular jobs - the interaction highlight, anti-aliasing, the depth of
    /// field, particles, ambient occlusion... - as opposed to the ubershaders materials pick permutations of. Each is listed
    /// by name, with its properties; each stage (vertex, pixel, geometry, compute) gets a tab of its own
    /// (<see cref="BespokeShaderStageView"/>) where it can be decompiled to HLSL, edited and compiled back into the game's
    /// file. None can be added or taken away: the engine asks for each one by its place in the file.
    /// </summary>
    public partial class BespokeShaderEditor : BaseWindow
    {
        private const string Folder = "DATA/SHADERS/UBERSHADER/";
        private static readonly string[] Files = { "BESPOKESHADERS_DX11.PAK", "BESPOKESHADERS_DX11_BIN.PAK", "BESPOKESHADERS_DX11_IDX_REMAP.PAK" };

        internal enum Stage { Vertex, Pixel, Hull, Domain, Geometry, Compute }

        /// <summary>The game's bespoke shaders, as they are on disk (and as edited).</summary>
        internal Shaders ShaderFile { get; private set; }
        /// <summary>The game's own bespoke shaders, when the Mod Manager has kept them: what Restore Original goes back to.</summary>
        internal Shaders Shipped { get; private set; }
        /// <summary>HLSL being edited, by shader and stage, kept while other shaders are looked at.</summary>
        internal readonly Dictionary<(int, Stage), string> Drafts = new Dictionary<(int, Stage), string>();
        internal ToolTip Tips => toolTip;

        /// <summary>Each shader's stages as they were when the window opened: what Restore Original goes back to otherwise.</summary>
        private readonly List<byte[][]> _opened = new List<byte[][]>();
        private string _shippedFolder;
        private int _selected = -1;

        public BespokeShaderEditor() : base()
        {
            InitializeComponent();
            EnableDocumentation("https://opencage.co.uk/docs/bespoke-shader-editor/");
        }

        private void BespokeShaderEditor_Load(object sender, EventArgs e)
        {
            Open();
        }

        private void BespokeShaderEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            KeepDrafts();
            if (Drafts.Count != 0 && MessageBox.Show("You've edited HLSL you haven't compiled (" + Drafts.Count + (Drafts.Count == 1 ? " stage" : " stages") + "). Close and lose those edits?", "Bespoke Shader Editor", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                e.Cancel = true;
        }

        private void BespokeShaderEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (_shippedFolder != null)
                try { Directory.Delete(_shippedFolder, true); } catch { }
            GC.KeepAlive(ShaderFile);
            GC.KeepAlive(Shipped);
        }

        private void searchTxt_TextChanged(object sender, EventArgs e)
        {
            FillList();
        }

        private void shaderList_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowSelected();
        }

        #region FILE
        private static string FullPath(int file) => Path.Combine(Singleton.PathToAI, Folder + Files[file]);

        private void Open()
        {
            if (!File.Exists(FullPath(0)) || !File.Exists(FullPath(1)))
            {
                MessageBox.Show("This game has no bespoke shaders to edit (" + Folder + Files[0] + " wasn't found).", "Bespoke Shader Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
                return;
            }
            if (!D3DCompiler.Available)
                MessageBox.Show("The Windows HLSL compiler (d3dcompiler_47.dll) wasn't found, so shaders can be looked at but not decompiled or compiled.", "Bespoke Shader Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Cursor = Cursors.WaitCursor;
            try
            {
                Reload();
                Shipped = LoadShipped();
            }
            finally
            {
                Cursor = Cursors.Default;
            }
            if (ShaderFile == null || !ShaderFile.Loaded)
            {
                MessageBox.Show("The bespoke shaders couldn't be read.", "Bespoke Shader Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }
            _opened.Clear();
            foreach (Shaders.Shader shader in ShaderFile.Entries)
                _opened.Add(Enum.GetValues(typeof(Stage)).Cast<Stage>().Select(st => (byte[])Get(shader, st)?.Clone()).ToArray());
            FillList();
            if (shaderList.Items.Count != 0)
                shaderList.Items[0].Selected = true;
            UpdateStatus();
        }

        private void Reload()
        {
            GC.KeepAlive(ShaderFile);
            ShaderFile = new Shaders(FullPath(0));
        }

        /* The game's own copies of the three files, from what the Mod Manager keeps of them, loaded from a folder of their own */
        private Shaders LoadShipped()
        {
#if ENABLE_MOD_PACKAGES
            try
            {
                Modding.ModInstaller installer = Modding.ModServices.Installer;
                if (installer == null || !Modding.ModServices.ManifestAvailable)
                    return null;
                string folder = Path.Combine(Path.GetTempPath(), "OpenCAGE_bespoke_shipped_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(folder);
                _shippedFolder = folder;
                for (int i = 0; i < Files.Length; i++)
                {
                    //A file still as the game shipped it is kept by this (it's what happens before any edit to it, too)
                    Modding.ModServices.CaptureBeforeWrite(FullPath(i), false);
                    string normalised = Modding.ModToolkit.NormaliseFull(Modding.ModServices.GameRoot, FullPath(i));
                    byte[] bytes = normalised == null ? null : installer.VanillaBytes(normalised);
                    if (bytes == null)
                        return null;
                    File.WriteAllBytes(Path.Combine(folder, Files[i]), bytes);
                }
                Shaders shipped = new Shaders(Path.Combine(folder, Files[0]));
                return shipped.Loaded && shipped.Entries.Count == ShaderFile.Entries.Count ? shipped : null;
            }
            catch
            {
                return null;
            }
#else
            return null;
#endif
        }

        /// <summary>Writes the files: the game's own bytes when every shader is back as it shipped, so they're the game's files again.</summary>
        internal bool Save()
        {
            EditorUtils.CloseAI(null, thisInstallOnly: true);
            try
            {
                for (int i = 0; i < Files.Length; i++)
                    Modding.ModServices.CaptureBeforeWrite(FullPath(i));
                bool ok;
                if (Shipped != null && _shippedFolder != null && Enumerable.Range(0, ShaderFile.Entries.Count).All(i => !Changed(i).Any()))
                {
                    for (int i = 0; i < Files.Length; i++)
                        File.WriteAllBytes(FullPath(i), File.ReadAllBytes(Path.Combine(_shippedFolder, Files[i])));
                    int keep = _selected;
                    Reload();
                    _selected = keep;
                    ok = ShaderFile.Loaded;
                }
                else
                {
                    ok = ShaderFile.Save();
                }
                OpenCAGE.ConfigEditors.ConfigEditorUtils.NotifyAutoSave(ok, ok ? null : "The bespoke shader files could not be written.");
                return ok;
            }
            catch (Exception ex)
            {
                OpenCAGE.ConfigEditors.ConfigEditorUtils.NotifyAutoSave(false, ex.Message);
                return false;
            }
        }
        #endregion

        #region SHADERS
        internal static byte[] Get(Shaders.Shader shader, Stage stage)
        {
            switch (stage)
            {
                case Stage.Vertex: return shader.VertexShader;
                case Stage.Pixel: return shader.PixelShader;
                case Stage.Hull: return shader.HullShader;
                case Stage.Domain: return shader.DomainShader;
                case Stage.Geometry: return shader.GeometryShader;
                default: return shader.ComputeShader;
            }
        }

        internal static void Set(Shaders.Shader shader, Stage stage, byte[] bytecode)
        {
            switch (stage)
            {
                case Stage.Vertex: shader.VertexShader = bytecode; break;
                case Stage.Pixel: shader.PixelShader = bytecode; break;
                case Stage.Hull: shader.HullShader = bytecode; break;
                case Stage.Domain: shader.DomainShader = bytecode; break;
                case Stage.Geometry: shader.GeometryShader = bytecode; break;
                default: shader.ComputeShader = bytecode; break;
            }
        }

        internal static string StageName(Stage stage) => stage + " Shader";
        internal static string StageShort(Stage stage) => stage == Stage.Vertex ? "VS" : stage == Stage.Pixel ? "PS" : stage == Stage.Hull ? "HS" : stage == Stage.Domain ? "DS" : stage == Stage.Geometry ? "GS" : "CS";

        private IEnumerable<Stage> Stages(Shaders.Shader shader) => Enum.GetValues(typeof(Stage)).Cast<Stage>().Where(st => Get(shader, st) != null);

        /// <summary>A stage as the game shipped it, or as it was when the window opened.</summary>
        internal byte[] Original(int index, Stage stage)
        {
            if (Shipped != null && index < Shipped.Entries.Count)
                return Get(Shipped.Entries[index], stage);
            return index < _opened.Count ? _opened[index][(int)stage] : null;
        }

        /// <summary>The stages of a shader that differ from the original.</summary>
        internal IEnumerable<Stage> Changed(int index)
        {
            Shaders.Shader shader = ShaderFile.Entries[index];
            foreach (Stage stage in Enum.GetValues(typeof(Stage)))
            {
                byte[] now = Get(shader, stage), was = Original(index, stage);
                if (now == null && was == null) continue;
                if (now == null || was == null || !now.SequenceEqual(was)) yield return stage;
            }
        }
        #endregion

        #region LIST
        private void FillList()
        {
            if (ShaderFile == null) return;
            string filter = searchTxt.Text.Trim();
            shaderList.BeginUpdate();
            shaderList.Items.Clear();
            for (int i = 0; i < ShaderFile.Entries.Count; i++)
            {
                Shaders.Shader shader = ShaderFile.Entries[i];
                if (filter.Length != 0 && (shader.Technique ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                ListViewItem item = new ListViewItem(i.ToString()) { Tag = i };
                item.SubItems.Add(shader.Technique ?? "");
                item.SubItems.Add(string.Join(" ", Stages(shader).Select(StageShort)));
                item.SubItems.Add(Changed(i).Any() ? "Yes" : "");
                item.Selected = i == _selected;
                shaderList.Items.Add(item);
            }
            shaderList.EndUpdate();
        }

        /// <summary>A shader's row and the status line, after one of its stages changed.</summary>
        internal void RefreshRow(int index)
        {
            foreach (ListViewItem item in shaderList.Items)
                if ((int)item.Tag == index)
                    item.SubItems[3].Text = Changed(index).Any() ? "Yes" : "";
            UpdateStatus();
            FillProperties(index);
        }

        private void UpdateStatus()
        {
            int changed = ShaderFile == null ? 0 : Enumerable.Range(0, ShaderFile.Entries.Count).Count(i => Changed(i).Any());
            statusLabel.Text = (ShaderFile?.Entries.Count ?? 0) + " bespoke shaders, " + changed + " changed from " + (Shipped != null ? "the game's own" : "when this window opened") +
                ".   Compiling with " + (D3DCompiler.Available ? D3DCompiler.CompilerName() : "no compiler (d3dcompiler_47.dll is missing)") + ".";
        }

        private void ShowSelected()
        {
            if (shaderList.SelectedItems.Count == 0) return;
            int index = (int)shaderList.SelectedItems[0].Tag;
            if (index == _selected && tabs.TabPages.Count > 1) return;
            KeepDrafts();
            _selected = index;
            Shaders.Shader shader = ShaderFile.Entries[index];

            //The Properties tab stays; a tab for each of the shader's stages
            Stage? showing = tabs.SelectedTab?.Tag as Stage?;
            tabs.SuspendLayout();
            foreach (TabPage page in tabs.TabPages.Cast<TabPage>().Where(p => p.Tag is Stage).ToList())
            {
                tabs.TabPages.Remove(page);
                page.Dispose();
            }
            FillProperties(index);
            foreach (Stage stage in Stages(shader))
            {
                TabPage page = new TabPage(StageName(stage)) { Tag = stage, UseVisualStyleBackColor = true };
                page.Controls.Add(new BespokeShaderStageView(this, index, stage) { Dock = DockStyle.Fill });
                tabs.TabPages.Add(page);
            }
            tabs.ResumeLayout();
            TabPage again = tabs.TabPages.Cast<TabPage>().FirstOrDefault(p => p.Tag is Stage st && showing.HasValue && st == showing.Value);
            tabs.SelectedTab = again ?? propertiesTab;
            Theming.ThemeManager.ApplyToForm(this);
        }

        private void KeepDrafts()
        {
            foreach (TabPage page in tabs.TabPages)
                foreach (BespokeShaderStageView view in page.Controls.OfType<BespokeShaderStageView>())
                    view.KeepDraft();
        }
        #endregion

        #region PROPERTIES
        private void FillProperties(int index)
        {
            Shaders.Shader shader = ShaderFile.Entries[index];
            propertiesList.BeginUpdate();
            propertiesList.Items.Clear();
            propertiesList.Groups.Clear();
            ListViewGroup Group(string name) { ListViewGroup g = new ListViewGroup(name); propertiesList.Groups.Add(g); return g; }
            void Row(ListViewGroup g, string name, string value, string tip = null)
            {
                ListViewItem item = new ListViewItem(name) { Group = g, ToolTipText = tip ?? value };
                item.SubItems.Add(value);
                propertiesList.Items.Add(item);
            }

            ListViewGroup general = Group("Shader");
            Row(general, "Name", shader.Technique);
            Row(general, "Number", index + " of " + ShaderFile.Entries.Count + " (the engine asks for it by this)");
            Row(general, "Stages", string.Join(", ", Stages(shader).Select(StageName)));
            List<Stage> changed = Changed(index).ToList();
            Row(general, "Changed", changed.Count == 0 ? "No" : "Yes: " + string.Join(", ", changed.Select(StageName)) + (Shipped != null ? " (from the game's own)" : " (since this window opened)"));
            if (shader.RequiredShaderModel != Shaders.SHADER_MODEL.PLATFORM_DEFAULT) Row(general, "Required shader model", shader.RequiredShaderModel.ToString());
            if (shader.CycleCount != 0) Row(general, "Cycle count", shader.CycleCount.ToString());
            if (shader.RegisterCount != 0) Row(general, "Register count", shader.RegisterCount.ToString());
            Row(general, "Permutation hash", "0x" + shader.PermutationHash.ToString("X8"));

            ListViewGroup states = Group("Render states");
            if (shader.RenderStates == null || shader.RenderStates.Entries.Count == 0)
                Row(states, "(none)", "The engine's defaults");
            else
                foreach (Shaders.StateBlock.Entry entry in shader.RenderStates.Entries)
                    Row(states, Enum.IsDefined(typeof(Shaders.RenderState), entry.StateId) ? ((Shaders.RenderState)entry.StateId).ToString() : "State " + entry.StateId, entry.Value + (entry.Value > 9 || entry.Value < 0 ? " (0x" + entry.Value.ToString("X") + ")" : ""));

            if (shader.Samplers.Count != 0)
            {
                ListViewGroup samplers = Group("Samplers");
                for (int i = 0; i < shader.Samplers.Count; i++)
                {
                    string value = string.Join(", ", shader.Samplers[i].Entries.Select(o => (Enum.IsDefined(typeof(Shaders.SamplerState), o.StateId) ? ((Shaders.SamplerState)o.StateId).ToString() : "State " + o.StateId) + " " + o.Value));
                    Row(samplers, "Sampler " + i + (i < shader.SamplerStageBindings.Count ? " (stage " + shader.SamplerStageBindings[i] + ")" : ""), value.Length == 0 ? "The engine's defaults" : value);
                }
            }

            ListViewGroup remaps = Group("Parameter remaps (the engine's constants, by where they go)");
            Row(remaps, "Engine", Remap(shader.EngineParameterRemaps));
            if (shader.VertexShader != null) Row(remaps, "Vertex shader", Remap(shader.VertexShaderParameterRemaps));
            if (shader.PixelShader != null) Row(remaps, "Pixel shader", Remap(shader.PixelShaderParameterRemaps));
            if (shader.HullShader != null) Row(remaps, "Hull shader", Remap(shader.HullShaderParameterRemaps));
            if (shader.DomainShader != null) Row(remaps, "Domain shader", Remap(shader.DomainShaderParameterRemaps));
            if (shader.SamplerRemaps.Count != 0) Row(remaps, "Samplers", Remap(shader.SamplerRemaps));

            foreach (Stage stage in Stages(shader))
            {
                byte[] bytecode = Get(shader, stage);
                ListViewGroup g = Group(StageName(stage));
                DxbcReflection reflection = DxbcReflection.Read(bytecode);
                string target = null;
                string slots = null;
                try
                {
                    string asm = D3DCompiler.Disassemble(bytecode);
                    if (asm != null)
                    {
                        target = DxbcAssembly.Parse(asm).Target;
                        int at = asm.LastIndexOf("// Approximately ", StringComparison.Ordinal);
                        if (at >= 0) slots = asm.Substring(at + 17).Split(' ')[0];
                    }
                }
                catch { }
                Row(g, "Target", target ?? "?");
                Row(g, "Size", bytecode.Length.ToString("N0") + " bytes" + (slots != null ? ", about " + slots + " instructions" : ""));
                Row(g, "Built with", reflection.Creator);
                if (reflection.Inputs.Count != 0)
                    Row(g, "Inputs", string.Join(", ", reflection.Inputs.Select(o => Element(o, "v"))));
                if (reflection.Outputs.Count != 0)
                    Row(g, "Outputs", string.Join(", ", reflection.Outputs.Select(o => Element(o, "o"))));
                foreach (DxbcReflection.Binding binding in reflection.Bindings.OrderBy(o => DxbcReflection.Space(o.Type)).ThenBy(o => o.BindPoint))
                {
                    string what;
                    switch (binding.Type)
                    {
                        case DxbcReflection.InputType.CBuffer:
                            {
                                DxbcReflection.ConstantBuffer cb = reflection.ConstantBuffers.FirstOrDefault(o => o.Name == binding.Name && o.Kind == 0);
                                what = "Constant buffer, " + (cb?.Size ?? 0) + " bytes" + (cb == null ? "" : ": reads " + string.Join(", ", cb.Variables.Where(v => v.Used).Select(v => v.Name)));
                                break;
                            }
                        case DxbcReflection.InputType.Sampler: what = binding.ComparisonSampler ? "Comparison sampler" : "Sampler"; break;
                        case DxbcReflection.InputType.Texture: what = binding.Dimension + " (" + binding.Return + (binding.Components > 1 ? "x" + binding.Components : "") + ")"; break;
                        case DxbcReflection.InputType.Structured: case DxbcReflection.InputType.UavRWStructured: case DxbcReflection.InputType.UavRWStructuredWithCounter:
                        case DxbcReflection.InputType.UavAppendStructured: case DxbcReflection.InputType.UavConsumeStructured:
                            what = binding.Type + ", " + binding.NumSamples + " bytes an element"; break;
                        default: what = binding.Type + " " + binding.Dimension; break;
                    }
                    Row(g, binding.Name + " (" + DxbcReflection.Space(binding.Type) + binding.BindPoint + (binding.BindCount > 1 ? "-" + (binding.BindPoint + binding.BindCount - 1) : "") + ")", what);
                }
            }
            propertiesList.EndUpdate();
        }

        private static string Remap(List<int> remaps) => remaps.Count == 0 ? "(none)" : remaps.Count + ": " + string.Join(", ", remaps);

        private static string Element(DxbcReflection.SignatureElement e, string prefix)
        {
            string name = e.Name + (e.SemanticIndex != 0 || !e.Name.StartsWith("SV_", StringComparison.OrdinalIgnoreCase) ? e.SemanticIndex.ToString() : "");
            string mask = "";
            for (int i = 0; i < 4; i++) if ((e.Mask & (1 << i)) != 0) mask += "xyzw"[i];
            return name + (e.Register >= 0 ? " (" + prefix + e.Register + "." + mask + ")" : "");
        }
        #endregion
    }
}
