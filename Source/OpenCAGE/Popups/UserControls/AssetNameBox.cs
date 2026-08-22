using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace OpenCAGE.Popups.UserControls
{
    /// <summary>
    /// A name field for an asset being imported, with the clash check done here rather than after
    /// the fact.
    ///
    /// Typing folders into the name is how folders are made - see <see cref="AssetName"/> - so this
    /// shows what the name will actually be stored as while it's being typed, and says why it can't
    /// be used the moment that's true, instead of letting someone finish the import and then telling
    /// them the name was taken all along.
    /// </summary>
    public class AssetNameBox : Panel
    {
        private readonly TextBox _text = new TextBox();
        private readonly Label _status = new Label();
        private Func<IEnumerable<string>> _taken;

        /// <summary>Raised whenever the name or its validity changes.</summary>
        public event EventHandler ValidityChanged;

        /// <summary>The tidied name, ready to store.</summary>
        public string Value { get { return AssetName.Normalise(_text.Text); } }

        /// <summary>Whether the name can be used as it stands.</summary>
        public bool IsValid { get; private set; }

        public AssetNameBox()
        {
            Height = 44;

            _text.Dock = DockStyle.Top;
            _text.TextChanged += (s, e) => Revalidate();

            _status.Dock = DockStyle.Top;
            _status.Height = 22;
            _status.AutoEllipsis = true;
            _status.Padding = new Padding(1, 4, 0, 0);

            //the status sits under the box, so it is added first and the box pushes it down
            Controls.Add(_status);
            Controls.Add(_text);
        }

        /// <summary>
        /// Point the field at a starting name and the names already in use.
        /// </summary>
        /// <param name="taken">Read each time it's needed, so a list that grows stays correct.</param>
        public void Bind(string initial, Func<IEnumerable<string>> taken)
        {
            _taken = taken;
            _text.Text = initial ?? "";
            _text.SelectionStart = _text.Text.Length;
            Revalidate();
        }

        /// <summary>Put the caret in the name, ready to type over it.</summary>
        public void FocusName(bool selectAll = false)
        {
            _text.Focus();
            if (selectAll) _text.SelectAll();
        }

        private void Revalidate()
        {
            string problem = AssetName.Problem(_text.Text);
            string tidy = Value;

            if (problem == null && AssetName.Exists(tidy, _taken?.Invoke()))
                problem = "Something is already called that. Pick another name.";

            IsValid = problem == null;

            if (!IsValid)
            {
                _status.ForeColor = Color.FromArgb(210, 90, 80);
                _status.Text = problem;
            }
            else
            {
                /* Only worth echoing back when it isn't what they typed - which is exactly when
                 * somebody has used forward slashes, or spaced the folders out. */
                bool changed = !string.Equals(tidy, _text.Text, StringComparison.Ordinal);
                _status.ForeColor = SystemColors.GrayText;
                _status.Text = changed ? "Will be stored as  " + tidy : "";
            }

            ValidityChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
