﻿namespace Viewer.UI.Presentation
{
    partial class PresentationView
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
            this.PresentationControl = new Viewer.UI.Presentation.PresentationControl();
            this.SuspendLayout();
            // 
            // PresentationControl
            // 
            this.PresentationControl.BackColor = System.Drawing.Color.Black;
            this.PresentationControl.CursorHideDelay = 1000;
            this.PresentationControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PresentationControl.IsFullscreen = false;
            this.PresentationControl.IsPlaying = false;
            this.PresentationControl.Location = new System.Drawing.Point(0, 0);
            this.PresentationControl.Margin = new System.Windows.Forms.Padding(2);
            this.PresentationControl.Name = "PresentationControl";
            this.PresentationControl.Picture = null;
            this.PresentationControl.Size = new System.Drawing.Size(466, 283);
            this.PresentationControl.Speed = 1000;
            this.PresentationControl.TabIndex = 0;
            this.PresentationControl.Zoom = 1D;
            // 
            // PresentationView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(466, 283);
            this.Controls.Add(this.PresentationControl);
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.Name = "PresentationView";
            this.Text = "Presentation";
            this.ResumeLayout(false);

        }

        #endregion

        private PresentationControl PresentationControl;
    }
}
