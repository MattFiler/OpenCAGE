using CATHODE;
using CATHODE.Scripting;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace OpenCAGE.Popups.UserControls
{
    public partial class GUI_Resource_AnimatedModel : ResourceUserControl
    {
        private ResourceReference _resourceRef;
        private EnvironmentAnimations.EnvironmentAnimation _envAnim;
        private bool _suppressEntryChange;

        private sealed class EnvAnimListItem
        {
            public EnvironmentAnimations.EnvironmentAnimation Anim;

            public override string ToString()
            {
                if (Anim == null)
                    return "(none)";

                string skel = string.IsNullOrEmpty(Anim.SkeletonName) ? "(no skeleton)" : Anim.SkeletonName;
                int bones = Anim.BoneMappings?.Count ?? 0;
                int meshes = Anim.MeshMappings?.Count ?? 0;
                return "#" + Anim.ID + "  ·  " + skel + "  ·  " + bones + " bones / " + meshes + " meshes";
            }
        }

        public GUI_Resource_AnimatedModel() : base()
        {
            InitializeComponent();
        }

        public override void PopulateUI(ResourceReference resource)
        {
            _resourceRef = resource;
            _envAnim = resource?.AnimatedModel;
            RebuildEntryList();
            RefreshDetails();
        }

        private void RebuildEntryList()
        {
            _suppressEntryChange = true;
            try
            {
                entryList.BeginUpdate();
                entryList.Items.Clear();

                List<EnvironmentAnimations.EnvironmentAnimation> entries =
                    Content?.Level?.EnvironmentAnimations?.Entries;
                if (entries != null)
                {
                    var ordered = new List<EnvironmentAnimations.EnvironmentAnimation>(entries);
                    ordered.Sort((a, b) => a.ID.CompareTo(b.ID));
                    for (int i = 0; i < ordered.Count; i++)
                        entryList.Items.Add(new EnvAnimListItem { Anim = ordered[i] });
                }

                if (_envAnim != null)
                {
                    bool found = false;
                    for (int i = 0; i < entryList.Items.Count; i++)
                    {
                        var item = entryList.Items[i] as EnvAnimListItem;
                        if (item?.Anim != null && (ReferenceEquals(item.Anim, _envAnim) || item.Anim.ID == _envAnim.ID))
                        {
                            entryList.SelectedIndex = i;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        entryList.Items.Insert(0, new EnvAnimListItem { Anim = _envAnim });
                        entryList.SelectedIndex = 0;
                    }
                }
                else
                {
                    entryList.SelectedIndex = -1;
                }
            }
            finally
            {
                entryList.EndUpdate();
                _suppressEntryChange = false;
            }
        }

        private void animatedModelIndex_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressEntryChange || _resourceRef == null)
                return;

            var item = entryList.SelectedItem as EnvAnimListItem;
            if (item?.Anim == null)
                return;

            if (ReferenceEquals(_resourceRef.AnimatedModel, item.Anim))
            {
                _envAnim = item.Anim;
                RefreshDetails();
                return;
            }

            _resourceRef.AnimatedModel = item.Anim;
            _envAnim = item.Anim;
            Singleton.OnResourceModified?.Invoke();
            RefreshDetails();
        }

        private void RefreshDetails()
        {
            if (_envAnim == null)
            {
                idValue.Text = "";
                skeletonValue.Text = "";
                animSetValue.Text = "";
                countsValue.Text = "";
                mappingsList.Items.Clear();
                helpersList.Items.Clear();
                return;
            }

            idValue.Text = _envAnim.ID.ToString();
            skeletonValue.Text = string.IsNullOrEmpty(_envAnim.SkeletonName) ? "(none)" : _envAnim.SkeletonName;
            animSetValue.Text = FormatAnimHash(_envAnim.AnimationSet);

            int bones = _envAnim.BoneMappings?.Count ?? 0;
            int meshes = _envAnim.MeshMappings?.Count ?? 0;
            int helpers = _envAnim.HelperMatrices?.Count ?? 0;
            int bindPoses = _envAnim.InverseBindPoses?.Count ?? 0;
            int havokMaps = _envAnim.HavokToCathodeMappings?.Count ?? 0;
            countsValue.Text = bones + " bones · " + meshes + " meshes · " + helpers + " helpers · "
                + bindPoses + " bind poses · " + havokMaps + " Havok→Cathode";

            mappingsList.BeginUpdate();
            mappingsList.Items.Clear();
            if (_envAnim.BoneMappings != null)
            {
                for (int i = 0; i < _envAnim.BoneMappings.Count; i++)
                    mappingsList.Items.Add("Bone  " + FormatGuid(_envAnim.BoneMappings[i]));
            }
            if (_envAnim.MeshMappings != null)
            {
                for (int i = 0; i < _envAnim.MeshMappings.Count; i++)
                    mappingsList.Items.Add("Mesh  " + FormatGuid(_envAnim.MeshMappings[i]));
            }
            if (mappingsList.Items.Count == 0)
                mappingsList.Items.Add("(none)");
            mappingsList.EndUpdate();

            helpersList.BeginUpdate();
            helpersList.Items.Clear();
            if (_envAnim.HelperMatrices != null)
            {
                for (int i = 0; i < _envAnim.HelperMatrices.Count; i++)
                {
                    var helper = _envAnim.HelperMatrices[i];
                    helpersList.Items.Add(FormatAnimHash(helper.HelperName));
                }
            }
            if (helpersList.Items.Count == 0)
                helpersList.Items.Add("(none)");
            helpersList.EndUpdate();
        }

        private static string FormatGuid(ShortGuid guid)
        {
            string name = guid.ToString();
            string bytes = guid.ToByteString();
            if (string.IsNullOrEmpty(name) || name == bytes)
                return bytes;
            return name + "  (" + bytes + ")";
        }

        private static string FormatAnimHash(uint id)
        {
            if (id == 0)
                return "(none)";

            string name = null;
            if (Singleton.AnimationStrings_Debug?.Entries != null
                && Singleton.AnimationStrings_Debug.Entries.TryGetValue(id, out name))
                return name + "  (" + id + ")";
            if (Singleton.AnimationStrings?.Entries != null
                && Singleton.AnimationStrings.Entries.TryGetValue(id, out name))
                return name + "  (" + id + ")";
            return id.ToString();
        }
    }
}
