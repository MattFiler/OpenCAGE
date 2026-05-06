namespace CommandsEditor.ConfigEditors
{
    partial class InventoryItemEditor
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.ListViewGroup listViewGroup1 = new System.Windows.Forms.ListViewGroup("Object", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup2 = new System.Windows.Forms.ListViewGroup("Weapon", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup3 = new System.Windows.Forms.ListViewGroup("Ammo", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup4 = new System.Windows.Forms.ListViewGroup("Medikit", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup5 = new System.Windows.Forms.ListViewGroup("Explosive", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup6 = new System.Windows.Forms.ListViewGroup("Light", System.Windows.Forms.HorizontalAlignment.Left);
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(InventoryItemEditor));
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.listView = new System.Windows.Forms.ListView();
            this.ItemName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.baseObject = new System.Windows.Forms.GroupBox();
            this.special_slot = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.crafting_resource = new System.Windows.Forms.CheckBox();
            this.label7 = new System.Windows.Forms.Label();
            this.radial_menu_order_index = new System.Windows.Forms.NumericUpDown();
            this.composite = new System.Windows.Forms.TextBox();
            this.display_quantity = new System.Windows.Forms.CheckBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.stack_limit = new System.Windows.Forms.NumericUpDown();
            this.label5 = new System.Windows.Forms.Label();
            this.default_quantity = new System.Windows.Forms.NumericUpDown();
            this.label4 = new System.Windows.Forms.Label();
            this.localisation_tag = new System.Windows.Forms.TextBox();
            this.vanish_when_collected = new System.Windows.Forms.CheckBox();
            this.label3 = new System.Windows.Forms.Label();
            this.keyframe = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.name = new System.Windows.Forms.TextBox();
            this.weapon = new System.Windows.Forms.GroupBox();
            this.label9 = new System.Windows.Forms.Label();
            this.weapon_type = new System.Windows.Forms.ComboBox();
            this.ammo = new System.Windows.Forms.GroupBox();
            this.label11 = new System.Windows.Forms.Label();
            this.ammo_type = new System.Windows.Forms.ComboBox();
            this.label10 = new System.Windows.Forms.Label();
            this.target_weapon = new System.Windows.Forms.ComboBox();
            this.held = new System.Windows.Forms.GroupBox();
            this.label12 = new System.Windows.Forms.Label();
            this.activated_by = new System.Windows.Forms.ComboBox();
            this.label14 = new System.Windows.Forms.Label();
            this.consume_when = new System.Windows.Forms.ComboBox();
            this.drop_when_consume = new System.Windows.Forms.CheckBox();
            this.droppable_when_held = new System.Windows.Forms.CheckBox();
            this.label16 = new System.Windows.Forms.Label();
            this.cancellable_duration_in_seconds = new System.Windows.Forms.NumericUpDown();
            this.label17 = new System.Windows.Forms.Label();
            this.thrown_object_name = new System.Windows.Forms.TextBox();
            this.label19 = new System.Windows.Forms.Label();
            this.held_object_name = new System.Windows.Forms.TextBox();
            this.medikit = new System.Windows.Forms.GroupBox();
            this.label13 = new System.Windows.Forms.Label();
            this.upgraded_health_increase_percentage = new System.Windows.Forms.NumericUpDown();
            this.label18 = new System.Windows.Forms.Label();
            this.health_increase_percentage = new System.Windows.Forms.NumericUpDown();
            this.addItemBtn = new System.Windows.Forms.Button();
            this.helpBtn = new System.Windows.Forms.Button();
            this.baseObject.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.radial_menu_order_index)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.stack_limit)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.default_quantity)).BeginInit();
            this.weapon.SuspendLayout();
            this.ammo.SuspendLayout();
            this.held.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.cancellable_duration_in_seconds)).BeginInit();
            this.medikit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.upgraded_health_increase_percentage)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.health_increase_percentage)).BeginInit();
            this.SuspendLayout();
            // 
            // listView
            // 
            this.listView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.ItemName});
            this.listView.FullRowSelect = true;
            listViewGroup1.Header = "Object";
            listViewGroup1.Name = "object";
            listViewGroup1.Tag = "object";
            listViewGroup2.Header = "Weapon";
            listViewGroup2.Name = "weapon";
            listViewGroup2.Tag = "weapon";
            listViewGroup3.Header = "Ammo";
            listViewGroup3.Name = "ammo";
            listViewGroup3.Tag = "ammo";
            listViewGroup4.Header = "Medikit";
            listViewGroup4.Name = "medikit";
            listViewGroup4.Tag = "medikit";
            listViewGroup5.Header = "Explosive";
            listViewGroup5.Name = "ied";
            listViewGroup5.Tag = "ied";
            listViewGroup6.Header = "Light";
            listViewGroup6.Name = "light";
            listViewGroup6.Tag = "light";
            this.listView.Groups.AddRange(new System.Windows.Forms.ListViewGroup[] {
            listViewGroup1,
            listViewGroup2,
            listViewGroup3,
            listViewGroup4,
            listViewGroup5,
            listViewGroup6});
            this.listView.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.listView.HideSelection = false;
            this.listView.Location = new System.Drawing.Point(13, 12);
            this.listView.MultiSelect = false;
            this.listView.Name = "listView";
            this.listView.Size = new System.Drawing.Size(316, 685);
            this.listView.TabIndex = 354;
            this.listView.UseCompatibleStateImageBehavior = false;
            this.listView.View = System.Windows.Forms.View.Details;
            this.listView.SelectedIndexChanged += new System.EventHandler(this.listView_SelectedIndexChanged);
            // 
            // ItemName
            // 
            this.ItemName.Text = "Item Name";
            this.ItemName.Width = 239;
            // 
            // baseObject
            // 
            this.baseObject.Controls.Add(this.special_slot);
            this.baseObject.Controls.Add(this.label8);
            this.baseObject.Controls.Add(this.crafting_resource);
            this.baseObject.Controls.Add(this.label7);
            this.baseObject.Controls.Add(this.radial_menu_order_index);
            this.baseObject.Controls.Add(this.composite);
            this.baseObject.Controls.Add(this.display_quantity);
            this.baseObject.Controls.Add(this.label2);
            this.baseObject.Controls.Add(this.label6);
            this.baseObject.Controls.Add(this.stack_limit);
            this.baseObject.Controls.Add(this.label5);
            this.baseObject.Controls.Add(this.default_quantity);
            this.baseObject.Controls.Add(this.label4);
            this.baseObject.Controls.Add(this.localisation_tag);
            this.baseObject.Controls.Add(this.vanish_when_collected);
            this.baseObject.Controls.Add(this.label3);
            this.baseObject.Controls.Add(this.keyframe);
            this.baseObject.Controls.Add(this.label1);
            this.baseObject.Controls.Add(this.name);
            this.baseObject.Location = new System.Drawing.Point(335, 12);
            this.baseObject.Name = "baseObject";
            this.baseObject.Size = new System.Drawing.Size(408, 294);
            this.baseObject.TabIndex = 356;
            this.baseObject.TabStop = false;
            this.baseObject.Text = "Object Parameters";
            // 
            // special_slot
            // 
            this.special_slot.Location = new System.Drawing.Point(16, 157);
            this.special_slot.Name = "special_slot";
            this.special_slot.Size = new System.Drawing.Size(374, 20);
            this.special_slot.TabIndex = 17;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(13, 141);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(72, 13);
            this.label8.TabIndex = 18;
            this.label8.Text = "Inventory Slot";
            // 
            // crafting_resource
            // 
            this.crafting_resource.AutoSize = true;
            this.crafting_resource.Location = new System.Drawing.Point(279, 261);
            this.crafting_resource.Name = "crafting_resource";
            this.crafting_resource.Size = new System.Drawing.Size(111, 17);
            this.crafting_resource.TabIndex = 16;
            this.crafting_resource.Text = "Crafting Resource";
            this.crafting_resource.UseVisualStyleBackColor = true;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(269, 219);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(66, 13);
            this.label7.TabIndex = 15;
            this.label7.Text = "Radial Index";
            // 
            // radial_menu_order_index
            // 
            this.radial_menu_order_index.Location = new System.Drawing.Point(272, 235);
            this.radial_menu_order_index.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.radial_menu_order_index.Name = "radial_menu_order_index";
            this.radial_menu_order_index.Size = new System.Drawing.Size(115, 20);
            this.radial_menu_order_index.TabIndex = 14;
            // 
            // composite
            // 
            this.composite.Location = new System.Drawing.Point(16, 118);
            this.composite.Name = "composite";
            this.composite.Size = new System.Drawing.Size(374, 20);
            this.composite.TabIndex = 2;
            // 
            // display_quantity
            // 
            this.display_quantity.AutoSize = true;
            this.display_quantity.Location = new System.Drawing.Point(171, 261);
            this.display_quantity.Name = "display_quantity";
            this.display_quantity.Size = new System.Drawing.Size(102, 17);
            this.display_quantity.TabIndex = 13;
            this.display_quantity.Text = "Display Quantity";
            this.display_quantity.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 102);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(123, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Pickup Composite Name";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(141, 219);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(75, 13);
            this.label6.TabIndex = 12;
            this.label6.Text = "Inventory Limit";
            // 
            // stack_limit
            // 
            this.stack_limit.Location = new System.Drawing.Point(144, 235);
            this.stack_limit.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.stack_limit.Name = "stack_limit";
            this.stack_limit.Size = new System.Drawing.Size(115, 20);
            this.stack_limit.TabIndex = 11;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(13, 219);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(82, 13);
            this.label5.TabIndex = 10;
            this.label5.Text = "Pickup Quantity";
            // 
            // default_quantity
            // 
            this.default_quantity.Location = new System.Drawing.Point(16, 235);
            this.default_quantity.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.default_quantity.Name = "default_quantity";
            this.default_quantity.Size = new System.Drawing.Size(115, 20);
            this.default_quantity.TabIndex = 9;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(13, 63);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(118, 13);
            this.label4.TabIndex = 8;
            this.label4.Text = "Localised Name (for UI)";
            // 
            // localisation_tag
            // 
            this.localisation_tag.Location = new System.Drawing.Point(16, 79);
            this.localisation_tag.Name = "localisation_tag";
            this.localisation_tag.Size = new System.Drawing.Size(374, 20);
            this.localisation_tag.TabIndex = 7;
            // 
            // vanish_when_collected
            // 
            this.vanish_when_collected.AutoSize = true;
            this.vanish_when_collected.Location = new System.Drawing.Point(16, 261);
            this.vanish_when_collected.Name = "vanish_when_collected";
            this.vanish_when_collected.Size = new System.Drawing.Size(149, 17);
            this.vanish_when_collected.TabIndex = 6;
            this.vanish_when_collected.Text = "Pickup Adds To Inventory";
            this.vanish_when_collected.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(13, 180);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(79, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "Flash Keyframe";
            // 
            // keyframe
            // 
            this.keyframe.Location = new System.Drawing.Point(16, 196);
            this.keyframe.Name = "keyframe";
            this.keyframe.Size = new System.Drawing.Size(374, 20);
            this.keyframe.TabIndex = 4;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 24);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(58, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Item Name";
            // 
            // name
            // 
            this.name.Location = new System.Drawing.Point(16, 40);
            this.name.Name = "name";
            this.name.Size = new System.Drawing.Size(374, 20);
            this.name.TabIndex = 0;
            // 
            // weapon
            // 
            this.weapon.Controls.Add(this.label9);
            this.weapon.Controls.Add(this.weapon_type);
            this.weapon.Location = new System.Drawing.Point(335, 312);
            this.weapon.Name = "weapon";
            this.weapon.Size = new System.Drawing.Size(408, 78);
            this.weapon.TabIndex = 357;
            this.weapon.TabStop = false;
            this.weapon.Text = "Weapon Parameters";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(13, 24);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(75, 13);
            this.label9.TabIndex = 19;
            this.label9.Text = "Weapon Type";
            // 
            // weapon_type
            // 
            this.weapon_type.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.weapon_type.FormattingEnabled = true;
            this.weapon_type.Location = new System.Drawing.Point(16, 40);
            this.weapon_type.Name = "weapon_type";
            this.weapon_type.Size = new System.Drawing.Size(374, 21);
            this.weapon_type.TabIndex = 0;
            // 
            // ammo
            // 
            this.ammo.Controls.Add(this.label11);
            this.ammo.Controls.Add(this.ammo_type);
            this.ammo.Controls.Add(this.label10);
            this.ammo.Controls.Add(this.target_weapon);
            this.ammo.Location = new System.Drawing.Point(335, 312);
            this.ammo.Name = "ammo";
            this.ammo.Size = new System.Drawing.Size(408, 120);
            this.ammo.TabIndex = 358;
            this.ammo.TabStop = false;
            this.ammo.Text = "Ammo Parameters";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(13, 64);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(63, 13);
            this.label11.TabIndex = 21;
            this.label11.Text = "Ammo Type";
            // 
            // ammo_type
            // 
            this.ammo_type.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ammo_type.FormattingEnabled = true;
            this.ammo_type.Location = new System.Drawing.Point(16, 80);
            this.ammo_type.Name = "ammo_type";
            this.ammo_type.Size = new System.Drawing.Size(374, 21);
            this.ammo_type.TabIndex = 20;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(13, 24);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(82, 13);
            this.label10.TabIndex = 19;
            this.label10.Text = "Target Weapon";
            // 
            // target_weapon
            // 
            this.target_weapon.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.target_weapon.FormattingEnabled = true;
            this.target_weapon.Location = new System.Drawing.Point(16, 40);
            this.target_weapon.Name = "target_weapon";
            this.target_weapon.Size = new System.Drawing.Size(374, 21);
            this.target_weapon.TabIndex = 0;
            // 
            // held
            // 
            this.held.Controls.Add(this.label12);
            this.held.Controls.Add(this.activated_by);
            this.held.Controls.Add(this.label14);
            this.held.Controls.Add(this.consume_when);
            this.held.Controls.Add(this.drop_when_consume);
            this.held.Controls.Add(this.droppable_when_held);
            this.held.Controls.Add(this.label16);
            this.held.Controls.Add(this.cancellable_duration_in_seconds);
            this.held.Controls.Add(this.label17);
            this.held.Controls.Add(this.thrown_object_name);
            this.held.Controls.Add(this.label19);
            this.held.Controls.Add(this.held_object_name);
            this.held.Location = new System.Drawing.Point(335, 312);
            this.held.Name = "held";
            this.held.Size = new System.Drawing.Size(408, 261);
            this.held.TabIndex = 357;
            this.held.TabStop = false;
            this.held.Text = "Held Parameters";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(13, 165);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(67, 13);
            this.label12.TabIndex = 25;
            this.label12.Text = "Activated By";
            // 
            // activated_by
            // 
            this.activated_by.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.activated_by.FormattingEnabled = true;
            this.activated_by.Items.AddRange(new object[] {
            "CannotBeActivated",
            "PressToActivate",
            "HoldToActivate"});
            this.activated_by.Location = new System.Drawing.Point(16, 181);
            this.activated_by.Name = "activated_by";
            this.activated_by.Size = new System.Drawing.Size(374, 21);
            this.activated_by.TabIndex = 24;
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(13, 125);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(89, 13);
            this.label14.TabIndex = 23;
            this.label14.Text = "Consumed When";
            // 
            // consume_when
            // 
            this.consume_when.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.consume_when.FormattingEnabled = true;
            this.consume_when.Items.AddRange(new object[] {
            "Never",
            "OnEquip",
            "OnActivate",
            "OnThrowOrPlace"});
            this.consume_when.Location = new System.Drawing.Point(16, 141);
            this.consume_when.Name = "consume_when";
            this.consume_when.Size = new System.Drawing.Size(374, 21);
            this.consume_when.TabIndex = 22;
            // 
            // drop_when_consume
            // 
            this.drop_when_consume.AutoSize = true;
            this.drop_when_consume.Location = new System.Drawing.Point(154, 105);
            this.drop_when_consume.Name = "drop_when_consume";
            this.drop_when_consume.Size = new System.Drawing.Size(134, 17);
            this.drop_when_consume.TabIndex = 17;
            this.drop_when_consume.Text = "Drop When Consumed";
            this.drop_when_consume.UseVisualStyleBackColor = true;
            // 
            // droppable_when_held
            // 
            this.droppable_when_held.AutoSize = true;
            this.droppable_when_held.Location = new System.Drawing.Point(16, 105);
            this.droppable_when_held.Name = "droppable_when_held";
            this.droppable_when_held.Size = new System.Drawing.Size(132, 17);
            this.droppable_when_held.TabIndex = 16;
            this.droppable_when_held.Text = "Droppable When Held";
            this.droppable_when_held.UseVisualStyleBackColor = true;
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(13, 205);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(195, 13);
            this.label16.TabIndex = 10;
            this.label16.Text = "Can Cancel Activation Before (seconds)";
            // 
            // cancellable_duration_in_seconds
            // 
            this.cancellable_duration_in_seconds.DecimalPlaces = 2;
            this.cancellable_duration_in_seconds.Location = new System.Drawing.Point(16, 221);
            this.cancellable_duration_in_seconds.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.cancellable_duration_in_seconds.Name = "cancellable_duration_in_seconds";
            this.cancellable_duration_in_seconds.Size = new System.Drawing.Size(374, 20);
            this.cancellable_duration_in_seconds.TabIndex = 9;
            // 
            // label17
            // 
            this.label17.AutoSize = true;
            this.label17.Location = new System.Drawing.Point(13, 63);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(126, 13);
            this.label17.TabIndex = 8;
            this.label17.Text = "Thrown Composite Name";
            // 
            // thrown_object_name
            // 
            this.thrown_object_name.Location = new System.Drawing.Point(16, 79);
            this.thrown_object_name.Name = "thrown_object_name";
            this.thrown_object_name.Size = new System.Drawing.Size(374, 20);
            this.thrown_object_name.TabIndex = 7;
            // 
            // label19
            // 
            this.label19.AutoSize = true;
            this.label19.Location = new System.Drawing.Point(13, 24);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(112, 13);
            this.label19.TabIndex = 1;
            this.label19.Text = "Held Composite Name";
            // 
            // held_object_name
            // 
            this.held_object_name.Location = new System.Drawing.Point(16, 40);
            this.held_object_name.Name = "held_object_name";
            this.held_object_name.Size = new System.Drawing.Size(374, 20);
            this.held_object_name.TabIndex = 0;
            // 
            // medikit
            // 
            this.medikit.Controls.Add(this.label13);
            this.medikit.Controls.Add(this.upgraded_health_increase_percentage);
            this.medikit.Controls.Add(this.label18);
            this.medikit.Controls.Add(this.health_increase_percentage);
            this.medikit.Location = new System.Drawing.Point(335, 579);
            this.medikit.Name = "medikit";
            this.medikit.Size = new System.Drawing.Size(408, 118);
            this.medikit.TabIndex = 358;
            this.medikit.TabStop = false;
            this.medikit.Text = "Medikit Parameters";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(13, 63);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(176, 13);
            this.label13.TabIndex = 12;
            this.label13.Text = "Health Increase % (when upgraded)";
            // 
            // upgraded_health_increase_percentage
            // 
            this.upgraded_health_increase_percentage.Location = new System.Drawing.Point(16, 79);
            this.upgraded_health_increase_percentage.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.upgraded_health_increase_percentage.Name = "upgraded_health_increase_percentage";
            this.upgraded_health_increase_percentage.Size = new System.Drawing.Size(374, 20);
            this.upgraded_health_increase_percentage.TabIndex = 11;
            // 
            // label18
            // 
            this.label18.AutoSize = true;
            this.label18.Location = new System.Drawing.Point(13, 24);
            this.label18.Name = "label18";
            this.label18.Size = new System.Drawing.Size(93, 13);
            this.label18.TabIndex = 10;
            this.label18.Text = "Health Increase %";
            // 
            // health_increase_percentage
            // 
            this.health_increase_percentage.Location = new System.Drawing.Point(16, 40);
            this.health_increase_percentage.Maximum = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.health_increase_percentage.Name = "health_increase_percentage";
            this.health_increase_percentage.Size = new System.Drawing.Size(374, 20);
            this.health_increase_percentage.TabIndex = 9;
            // 
            // addItemBtn
            // 
            this.addItemBtn.Location = new System.Drawing.Point(13, 703);
            this.addItemBtn.Name = "addItemBtn";
            this.addItemBtn.Size = new System.Drawing.Size(316, 23);
            this.addItemBtn.TabIndex = 355;
            this.addItemBtn.Text = "Add New Item";
            this.addItemBtn.UseVisualStyleBackColor = true;
            this.addItemBtn.Click += new System.EventHandler(this.addItemBtn_Click);
            // 
            // helpBtn
            // 
            this.helpBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.helpBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.helpBtn.Image = ((System.Drawing.Image)(resources.GetObject("helpBtn.Image")));
            this.helpBtn.Location = new System.Drawing.Point(734, 0);
            this.helpBtn.Name = "helpBtn";
            this.helpBtn.Size = new System.Drawing.Size(20, 20);
            this.helpBtn.TabIndex = 373;
            this.helpBtn.UseVisualStyleBackColor = true;
            this.helpBtn.Click += new System.EventHandler(this.helpBtn_Click);
            // 
            // InventoryItemEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(754, 739);
            this.Controls.Add(this.helpBtn);
            this.Controls.Add(this.addItemBtn);
            this.Controls.Add(this.medikit);
            this.Controls.Add(this.held);
            this.Controls.Add(this.ammo);
            this.Controls.Add(this.weapon);
            this.Controls.Add(this.baseObject);
            this.Controls.Add(this.listView);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "InventoryItemEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Item and Inventory Editor";
            this.baseObject.ResumeLayout(false);
            this.baseObject.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.radial_menu_order_index)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.stack_limit)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.default_quantity)).EndInit();
            this.weapon.ResumeLayout(false);
            this.weapon.PerformLayout();
            this.ammo.ResumeLayout(false);
            this.ammo.PerformLayout();
            this.held.ResumeLayout(false);
            this.held.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.cancellable_duration_in_seconds)).EndInit();
            this.medikit.ResumeLayout(false);
            this.medikit.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.upgraded_health_increase_percentage)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.health_increase_percentage)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.ListView listView;
        private System.Windows.Forms.ColumnHeader ItemName;
        private System.Windows.Forms.GroupBox baseObject;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox composite;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox name;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox keyframe;
        private System.Windows.Forms.CheckBox vanish_when_collected;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox localisation_tag;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.NumericUpDown default_quantity;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.NumericUpDown stack_limit;
        private System.Windows.Forms.CheckBox display_quantity;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.NumericUpDown radial_menu_order_index;
        private System.Windows.Forms.CheckBox crafting_resource;
        private System.Windows.Forms.TextBox special_slot;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.GroupBox weapon;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.ComboBox weapon_type;
        private System.Windows.Forms.GroupBox ammo;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.ComboBox target_weapon;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.ComboBox ammo_type;
        private System.Windows.Forms.GroupBox held;
        private System.Windows.Forms.CheckBox droppable_when_held;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.NumericUpDown cancellable_duration_in_seconds;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.TextBox thrown_object_name;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.TextBox held_object_name;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.ComboBox activated_by;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.ComboBox consume_when;
        private System.Windows.Forms.CheckBox drop_when_consume;
        private System.Windows.Forms.GroupBox medikit;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.NumericUpDown upgraded_health_increase_percentage;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.NumericUpDown health_increase_percentage;
        private System.Windows.Forms.Button addItemBtn;
        private System.Windows.Forms.Button helpBtn;
    }
}
