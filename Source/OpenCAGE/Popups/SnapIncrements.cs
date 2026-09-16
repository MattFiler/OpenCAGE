using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    /// <summary>
    /// Edits the transform and rotation snap increments the viewport offers (Options > Viewport > Snap
    /// Increments). "Off" is implicit and cannot be shown or removed; deleting the increment currently
    /// selected on the toolbar is handled gracefully - the selection lands on its nearest survivor,
    /// which <see cref="SnapIncrementSettings"/> and the toolbar do the moment the list is written.
    /// </summary>
    public partial class SnapIncrements : Form
    {
        private ListBox _gridList;
        private ListBox _rotationList;
        private NumericUpDown _gridInput;
        private NumericUpDown _rotationInput;

        public SnapIncrements()
        {
            BuildUi();
            Theming.ThemeManager.ApplyToForm(this);
            RefreshLists();
        }

        private void BuildUi()
        {
            Text = "Snap Increments";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(430, 320);

            GroupBox gridGroup = BuildGroup("Transform (units)", 10, out _gridList, out _gridInput, 3, 0.001m, 10000m,
                AddGrid, RemoveGrid);
            GroupBox rotationGroup = BuildGroup("Rotation (degrees)", 220, out _rotationList, out _rotationInput, 2, 0.01m, 360m,
                AddRotation, RemoveRotation);
            Controls.Add(gridGroup);
            Controls.Add(rotationGroup);

            Button reset = new Button()
            {
                Text = "Reset to Defaults",
                Bounds = new Rectangle(10, 285, 130, 26),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            };
            reset.Click += (s, e) =>
            {
                SnapIncrementSettings.ResetToDefaults();
                NotifyEditor(Settings.TransformSnapIncrements, Settings.RotationSnapIncrements);
                RefreshLists();
            };
            Controls.Add(reset);

            Button close = new Button()
            {
                Text = "Close",
                Bounds = new Rectangle(340, 285, 80, 26),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.OK,
            };
            close.Click += (s, e) => Close();
            Controls.Add(close);
            AcceptButton = close;
        }

        private GroupBox BuildGroup(
            string title, int x, out ListBox list, out NumericUpDown input, int decimals, decimal min, decimal max,
            EventHandler onAdd, EventHandler onRemove)
        {
            GroupBox group = new GroupBox() { Text = title, Bounds = new Rectangle(x, 10, 200, 265) };

            list = new ListBox() { Bounds = new Rectangle(12, 24, 176, 165), IntegralHeight = false };
            group.Controls.Add(list);

            input = new NumericUpDown()
            {
                Bounds = new Rectangle(12, 200, 90, 24),
                DecimalPlaces = decimals,
                Minimum = min,
                Maximum = max,
                Increment = decimals >= 3 ? 0.05m : 1m,
                Value = decimals >= 3 ? 0.25m : 5m,
            };
            group.Controls.Add(input);

            Button add = new Button() { Text = "Add", Bounds = new Rectangle(108, 200, 80, 24) };
            add.Click += onAdd;
            group.Controls.Add(add);

            Button remove = new Button() { Text = "Remove Selected", Bounds = new Rectangle(12, 230, 176, 26) };
            remove.Click += onRemove;
            group.Controls.Add(remove);

            return group;
        }

        private void RefreshLists()
        {
            Fill(_gridList, SnapIncrementSettings.GridValues, "0.###");
            Fill(_rotationList, SnapIncrementSettings.RotationValues, "0.##");
        }

        //The stored list is what the menu shows minus Off; Off is never edited here
        private static void Fill(ListBox list, IReadOnlyList<float> values, string format)
        {
            list.Items.Clear();
            foreach (float value in values)
            {
                if (value <= 0f)
                    continue;
                list.Items.Add(value.ToString(format, CultureInfo.InvariantCulture));
            }
        }

        private void AddGrid(object sender, EventArgs e) => Add(_gridList, _gridInput, SnapIncrementSettings.GridValues,
            SnapIncrementSettings.SetGridValues, Settings.TransformSnapIncrements);

        private void AddRotation(object sender, EventArgs e) => Add(_rotationList, _rotationInput, SnapIncrementSettings.RotationValues,
            SnapIncrementSettings.SetRotationValues, Settings.RotationSnapIncrements);

        private void Add(ListBox list, NumericUpDown input, IReadOnlyList<float> current,
            Action<IEnumerable<float>> save, string key)
        {
            float value = (float)input.Value;
            if (value <= 0f)
            {
                MessageBox.Show("A snap increment must be greater than zero.", "Snap Increments",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<float> values = current.Where(o => o > 0f).ToList();
            if (values.Any(o => Math.Abs(o - value) < 0.0005f))
            {
                MessageBox.Show("That increment is already in the list.", "Snap Increments",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            values.Add(value);
            save(values);
            NotifyEditor(key);
            RefreshLists();
        }

        private void RemoveGrid(object sender, EventArgs e) => Remove(_gridList, SnapIncrementSettings.GridValues,
            SnapIncrementSettings.SetGridValues, Settings.TransformSnapIncrements);

        private void RemoveRotation(object sender, EventArgs e) => Remove(_rotationList, SnapIncrementSettings.RotationValues,
            SnapIncrementSettings.SetRotationValues, Settings.RotationSnapIncrements);

        private void Remove(ListBox list, IReadOnlyList<float> current, Action<IEnumerable<float>> save, string key)
        {
            if (list.SelectedIndex < 0)
                return;

            List<float> values = current.Where(o => o > 0f).ToList();
            if (list.SelectedIndex >= values.Count)
                return;

            values.RemoveAt(list.SelectedIndex);
            save(values);
            NotifyEditor(key);
            RefreshLists();
        }

        //A local settings write does not raise the external-change event the editor listens for, so
        //route the effect through the editor the same way the toolbar menus do.
        private static void NotifyEditor(params string[] keys)
        {
            Singleton.Editor?.ApplySnapIncrementChange(keys);
        }
    }
}
