namespace Alien_Isolation_Mod_Tools
{
    partial class CSE_Alpha
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CSE_Alpha));
            this.flowgraph_count = new System.Windows.Forms.Label();
            this.first_executed_flowgraph = new System.Windows.Forms.Label();
            this.save_commands_pak = new System.Windows.Forms.Button();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.FileTree = new System.Windows.Forms.TreeView();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox7 = new System.Windows.Forms.GroupBox();
            this.selected_node_name = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.selected_node_type_description = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.selected_node_type = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.selected_node_id = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.groupBox9 = new System.Windows.Forms.GroupBox();
            this.node_parents = new System.Windows.Forms.ListBox();
            this.groupBox6 = new System.Windows.Forms.GroupBox();
            this.out_pin_edit = new System.Windows.Forms.Button();
            this.out_pin_goto = new System.Windows.Forms.Button();
            this.node_children = new System.Windows.Forms.ListBox();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.node_search_box = new System.Windows.Forms.TextBox();
            this.node_search_btn = new System.Windows.Forms.Button();
            this.flowgraph_content = new System.Windows.Forms.ListBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.NodeParams = new System.Windows.Forms.Panel();
            this.load_commands_pak = new System.Windows.Forms.Button();
            this.env_list = new System.Windows.Forms.ComboBox();
            this.groupBox8 = new System.Windows.Forms.GroupBox();
            this.groupBox10 = new System.Windows.Forms.GroupBox();
            this.node_to_flowgraph_jump = new System.Windows.Forms.Button();
            this.groupBox3.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.groupBox7.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.groupBox9.SuspendLayout();
            this.groupBox6.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox8.SuspendLayout();
            this.groupBox10.SuspendLayout();
            this.SuspendLayout();
            // 
            // flowgraph_count
            // 
            this.flowgraph_count.AutoSize = true;
            this.flowgraph_count.Location = new System.Drawing.Point(6, 28);
            this.flowgraph_count.Name = "flowgraph_count";
            this.flowgraph_count.Size = new System.Drawing.Size(95, 13);
            this.flowgraph_count.TabIndex = 172;
            this.flowgraph_count.Text = "Flowgraph count:  ";
            // 
            // first_executed_flowgraph
            // 
            this.first_executed_flowgraph.AutoSize = true;
            this.first_executed_flowgraph.Location = new System.Drawing.Point(6, 12);
            this.first_executed_flowgraph.Name = "first_executed_flowgraph";
            this.first_executed_flowgraph.Size = new System.Drawing.Size(63, 13);
            this.first_executed_flowgraph.TabIndex = 170;
            this.first_executed_flowgraph.Text = "Entry point: ";
            // 
            // save_commands_pak
            // 
            this.save_commands_pak.Location = new System.Drawing.Point(457, 16);
            this.save_commands_pak.Name = "save_commands_pak";
            this.save_commands_pak.Size = new System.Drawing.Size(86, 23);
            this.save_commands_pak.TabIndex = 164;
            this.save_commands_pak.Text = "Save";
            this.save_commands_pak.UseVisualStyleBackColor = true;
            this.save_commands_pak.Click += new System.EventHandler(this.save_commands_pak_Click);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.FileTree);
            this.groupBox3.Location = new System.Drawing.Point(8, 55);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(378, 820);
            this.groupBox3.TabIndex = 163;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Cathode Flowgraphs";
            // 
            // FileTree
            // 
            this.FileTree.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.FileTree.Location = new System.Drawing.Point(6, 19);
            this.FileTree.Name = "FileTree";
            this.FileTree.Size = new System.Drawing.Size(366, 795);
            this.FileTree.TabIndex = 99;
            this.FileTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.FileTree_AfterSelect);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.groupBox7);
            this.groupBox1.Controls.Add(this.groupBox5);
            this.groupBox1.Controls.Add(this.groupBox4);
            this.groupBox1.Controls.Add(this.groupBox2);
            this.groupBox1.Location = new System.Drawing.Point(392, 55);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(1407, 820);
            this.groupBox1.TabIndex = 162;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Selected Flowgraph Content";
            // 
            // groupBox7
            // 
            this.groupBox7.Controls.Add(this.node_to_flowgraph_jump);
            this.groupBox7.Controls.Add(this.selected_node_name);
            this.groupBox7.Controls.Add(this.label9);
            this.groupBox7.Controls.Add(this.selected_node_type_description);
            this.groupBox7.Controls.Add(this.label6);
            this.groupBox7.Controls.Add(this.selected_node_type);
            this.groupBox7.Controls.Add(this.label4);
            this.groupBox7.Controls.Add(this.selected_node_id);
            this.groupBox7.Controls.Add(this.label1);
            this.groupBox7.Location = new System.Drawing.Point(643, 19);
            this.groupBox7.Name = "groupBox7";
            this.groupBox7.Size = new System.Drawing.Size(756, 66);
            this.groupBox7.TabIndex = 151;
            this.groupBox7.TabStop = false;
            this.groupBox7.Text = "Selected Node Info";
            // 
            // selected_node_name
            // 
            this.selected_node_name.AutoSize = true;
            this.selected_node_name.Location = new System.Drawing.Point(284, 22);
            this.selected_node_name.Name = "selected_node_name";
            this.selected_node_name.Size = new System.Drawing.Size(0, 13);
            this.selected_node_name.TabIndex = 7;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(207, 22);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(70, 13);
            this.label9.TabIndex = 6;
            this.label9.Text = "Node Name: ";
            // 
            // selected_node_type_description
            // 
            this.selected_node_type_description.AutoSize = true;
            this.selected_node_type_description.Location = new System.Drawing.Point(332, 38);
            this.selected_node_type_description.Name = "selected_node_type_description";
            this.selected_node_type_description.Size = new System.Drawing.Size(0, 13);
            this.selected_node_type_description.TabIndex = 5;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(207, 38);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(119, 13);
            this.label6.TabIndex = 4;
            this.label6.Text = "Node Type Description:";
            // 
            // selected_node_type
            // 
            this.selected_node_type.AutoSize = true;
            this.selected_node_type.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.selected_node_type.Location = new System.Drawing.Point(111, 38);
            this.selected_node_type.Name = "selected_node_type";
            this.selected_node_type.Size = new System.Drawing.Size(0, 13);
            this.selected_node_type.TabIndex = 3;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(39, 38);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(63, 13);
            this.label4.TabIndex = 2;
            this.label4.Text = "Node Type:";
            // 
            // selected_node_id
            // 
            this.selected_node_id.AutoSize = true;
            this.selected_node_id.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.selected_node_id.Location = new System.Drawing.Point(111, 22);
            this.selected_node_id.Name = "selected_node_id";
            this.selected_node_id.Size = new System.Drawing.Size(0, 13);
            this.selected_node_id.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(52, 22);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(53, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Node ID: ";
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.groupBox9);
            this.groupBox5.Controls.Add(this.groupBox6);
            this.groupBox5.Location = new System.Drawing.Point(643, 512);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(756, 302);
            this.groupBox5.TabIndex = 149;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Selected Node Parameter Links";
            // 
            // groupBox9
            // 
            this.groupBox9.Controls.Add(this.node_parents);
            this.groupBox9.Location = new System.Drawing.Point(6, 19);
            this.groupBox9.Name = "groupBox9";
            this.groupBox9.Size = new System.Drawing.Size(744, 138);
            this.groupBox9.TabIndex = 151;
            this.groupBox9.TabStop = false;
            this.groupBox9.Text = "In Pins";
            // 
            // node_parents
            // 
            this.node_parents.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.node_parents.FormattingEnabled = true;
            this.node_parents.HorizontalScrollbar = true;
            this.node_parents.Location = new System.Drawing.Point(6, 19);
            this.node_parents.Name = "node_parents";
            this.node_parents.Size = new System.Drawing.Size(731, 108);
            this.node_parents.TabIndex = 146;
            this.node_parents.SelectedIndexChanged += new System.EventHandler(this.node_parents_SelectedIndexChanged);
            // 
            // groupBox6
            // 
            this.groupBox6.Controls.Add(this.out_pin_edit);
            this.groupBox6.Controls.Add(this.out_pin_goto);
            this.groupBox6.Controls.Add(this.node_children);
            this.groupBox6.Location = new System.Drawing.Point(6, 159);
            this.groupBox6.Name = "groupBox6";
            this.groupBox6.Size = new System.Drawing.Size(744, 138);
            this.groupBox6.TabIndex = 150;
            this.groupBox6.TabStop = false;
            this.groupBox6.Text = "Out Pins";
            // 
            // out_pin_edit
            // 
            this.out_pin_edit.Location = new System.Drawing.Point(679, 76);
            this.out_pin_edit.Name = "out_pin_edit";
            this.out_pin_edit.Size = new System.Drawing.Size(58, 52);
            this.out_pin_edit.TabIndex = 147;
            this.out_pin_edit.Text = "Edit Selected Link";
            this.out_pin_edit.UseVisualStyleBackColor = true;
            this.out_pin_edit.Click += new System.EventHandler(this.out_pin_edit_Click);
            // 
            // out_pin_goto
            // 
            this.out_pin_goto.Location = new System.Drawing.Point(679, 18);
            this.out_pin_goto.Name = "out_pin_goto";
            this.out_pin_goto.Size = new System.Drawing.Size(58, 52);
            this.out_pin_goto.TabIndex = 146;
            this.out_pin_goto.Text = "Go To Selected Link";
            this.out_pin_goto.UseVisualStyleBackColor = true;
            this.out_pin_goto.Click += new System.EventHandler(this.out_pin_goto_Click);
            // 
            // node_children
            // 
            this.node_children.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.node_children.FormattingEnabled = true;
            this.node_children.HorizontalScrollbar = true;
            this.node_children.Location = new System.Drawing.Point(6, 19);
            this.node_children.Name = "node_children";
            this.node_children.Size = new System.Drawing.Size(667, 108);
            this.node_children.TabIndex = 145;
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.node_search_box);
            this.groupBox4.Controls.Add(this.node_search_btn);
            this.groupBox4.Controls.Add(this.flowgraph_content);
            this.groupBox4.Location = new System.Drawing.Point(6, 19);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(631, 795);
            this.groupBox4.TabIndex = 148;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Nodes";
            // 
            // node_search_box
            // 
            this.node_search_box.Location = new System.Drawing.Point(6, 19);
            this.node_search_box.Name = "node_search_box";
            this.node_search_box.Size = new System.Drawing.Size(522, 20);
            this.node_search_box.TabIndex = 146;
            // 
            // node_search_btn
            // 
            this.node_search_btn.Location = new System.Drawing.Point(534, 18);
            this.node_search_btn.Name = "node_search_btn";
            this.node_search_btn.Size = new System.Drawing.Size(90, 23);
            this.node_search_btn.TabIndex = 145;
            this.node_search_btn.Text = "Search";
            this.node_search_btn.UseVisualStyleBackColor = true;
            this.node_search_btn.Click += new System.EventHandler(this.node_search_btn_Click);
            // 
            // flowgraph_content
            // 
            this.flowgraph_content.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.flowgraph_content.FormattingEnabled = true;
            this.flowgraph_content.HorizontalScrollbar = true;
            this.flowgraph_content.Location = new System.Drawing.Point(6, 45);
            this.flowgraph_content.Name = "flowgraph_content";
            this.flowgraph_content.Size = new System.Drawing.Size(618, 745);
            this.flowgraph_content.TabIndex = 144;
            this.flowgraph_content.SelectedIndexChanged += new System.EventHandler(this.flowgraph_content_SelectedIndexChanged);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.NodeParams);
            this.groupBox2.Location = new System.Drawing.Point(643, 91);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(756, 415);
            this.groupBox2.TabIndex = 147;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Selected Node Parameters";
            // 
            // NodeParams
            // 
            this.NodeParams.AutoScroll = true;
            this.NodeParams.Location = new System.Drawing.Point(6, 20);
            this.NodeParams.Name = "NodeParams";
            this.NodeParams.Size = new System.Drawing.Size(737, 389);
            this.NodeParams.TabIndex = 0;
            // 
            // load_commands_pak
            // 
            this.load_commands_pak.Location = new System.Drawing.Point(365, 16);
            this.load_commands_pak.Name = "load_commands_pak";
            this.load_commands_pak.Size = new System.Drawing.Size(86, 23);
            this.load_commands_pak.TabIndex = 160;
            this.load_commands_pak.Text = "Load";
            this.load_commands_pak.UseVisualStyleBackColor = true;
            this.load_commands_pak.Click += new System.EventHandler(this.load_commands_pak_Click);
            // 
            // env_list
            // 
            this.env_list.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.env_list.FormattingEnabled = true;
            this.env_list.Location = new System.Drawing.Point(9, 17);
            this.env_list.Name = "env_list";
            this.env_list.Size = new System.Drawing.Size(350, 21);
            this.env_list.TabIndex = 173;
            // 
            // groupBox8
            // 
            this.groupBox8.Controls.Add(this.first_executed_flowgraph);
            this.groupBox8.Controls.Add(this.flowgraph_count);
            this.groupBox8.Location = new System.Drawing.Point(565, 3);
            this.groupBox8.Name = "groupBox8";
            this.groupBox8.Size = new System.Drawing.Size(1234, 49);
            this.groupBox8.TabIndex = 174;
            this.groupBox8.TabStop = false;
            // 
            // groupBox10
            // 
            this.groupBox10.Controls.Add(this.env_list);
            this.groupBox10.Controls.Add(this.load_commands_pak);
            this.groupBox10.Controls.Add(this.save_commands_pak);
            this.groupBox10.Location = new System.Drawing.Point(8, 3);
            this.groupBox10.Name = "groupBox10";
            this.groupBox10.Size = new System.Drawing.Size(551, 49);
            this.groupBox10.TabIndex = 175;
            this.groupBox10.TabStop = false;
            // 
            // node_to_flowgraph_jump
            // 
            this.node_to_flowgraph_jump.Location = new System.Drawing.Point(715, 13);
            this.node_to_flowgraph_jump.Name = "node_to_flowgraph_jump";
            this.node_to_flowgraph_jump.Size = new System.Drawing.Size(35, 47);
            this.node_to_flowgraph_jump.TabIndex = 8;
            this.node_to_flowgraph_jump.Text = "Go To";
            this.node_to_flowgraph_jump.UseVisualStyleBackColor = true;
            this.node_to_flowgraph_jump.Visible = false;
            this.node_to_flowgraph_jump.Click += new System.EventHandler(this.node_to_flowgraph_jump_Click);
            // 
            // CSE_Alpha
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1805, 882);
            this.Controls.Add(this.groupBox10);
            this.Controls.Add(this.groupBox8);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "CSE_Alpha";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "OpenCAGE Cathode Editor (ALPHA)";
            this.groupBox3.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.groupBox7.ResumeLayout(false);
            this.groupBox7.PerformLayout();
            this.groupBox5.ResumeLayout(false);
            this.groupBox9.ResumeLayout(false);
            this.groupBox6.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox8.ResumeLayout(false);
            this.groupBox8.PerformLayout();
            this.groupBox10.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Label flowgraph_count;
        private System.Windows.Forms.Label first_executed_flowgraph;
        private System.Windows.Forms.Button save_commands_pak;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.TreeView FileTree;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox7;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label selected_node_type_description;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label selected_node_type;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label selected_node_id;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.GroupBox groupBox9;
        private System.Windows.Forms.ListBox node_parents;
        private System.Windows.Forms.GroupBox groupBox6;
        private System.Windows.Forms.ListBox node_children;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.TextBox node_search_box;
        private System.Windows.Forms.Button node_search_btn;
        private System.Windows.Forms.ListBox flowgraph_content;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Panel NodeParams;
        private System.Windows.Forms.Button load_commands_pak;
        private System.Windows.Forms.ComboBox env_list;
        private System.Windows.Forms.GroupBox groupBox8;
        private System.Windows.Forms.GroupBox groupBox10;
        private System.Windows.Forms.Label selected_node_name;
        private System.Windows.Forms.Button out_pin_edit;
        private System.Windows.Forms.Button out_pin_goto;
        private System.Windows.Forms.Button node_to_flowgraph_jump;
    }
}