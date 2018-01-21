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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label78 = new System.Windows.Forms.Label();
            this.btnSave = new System.Windows.Forms.Button();
            this.blueprints = new System.Windows.Forms.ComboBox();
            this.btnSelectClass = new System.Windows.Forms.Button();
            this.craft_itemname = new System.Windows.Forms.ListBox();
            this.craft_quantity = new System.Windows.Forms.ListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.output_quantity = new System.Windows.Forms.ListBox();
            this.output_itemname = new System.Windows.Forms.ListBox();
            this.button3 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.button4 = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.button1);
            this.groupBox1.Controls.Add(this.button4);
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
            this.btnSave.Location = new System.Drawing.Point(348, 312);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(150, 35);
            this.btnSave.TabIndex = 326;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
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
            // craft_itemname
            // 
            this.craft_itemname.FormattingEnabled = true;
            this.craft_itemname.Location = new System.Drawing.Point(6, 37);
            this.craft_itemname.Name = "craft_itemname";
            this.craft_itemname.Size = new System.Drawing.Size(162, 160);
            this.craft_itemname.TabIndex = 331;
            // 
            // craft_quantity
            // 
            this.craft_quantity.FormattingEnabled = true;
            this.craft_quantity.Location = new System.Drawing.Point(174, 37);
            this.craft_quantity.Name = "craft_quantity";
            this.craft_quantity.Size = new System.Drawing.Size(60, 160);
            this.craft_quantity.TabIndex = 332;
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
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(171, 21);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(46, 13);
            this.label2.TabIndex = 334;
            this.label2.Text = "Quantity";
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.button2);
            this.groupBox3.Controls.Add(this.label3);
            this.groupBox3.Controls.Add(this.label4);
            this.groupBox3.Controls.Add(this.output_quantity);
            this.groupBox3.Controls.Add(this.output_itemname);
            this.groupBox3.Controls.Add(this.button3);
            this.groupBox3.Location = new System.Drawing.Point(257, 72);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(241, 234);
            this.groupBox3.TabIndex = 335;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Blueprint Output When Crafted";
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
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(124, 203);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(109, 25);
            this.button3.TabIndex = 330;
            this.button3.Text = "Add New";
            this.button3.UseVisualStyleBackColor = true;
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(8, 203);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(110, 25);
            this.button2.TabIndex = 335;
            this.button2.Text = "Remove Selected";
            this.button2.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(8, 203);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(110, 25);
            this.button1.TabIndex = 337;
            this.button1.Text = "Remove Selected";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // button4
            // 
            this.button4.Location = new System.Drawing.Point(124, 203);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(109, 25);
            this.button4.TabIndex = 336;
            this.button4.Text = "Add New";
            this.button4.UseVisualStyleBackColor = true;
            // 
            // BlueprintEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(504, 353);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.label78);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.blueprints);
            this.Controls.Add(this.btnSelectClass);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "BlueprintEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Alien: Isolation Blueprint Recipe Edtor";
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
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button4;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ListBox craft_quantity;
        private System.Windows.Forms.ListBox craft_itemname;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ListBox output_quantity;
        private System.Windows.Forms.ListBox output_itemname;
        private System.Windows.Forms.Button button3;
    }
}