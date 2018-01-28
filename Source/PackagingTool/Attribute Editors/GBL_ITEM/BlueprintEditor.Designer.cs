namespace Alien_Isolation_Mod_Tools
{
    partial class BlueprintEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BlueprintEditor));
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.removeInputItem = new System.Windows.Forms.Button();
            this.addNewItemRequired = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.craft_quantity = new System.Windows.Forms.ListBox();
            this.craft_itemname = new System.Windows.Forms.ListBox();
            this.label78 = new System.Windows.Forms.Label();
            this.btnSave = new System.Windows.Forms.Button();
            this.blueprints = new System.Windows.Forms.ComboBox();
            this.btnSelectClass = new System.Windows.Forms.Button();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.removeOutputItem = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.output_quantity = new System.Windows.Forms.ListBox();
            this.output_itemname = new System.Windows.Forms.ListBox();
            this.addNewOutputItem = new System.Windows.Forms.Button();
            this.label5 = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.removeInputItem);
            this.groupBox1.Controls.Add(this.addNewItemRequired);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.craft_quantity);
            this.groupBox1.Controls.Add(this.craft_itemname);
            this.groupBox1.Location = new System.Drawing.Point(10, 72);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(241, 234);
            this.groupBox1.TabIndex = 328;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Blueprint Required Items To Craft";
            // 
            // removeInputItem
            // 
            this.removeInputItem.Enabled = false;
            this.removeInputItem.Location = new System.Drawing.Point(8, 203);
            this.removeInputItem.Name = "removeInputItem";
            this.removeInputItem.Size = new System.Drawing.Size(110, 25);
            this.removeInputItem.TabIndex = 337;
            this.removeInputItem.Text = "Remove Selected";
            this.removeInputItem.UseVisualStyleBackColor = true;
            this.removeInputItem.Click += new System.EventHandler(this.removeInputItem_Click);
            // 
            // addNewItemRequired
            // 
            this.addNewItemRequired.Enabled = false;
            this.addNewItemRequired.Location = new System.Drawing.Point(124, 203);
            this.addNewItemRequired.Name = "addNewItemRequired";
            this.addNewItemRequired.Size = new System.Drawing.Size(109, 25);
            this.addNewItemRequired.TabIndex = 336;
            this.addNewItemRequired.Text = "Add New";
            this.addNewItemRequired.UseVisualStyleBackColor = true;
            this.addNewItemRequired.Click += new System.EventHandler(this.addNewItemRequired_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(171, 21);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(46, 13);
            this.label2.TabIndex = 334;
            this.label2.Text = "Quantity";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(4, 21);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(58, 13);
            this.label1.TabIndex = 333;
            this.label1.Text = "Item Name";
            // 
            // craft_quantity
            // 
            this.craft_quantity.FormattingEnabled = true;
            this.craft_quantity.Location = new System.Drawing.Point(174, 37);
            this.craft_quantity.Name = "craft_quantity";
            this.craft_quantity.Size = new System.Drawing.Size(60, 160);
            this.craft_quantity.TabIndex = 332;
            // 
            // craft_itemname
            // 
            this.craft_itemname.FormattingEnabled = true;
            this.craft_itemname.Location = new System.Drawing.Point(6, 37);
            this.craft_itemname.Name = "craft_itemname";
            this.craft_itemname.Size = new System.Drawing.Size(162, 160);
            this.craft_itemname.TabIndex = 331;
            // 
            // label78
            // 
            this.label78.AutoSize = true;
            this.label78.Font = new System.Drawing.Font("Microsoft Sans Serif", 17F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label78.Location = new System.Drawing.Point(40, 9);
            this.label78.Name = "label78";
            this.label78.Size = new System.Drawing.Size(424, 29);
            this.label78.TabIndex = 327;
            this.label78.Text = "Alien: Isolation Blueprint Recipe Editor";
            // 
            // btnSave
            // 
            this.btnSave.Enabled = false;
            this.btnSave.Location = new System.Drawing.Point(348, 312);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(150, 35);
            this.btnSave.TabIndex = 326;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // blueprints
            // 
            this.blueprints.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.blueprints.Enabled = false;
            this.blueprints.FormattingEnabled = true;
            this.blueprints.Items.AddRange(new object[] {
            "DEFAULTS",
            "THE_PLAYER",
            "ALIEN",
            "ANDROID",
            "CIVILIAN",
            "SECURITY_GUARD",
            "CUTSCENE",
            "FACEHUGGER",
            "SPACESUIT_NPC",
            "RIOT_GUARD",
            "ANDROID_HEAVY",
            "MELEE_HUMAN",
            "INNOCENT",
            "CUTSCENE_ANDROID"});
            this.blueprints.Location = new System.Drawing.Point(10, 44);
            this.blueprints.Name = "blueprints";
            this.blueprints.Size = new System.Drawing.Size(339, 21);
            this.blueprints.TabIndex = 325;
            // 
            // btnSelectClass
            // 
            this.btnSelectClass.Enabled = false;
            this.btnSelectClass.Location = new System.Drawing.Point(355, 43);
            this.btnSelectClass.Name = "btnSelectClass";
            this.btnSelectClass.Size = new System.Drawing.Size(143, 23);
            this.btnSelectClass.TabIndex = 324;
            this.btnSelectClass.Text = "Load Blueprint Recipe";
            this.btnSelectClass.UseVisualStyleBackColor = true;
            this.btnSelectClass.Click += new System.EventHandler(this.btnSelectClass_Click);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.removeOutputItem);
            this.groupBox3.Controls.Add(this.label3);
            this.groupBox3.Controls.Add(this.label4);
            this.groupBox3.Controls.Add(this.output_quantity);
            this.groupBox3.Controls.Add(this.output_itemname);
            this.groupBox3.Controls.Add(this.addNewOutputItem);
            this.groupBox3.Location = new System.Drawing.Point(257, 72);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(241, 234);
            this.groupBox3.TabIndex = 335;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Blueprint Output When Crafted";
            // 
            // removeOutputItem
            // 
            this.removeOutputItem.Enabled = false;
            this.removeOutputItem.Location = new System.Drawing.Point(8, 203);
            this.removeOutputItem.Name = "removeOutputItem";
            this.removeOutputItem.Size = new System.Drawing.Size(110, 25);
            this.removeOutputItem.TabIndex = 335;
            this.removeOutputItem.Text = "Remove Selected";
            this.removeOutputItem.UseVisualStyleBackColor = true;
            this.removeOutputItem.Click += new System.EventHandler(this.removeOutputItem_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(171, 21);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(46, 13);
            this.label3.TabIndex = 334;
            this.label3.Text = "Quantity";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(4, 21);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(58, 13);
            this.label4.TabIndex = 333;
            this.label4.Text = "Item Name";
            // 
            // output_quantity
            // 
            this.output_quantity.FormattingEnabled = true;
            this.output_quantity.Location = new System.Drawing.Point(174, 37);
            this.output_quantity.Name = "output_quantity";
            this.output_quantity.Size = new System.Drawing.Size(60, 160);
            this.output_quantity.TabIndex = 332;
            // 
            // output_itemname
            // 
            this.output_itemname.FormattingEnabled = true;
            this.output_itemname.Location = new System.Drawing.Point(6, 37);
            this.output_itemname.Name = "output_itemname";
            this.output_itemname.Size = new System.Drawing.Size(162, 160);
            this.output_itemname.TabIndex = 331;
            // 
            // addNewOutputItem
            // 
            this.addNewOutputItem.Enabled = false;
            this.addNewOutputItem.Location = new System.Drawing.Point(124, 203);
            this.addNewOutputItem.Name = "addNewOutputItem";
            this.addNewOutputItem.Size = new System.Drawing.Size(109, 25);
            this.addNewOutputItem.TabIndex = 330;
            this.addNewOutputItem.Text = "Add New";
            this.addNewOutputItem.UseVisualStyleBackColor = true;
            this.addNewOutputItem.Click += new System.EventHandler(this.button3_Click);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(14, 315);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(299, 26);
            this.label5.TabIndex = 336;
            this.label5.Text = "It\'s recommended to stick to existing item input requirements\r\nas the UI will nee" +
    "d to be modded to support new components.\r\n";
            // 
            // BlueprintEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(504, 353);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.label78);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.blueprints);
            this.Controls.Add(this.btnSelectClass);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "BlueprintEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Alien: Isolation Blueprint Recipe Editor";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label78;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.ComboBox blueprints;
        private System.Windows.Forms.Button btnSelectClass;
        private System.Windows.Forms.Button removeInputItem;
        private System.Windows.Forms.Button addNewItemRequired;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ListBox craft_quantity;
        private System.Windows.Forms.ListBox craft_itemname;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Button removeOutputItem;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ListBox output_quantity;
        private System.Windows.Forms.ListBox output_itemname;
        private System.Windows.Forms.Button addNewOutputItem;
        private System.Windows.Forms.Label label5;
    }
}