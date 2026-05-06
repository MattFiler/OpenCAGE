using CATHODE;
using CATHODE.Scripting;
using CommandsEditor.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;

namespace CommandsEditor.Popups
{
    public partial class GalaxyEditor : BaseWindow
    {
        public GalaxyEditor() : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            InitializeComponent();
            SetupGridColumns();

            gridEntries.CellEndEdit += GridEntries_CellEndEdit;
            gridEntries.CellValidating += GridEntries_CellValidating;
            this.FormClosing += GalaxyEditor_FormClosing;

            LoadFromDefinition();
        }

        private void GalaxyEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            gridEntries.CellEndEdit -= GridEntries_CellEndEdit;
            gridEntries.CellValidating -= GridEntries_CellValidating;
            this.FormClosing -= GalaxyEditor_FormClosing;
        }

        private void SetupGridColumns()
        {
            gridEntries.AllowUserToAddRows = false;
            gridEntries.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridEntries.MultiSelect = false;
            gridEntries.RowHeadersVisible = true;
            gridEntries.Columns.Clear();
            gridEntries.Columns.Add(new DataGridViewTextBoxColumn { Name = "Frequency", HeaderText = "Frequency", Width = 70 });
            gridEntries.Columns.Add(new DataGridViewTextBoxColumn { Name = "MinSize", HeaderText = "Min Size", Width = 70 });
            gridEntries.Columns.Add(new DataGridViewTextBoxColumn { Name = "MaxSize", HeaderText = "Max Size", Width = 70 });
            gridEntries.Columns.Add(new DataGridViewTextBoxColumn { Name = "MinIntensity", HeaderText = "Min Intensity", Width = 85 });
            gridEntries.Columns.Add(new DataGridViewTextBoxColumn { Name = "MaxIntensity", HeaderText = "Max Intensity", Width = 85 });
            gridEntries.Columns.Add(new DataGridViewTextBoxColumn { Name = "R", HeaderText = "R (0-1)", Width = 55 });
            gridEntries.Columns.Add(new DataGridViewTextBoxColumn { Name = "G", HeaderText = "G (0-1)", Width = 55 });
            gridEntries.Columns.Add(new DataGridViewTextBoxColumn { Name = "B", HeaderText = "B (0-1)", Width = 55 });
        }

        private void LoadFromDefinition()
        {
            textBoxName.Text = Content.Level.GalaxyDefinition?.Name ?? "";
            numericUpDown1.Value = Content.Level.GalaxyDefinition != null ? Math.Min((decimal)Content.Level.GalaxyDefinition.StarCount, numericUpDown1.Maximum) : 0;

            gridEntries.Rows.Clear();
            if (Content.Level.GalaxyDefinition?.Entries == null) return;
            foreach (var entry in Content.Level.GalaxyDefinition.Entries)
            {
                gridEntries.Rows.Add(
                    entry.Frequency.ToString("G4"),
                    entry.MinSize.ToString("G4"),
                    entry.MaxSize.ToString("G4"),
                    entry.MinIntensity.ToString("G4"),
                    entry.MaxIntensity.ToString("G4"),
                    entry.Colour.X.ToString("G4"),
                    entry.Colour.Y.ToString("G4"),
                    entry.Colour.Z.ToString("G4"));
            }
        }

        private void GridEntries_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            string col = gridEntries.Columns[e.ColumnIndex].Name;
            string raw = e.FormattedValue?.ToString()?.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                e.Cancel = true;
                gridEntries.Rows[e.RowIndex].ErrorText = "Value is required.";
                return;
            }
            if (!float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
            {
                e.Cancel = true;
                gridEntries.Rows[e.RowIndex].ErrorText = "Must be a number.";
                return;
            }
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                e.Cancel = true;
                gridEntries.Rows[e.RowIndex].ErrorText = "Value must be finite.";
                return;
            }
            string rangeError = GetRangeError(col, value);
            if (rangeError != null)
            {
                e.Cancel = true;
                gridEntries.Rows[e.RowIndex].ErrorText = rangeError;
                return;
            }
            gridEntries.Rows[e.RowIndex].ErrorText = null;
        }

        private static string GetRangeError(string columnName, float value)
        {
            switch (columnName)
            {
                case "Frequency":
                    if (value < 0f || value > 10000f) return "Frequency must be between 0 and 10000.";
                    break;
                case "MinSize":
                case "MaxSize":
                    if (value < 0f || value > 10f) return "Size must be between 0 and 10 (radians).";
                    break;
                case "MinIntensity":
                case "MaxIntensity":
                    if (value < 0f || value > 1f) return "Intensity must be between 0 and 1.";
                    break;
                case "R":
                case "G":
                case "B":
                    if (value < 0f || value > 1f) return "Colour component must be between 0 and 1.";
                    break;
            }
            return null;
        }

        private void GridEntries_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (Content.Level.GalaxyDefinition?.Entries == null || e.RowIndex < 0 || e.RowIndex >= Content.Level.GalaxyDefinition.Entries.Count) return;
            gridEntries.Rows[e.RowIndex].ErrorText = null;
            SyncRowToEntry(e.RowIndex);
        }

        private void SyncRowToEntry(int rowIndex)
        {
            if (Content.Level.GalaxyDefinition.Entries == null || rowIndex < 0 || rowIndex >= Content.Level.GalaxyDefinition.Entries.Count || rowIndex >= gridEntries.Rows.Count) return;
            var row = gridEntries.Rows[rowIndex];
            var entry = Content.Level.GalaxyDefinition.Entries[rowIndex];
            float parse(string col, float def)
            {
                return float.TryParse(row.Cells[col].Value?.ToString(), out float f) ? f : def;
            }
            entry.Frequency = parse("Frequency", 1f);
            entry.MinSize = parse("MinSize", 0f);
            entry.MaxSize = parse("MaxSize", 0.001f);
            entry.MinIntensity = parse("MinIntensity", 0.5f);
            entry.MaxIntensity = parse("MaxIntensity", 1f);
            entry.Colour = new Vector3(
                parse("R", 1f),
                parse("G", 1f),
                parse("B", 1f));
        }

        private void btnAddEntry_Click(object sender, EventArgs e)
        {
            if (Content.Level.GalaxyDefinition.Entries == null) return;
            var template = new GalaxyDefinition.StarTemplate
            {
                Frequency = 1f,
                MinSize = 0f,
                MaxSize = 0.001f,
                MinIntensity = 0.5f,
                MaxIntensity = 1f,
                Colour = new Vector3(1f, 1f, 1f)
            };
            Content.Level.GalaxyDefinition.Entries.Add(template);
            gridEntries.Rows.Add(
                template.Frequency.ToString("G4"),
                template.MinSize.ToString("G4"),
                template.MaxSize.ToString("G4"),
                template.MinIntensity.ToString("G4"),
                template.MaxIntensity.ToString("G4"),
                template.Colour.X.ToString("G4"),
                template.Colour.Y.ToString("G4"),
                template.Colour.Z.ToString("G4"));
        }

        private void btnRemoveEntry_Click(object sender, EventArgs e)
        {
            if (Content.Level.GalaxyDefinition.Entries == null) return;
            int idx = gridEntries.SelectedCells.Count > 0 ? gridEntries.SelectedCells[0].RowIndex : -1;
            if (idx < 0 || idx >= Content.Level.GalaxyDefinition.Entries.Count) return;
            Content.Level.GalaxyDefinition.Entries.RemoveAt(idx);
            gridEntries.Rows.RemoveAt(idx);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < gridEntries.Rows.Count && i < Content.Level.GalaxyDefinition.Entries.Count; i++)
                SyncRowToEntry(i);

            Content.Level.GalaxyDefinition.Name = textBoxName.Text ?? "";
            Content.Level.GalaxyDefinition.StarCount = (int)numericUpDown1.Value;
            
            if (Content.Level.GalaxyItems.Generate(Content.Level.GalaxyDefinition))
            {
                Steam.UnlockAchievement(Steam.Achievements.GALAXY_MODIFIED);
                MessageBox.Show("Successfully regenerated galaxy!", "Galaxy Generated", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
