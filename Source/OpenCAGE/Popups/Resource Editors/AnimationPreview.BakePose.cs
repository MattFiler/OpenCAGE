using CATHODE;
using Newtonsoft.Json.Linq;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Animation Preview's "Bake Pose...": freezes the model shown at the frame shown into a static model in the open level
    /// (bake_pose_model), optionally placed in the level's root.
    /// </summary>
    public partial class AnimationPreview
    {
        private Button _bakePoseBtn;

        private void SetupBakePose()
        {
            _bakePoseBtn = new Button()
            {
                Text = "Bake Pose...",
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(112, 28),
                Location = new Point(exportBtn.Left - 2 * (exportBtn.Width + 6), exportBtn.Top),
                Enabled = false,
                UseVisualStyleBackColor = true,
            };
            new ToolTip().SetToolTip(_bakePoseBtn, "Freeze this model at this frame into a static model in the open level (it can ride a vehicle with no lag, or stand as set dressing).");
            exportBtn.Parent.Controls.Add(_bakePoseBtn);
            _bakePoseBtn.Click += (s, e) => OpenBakePose();
            //Re-judged whenever the preview is rebuilt: the Export button, the model and rig labels and the frame all change then
            EventHandler judge = (s, e) => _bakePoseBtn.Enabled = CanBakePose;
            exportBtn.EnabledChanged += judge;
            modelLabel.TextChanged += judge;
            skeletonLabel.TextChanged += judge;
            frameLabel.TextChanged += judge;
        }

        private bool CanBakePose => exportBtn.Enabled && _model != null && _skeleton != null && _clip?.Animation != null
            && Content?.Level != null && Skeleton.RequiredBoneCount(_model) > 0 && !ShowingOnEnvironmentRig;

        private void OpenBakePose()
        {
            if (!CanBakePose) return;
            Stop();
            int frame = timeline.Frame;
            string leaf = Path.GetFileNameWithoutExtension((_model.Name ?? "MODEL").Replace('/', '\\').Split('\\').Last());
            CAGEAnimationEditor.PathToolDialog d = new CAGEAnimationEditor.PathToolDialog("Bake Pose",
                "Freezes " + leaf + " at frame " + (frame + 1) + " of " + _clip.Name + " (frame index " + frame + ") into a static model in the level (materials made from its own textures). Marker space lines it up with a character playing this clip at a marker; Preview space is what is shown here.");
            TextBox name = d.TextRow("New model's name", "POSED\\" + leaf + "_" + _clip.Name + "_F" + frame);
            ComboBox space = d.Combo("Space", new[] { "marker", "preview" }, 0);
            CheckBox place = d.Check("Place it in the level's root", true);
            NumericUpDown x = d.Number("  at x", 0, -100000, 100000, 3);
            NumericUpDown y = d.Number("  at y", 0, -100000, 100000, 3);
            NumericUpDown z = d.Number("  at z", 0, -100000, 100000, 3);
            NumericUpDown yaw = d.Number("  facing (yaw, degrees)", 0, -360, 360, 1);
            d.OnApply = () =>
            {
                JObject args = new JObject()
                {
                    ["model"] = _model.Name,
                    ["set"] = _clip.Context?.Set?.Name ?? "",
                    ["animation"] = _clip.Name,
                    ["rig"] = _skeleton.Name,
                    ["frame"] = frame,
                    ["space"] = space.Text,
                    ["root_motion"] = rootMotionCheck.Checked ? "follow" : "hold",
                    ["name"] = name.Text.Trim(),
                };
                if (!string.IsNullOrEmpty(_clip.Context?.Name)) args["context"] = _clip.Context.Name;
                if (place.Checked)
                {
                    args["place_in"] = "root";
                    args["position"] = new JArray((double)x.Value, (double)y.Value, (double)z.Value);
                    args["rotation"] = new JArray(0, (double)yaw.Value, 0);
                }
                return ("bake_pose_model", args);
            };
            d.OnCheck = () =>
            {
                JObject args = new JObject()
                {
                    ["model"] = _model.Name,
                    ["set"] = _clip.Context?.Set?.Name ?? "",
                    ["animation"] = _clip.Name,
                    ["rig"] = _skeleton.Name,
                    ["frame"] = frame,
                    ["space"] = space.Text,
                    ["root_motion"] = rootMotionCheck.Checked ? "follow" : "hold",
                    ["preview"] = true,
                };
                if (!string.IsNullOrEmpty(_clip.Context?.Name)) args["context"] = _clip.Context.Name;
                return ("bake_pose_model", args);
            };
            d.Run = (tool, args) => CAGEAnimationEditor.RunToolAsync(tool, args);
            d.Show(this);
        }
    }
}
