using OpenCAGE.ShaderTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Stage = OpenCAGE.BespokeShaderEditor.Stage;

namespace OpenCAGE
{
    /// <summary>
    /// One stage of a shader in the <see cref="BespokeShaderEditor"/>, on a tab of its own: its HLSL to edit (decompiled from
    /// the game's file), its assembly as it is now, and what the compiler said. Compiling saves the stage straight into the
    /// game's bespoke shaders.
    /// </summary>
    public partial class BespokeShaderStageView : UserControl
    {
        private readonly BespokeShaderEditor _editor;
        private readonly int _index;
        private readonly Stage _stage;
        private bool _filling;
        private bool _edited;
        private int _pendingLine = -1;

        /// <summary>For the designer.</summary>
        public BespokeShaderStageView()
        {
            InitializeComponent();
        }

        internal BespokeShaderStageView(BespokeShaderEditor editor, int index, Stage stage) : this()
        {
            _editor = editor;
            _index = index;
            _stage = stage;

            editor.Tips.SetToolTip(decompileBtn, "Turn the " + BespokeShaderEditor.StageName(stage).ToLower() + " as it is in the game's file into HLSL, in place of what's in the editor.");
            editor.Tips.SetToolTip(compileBtn, "Compile the HLSL for this stage and save it into the game's bespoke shaders (Ctrl+S).");
            editor.Tips.SetToolTip(restoreBtn, "Put this stage back as " + (editor.Shipped != null ? "the game shipped it" : "it was when this window opened") + ", and save.");
            editor.Tips.SetToolTip(exportBtn, "Save the HLSL in the editor to a file.");
            editor.Tips.SetToolTip(importBtn, "Read HLSL from a file into the editor, to compile.");

            FillAssembly();
            if (editor.Drafts.TryGetValue((index, stage), out string draft))
            {
                SetHlsl(draft);
                _edited = true;
                stateLabel.Text = "Edited - not compiled yet";
            }
            else
                Decompile(false);
        }

        private byte[] Bytecode => BespokeShaderEditor.Get(_editor.ShaderFile.Entries[_index], _stage);
        private string Technique => _editor.ShaderFile.Entries[_index].Technique;

        /// <summary>Keeps HLSL that's been edited and not compiled, for when the shader is shown again.</summary>
        public void KeepDraft()
        {
            if (_editor == null) return;
            if (_edited) _editor.Drafts[(_index, _stage)] = hlslText.Text;
            else _editor.Drafts.Remove((_index, _stage));
        }

        #region EVENTS
        private void decompileBtn_Click(object sender, EventArgs e) => Decompile(true);
        private void compileBtn_Click(object sender, EventArgs e) => Compile();
        private void restoreBtn_Click(object sender, EventArgs e) => Restore();
        private void exportBtn_Click(object sender, EventArgs e) => Export();
        private void importBtn_Click(object sender, EventArgs e) => Import();

        private void hlslText_TextChanged(object sender, EventArgs e)
        {
            if (_filling || _editor == null) return;
            _edited = true;
            stateLabel.Text = "Edited - not compiled yet";
        }

