////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2009, Daniel Kollmann
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification, are permitted
// provided that the following conditions are met:
//
// - Redistributions of source code must retain the above copyright notice, this list of conditions
//   and the following disclaimer.
//
// - Redistributions in binary form must reproduce the above copyright notice, this list of
//   conditions and the following disclaimer in the documentation and/or other materials provided
//   with the distribution.
//
// - Neither the name of Daniel Kollmann nor the names of its contributors may be used to endorse
//   or promote products derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR
// IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
// WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY
// WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
////////////////////////////////////////////////////////////////////////////////////////////////////

namespace CustomPropertyGridTest
{
	partial class DynamicPropertyGrid
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

		#region Component Designer generated code

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.panel2 = new System.Windows.Forms.Panel();
			this.propertiesSplitContainer = new System.Windows.Forms.SplitContainer();
			this.panel1 = new System.Windows.Forms.Panel();
			this.propertyDescriptionLabel = new System.Windows.Forms.Label();
			this.propertyNameLabel = new System.Windows.Forms.Label();
			this.panel2.SuspendLayout();
			this.propertiesSplitContainer.SuspendLayout();
			this.panel1.SuspendLayout();
			this.SuspendLayout();
			// 
			// panel2
			// 
			this.panel2.AutoScroll = true;
			this.panel2.BackColor = System.Drawing.SystemColors.Control;
			this.panel2.Controls.Add(this.propertiesSplitContainer);
			this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel2.Location = new System.Drawing.Point(0, 0);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(555, 368);
			this.panel2.TabIndex = 2;
			// 
			// propertiesSplitContainer
			// 
			this.propertiesSplitContainer.BackColor = System.Drawing.Color.Gray;
			this.propertiesSplitContainer.Dock = System.Windows.Forms.DockStyle.Top;
			this.propertiesSplitContainer.Location = new System.Drawing.Point(0, 0);
			this.propertiesSplitContainer.Name = "propertiesSplitContainer";
			// 
			// propertiesSplitContainer.Panel1
			// 
			this.propertiesSplitContainer.Panel1.BackColor = System.Drawing.Color.White;
			this.propertiesSplitContainer.Panel1.Padding = new System.Windows.Forms.Padding(3);
			// 
			// propertiesSplitContainer.Panel2
			// 
			this.propertiesSplitContainer.Panel2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
			this.propertiesSplitContainer.Panel2.Padding = new System.Windows.Forms.Padding(3);
			this.propertiesSplitContainer.Size = new System.Drawing.Size(555, 67);
			this.propertiesSplitContainer.SplitterDistance = 231;
			this.propertiesSplitContainer.SplitterWidth = 2;
			this.propertiesSplitContainer.TabIndex = 1;
			this.propertiesSplitContainer.Resize += new System.EventHandler(this.propertiesSplitContainer_Resize);
			this.propertiesSplitContainer.SplitterMoved += new System.Windows.Forms.SplitterEventHandler(this.propertiesSplitContainer_SplitterMoved);
			// 
			// panel1
			// 
			this.panel1.BackColor = System.Drawing.SystemColors.Control;
			this.panel1.Controls.Add(this.propertyDescriptionLabel);
			this.panel1.Controls.Add(this.propertyNameLabel);
			this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.panel1.Location = new System.Drawing.Point(0, 368);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(555, 100);
			this.panel1.TabIndex = 1;
			// 
			// propertyDescriptionLabel
			// 
			this.propertyDescriptionLabel.Dock = System.Windows.Forms.DockStyle.Fill;
			this.propertyDescriptionLabel.Location = new System.Drawing.Point(0, 15);
			this.propertyDescriptionLabel.Name = "propertyDescriptionLabel";
			this.propertyDescriptionLabel.Size = new System.Drawing.Size(555, 85);
			this.propertyDescriptionLabel.TabIndex = 1;
			this.propertyDescriptionLabel.Text = "Property Description";
			// 
			// propertyNameLabel
			// 
			this.propertyNameLabel.BackColor = System.Drawing.Color.DarkGray;
			this.propertyNameLabel.Dock = System.Windows.Forms.DockStyle.Top;
			this.propertyNameLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.propertyNameLabel.Location = new System.Drawing.Point(0, 0);
			this.propertyNameLabel.Name = "propertyNameLabel";
			this.propertyNameLabel.Size = new System.Drawing.Size(555, 15);
			this.propertyNameLabel.TabIndex = 0;
			this.propertyNameLabel.Text = "Property Name";
			// 
			// DynamicPropertyGrid
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.BackColor = System.Drawing.SystemColors.Control;
			this.Controls.Add(this.panel2);
			this.Controls.Add(this.panel1);
			this.Name = "DynamicPropertyGrid";
			this.Size = new System.Drawing.Size(555, 468);
			this.panel2.ResumeLayout(false);
			this.propertiesSplitContainer.ResumeLayout(false);
			this.panel1.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Panel panel2;
		private System.Windows.Forms.SplitContainer propertiesSplitContainer;
		private System.Windows.Forms.Panel panel1;
		private System.Windows.Forms.Label propertyNameLabel;
		private System.Windows.Forms.Label propertyDescriptionLabel;


	}
}
