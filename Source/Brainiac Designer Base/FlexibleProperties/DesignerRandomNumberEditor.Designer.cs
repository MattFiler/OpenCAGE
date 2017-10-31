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

namespace Brainiac.Design.FlexibleProperties
{
	partial class DesignerRandomNumberEditor
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
			this.minNumericUpDown = new System.Windows.Forms.NumericUpDown();
			this.unitLabel = new System.Windows.Forms.Label();
			this.maxNumericUpDown = new System.Windows.Forms.NumericUpDown();
			((System.ComponentModel.ISupportInitialize)(this.minNumericUpDown)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.maxNumericUpDown)).BeginInit();
			this.SuspendLayout();
			// 
			// minNumericUpDown
			// 
			this.minNumericUpDown.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
						| System.Windows.Forms.AnchorStyles.Left)));
			this.minNumericUpDown.Location = new System.Drawing.Point(2, 3);
			this.minNumericUpDown.Name = "minNumericUpDown";
			this.minNumericUpDown.Size = new System.Drawing.Size(77, 20);
			this.minNumericUpDown.TabIndex = 0;
			this.minNumericUpDown.ValueChanged += new System.EventHandler(this.minNumericUpDown_ValueChanged);
			// 
			// unitLabel
			// 
			this.unitLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.unitLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.unitLabel.Location = new System.Drawing.Point(159, 6);
			this.unitLabel.Name = "unitLabel";
			this.unitLabel.RightToLeft = System.Windows.Forms.RightToLeft.No;
			this.unitLabel.Size = new System.Drawing.Size(43, 13);
			this.unitLabel.TabIndex = 1;
			this.unitLabel.Text = "unit";
			this.unitLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// maxNumericUpDown
			// 
			this.maxNumericUpDown.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
						| System.Windows.Forms.AnchorStyles.Right)));
			this.maxNumericUpDown.Location = new System.Drawing.Point(82, 3);
			this.maxNumericUpDown.Name = "maxNumericUpDown";
			this.maxNumericUpDown.Size = new System.Drawing.Size(77, 20);
			this.maxNumericUpDown.TabIndex = 2;
			this.maxNumericUpDown.ValueChanged += new System.EventHandler(this.maxNumericUpDown_ValueChanged);
			// 
			// DesignerRandomNumberEditor
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.Controls.Add(this.maxNumericUpDown);
			this.Controls.Add(this.unitLabel);
			this.Controls.Add(this.minNumericUpDown);
			this.Name = "DesignerRandomNumberEditor";
			((System.ComponentModel.ISupportInitialize)(this.minNumericUpDown)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.maxNumericUpDown)).EndInit();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.NumericUpDown minNumericUpDown;
		private System.Windows.Forms.Label unitLabel;
		private System.Windows.Forms.NumericUpDown maxNumericUpDown;
	}
}
