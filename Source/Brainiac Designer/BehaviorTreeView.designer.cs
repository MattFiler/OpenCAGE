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

namespace Brainiac.Design
{
	partial class BehaviorTreeView
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
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BehaviorTreeView));
			this.toolTip = new System.Windows.Forms.ToolTip(this.components);
			this.saveButton = new System.Windows.Forms.Button();
			this.imageList = new System.Windows.Forms.ImageList(this.components);
			this.exportButton = new System.Windows.Forms.Button();
			this.checkButton = new System.Windows.Forms.Button();
			this.saveAsButton = new System.Windows.Forms.Button();
			this.imageButton = new System.Windows.Forms.Button();
			this.propertiesButton = new System.Windows.Forms.Button();
			this.saveImageDialog = new System.Windows.Forms.SaveFileDialog();
			this.SuspendLayout();
			// 
			// toolTip
			// 
			this.toolTip.AutomaticDelay = 400;
			// 
			// saveButton
			// 
			this.saveButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.saveButton.Cursor = System.Windows.Forms.Cursors.Arrow;
			this.saveButton.ImageIndex = 1;
			this.saveButton.ImageList = this.imageList;
			this.saveButton.Location = new System.Drawing.Point(604, 3);
			this.saveButton.Name = "saveButton";
			this.saveButton.Size = new System.Drawing.Size(23, 23);
			this.saveButton.TabIndex = 1;
			this.saveButton.TabStop = false;
			this.toolTip.SetToolTip(this.saveButton, "Save Behavior");
			this.saveButton.UseVisualStyleBackColor = true;
			this.saveButton.Click += new System.EventHandler(this.saveButton_Click);
			// 
			// imageList
			// 
			this.imageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList.ImageStream")));
			this.imageList.TransparentColor = System.Drawing.Color.Magenta;
			this.imageList.Images.SetKeyName(0, "close");
			this.imageList.Images.SetKeyName(1, "save");
			this.imageList.Images.SetKeyName(2, "export");
			this.imageList.Images.SetKeyName(3, "check");
			this.imageList.Images.SetKeyName(4, "saveas");
			this.imageList.Images.SetKeyName(5, "properties");
			// 
			// exportButton
			// 
			this.exportButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.exportButton.Cursor = System.Windows.Forms.Cursors.Arrow;
			this.exportButton.ImageIndex = 2;
			this.exportButton.ImageList = this.imageList;
			this.exportButton.Location = new System.Drawing.Point(654, 3);
			this.exportButton.Name = "exportButton";
			this.exportButton.Size = new System.Drawing.Size(23, 23);
			this.exportButton.TabIndex = 3;
			this.exportButton.TabStop = false;
			this.toolTip.SetToolTip(this.exportButton, "Export Behavior");
			this.exportButton.UseVisualStyleBackColor = true;
			this.exportButton.Click += new System.EventHandler(this.exportButton_Click);
			// 
			// checkButton
			// 
			this.checkButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.checkButton.Cursor = System.Windows.Forms.Cursors.Arrow;
			this.checkButton.ImageIndex = 3;
			this.checkButton.ImageList = this.imageList;
			this.checkButton.Location = new System.Drawing.Point(679, 3);
			this.checkButton.Name = "checkButton";
			this.checkButton.Size = new System.Drawing.Size(23, 23);
			this.checkButton.TabIndex = 4;
			this.checkButton.TabStop = false;
			this.toolTip.SetToolTip(this.checkButton, "Check Behavior for Errors");
			this.checkButton.UseVisualStyleBackColor = true;
			this.checkButton.Click += new System.EventHandler(this.checkButton_Click);
			// 
			// saveAsButton
			// 
			this.saveAsButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.saveAsButton.Cursor = System.Windows.Forms.Cursors.Arrow;
			this.saveAsButton.ImageIndex = 4;
			this.saveAsButton.ImageList = this.imageList;
			this.saveAsButton.Location = new System.Drawing.Point(629, 3);
			this.saveAsButton.Name = "saveAsButton";
			this.saveAsButton.Size = new System.Drawing.Size(23, 23);
			this.saveAsButton.TabIndex = 2;
			this.saveAsButton.TabStop = false;
			this.toolTip.SetToolTip(this.saveAsButton, "Save Behavior Copy");
			this.saveAsButton.UseVisualStyleBackColor = true;
			this.saveAsButton.Click += new System.EventHandler(this.saveAsButton_Click);
			// 
			// imageButton
			// 
			this.imageButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.imageButton.Cursor = System.Windows.Forms.Cursors.Arrow;
			this.imageButton.Image = ((System.Drawing.Image)(resources.GetObject("imageButton.Image")));
			this.imageButton.Location = new System.Drawing.Point(704, 3);
			this.imageButton.Name = "imageButton";
			this.imageButton.Size = new System.Drawing.Size(23, 23);
			this.imageButton.TabIndex = 5;
			this.imageButton.TabStop = false;
			this.toolTip.SetToolTip(this.imageButton, "Save Image Of Behavior");
			this.imageButton.UseVisualStyleBackColor = true;
			this.imageButton.Click += new System.EventHandler(this.imageButton_Click);
			// 
			// propertiesButton
			// 
			this.propertiesButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.propertiesButton.Cursor = System.Windows.Forms.Cursors.Arrow;
			this.propertiesButton.Enabled = false;
			this.propertiesButton.ImageIndex = 5;
			this.propertiesButton.ImageList = this.imageList;
			this.propertiesButton.Location = new System.Drawing.Point(579, 3);
			this.propertiesButton.Name = "propertiesButton";
			this.propertiesButton.Size = new System.Drawing.Size(23, 23);
			this.propertiesButton.TabIndex = 0;
			this.propertiesButton.TabStop = false;
			this.toolTip.SetToolTip(this.propertiesButton, "Show Properties");
			this.propertiesButton.UseVisualStyleBackColor = true;
			this.propertiesButton.Click += new System.EventHandler(this.propertiesButton_Click);
			// 
			// saveImageDialog
			// 
			this.saveImageDialog.Filter = "Enhanced Metafile|*.emf|Portable Network Graphics|*.png";
			this.saveImageDialog.Title = "Save Graph As Image";
			// 
			// BehaviorTreeView
			// 
			this.AllowDrop = true;
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.BackColor = System.Drawing.Color.White;
			this.Controls.Add(this.propertiesButton);
			this.Controls.Add(this.imageButton);
			this.Controls.Add(this.saveAsButton);
			this.Controls.Add(this.checkButton);
			this.Controls.Add(this.exportButton);
			this.Controls.Add(this.saveButton);
			this.Cursor = System.Windows.Forms.Cursors.Hand;
			this.DoubleBuffered = true;
			this.Name = "BehaviorTreeView";
			this.Size = new System.Drawing.Size(730, 462);
			this.DragOver += new System.Windows.Forms.DragEventHandler(this.BehaviorTreeView_DragOver);
			this.DragDrop += new System.Windows.Forms.DragEventHandler(this.BehaviorTreeView_DragDrop);
			this.DragEnter += new System.Windows.Forms.DragEventHandler(this.BehaviorTreeView_DragEnter);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.ToolTip toolTip;
		private System.Windows.Forms.ImageList imageList;
		private System.Windows.Forms.Button saveButton;
		private System.Windows.Forms.Button exportButton;
		private System.Windows.Forms.Button checkButton;
		private System.Windows.Forms.Button saveAsButton;
		private System.Windows.Forms.Button imageButton;
		private System.Windows.Forms.SaveFileDialog saveImageDialog;
		private System.Windows.Forms.Button propertiesButton;


	}
}