        private void hlslText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A) { hlslText.SelectAll(); e.SuppressKeyPress = true; }
            if (e.Control && e.KeyCode == Keys.S) { Compile(); e.SuppressKeyPress = true; }
        }

        private void hlslText_VisibleChanged(object sender, EventArgs e)
        {
            if (hlslText.Visible && _pendingLine >= 0)
                BeginInvoke((Action)(() => ShowLine(_pendingLine)));
        }

        private void assemblyText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A) { assemblyText.SelectAll(); e.SuppressKeyPress = true; }
        }
        #endregion

        private void SetHlsl(string text)
        {
            _filling = true;
            hlslText.Text = (text ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n");
            _filling = false;
        }

        private void FillAssembly()
        {
            assemblyText.Text = (D3DCompiler.Disassemble(Bytecode) ?? "The bytecode couldn't be disassembled.").Replace("\r\n", "\n").Replace("\n", "\r\n");
        }

        private void Decompile(bool asked)
        {
            if (asked && _edited && MessageBox.Show("Replace your edits with the shader decompiled again?", "Decompile", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            Cursor = Cursors.WaitCursor;
            try
            {
                SetHlsl(HlslDecompiler.Decompile(Bytecode, Technique));
                messagesText.Text = "";
                //The declarations come first: show the code
                int main = hlslText.Text.IndexOf("void main(", StringComparison.Ordinal);
                if (main >= 0) ShowLine(hlslText.Text.LastIndexOf("\r\n\r\n", main, StringComparison.Ordinal) + 4);
                stateLabel.Text = "Decompiled from the shader as it is" + (_editor.Changed(_index).Contains(_stage) ? " (changed from the original)" : "");
            }
            catch (Exception ex)
            {
                SetHlsl("// This shader couldn't be decompiled:\n// " + ex.Message.Replace("\n", "\n// "));
                messagesText.Text = ex.Message;
                stateLabel.Text = "Couldn't be decompiled";
            }
            finally
            {
                Cursor = Cursors.Default;
            }
            _edited = false;
            _editor.Drafts.Remove((_index, _stage));
        }

        private void Compile()
        {
            if (_editor == null) return;
            byte[] current = Bytecode;
            string target = null;
            try { target = HlslDecompiler.TargetOf(current); } catch { }
            if (target == null)
            {
                messagesText.Text = "The stage's shader model couldn't be read from its bytecode.";
                return;
            }
            Cursor = Cursors.WaitCursor;
            byte[] compiled;
            string output;
            try
            {
                compiled = D3DCompiler.Compile(hlslText.Text, "main", target, out output);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
            output = Readable(output);
            if (compiled == null)
            {
                messagesText.Text = "Didn't compile (" + target + ", entry point main):\r\n" + output;
                stateLabel.Text = "Didn't compile";
                //To the first line it names
                Match at = Regex.Match(output, @"^Line (\d+)", RegexOptions.Multiline);
                if (at.Success && int.TryParse(at.Groups[1].Value, out int line) && line >= 1 && line <= hlslText.Lines.Length)
                {
                    int start = hlslText.GetFirstCharIndexFromLine(line - 1);
                    ShowLine(start);
                    hlslText.Select(start, hlslText.Lines[line - 1].Length);
                }
                return;
            }

            //The game sets the shader up as the original takes it: say where the new one takes things differently
            byte[] original = _editor.Original(_index, _stage) ?? current;
            List<string> differences = DxbcReflection.Compare(DxbcReflection.Read(original), DxbcReflection.Read(compiled));
            if (differences.Count != 0)
            {
                string list = string.Join("\n", differences.Take(10).Select(o => "- " + o)) + (differences.Count > 10 ? "\n- ...and " + (differences.Count - 10) + " more" : "");
                if (MessageBox.Show("The game sets this shader up the way the original takes its inputs, constants and textures. Yours takes them differently:\n\n" + list +
                    "\n\nIt may draw wrongly, or not at all. Save it anyway?", "Compile and Save", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                {
                    messagesText.Text = "Compiled, but not saved: it takes its inputs differently from the original.\r\n" + string.Join("\r\n", differences);
                    return;
                }
            }

            BespokeShaderEditor.Set(_editor.ShaderFile.Entries[_index], _stage, compiled);
            if (!_editor.Save())
                return;
            _edited = false;
            _editor.Drafts.Remove((_index, _stage));
            FillAssembly();
            _editor.RefreshRow(_index);
            messagesText.Text = "Compiled for " + target + " with " + D3DCompiler.CompilerName() + " and saved." + (string.IsNullOrWhiteSpace(output) ? "" : "\r\n" + output);
            stateLabel.Text = "Compiled and saved";
        }

        /* The compiler names the file it was given with the folder it ran in: "...\shader.hlsl(105,1): error" is "Line 105, column 1: error" */
        private static string Readable(string output)
        {
            if (string.IsNullOrEmpty(output)) return "";
            string text = output.Replace("\r\n", "\n");
            text = Regex.Replace(text, @"^[^\n]*?shader\.hlsl\((\d+),(\d+)(-\d+)?\):\s*", m => "Line " + m.Groups[1].Value + ", column " + m.Groups[2].Value + ": ", RegexOptions.Multiline);
            return text.Replace("\n", "\r\n");
        }

        /* The caret to this character, with its line at the top of the view (once the box is on screen) */
        private void ShowLine(int index)
        {
            if (index < 0) index = 0;
            if (!hlslText.IsHandleCreated || !hlslText.Visible)
            {
                _pendingLine = index;
                return;
            }
            _pendingLine = -1;
            hlslText.Select(hlslText.TextLength, 0);
            hlslText.ScrollToCaret();
            hlslText.Select(Math.Min(index, hlslText.TextLength), 0);
            hlslText.ScrollToCaret();
        }

        private void Restore()
        {
            if (_editor == null) return;
            byte[] original = _editor.Original(_index, _stage);
            if (original == null) return;
            if (Bytecode != null && Bytecode.SequenceEqual(original) && !_edited)
            {
                messagesText.Text = "This stage is already " + (_editor.Shipped != null ? "as the game shipped it." : "as it was when this window opened.");
                return;
            }
            if (MessageBox.Show("Put the " + BespokeShaderEditor.StageName(_stage).ToLower() + " of " + Technique + " back as " + (_editor.Shipped != null ? "the game shipped it" : "it was when this window opened") + "? Your HLSL for it is replaced.",
                "Restore Original", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            BespokeShaderEditor.Set(_editor.ShaderFile.Entries[_index], _stage, (byte[])original.Clone());
            if (!_editor.Save())
                return;
            FillAssembly();
            Decompile(false);
            _editor.RefreshRow(_index);
            messagesText.Text = "Restored and saved.";
            stateLabel.Text = "Restored";
        }

        private void Export()
        {
            if (_editor == null) return;
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "HLSL|*.hlsl|All files|*.*";
                dialog.FileName = Technique + "_" + BespokeShaderEditor.StageShort(_stage).ToLower() + ".hlsl";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { File.WriteAllText(dialog.FileName, hlslText.Text); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Export HLSL", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private void Import()
        {
            if (_editor == null) return;
            if (_edited && MessageBox.Show("Replace your edits with the file's HLSL?", "Import HLSL", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "HLSL|*.hlsl;*.fx;*.txt|All files|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    SetHlsl(File.ReadAllText(dialog.FileName));
                    _edited = true;
                    stateLabel.Text = "Imported - not compiled yet";
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Import HLSL", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
    }
}
