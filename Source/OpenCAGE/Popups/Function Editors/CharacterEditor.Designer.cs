namespace CommandsEditor
{
    partial class CharacterEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CharacterEditor));
            this.shirtComposite = new System.Windows.Forms.TextBox();
            this.selectNewShirt = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.selectNewTrousers = new System.Windows.Forms.Button();
            this.trousersComposite = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.selectNewShoes = new System.Windows.Forms.Button();
            this.shoesComposite = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.selectNewHead = new System.Windows.Forms.Button();
            this.headComposite = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.selectNewArms = new System.Windows.Forms.Button();
            this.armsComposite = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.selectNewCollision = new System.Windows.Forms.Button();
            this.collisionComposite = new System.Windows.Forms.TextBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label15 = new System.Windows.Forms.Label();
            this.foleyFootwear = new System.Windows.Forms.ComboBox();
            this.label14 = new System.Windows.Forms.Label();
            this.foleyLeg = new System.Windows.Forms.ComboBox();
            this.label13 = new System.Windows.Forms.Label();
            this.buildAttr = new System.Windows.Forms.ComboBox();
            this.label12 = new System.Windows.Forms.Label();
            this.foleyTorso = new System.Windows.Forms.ComboBox();
            this.label11 = new System.Windows.Forms.Label();
            this.ethnicityAttr = new System.Windows.Forms.ComboBox();
            this.label10 = new System.Windows.Forms.Label();
            this.genderAttr = new System.Windows.Forms.ComboBox();
            this.label9 = new System.Windows.Forms.Label();
            this.voiceActor = new System.Windows.Forms.ComboBox();
            this.gender = new System.Windows.Forms.ComboBox();
            this.label8 = new System.Windows.Forms.Label();
            this.assetType = new System.Windows.Forms.ComboBox();
            this.label7 = new System.Windows.Forms.Label();
            this.bodyTypes = new System.Windows.Forms.ComboBox();
            this.characterInstances = new System.Windows.Forms.ComboBox();
            this.addNewCharacter = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.editAssetTypes = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // shirtComposite
            // 
            this.shirtComposite.Location = new System.Drawing.Point(19, 82);
            this.shirtComposite.Name = "shirtComposite";
            this.shirtComposite.ReadOnly = true;
            this.shirtComposite.Size = new System.Drawing.Size(514, 20);
            this.shirtComposite.TabIndex = 0;
            // 
            // selectNewShirt
            // 
            this.selectNewShirt.Location = new System.Drawing.Point(539, 80);
            this.selectNewShirt.Name = "selectNewShirt";
            this.selectNewShirt.Size = new System.Drawing.Size(94, 23);
            this.selectNewShirt.TabIndex = 1;
            this.selectNewShirt.Text = "Change";
            this.selectNewShirt.UseVisualStyleBackColor = true;
            this.selectNewShirt.Click += new System.EventHandler(this.selectNewShirt_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(19, 66);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(34, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Torso";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(19, 144);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(30, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "Legs";
            // 
            // selectNewTrousers
            // 
            this.selectNewTrousers.Location = new System.Drawing.Point(539, 158);
            this.selectNewTrousers.Name = "selectNewTrousers";
            this.selectNewTrousers.Size = new System.Drawing.Size(94, 23);
            this.selectNewTrousers.TabIndex = 4;
            this.selectNewTrousers.Text = "Change";
            this.selectNewTrousers.UseVisualStyleBackColor = true;
            this.selectNewTrousers.Click += new System.EventHandler(this.selectNewTrousers_Click);
            // 
            // trousersComposite
            // 
            this.trousersComposite.Location = new System.Drawing.Point(19, 160);
            this.trousersComposite.Name = "trousersComposite";
            this.trousersComposite.ReadOnly = true;
            this.trousersComposite.Size = new System.Drawing.Size(514, 20);
            this.trousersComposite.TabIndex = 3;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(19, 183);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(37, 13);
            this.label3.TabIndex = 8;
            this.label3.Text = "Shoes";
            // 
            // selectNewShoes
            // 
            this.selectNewShoes.Location = new System.Drawing.Point(539, 197);
            this.selectNewShoes.Name = "selectNewShoes";
            this.selectNewShoes.Size = new System.Drawing.Size(94, 23);
            this.selectNewShoes.TabIndex = 7;
            this.selectNewShoes.Text = "Change";
            this.selectNewShoes.UseVisualStyleBackColor = true;
            this.selectNewShoes.Click += new System.EventHandler(this.selectNewShoes_Click);
            // 
            // shoesComposite
            // 
            this.shoesComposite.Location = new System.Drawing.Point(19, 199);
            this.shoesComposite.Name = "shoesComposite";
            this.shoesComposite.ReadOnly = true;
            this.shoesComposite.Size = new System.Drawing.Size(514, 20);
            this.shoesComposite.TabIndex = 6;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(19, 27);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(33, 13);
            this.label4.TabIndex = 11;
            this.label4.Text = "Head";
            // 
            // selectNewHead
            // 
            this.selectNewHead.Location = new System.Drawing.Point(539, 41);
            this.selectNewHead.Name = "selectNewHead";
            this.selectNewHead.Size = new System.Drawing.Size(94, 23);
            this.selectNewHead.TabIndex = 10;
            this.selectNewHead.Text = "Change";
            this.selectNewHead.UseVisualStyleBackColor = true;
            this.selectNewHead.Click += new System.EventHandler(this.selectNewHead_Click);
            // 
            // headComposite
            // 
            this.headComposite.Location = new System.Drawing.Point(19, 43);
            this.headComposite.Name = "headComposite";
            this.headComposite.ReadOnly = true;
            this.headComposite.Size = new System.Drawing.Size(514, 20);
            this.headComposite.TabIndex = 9;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(19, 105);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(30, 13);
            this.label5.TabIndex = 14;
            this.label5.Text = "Arms";
            // 
            // selectNewArms
            // 
            this.selectNewArms.Location = new System.Drawing.Point(539, 119);
            this.selectNewArms.Name = "selectNewArms";
            this.selectNewArms.Size = new System.Drawing.Size(94, 23);
            this.selectNewArms.TabIndex = 13;
            this.selectNewArms.Text = "Change";
            this.selectNewArms.UseVisualStyleBackColor = true;
            this.selectNewArms.Click += new System.EventHandler(this.selectNewArms_Click);
            // 
            // armsComposite
            // 
            this.armsComposite.Location = new System.Drawing.Point(19, 121);
            this.armsComposite.Name = "armsComposite";
            this.armsComposite.ReadOnly = true;
            this.armsComposite.Size = new System.Drawing.Size(514, 20);
            this.armsComposite.TabIndex = 12;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(19, 222);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(45, 13);
            this.label6.TabIndex = 17;
            this.label6.Text = "Collision";
            // 
            // selectNewCollision
            // 
            this.selectNewCollision.Location = new System.Drawing.Point(539, 236);
            this.selectNewCollision.Name = "selectNewCollision";
            this.selectNewCollision.Size = new System.Drawing.Size(94, 23);
            this.selectNewCollision.TabIndex = 16;
            this.selectNewCollision.Text = "Change";
            this.selectNewCollision.UseVisualStyleBackColor = true;
            this.selectNewCollision.Click += new System.EventHandler(this.selectNewCollision_Click);
            // 
            // collisionComposite
            // 
            this.collisionComposite.Location = new System.Drawing.Point(19, 238);
            this.collisionComposite.Name = "collisionComposite";
            this.collisionComposite.ReadOnly = true;
            this.collisionComposite.Size = new System.Drawing.Size(514, 20);
            this.collisionComposite.TabIndex = 15;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.label6);
            this.groupBox1.Controls.Add(this.shirtComposite);
            this.groupBox1.Controls.Add(this.selectNewCollision);
            this.groupBox1.Controls.Add(this.selectNewShirt);
            this.groupBox1.Controls.Add(this.collisionComposite);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.trousersComposite);
            this.groupBox1.Controls.Add(this.selectNewArms);
            this.groupBox1.Controls.Add(this.selectNewTrousers);
            this.groupBox1.Controls.Add(this.armsComposite);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.shoesComposite);
            this.groupBox1.Controls.Add(this.selectNewHead);
            this.groupBox1.Controls.Add(this.selectNewShoes);
            this.groupBox1.Controls.Add(this.headComposite);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Location = new System.Drawing.Point(12, 39);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(654, 280);
            this.groupBox1.TabIndex = 18;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Composites";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.editAssetTypes);
            this.groupBox2.Controls.Add(this.label15);
            this.groupBox2.Controls.Add(this.foleyFootwear);
            this.groupBox2.Controls.Add(this.label14);
            this.groupBox2.Controls.Add(this.foleyLeg);
            this.groupBox2.Controls.Add(this.label13);
            this.groupBox2.Controls.Add(this.buildAttr);
            this.groupBox2.Controls.Add(this.label12);
            this.groupBox2.Controls.Add(this.foleyTorso);
            this.groupBox2.Controls.Add(this.label11);
            this.groupBox2.Controls.Add(this.ethnicityAttr);
            this.groupBox2.Controls.Add(this.label10);
            this.groupBox2.Controls.Add(this.genderAttr);
            this.groupBox2.Controls.Add(this.label9);
            this.groupBox2.Controls.Add(this.voiceActor);
            this.groupBox2.Controls.Add(this.gender);
            this.groupBox2.Controls.Add(this.label8);
            this.groupBox2.Controls.Add(this.assetType);
            this.groupBox2.Controls.Add(this.label7);
            this.groupBox2.Controls.Add(this.bodyTypes);
            this.groupBox2.Location = new System.Drawing.Point(12, 325);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(654, 240);
            this.groupBox2.TabIndex = 19;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Metadata";
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(331, 186);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(82, 13);
            this.label15.TabIndex = 35;
            this.label15.Text = "Foley: Footwear";
            // 
            // foleyFootwear
            // 
            this.foleyFootwear.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.foleyFootwear.FormattingEnabled = true;
            this.foleyFootwear.Location = new System.Drawing.Point(331, 202);
            this.foleyFootwear.Name = "foleyFootwear";
            this.foleyFootwear.Size = new System.Drawing.Size(302, 21);
            this.foleyFootwear.TabIndex = 34;
            this.foleyFootwear.SelectedIndexChanged += new System.EventHandler(this.foleyFootwear_SelectedIndexChanged);
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(19, 186);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(56, 13);
            this.label14.TabIndex = 33;
            this.label14.Text = "Foley: Leg";
            // 
            // foleyLeg
            // 
            this.foleyLeg.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.foleyLeg.FormattingEnabled = true;
            this.foleyLeg.Location = new System.Drawing.Point(19, 202);
            this.foleyLeg.Name = "foleyLeg";
            this.foleyLeg.Size = new System.Drawing.Size(302, 21);
            this.foleyLeg.TabIndex = 32;
            this.foleyLeg.SelectedIndexChanged += new System.EventHandler(this.foleyLeg_SelectedIndexChanged);
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(19, 146);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(30, 13);
            this.label13.TabIndex = 31;
            this.label13.Text = "Build";
            // 
            // buildAttr
            // 
            this.buildAttr.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.buildAttr.FormattingEnabled = true;
            this.buildAttr.Location = new System.Drawing.Point(19, 162);
            this.buildAttr.Name = "buildAttr";
            this.buildAttr.Size = new System.Drawing.Size(302, 21);
            this.buildAttr.TabIndex = 30;
            this.buildAttr.SelectedIndexChanged += new System.EventHandler(this.buildAttr_SelectedIndexChanged);
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(331, 146);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(65, 13);
            this.label12.TabIndex = 29;
            this.label12.Text = "Foley: Torso";
            // 
            // foleyTorso
            // 
            this.foleyTorso.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.foleyTorso.FormattingEnabled = true;
            this.foleyTorso.Location = new System.Drawing.Point(331, 162);
            this.foleyTorso.Name = "foleyTorso";
            this.foleyTorso.Size = new System.Drawing.Size(302, 21);
            this.foleyTorso.TabIndex = 28;
            this.foleyTorso.SelectedIndexChanged += new System.EventHandler(this.foleyTorso_SelectedIndexChanged);
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(331, 106);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(47, 13);
            this.label11.TabIndex = 27;
            this.label11.Text = "Ethnicity";
            // 
            // ethnicityAttr
            // 
            this.ethnicityAttr.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ethnicityAttr.FormattingEnabled = true;
            this.ethnicityAttr.Location = new System.Drawing.Point(331, 122);
            this.ethnicityAttr.Name = "ethnicityAttr";
            this.ethnicityAttr.Size = new System.Drawing.Size(302, 21);
            this.ethnicityAttr.TabIndex = 26;
            this.ethnicityAttr.SelectedIndexChanged += new System.EventHandler(this.ethnicityAttr_SelectedIndexChanged);
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(19, 106);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(42, 13);
            this.label10.TabIndex = 25;
            this.label10.Text = "Gender";
            // 
            // genderAttr
            // 
            this.genderAttr.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.genderAttr.FormattingEnabled = true;
            this.genderAttr.Location = new System.Drawing.Point(19, 122);
            this.genderAttr.Name = "genderAttr";
            this.genderAttr.Size = new System.Drawing.Size(302, 21);
            this.genderAttr.TabIndex = 24;
            this.genderAttr.SelectedIndexChanged += new System.EventHandler(this.genderAttr_SelectedIndexChanged);
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(331, 66);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(62, 13);
            this.label9.TabIndex = 23;
            this.label9.Text = "Voice Actor";
            // 
            // voiceActor
            // 
            this.voiceActor.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.voiceActor.FormattingEnabled = true;
            this.voiceActor.Location = new System.Drawing.Point(331, 82);
            this.voiceActor.Name = "voiceActor";
            this.voiceActor.Size = new System.Drawing.Size(302, 21);
            this.voiceActor.TabIndex = 22;
            this.voiceActor.SelectedIndexChanged += new System.EventHandler(this.voiceActor_SelectedIndexChanged);
            // 
            // gender
            // 
            this.gender.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.gender.FormattingEnabled = true;
            this.gender.Location = new System.Drawing.Point(481, 42);
            this.gender.Name = "gender";
            this.gender.Size = new System.Drawing.Size(152, 21);
            this.gender.TabIndex = 21;
            this.gender.SelectedIndexChanged += new System.EventHandler(this.gender_SelectedIndexChanged);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(19, 66);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(60, 13);
            this.label8.TabIndex = 20;
            this.label8.Text = "Asset Type";
            // 
            // assetType
            // 
            this.assetType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.assetType.FormattingEnabled = true;
            this.assetType.Location = new System.Drawing.Point(19, 82);
            this.assetType.Name = "assetType";
            this.assetType.Size = new System.Drawing.Size(223, 21);
            this.assetType.TabIndex = 19;
            this.assetType.SelectedIndexChanged += new System.EventHandler(this.shirtDecal_SelectedIndexChanged);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(19, 26);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(49, 13);
            this.label7.TabIndex = 18;
            this.label7.Text = "Skeleton";
            // 
            // bodyTypes
            // 
            this.bodyTypes.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.bodyTypes.FormattingEnabled = true;
            this.bodyTypes.Location = new System.Drawing.Point(19, 42);
            this.bodyTypes.Name = "bodyTypes";
            this.bodyTypes.Size = new System.Drawing.Size(456, 21);
            this.bodyTypes.TabIndex = 12;
            this.bodyTypes.SelectedIndexChanged += new System.EventHandler(this.bodyTypes_SelectedIndexChanged);
            // 
            // characterInstances
            // 
            this.characterInstances.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.characterInstances.FormattingEnabled = true;
            this.characterInstances.Location = new System.Drawing.Point(12, 12);
            this.characterInstances.Name = "characterInstances";
            this.characterInstances.Size = new System.Drawing.Size(521, 21);
            this.characterInstances.TabIndex = 15;
            this.characterInstances.SelectedIndexChanged += new System.EventHandler(this.characterInstances_SelectedIndexChanged);
            // 
            // addNewCharacter
            // 
            this.addNewCharacter.Location = new System.Drawing.Point(539, 10);
            this.addNewCharacter.Name = "addNewCharacter";
            this.addNewCharacter.Size = new System.Drawing.Size(127, 23);
            this.addNewCharacter.TabIndex = 23;
            this.addNewCharacter.Text = "Add New";
            this.toolTip1.SetToolTip(this.addNewCharacter, "Add a new Character instance to define NPC components for. Please note: this only" +
        " applies to human NPCs, not story characters or the Alien, which are configured " +
        "differently.");
            this.addNewCharacter.UseVisualStyleBackColor = true;
            this.addNewCharacter.Click += new System.EventHandler(this.addNewCharacter_Click);
            // 
            // editAssetTypes
            // 
            this.editAssetTypes.Location = new System.Drawing.Point(248, 81);
            this.editAssetTypes.Name = "editAssetTypes";
            this.editAssetTypes.Size = new System.Drawing.Size(77, 23);
            this.editAssetTypes.TabIndex = 18;
            this.editAssetTypes.Text = "Edit";
            this.editAssetTypes.UseVisualStyleBackColor = true;
            this.editAssetTypes.Click += new System.EventHandler(this.editAssetTypes_Click);
            // 
            // CharacterEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(678, 575);
            this.Controls.Add(this.addNewCharacter);
            this.Controls.Add(this.characterInstances);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = global::CommandsEditor.SharedFormIcon.Icon;
            this.MaximizeBox = false;
            this.Name = "CharacterEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Character Editor";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TextBox shirtComposite;
        private System.Windows.Forms.Button selectNewShirt;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button selectNewTrousers;
        private System.Windows.Forms.TextBox trousersComposite;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button selectNewShoes;
        private System.Windows.Forms.TextBox shoesComposite;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button selectNewHead;
        private System.Windows.Forms.TextBox headComposite;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button selectNewArms;
        private System.Windows.Forms.TextBox armsComposite;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button selectNewCollision;
        private System.Windows.Forms.TextBox collisionComposite;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.ComboBox bodyTypes;
        private System.Windows.Forms.ComboBox characterInstances;
        private System.Windows.Forms.Button addNewCharacter;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.ComboBox assetType;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.ComboBox gender;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.ComboBox foleyLeg;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.ComboBox buildAttr;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.ComboBox foleyTorso;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.ComboBox ethnicityAttr;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.ComboBox genderAttr;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.ComboBox voiceActor;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.ComboBox foleyFootwear;
        private System.Windows.Forms.Button editAssetTypes;
    }
}
