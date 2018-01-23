namespace Alien_Isolation_Mod_Tools
{
    partial class InventoryLoot
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(InventoryLoot));
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.Inv_Objects = new System.Windows.Forms.ListBox();
            this.label78 = new System.Windows.Forms.Label();
            this.terminate_on_load_completed = new System.Windows.Forms.ComboBox();
            this.label21 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.edit_objects = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.edit_weapons = new System.Windows.Forms.Button();
            this.Inv_Weapons = new System.Windows.Forms.ListBox();
            this.label3 = new System.Windows.Forms.Label();
            this.edit_ammo = new System.Windows.Forms.Button();
            this.Inv_Ammo = new System.Windows.Forms.ListBox();
            this.label4 = new System.Windows.Forms.Label();
            this.edit_medikit = new System.Windows.Forms.Button();
            this.Inv_MedKit = new System.Windows.Forms.ListBox();
            this.label5 = new System.Windows.Forms.Label();
            this.edit_ied = new System.Windows.Forms.Button();
            this.Inv_IED = new System.Windows.Forms.ListBox();
            this.label6 = new System.Windows.Forms.Label();
            this.edit_light = new System.Windows.Forms.Button();
            this.Inv_Lights = new System.Windows.Forms.ListBox();
            this.btnSave = new System.Windows.Forms.Button();
            this.label29 = new System.Windows.Forms.Label();
            this.Light_meter_dark_level = new System.Windows.Forms.TextBox();
            this.testDev = new System.Windows.Forms.TextBox();
            this.groupBox3.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.label6);
            this.groupBox3.Controls.Add(this.edit_light);
            this.groupBox3.Controls.Add(this.Inv_Lights);
            this.groupBox3.Controls.Add(this.label5);
            this.groupBox3.Controls.Add(this.edit_ied);
            this.groupBox3.Controls.Add(this.Inv_IED);
            this.groupBox3.Controls.Add(this.label4);
            this.groupBox3.Controls.Add(this.edit_medikit);
            this.groupBox3.Controls.Add(this.Inv_MedKit);
            this.groupBox3.Controls.Add(this.label3);
            this.groupBox3.Controls.Add(this.edit_ammo);
            this.groupBox3.Controls.Add(this.Inv_Ammo);
            this.groupBox3.Controls.Add(this.label2);
            this.groupBox3.Controls.Add(this.edit_weapons);
            this.groupBox3.Controls.Add(this.Inv_Weapons);
            this.groupBox3.Controls.Add(this.label1);
            this.groupBox3.Controls.Add(this.edit_objects);
            this.groupBox3.Controls.Add(this.Inv_Objects);
            this.groupBox3.Location = new System.Drawing.Point(12, 41);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(519, 393);
            this.groupBox3.TabIndex = 353;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Inventory Items";
            // 
            // Inv_Objects
            // 
            this.Inv_Objects.FormattingEnabled = true;
            this.Inv_Objects.Location = new System.Drawing.Point(6, 37);
            this.Inv_Objects.Name = "Inv_Objects";
            this.Inv_Objects.Size = new System.Drawing.Size(165, 134);
            this.Inv_Objects.TabIndex = 348;
            // 
            // label78
            // 
            this.label78.AutoSize = true;
            this.label78.Font = new System.Drawing.Font("Microsoft Sans Serif", 17F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label78.Location = new System.Drawing.Point(49, 9);
            this.label78.Name = "label78";
            this.label78.Size = new System.Drawing.Size(439, 29);
            this.label78.TabIndex = 351;
            this.label78.Text = "Alien: Isolation Item and Inventory Editor";
            // 
            // terminate_on_load_completed
            // 
            this.terminate_on_load_completed.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.terminate_on_load_completed.Enabled = false;
            this.terminate_on_load_completed.FormattingEnabled = true;
            this.terminate_on_load_completed.Items.AddRange(new object[] {
            "true",
            "false"});
            this.terminate_on_load_completed.Location = new System.Drawing.Point(313, 258);
            this.terminate_on_load_completed.Name = "terminate_on_load_completed";
            this.terminate_on_load_completed.Size = new System.Drawing.Size(187, 21);
            this.terminate_on_load_completed.TabIndex = 354;
            // 
            // label21
            // 
            this.label21.AutoSize = true;
            this.label21.Location = new System.Drawing.Point(310, 242);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(182, 13);
            this.label21.TabIndex = 353;
            this.label21.Text = "End Playlist When Loading Finished?";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.testDev);
            this.groupBox1.Controls.Add(this.label29);
            this.groupBox1.Controls.Add(this.Light_meter_dark_level);
            this.groupBox1.Controls.Add(this.label21);
            this.groupBox1.Controls.Add(this.terminate_on_load_completed);
            this.groupBox1.Location = new System.Drawing.Point(12, 440);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(519, 329);
            this.groupBox1.TabIndex = 352;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Item Attributes";
            // 
            // edit_objects
            // 
            this.edit_objects.Location = new System.Drawing.Point(6, 177);
            this.edit_objects.Name = "edit_objects";
            this.edit_objects.Size = new System.Drawing.Size(165, 23);
            this.edit_objects.TabIndex = 356;
            this.edit_objects.Text = "Edit Selected";
            this.edit_objects.UseVisualStyleBackColor = true;
            this.edit_objects.Click += new System.EventHandler(this.edit_objects_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 21);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(43, 13);
            this.label1.TabIndex = 362;
            this.label1.Text = "Objects";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(177, 21);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(53, 13);
            this.label2.TabIndex = 365;
            this.label2.Text = "Weapons";
            // 
            // edit_weapons
            // 
            this.edit_weapons.Location = new System.Drawing.Point(177, 177);
            this.edit_weapons.Name = "edit_weapons";
            this.edit_weapons.Size = new System.Drawing.Size(165, 23);
            this.edit_weapons.TabIndex = 364;
            this.edit_weapons.Text = "Edit Selected";
            this.edit_weapons.UseVisualStyleBackColor = true;
            this.edit_weapons.Click += new System.EventHandler(this.edit_weapons_Click);
            // 
            // Inv_Weapons
            // 
            this.Inv_Weapons.FormattingEnabled = true;
            this.Inv_Weapons.Location = new System.Drawing.Point(177, 37);
            this.Inv_Weapons.Name = "Inv_Weapons";
            this.Inv_Weapons.Size = new System.Drawing.Size(165, 134);
            this.Inv_Weapons.TabIndex = 363;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(348, 21);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(68, 13);
            this.label3.TabIndex = 368;
            this.label3.Text = "Ammo Types";
            // 
            // edit_ammo
            // 
            this.edit_ammo.Location = new System.Drawing.Point(348, 177);
            this.edit_ammo.Name = "edit_ammo";
            this.edit_ammo.Size = new System.Drawing.Size(165, 23);
            this.edit_ammo.TabIndex = 367;
            this.edit_ammo.Text = "Edit Selected";
            this.edit_ammo.UseVisualStyleBackColor = true;
            this.edit_ammo.Click += new System.EventHandler(this.edit_ammo_Click);
            // 
            // Inv_Ammo
            // 
            this.Inv_Ammo.FormattingEnabled = true;
            this.Inv_Ammo.Location = new System.Drawing.Point(348, 37);
            this.Inv_Ammo.Name = "Inv_Ammo";
            this.Inv_Ammo.Size = new System.Drawing.Size(165, 134);
            this.Inv_Ammo.TabIndex = 366;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(6, 208);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(46, 13);
            this.label4.TabIndex = 371;
            this.label4.Text = "Medikits";
            // 
            // edit_medikit
            // 
            this.edit_medikit.Location = new System.Drawing.Point(6, 364);
            this.edit_medikit.Name = "edit_medikit";
            this.edit_medikit.Size = new System.Drawing.Size(165, 23);
            this.edit_medikit.TabIndex = 370;
            this.edit_medikit.Text = "Edit Selected";
            this.edit_medikit.UseVisualStyleBackColor = true;
            this.edit_medikit.Click += new System.EventHandler(this.edit_medikit_Click);
            // 
            // Inv_MedKit
            // 
            this.Inv_MedKit.FormattingEnabled = true;
            this.Inv_MedKit.Location = new System.Drawing.Point(6, 224);
            this.Inv_MedKit.Name = "Inv_MedKit";
            this.Inv_MedKit.Size = new System.Drawing.Size(165, 134);
            this.Inv_MedKit.TabIndex = 369;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(177, 208);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(57, 13);
            this.label5.TabIndex = 374;
            this.label5.Text = "Explosives";
            // 
            // edit_ied
            // 
            this.edit_ied.Location = new System.Drawing.Point(177, 364);
            this.edit_ied.Name = "edit_ied";
            this.edit_ied.Size = new System.Drawing.Size(165, 23);
            this.edit_ied.TabIndex = 373;
            this.edit_ied.Text = "Edit Selected";
            this.edit_ied.UseVisualStyleBackColor = true;
            this.edit_ied.Click += new System.EventHandler(this.edit_ied_Click);
            // 
            // Inv_IED
            // 
            this.Inv_IED.FormattingEnabled = true;
            this.Inv_IED.Location = new System.Drawing.Point(177, 224);
            this.Inv_IED.Name = "Inv_IED";
            this.Inv_IED.Size = new System.Drawing.Size(165, 134);
            this.Inv_IED.TabIndex = 372;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(348, 208);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(70, 13);
            this.label6.TabIndex = 377;
            this.label6.Text = "Light Emitters";
            // 
            // edit_light
            // 
            this.edit_light.Location = new System.Drawing.Point(348, 364);
            this.edit_light.Name = "edit_light";
            this.edit_light.Size = new System.Drawing.Size(165, 23);
            this.edit_light.TabIndex = 376;
            this.edit_light.Text = "Edit Selected";
            this.edit_light.UseVisualStyleBackColor = true;
            this.edit_light.Click += new System.EventHandler(this.edit_light_Click);
            // 
            // Inv_Lights
            // 
            this.Inv_Lights.FormattingEnabled = true;
            this.Inv_Lights.Location = new System.Drawing.Point(348, 224);
            this.Inv_Lights.Name = "Inv_Lights";
            this.Inv_Lights.Size = new System.Drawing.Size(165, 134);
            this.Inv_Lights.TabIndex = 375;
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(381, 775);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(150, 35);
            this.btnSave.TabIndex = 354;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // label29
            // 
            this.label29.AutoSize = true;
            this.label29.Location = new System.Drawing.Point(73, 242);
            this.label29.Name = "label29";
            this.label29.Size = new System.Drawing.Size(85, 13);
            this.label29.TabIndex = 361;
            this.label29.Text = "Dark Light Level";
            // 
            // Light_meter_dark_level
            // 
            this.Light_meter_dark_level.Enabled = false;
            this.Light_meter_dark_level.Location = new System.Drawing.Point(76, 258);
            this.Light_meter_dark_level.Name = "Light_meter_dark_level";
            this.Light_meter_dark_level.Size = new System.Drawing.Size(187, 20);
            this.Light_meter_dark_level.TabIndex = 362;
            // 
            // testDev
            // 
            this.testDev.Location = new System.Drawing.Point(6, 19);
            this.testDev.Multiline = true;
            this.testDev.Name = "testDev";
            this.testDev.Size = new System.Drawing.Size(494, 199);
            this.testDev.TabIndex = 363;
            // 
            // InventoryLoot
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(541, 822);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.label78);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "InventoryLoot";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Alien: Isolation Item and Inventory Editor";
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button edit_light;
        private System.Windows.Forms.ListBox Inv_Lights;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button edit_ied;
        private System.Windows.Forms.ListBox Inv_IED;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button edit_medikit;
        private System.Windows.Forms.ListBox Inv_MedKit;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button edit_ammo;
        private System.Windows.Forms.ListBox Inv_Ammo;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button edit_weapons;
        private System.Windows.Forms.ListBox Inv_Weapons;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button edit_objects;
        private System.Windows.Forms.ListBox Inv_Objects;
        private System.Windows.Forms.Label label78;
        private System.Windows.Forms.ComboBox terminate_on_load_completed;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Label label29;
        private System.Windows.Forms.TextBox Light_meter_dark_level;
        private System.Windows.Forms.TextBox testDev;
    }
}