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
        public SnapIncrements()
        {
            InitializeComponent();
            Icon = SharedFormIcon.Icon;
            Theming.ThemeManager.ApplyToForm(this);
            RefreshLists();
        }

        private void resetButton_Click(object sender, EventArgs e)
        {
            SnapIncrementSettings.ResetToDefaults();
            NotifyEditor(Settings.TransformSnapIncrements, Settings.RotationSnapIncrements);
            RefreshLists();
        }

        private void closeButton_Click(object sender, EventArgs e) => Close();

        private void RefreshLists()
        {
            Fill(_gridList, SnapIncrementSettings.GridValues, "0.#######");
            Fill(_rotationList, SnapIncrementSettings.RotationValues, "0.#######");
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
