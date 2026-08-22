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
        private readonly CathodeLib.Animation.AnimationSet _set;
        private readonly string _file;

        private readonly AnimationImport.Options _options = new AnimationImport.Options();
        private AnimationImport.Reading _reading;
        private bool _offered;
        private AnimationPreview _preview;
        private bool _filling = true;

        /// <summary>The name the set will play the imported clip by, once it has been imported.</summary>
        public string ImportedName { get; private set; }

        private static readonly float[] Rates = { 0, 24, 25, 30, 60 };

        public ImportAnimation(CathodeLib.Animation animations, CathodeLib.Animation.AnimationSet set, string file)
            : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            _animations = animations;
            _set = set;
            _file = file;

            InitializeComponent();
            Icon = SharedFormIcon.Icon;
            Text = "Import animation into " + set.Name;

            fileLabel.Text = file;
            nameBox.Text = AnimationImport.Sanitise(Path.GetFileNameWithoutExtension(file)).ToLowerInvariant();
            pathBox.Text = AnimationImport.PathFor(set, file);

            foreach (string rig in AnimationImport.RigsFor(set)) rigBox.Items.Add(rig);
            if (rigBox.Items.Count == 0) rigBox.Items.Add(set.Skeleton);
            rigBox.SelectedIndex = 0;

            /* The root bone is the engine's business, not the rig's - a clip that doesn't animate it
             * leaves it out and the engine places the character. Getting this wrong is what turns a
             * character round, so it is spelled out rather than hidden. */
            rootBox.Items.Add("Leave to the engine unless the file animates it");
            rootBox.Items.Add("Always leave to the engine");
            rootBox.Items.Add("Keep whatever the file holds (root motion)");
            rootBox.SelectedIndex = 0;

            foreach (float rate in Rates) rateBox.Items.Add(rate == 0 ? "From the file" : rate.ToString("0.##") + " fps");
            rateBox.SelectedIndex = 0;

            nameBox.TextChanged += (s, e) => UpdateButtons();
            pathBox.TextChanged += (s, e) => UpdateButtons();
            rigBox.SelectedIndexChanged += (s, e) => Reread();
            rootBox.SelectedIndexChanged += (s, e) => Reread();
            rateBox.SelectedIndexChanged += (s, e) => Reread();
            additiveCheck.CheckedChanged += (s, e) => { _options.Additive = additiveCheck.Checked; };
            retargetCheck.CheckedChanged += (s, e) => Reread();

            previewBtn.Click += PreviewBtn_Click;
            importBtn.Click += ImportBtn_Click;

            _filling = false;
            Reread();
        }

        /* Read the file again with whatever the options now say, and describe what came out. */
        private void Reread()
        {
            if (_filling) return;

            _options.Rig = rigBox.SelectedItem as string ?? "";
            _options.Root = (AnimationImport.RootHandling)Math.Max(0, rootBox.SelectedIndex);
            _options.FrameRate = rateBox.SelectedIndex >= 0 && rateBox.SelectedIndex < Rates.Length ? Rates[rateBox.SelectedIndex] : 0;
            _options.Retarget = retargetCheck.Checked;

            Cursor.Current = Cursors.WaitCursor;
            try { _reading = AnimationImport.Read(_file, _animations.GetSkeleton(_options.Rig)?.Skeleton, _options); }
            catch (Exception ex) { _reading = new AnimationImport.Reading { Problem = ex.Message }; }
            finally { Cursor.Current = Cursors.Default; }

            OfferRetarget();
            summaryBox.Text = Describe();
            UpdateButtons();
        }

        /* The conversion is only worth showing when the file is on a rig it recognises and the chosen
         * rig is one it can build onto - otherwise it is a box that would do nothing. When the file
         * is on another skeleton there is no other way in, so it ticks itself rather than leaving
         * someone to work out why nothing matched. */
        private void OfferRetarget()
        {
            if (_reading == null) return;

            bool offer = _reading.CanRetarget || _reading.Retargeted;
            retargetCheck.Visible = offer;
            if (!offer)
            {
                if (retargetCheck.Checked) { _filling = true; retargetCheck.Checked = false; _filling = false; }
                return;
            }

            //nothing matched by name and this is the only way the file gets in, so start it ticked
            if (_offered || retargetCheck.Checked || _reading.Ok || _reading.Matched != 0) return;

            _offered = true;                       //only ever ticks itself once, so Reread cannot loop
            _filling = true;
            retargetCheck.Checked = true;
            _filling = false;
            Reread();
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

            ImportedName = name;
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
