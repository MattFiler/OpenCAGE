using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;

namespace OpenCAGE
{
    /// <summary>
    /// The transform and rotation snap increments the viewport offers, as the user has them. The
    /// shipped lists in <see cref="TransformSnapDefinitions"/> are the defaults; anything the user
    /// edits in Options > Viewport > Snap Increments lives in settings and takes their place.
    /// </summary>
    /// <remarks>
    /// "Off" (zero) is not stored and cannot be removed: it is always the first entry of both lists,
    /// because a menu with no way to switch snapping off is a trap. Everything else is kept positive,
    /// distinct and sorted, so the menus read in order whatever was typed.
    /// </remarks>
    public static class SnapIncrementSettings
    {
        /// <summary>Raised after either list is changed, so the menus can rebuild.</summary>
        public static event Action Changed;

        public static IReadOnlyList<float> GridValues => Load(Settings.TransformSnapIncrements, TransformSnapDefinitions.GridSnapValues);
        public static IReadOnlyList<float> RotationValues => Load(Settings.RotationSnapIncrements, TransformSnapDefinitions.RotationSnapValues);

        public static void SetGridValues(IEnumerable<float> values)
        {
            SettingsManager.SetFloatArray(Settings.TransformSnapIncrements, Clean(values).ToArray());
            Changed?.Invoke();
        }

        public static void SetRotationValues(IEnumerable<float> values)
        {
            SettingsManager.SetFloatArray(Settings.RotationSnapIncrements, Clean(values).ToArray());
            Changed?.Invoke();
        }

        /// <summary>Back to the shipped lists.</summary>
        public static void ResetToDefaults()
        {
            SettingsManager.SetFloatArray(Settings.TransformSnapIncrements, StripOff(TransformSnapDefinitions.GridSnapValues));
            SettingsManager.SetFloatArray(Settings.RotationSnapIncrements, StripOff(TransformSnapDefinitions.RotationSnapValues));
            Changed?.Invoke();
        }

        /// <summary>
        /// The increment to use for a stored value against the current list: the value itself if it
        /// is still offered, otherwise the nearest one. A user who deletes the increment they had
        /// selected lands on its neighbour rather than on something arbitrary - and the lists always
        /// hold Off, so there is always an answer.
        /// </summary>
        public static float NormalizeGrid(float value) => Nearest(value, GridValues);
        public static float NormalizeRotation(float value) => Nearest(value, RotationValues);

        private static IReadOnlyList<float> Load(string key, IReadOnlyList<float> defaults)
        {
            float[] stored = SettingsManager.IsSet(key) ? SettingsManager.GetFloatArray(key) : null;
            List<float> values = Clean(stored ?? defaults);
            values.Insert(0, 0f);
            return values;
        }

        /* Positive, finite, distinct, ascending - and never Off, which is added on the way out */
        private static List<float> Clean(IEnumerable<float> values)
        {
            List<float> clean = new List<float>();
            if (values == null)
                return clean;

            foreach (float value in values)
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                    continue;
                float rounded = (float)Math.Round(value, 4);
                if (!clean.Exists(o => Math.Abs(o - rounded) < 0.00005f))
                    clean.Add(rounded);
            }
            clean.Sort();
            return clean;
        }

        private static float[] StripOff(IReadOnlyList<float> values)
        {
            return Clean(values).ToArray();
        }

        private static float Nearest(float value, IReadOnlyList<float> options)
        {
            //Options are ascending; <= means a tie goes to the LARGER entry. That matters at the low
            //end: deleting the selected 0.5 from {Off, 1, 2} ties Off and 1, and landing on the real
            //increment (1) rather than switching snapping off is the least surprising outcome.
            float closest = options[0];
            float bestDistance = float.MaxValue;
            for (int i = 0; i < options.Count; i++)
            {
                float distance = Math.Abs(options[i] - value);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    closest = options[i];
                }
            }
            return closest;
        }
    }
}
