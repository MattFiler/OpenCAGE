using CATHODE;
using CATHODE.Enums;
using CATHODE.Scripting;
using CathodeLib;
using OpenCAGE.ConfigEditors;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups;
using OpenCAGE.Popups.Base;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE
{
    public partial class CharacterEditor : BaseWindow
    {
        private List<EntityPath> _hierarchies = new List<EntityPath>();
        private CharacterAccessorySets.CharacterAttributes _accessories;

        private EntityInspector _entityDisplay;

        public CharacterEditor(EntityInspector editor) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            _entityDisplay = editor;
            InitializeComponent();

            foreach (KeyValuePair<string, HashSet<string>> skeletons in Singleton.GenderedSkeletons)
                gender.Items.Add(skeletons.Key);

            assetType.Items.Clear();
            foreach (CUSTOM_CHARACTER_ASSETS entry in Enum.GetValues(typeof(CUSTOM_CHARACTER_ASSETS)))
                assetType.Items.Add(entry);
            voiceActor.Items.Clear();
            foreach (DIALOGUE_VOICE_ACTOR entry in Enum.GetValues(typeof(DIALOGUE_VOICE_ACTOR)))
                voiceActor.Items.Add(entry);
            genderAttr.Items.Clear();
            foreach (CUSTOM_CHARACTER_GENDER entry in Enum.GetValues(typeof(CUSTOM_CHARACTER_GENDER)))
                genderAttr.Items.Add(entry);
            ethnicityAttr.Items.Clear();
            foreach (CUSTOM_CHARACTER_ETHNICITY entry in Enum.GetValues(typeof(CUSTOM_CHARACTER_ETHNICITY)))
                ethnicityAttr.Items.Add(entry);
            buildAttr.Items.Clear();
            foreach (CUSTOM_CHARACTER_BUILD entry in Enum.GetValues(typeof(CUSTOM_CHARACTER_BUILD)))
                buildAttr.Items.Add(entry);

            foleyFootwear.Items.Clear();
            foreach (CHARACTER_FOLEY_SOUND entry in Enum.GetValues(typeof(CHARACTER_FOLEY_SOUND)))
                foleyFootwear.Items.Add(entry);
            foleyLeg.Items.Clear();
            foreach (CHARACTER_FOLEY_SOUND entry in Enum.GetValues(typeof(CHARACTER_FOLEY_SOUND)))
                foleyLeg.Items.Add(entry);
            foleyTorso.Items.Clear();
            foreach (CHARACTER_FOLEY_SOUND entry in Enum.GetValues(typeof(CHARACTER_FOLEY_SOUND)))
                foleyTorso.Items.Add(entry);

            RefreshUI(ShortGuid.Invalid);

            this.FormClosing += CharacterEditor_FormClosing;
        }

        private void CharacterEditor_FormClosing(object sender, System.Windows.Forms.FormClosingEventArgs e)
        {
            this.FormClosing -= CharacterEditor_FormClosing;

            _assetEditor?.Close();
            _assetEditor = null;
        }

        private void RefreshUI(ShortGuid selected)
        {
            int toSelect = 0;

            _hierarchies.Clear();
            List<EntityPath> hierarchies = _entityDisplay.Content.EditorUtils.GetHierarchiesForEntity(_entityDisplay.Composite, _entityDisplay.Entity);
            for (int i = 0; i < hierarchies.Count; i++)
            {
                ShortGuid instance = hierarchies[i].GenerateCompositeInstanceID();
                if (Content.Level.AccessorySets.Entries.FirstOrDefault(o => o.character.composite_instance_id == instance) == null) continue;
                if (toSelect == 0 && instance == selected) toSelect = _hierarchies.Count;
                _hierarchies.Add(hierarchies[i]);
            }

            characterInstances.Items.Clear();
            for (int i = 0; i < _hierarchies.Count; i++)
                characterInstances.Items.Add(Content.Level.Commands.Utils.GetResolvedAsString(Content.Level.Commands.Utils.ResolveHierarchy(_hierarchies[i]), SettingsManager.GetBool(Singleton.Settings.ShowShortGuids)));

            selectNewHead.Enabled = characterInstances.Items.Count != 0;
            selectNewShirt.Enabled = characterInstances.Items.Count != 0;
            selectNewArms.Enabled = characterInstances.Items.Count != 0;
            selectNewTrousers.Enabled = characterInstances.Items.Count != 0;
            selectNewShoes.Enabled = characterInstances.Items.Count != 0;
            selectNewCollision.Enabled = characterInstances.Items.Count != 0;
            bodyTypes.Enabled = characterInstances.Items.Count != 0;
            gender.Enabled = characterInstances.Items.Count != 0;
            assetType.Enabled = characterInstances.Items.Count != 0;
            voiceActor.Enabled = characterInstances.Items.Count != 0;
            genderAttr.Enabled = characterInstances.Items.Count != 0;
            ethnicityAttr.Enabled = characterInstances.Items.Count != 0;
            buildAttr.Enabled = characterInstances.Items.Count != 0;
            foleyFootwear.Enabled = characterInstances.Items.Count != 0;
            foleyLeg.Enabled = characterInstances.Items.Count != 0;
            foleyTorso.Enabled = characterInstances.Items.Count != 0;

            if (characterInstances.Items.Count != 0)
                characterInstances.SelectedIndex = toSelect;
        }

        private void RefreshSkeletonsForGender()
        {
            bodyTypes.Items.Clear();
            foreach (string skeleton in Singleton.GenderedSkeletons[gender.Text])
                bodyTypes.Items.Add(skeleton);
        }

        private void characterInstances_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShortGuid hierarchyID = _hierarchies[characterInstances.SelectedIndex].GenerateCompositeInstanceID();
            _accessories = Content.Level.AccessorySets.Entries.FirstOrDefault(o => o.character.composite_instance_id == hierarchyID);

            shirtComposite.Text = Content.Level.Commands.GetComposite(_accessories.components.Torso.Composite)?.name;
            trousersComposite.Text = Content.Level.Commands.GetComposite(_accessories.components.Legs.Composite)?.name;
            shoesComposite.Text = Content.Level.Commands.GetComposite(_accessories.components.Shoes.Composite)?.name;
            headComposite.Text = Content.Level.Commands.GetComposite(_accessories.components.Head.Composite)?.name;
            armsComposite.Text = Content.Level.Commands.GetComposite(_accessories.components.Arms.Composite)?.name;
            collisionComposite.Text = Content.Level.Commands.GetComposite(_accessories.components.Collision.Composite)?.name;

            gender.Text = _accessories.gender_skeleton;
            RefreshSkeletonsForGender();
            bodyTypes.Text = _accessories.face_skeleton;
            assetType.SelectedIndex = (int)_accessories.asset_type;
            voiceActor.SelectedIndex = (int)_accessories.voice_actor;
            genderAttr.SelectedIndex = (int)_accessories.gender;
            ethnicityAttr.SelectedIndex = (int)_accessories.ethnicity;
            buildAttr.SelectedIndex = (int)_accessories.build;
            foleyFootwear.SelectedIndex = (int)_accessories.foley.Footwear;
            foleyLeg.SelectedIndex = (int)_accessories.foley.Leg;
            foleyTorso.SelectedIndex = (int)_accessories.foley.Torso;
        }

        private void addNewCharacter_Click(object sender, EventArgs e)
        {
            List<ShortGuid> existingCharacters = new List<ShortGuid>();
            for (int i = 0; i < _hierarchies.Count; i++)
            {
                existingCharacters.Add(_hierarchies[i].GenerateCompositeInstanceID());
            }

            InstanceSelection instanceSelector = new InstanceSelection(_entityDisplay, existingCharacters);
            instanceSelector.Show();
            instanceSelector.OnInstanceSelected += OnCharacterInstanceSelected;
        }
        private void OnCharacterInstanceSelected(ShortGuid instance)
        {
            Content.Level.AccessorySets.Entries.Add(new CharacterAccessorySets.CharacterAttributes()
            {
                character = new EntityHandle() { entity_id = _entityDisplay.Entity.shortGUID, composite_instance_id = instance }
            });

            RefreshUI(instance);
        }

        private SelectComposite CompositeSelector(string composite)
        {
            SelectComposite selectComposite = new SelectComposite(composite);
            selectComposite.Show();
            return selectComposite;
        }
        private void selectNewHead_Click(object sender, EventArgs e)
        {
            CompositeSelector(headComposite.Text).OnCompositeGenerated += OnNewHeadSelected;
        }
        private void OnNewHeadSelected(Composite comp)
        {
            headComposite.Text = comp.name;
            _accessories.components.Head.Composite = comp.shortGUID;
        }
        private void selectNewShirt_Click(object sender, EventArgs e)
        {
            CompositeSelector(shirtComposite.Text).OnCompositeGenerated += OnNewShirtSelected;
        }
        private void OnNewShirtSelected(Composite comp)
        {
            shirtComposite.Text = comp.name;
            _accessories.components.Torso.Composite = comp.shortGUID;
        }
        private void selectNewArms_Click(object sender, EventArgs e)
        {
            CompositeSelector(armsComposite.Text).OnCompositeGenerated += OnNewArmsSelected;
        }
        private void OnNewArmsSelected(Composite comp)
        {
            armsComposite.Text = comp.name;
            _accessories.components.Arms.Composite = comp.shortGUID;
        }
        private void selectNewTrousers_Click(object sender, EventArgs e)
        {
            CompositeSelector(trousersComposite.Text).OnCompositeGenerated += OnNewTrousersSelected;
        }
        private void OnNewTrousersSelected(Composite comp)
        {
            trousersComposite.Text = comp.name;
            _accessories.components.Legs.Composite = comp.shortGUID;
        }
        private void selectNewShoes_Click(object sender, EventArgs e)
        {
            CompositeSelector(shoesComposite.Text).OnCompositeGenerated += OnNewShoesSelected;
        }
        private void OnNewShoesSelected(Composite comp)
        {
            shoesComposite.Text = comp.name;
            _accessories.components.Shoes.Composite = comp.shortGUID;
        }
        private void selectNewCollision_Click(object sender, EventArgs e)
        {
            CompositeSelector(collisionComposite.Text).OnCompositeGenerated += OnNewCollisionSelected;
        }
        private void OnNewCollisionSelected(Composite comp)
        {
            collisionComposite.Text = comp.name;
            _accessories.components.Collision.Composite = comp.shortGUID;
        }

        private void gender_SelectedIndexChanged(object sender, EventArgs e)
        {
            _accessories.gender_skeleton = gender.Text;
            RefreshSkeletonsForGender();
        }

        private void bodyTypes_SelectedIndexChanged(object sender, EventArgs e)
        {
            _accessories.face_skeleton = bodyTypes.Text;
        }

        private void shirtDecal_SelectedIndexChanged(object sender, EventArgs e)
        {
            _accessories.asset_type = (CUSTOM_CHARACTER_ASSETS)assetType.SelectedIndex;
        }

        private void voiceActor_SelectedIndexChanged(object sender, EventArgs e)
        {
            _accessories.voice_actor = (DIALOGUE_VOICE_ACTOR)voiceActor.SelectedIndex;
        }

        private void genderAttr_SelectedIndexChanged(object sender, EventArgs e)
        {
            _accessories.gender = (CUSTOM_CHARACTER_GENDER)gender.SelectedIndex;
        }

        private void ethnicityAttr_SelectedIndexChanged(object sender, EventArgs e)
        {
            _accessories.ethnicity = (CUSTOM_CHARACTER_ETHNICITY)ethnicityAttr.SelectedIndex;
        }

        private void buildAttr_SelectedIndexChanged(object sender, EventArgs e)
        {
            _accessories.build = (CUSTOM_CHARACTER_BUILD)buildAttr.SelectedIndex;
        }

        private void foleyTorso_SelectedIndexChanged(object sender, EventArgs e)
        {
            _accessories.foley.Torso = (CHARACTER_FOLEY_SOUND)foleyTorso.SelectedIndex;
        }

        private void foleyLeg_SelectedIndexChanged(object sender, EventArgs e)
        {
            _accessories.foley.Leg = (CHARACTER_FOLEY_SOUND)foleyLeg.SelectedIndex;
        }

        private void foleyFootwear_SelectedIndexChanged(object sender, EventArgs e)
        {
            _accessories.foley.Footwear = (CHARACTER_FOLEY_SOUND)foleyLeg.SelectedIndex;
        }

        CharacterAssetEditor _assetEditor = null;
        private void editAssetTypes_Click(object sender, EventArgs e)
        {
            if (_assetEditor != null)
            {
                _assetEditor.Close();
                _assetEditor = null;
            }
            _assetEditor = new CharacterAssetEditor();
            _assetEditor.Show();
        }
    }
}
