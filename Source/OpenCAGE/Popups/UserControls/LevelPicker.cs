using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.Popups.UserControls
{
    /// <summary>
    /// Any number of levels ticked in a CheckedListBox, with a button each to tick them all or none.
    /// The selection is read back in the list's order when the user acts.
    /// </summary>
    public class LevelPicker
    {
        private readonly CheckedListBox _list;
        private bool _bulk;

        public event Action SelectionChanged;

        public LevelPicker(CheckedListBox list, Button all, Button none)
        {
            _list = list;
            _list.CheckOnClick = true;
            _list.IntegralHeight = false;
            //ItemCheck arrives before the state changes; report once it has
            _list.ItemCheck += (s, e) =>
            {
                if (!_bulk && _list.IsHandleCreated)
                    _list.BeginInvoke(new Action(() => SelectionChanged?.Invoke()));
            };
            all.Click += (s, e) => SetAll(true);
            none.Click += (s, e) => SetAll(false);
        }

        /// <summary>Show these levels, with <paramref name="ticked"/> ticked to start with.</summary>
        public void Load(IEnumerable<string> levels, IEnumerable<string> ticked = null)
        {
            HashSet<string> on = new HashSet<string>(ticked ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            _bulk = true;
            _list.BeginUpdate();
            try
            {
                _list.Items.Clear();
                foreach (string level in levels ?? Enumerable.Empty<string>())
                    _list.Items.Add(level, on.Contains(level));
            }
            finally
            {
                _list.EndUpdate();
                _bulk = false;
            }
            SelectionChanged?.Invoke();
        }

        public int Count => _list.Items.Count;

        /// <summary>The ticked levels, in the list's order.</summary>
        public List<string> Selected => _list.CheckedItems.Cast<object>().Select(o => o.ToString()).ToList();

        public void SetAll(bool on)
        {
            _bulk = true;
            _list.BeginUpdate();
            try
            {
                for (int i = 0; i < _list.Items.Count; i++)
                    _list.SetItemChecked(i, on);
            }
            finally
            {
                _list.EndUpdate();
                _bulk = false;
            }
            SelectionChanged?.Invoke();
        }
    }
}
