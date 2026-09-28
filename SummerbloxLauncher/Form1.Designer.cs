using System.ComponentModel;
using System.Windows.Forms;

namespace SummerbloxLauncher
{
    partial class Form1
    {
        private IContainer components = null;

        private PictureBox pictureBox1;
        private Label statusLabel;
        private ProgressBar installProgressBar;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SummerbloxLauncher.Form1));
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.statusLabel = new System.Windows.Forms.Label();
            this.installProgressBar = new System.Windows.Forms.ProgressBar();
            ((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
            base.SuspendLayout();
            this.pictureBox1.BackColor = System.Drawing.SystemColors.Control;
            this.pictureBox1.Image = (System.Drawing.Image)resources.GetObject("pictureBox1.Image");
            this.pictureBox1.InitialImage = null;
            this.pictureBox1.Location = new System.Drawing.Point(12, 12);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(60, 60);
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            this.statusLabel.AutoSize = true;
            this.statusLabel.Location = new System.Drawing.Point(78, 26);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(52, 13);
            this.statusLabel.TabIndex = 1;
            this.statusLabel.Text = "Starting...";
            this.installProgressBar.Location = new System.Drawing.Point(79, 43);
            this.installProgressBar.Name = "installProgressBar";
            this.installProgressBar.Size = new System.Drawing.Size(229, 18);
            this.installProgressBar.TabIndex = 2;
            base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
            base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            base.ClientSize = new System.Drawing.Size(320, 84);
            base.Controls.Add(this.installProgressBar);
            base.Controls.Add(this.statusLabel);
            base.Controls.Add(this.pictureBox1);
            base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            base.Name = "Form1";
            this.Text = "Sb Launcher";
            base.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
            base.ResumeLayout(false);
            base.PerformLayout();
        }
    }
}