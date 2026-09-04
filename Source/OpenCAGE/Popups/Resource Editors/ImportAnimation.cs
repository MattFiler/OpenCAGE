using CATHODE;
using CathodeLib;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Everything that has to be decided about an animation on its way in, with a preview so the
    /// decisions can be made by looking rather than by guessing. Nothing is committed until Import.
    /// </summary>
    public partial class ImportAnimation : BaseWindow
    {
        private readonly CathodeLib.Animation _animations;
        private CathodeLib.Animation.AnimationSet _set;
        private readonly string _preferRig;
        private ComboBox _setBox;
        private readonly string _file;

        private readonly AnimationImport.Options _options = new AnimationImport.Options();
        private AnimationImport.Reading _reading;
        private AnimationPreview _preview;
        private bool _filling = true;

        /// <summary>The name the set will play the imported clip by, once it has been imported.</summary>
        public string ImportedName { get; private set; }        /// <summary>The set it went into, which the window may have been the one to choose.</summary>        public string ImportedSet { get; private set; }

        private static readonly float[] Rates = { 0, 24, 25, 30, 60 };

        /// <param name="set">
        /// The set the clip goes into, or null to have the window ask - which is what a model import
        /// does, so that the set and the rig are chosen once, together, in the one window.
        /// </param>
        /// <param name="preferRig">
        /// The rig to start on, when the caller already knows which one.
        /// </param>
        /// <param name="clipIndex">Which animation in the file, for a file carrying more than one.</param>
        /// <param name="suggestedName">What to call it, when the file names the clip better than it names itself.</param>
        /// <param name="preferSet">Which set to start on, when the window is asking.</param>
        public ImportAnimation(CathodeLib.Animation animations, CathodeLib.Animation.AnimationSet set, string file,
                               string preferRig = null, int clipIndex = 0, string suggestedName = null,
                               string preferSet = null)
            : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            _animations = animations;
            _set = set;
            _file = file;
            _preferRig = preferRig;

            InitializeComponent();
            Icon = SharedFormIcon.Icon;

            fileLabel.Text = file;
            _options.Index = clipIndex;
            nameBox.Text = AnimationImport.Sanitise(string.IsNullOrEmpty(suggestedName)
                ? Path.GetFileNameWithoutExtension(file) : suggestedName).ToLowerInvariant();

            /* A caller that already knows the set says so and the window doesn't ask. One that doesn.t
             * gets a picker: the set and the rig are two halves of the same decision, and splitting
             * them over two windows only makes it look like the same question twice. */
            if (_set == null) BuildSetPicker(preferSet);
            UseSet(_set ?? _animations.Sets.FirstOrDefault());

            nameBox.TextChanged += (s, e) => UpdateButtons();
            pathBox.TextChanged += (s, e) => UpdateButtons();
            rigBox.SelectedIndexChanged += (s, e) => Reread();
            rootBox.SelectedIndexChanged += (s, e) => Reread();
            rateBox.SelectedIndexChanged += (s, e) => Reread();
            additiveCheck.CheckedChanged += (s, e) => { _options.Additive = additiveCheck.Checked; };

            previewBtn.Click += PreviewBtn_Click;
            importBtn.Click += ImportBtn_Click;

            _filling = false;
            Reread();
        }

        /* The row the retarget tick used to sit on, which is free now that the conversion decides
         * itself. Built here rather than in the designer because it only exists for one of the two
         * callers. */
        private void BuildSetPicker(string preferSet)
        {
            Controls.Add(new Label
            {
                AutoSize = true,
                Location = new System.Drawing.Point(12, 199),
                Text = "Set",
            });
            _setBox = new ComboBox
            {
                Location = new System.Drawing.Point(120, 195),
                Size = new System.Drawing.Size(432, 21),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            foreach (CathodeLib.Animation.AnimationSet option in _animations.Sets) _setBox.Items.Add(option.Name);
            int at = string.IsNullOrEmpty(preferSet) ? -1 : _setBox.Items.IndexOf(preferSet);
            _setBox.SelectedIndex = _setBox.Items.Count == 0 ? -1 : Math.Max(0, at);
            _setBox.SelectedIndexChanged += (s, e) =>
            {
                UseSet(_animations.GetSet(_setBox.SelectedItem as string));
                Reread();
            };
            Controls.Add(_setBox);
        }

        /* Everything that hangs off which set the clip is going into: what the window is called, where
         * the clip is stored, and which rigs it can be built against. */
        private void UseSet(CathodeLib.Animation.AnimationSet set)
        {
            _set = set;
            if (_set == null) return;

            bool was = _filling;
            _filling = true;

            Text = "Import animation into " + _set.Name;
            pathBox.Text = AnimationImport.PathFor(_set, _file);

            rigBox.Items.Clear();
            foreach (string rig in AnimationImport.RigsFor(_set)) rigBox.Items.Add(rig);
            if (rigBox.Items.Count == 0) rigBox.Items.Add(_set.Skeleton);
            /* A rig the caller already settled on is not necessarily one this set lists - a mesh can be
             * bound to a rig whose set is not the one its clips are going into - so it is added rather
             * than only selected. */
            if (!string.IsNullOrEmpty(_preferRig) && !rigBox.Items.Contains(_preferRig)) rigBox.Items.Insert(0, _preferRig);
            rigBox.SelectedIndex = Math.Max(0, string.IsNullOrEmpty(_preferRig) ? 0 : rigBox.Items.IndexOf(_preferRig));

            _filling = was;
        }

        /* Read the file again with whatever the options now say, and describe what came out. */
        private void Reread()
        {
            if (_filling) return;

            _options.Rig = rigBox.SelectedItem as string ?? "";
            _options.Root = (AnimationImport.RootHandling)Math.Max(0, rootBox.SelectedIndex);
            _options.FrameRate = rateBox.SelectedIndex >= 0 && rateBox.SelectedIndex < Rates.Length ? Rates[rateBox.SelectedIndex] : 0;
            _options.Retarget = false;

            Cursor.Current = Cursors.WaitCursor;
            try { _reading = AnimationImport.Read(_file, _animations.GetSkeleton(_options.Rig)?.Skeleton, _options); }
            catch (Exception ex) { _reading = new AnimationImport.Reading { Problem = ex.Message }; }

            /* A file whose nodes are already the game's bone names needs nothing doing to it; one on
             * another skeleton has no other way in. That is the whole of the decision, and the read
             * has just answered it, so it is made here rather than put to someone as a box to tick
             * after working out for themselves why nothing matched. */
            if (!_reading.Ok && _reading.Matched == 0 && _reading.CanRetarget)
            {
                _options.Retarget = true;
                try { _reading = AnimationImport.Read(_file, _animations.GetSkeleton(_options.Rig)?.Skeleton, _options); }
                catch (Exception ex) { _reading = new AnimationImport.Reading { Problem = ex.Message }; }
            }
            Cursor.Current = Cursors.Default;

            summaryBox.Text = Describe();
            UpdateButtons();
        }

        private string Describe()
        {
            StringBuilder text = new StringBuilder();
            if (_reading == null) return "";

            if (!_reading.Ok)
            {
                text.AppendLine("This can't be imported:").AppendLine();
                text.AppendLine(_reading.Problem);
                return text.ToString();
            }

            Skeleton rig = _animations.GetSkeleton(_options.Rig)?.Skeleton;
            text.AppendLine(_reading.Frames + " frames, " + _reading.Duration.ToString("0.##") + " seconds at "
                + (1f / _reading.FrameDuration).ToString("0.##") + " fps.");

            if (_reading.Retargeted)
                text.AppendLine("Converted from the file's own skeleton onto " + _options.Rig + ", driving "
                    + _reading.Matched + " of its " + (rig?.Bones.Count ?? 0) + " bones."
                    + (_reading.Mirrored ? " The two rigs are mirror images of each other, which is normal and is handled." : ""));
            else
                text.AppendLine(_reading.Matched + " of " + _reading.Channels + " animated nodes match a bone on "
                    + _options.Rig + " (" + (rig?.Bones.Count ?? 0) + " bones).");

            if (_reading.FileFrameRate > 0)
                text.AppendLine("The file says it runs at " + _reading.FileFrameRate.ToString("0.##")
                    + " fps" + (_options.FrameRate > 0 ? ", which is being overridden." : "."));

            text.AppendLine(_reading.RootAnimated
                ? "The root bone moves over the clip, so this animation carries the character with it."
                : "The root bone holds still, so where the character stands is left to the game.");

            if (!string.Equals(_options.Rig, _set.Skeleton, StringComparison.OrdinalIgnoreCase))
                text.AppendLine().AppendLine("Built against " + _options.Rig + " rather than " + _set.Skeleton
                    + ", so the game retargets it onto " + _set.Skeleton + " as it plays - which is how most of "
                    + _set.Name + "'s animations already work.");

            if (_reading.Warnings.Count != 0)
            {
                text.AppendLine();
                foreach (string warning in _reading.Warnings) text.AppendLine("* " + warning);
            }

            text.AppendLine().AppendLine("Preview it before importing - a clip that comes in turned round or "
                + "mirrored looks fine as numbers and obvious on screen.");
            return text.ToString();
        }

        private void UpdateButtons()
        {
            bool ready = _reading != null && _reading.Ok && nameBox.Text.Trim().Length != 0 && pathBox.Text.Trim().Length != 0;
            importBtn.Enabled = ready;
            previewBtn.Enabled = ready;
        }

        private void PreviewBtn_Click(object sender, EventArgs e)
        {
            CathodeLib.Animation.ClipReference clip = AnimationImport.BuildPreview(
                _animations, _set, _reading, nameBox.Text.Trim(), pathBox.Text.Trim(), _options);
            if (clip == null || !clip.Playable)
            {
                MessageBox.Show("The animation couldn't be built for preview.", "Preview",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_preview == null || _preview.IsDisposed)
            {
                _preview = new AnimationPreview(_animations);
                _preview.Owner = this;
                _preview.FormClosed += (s, args) => _preview = null;
                _preview.Show();
            }
            _preview.Show(clip);
            _preview.BringToFront();
        }

        private void ImportBtn_Click(object sender, EventArgs e)
        {
            string name = nameBox.Text.Trim(), path = pathBox.Text.Trim();
            if (!AnimationImport.Add(_animations, _set, _reading, name, path, _options, out string problem))
            {
                MessageBox.Show(problem, "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ImportedName = name;            ImportedSet = _set.Name;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_preview != null && !_preview.IsDisposed) _preview.Close();
            base.OnFormClosed(e);
        }
    }
}
