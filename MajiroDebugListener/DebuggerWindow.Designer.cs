
namespace MajiroDebugListener {
	partial class DebuggerWindow {
		/// <summary>
		///  Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		///  Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing) {
			if(disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		///  Required method for Designer support - do not modify
		///  the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			this.DebugFlagTextBox = new System.Windows.Forms.TextBox();
			this.StartGameButton = new System.Windows.Forms.Button();
			this.StopGameButton = new System.Windows.Forms.Button();
			this.StatusLabel = new System.Windows.Forms.Label();
			this.ProtocolTextBox = new System.Windows.Forms.RichTextBox();
			this.ProtocolPanel = new System.Windows.Forms.Panel();
			this.StepOutButton = new System.Windows.Forms.Button();
			this.StepOverButton = new System.Windows.Forms.Button();
			this.StepInButton = new System.Windows.Forms.Button();
			this.ResumeButton = new System.Windows.Forms.Button();
			this.PauseButton = new System.Windows.Forms.Button();
			this.ProtocolPanel.SuspendLayout();
			this.SuspendLayout();
			// 
			// DebugFlagTextBox
			// 
			this.DebugFlagTextBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.DebugFlagTextBox.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
			this.DebugFlagTextBox.Location = new System.Drawing.Point(12, 12);
			this.DebugFlagTextBox.Name = "DebugFlagTextBox";
			this.DebugFlagTextBox.ReadOnly = true;
			this.DebugFlagTextBox.Size = new System.Drawing.Size(140, 22);
			this.DebugFlagTextBox.TabIndex = 0;
			this.DebugFlagTextBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
			// 
			// StartGameButton
			// 
			this.StartGameButton.Image = global::MajiroDebugListener.Properties.Resources.run;
			this.StartGameButton.Location = new System.Drawing.Point(158, 11);
			this.StartGameButton.Name = "StartGameButton";
			this.StartGameButton.Size = new System.Drawing.Size(45, 24);
			this.StartGameButton.TabIndex = 2;
			this.StartGameButton.UseVisualStyleBackColor = true;
			this.StartGameButton.Click += new System.EventHandler(this.StartGameButton_Click);
			// 
			// StopGameButton
			// 
			this.StopGameButton.Image = global::MajiroDebugListener.Properties.Resources.stop;
			this.StopGameButton.Location = new System.Drawing.Point(209, 11);
			this.StopGameButton.Name = "StopGameButton";
			this.StopGameButton.Size = new System.Drawing.Size(45, 24);
			this.StopGameButton.TabIndex = 2;
			this.StopGameButton.UseVisualStyleBackColor = true;
			this.StopGameButton.Click += new System.EventHandler(this.StopGameButton_Click);
			// 
			// StatusLabel
			// 
			this.StatusLabel.AutoSize = true;
			this.StatusLabel.Location = new System.Drawing.Point(260, 16);
			this.StatusLabel.Name = "StatusLabel";
			this.StatusLabel.Size = new System.Drawing.Size(39, 15);
			this.StatusLabel.TabIndex = 3;
			this.StatusLabel.Text = "State";
			// 
			// ProtocolTextBox
			// 
			this.ProtocolTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.ProtocolTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
			this.ProtocolTextBox.Cursor = System.Windows.Forms.Cursors.IBeam;
			this.ProtocolTextBox.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
			this.ProtocolTextBox.Location = new System.Drawing.Point(-1, -1);
			this.ProtocolTextBox.Name = "ProtocolTextBox";
			this.ProtocolTextBox.ReadOnly = true;
			this.ProtocolTextBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
			this.ProtocolTextBox.Size = new System.Drawing.Size(510, 208);
			this.ProtocolTextBox.TabIndex = 4;
			this.ProtocolTextBox.Text = "";
			// 
			// ProtocolPanel
			// 
			this.ProtocolPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.ProtocolPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.ProtocolPanel.Controls.Add(this.ProtocolTextBox);
			this.ProtocolPanel.Location = new System.Drawing.Point(12, 41);
			this.ProtocolPanel.Name = "ProtocolPanel";
			this.ProtocolPanel.Size = new System.Drawing.Size(510, 208);
			this.ProtocolPanel.TabIndex = 5;
			// 
			// StepOutButton
			// 
			this.StepOutButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.StepOutButton.Image = global::MajiroDebugListener.Properties.Resources.step_out;
			this.StepOutButton.Location = new System.Drawing.Point(499, 11);
			this.StepOutButton.Name = "StepOutButton";
			this.StepOutButton.Size = new System.Drawing.Size(24, 24);
			this.StepOutButton.TabIndex = 2;
			this.StepOutButton.UseVisualStyleBackColor = true;
			this.StepOutButton.Click += new System.EventHandler(this.StepOutButton_Click);
			// 
			// StepOverButton
			// 
			this.StepOverButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.StepOverButton.Image = global::MajiroDebugListener.Properties.Resources.step_over;
			this.StepOverButton.Location = new System.Drawing.Point(469, 11);
			this.StepOverButton.Name = "StepOverButton";
			this.StepOverButton.Size = new System.Drawing.Size(24, 24);
			this.StepOverButton.TabIndex = 2;
			this.StepOverButton.UseVisualStyleBackColor = true;
			this.StepOverButton.Click += new System.EventHandler(this.StepOverButton_Click);
			// 
			// StepInButton
			// 
			this.StepInButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.StepInButton.Image = global::MajiroDebugListener.Properties.Resources.step_in;
			this.StepInButton.Location = new System.Drawing.Point(439, 11);
			this.StepInButton.Name = "StepInButton";
			this.StepInButton.Size = new System.Drawing.Size(24, 24);
			this.StepInButton.TabIndex = 2;
			this.StepInButton.UseVisualStyleBackColor = true;
			this.StepInButton.Click += new System.EventHandler(this.StepInButton_Click);
			// 
			// ResumeButton
			// 
			this.ResumeButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.ResumeButton.Image = global::MajiroDebugListener.Properties.Resources.resume;
			this.ResumeButton.Location = new System.Drawing.Point(409, 11);
			this.ResumeButton.Name = "ResumeButton";
			this.ResumeButton.Size = new System.Drawing.Size(24, 24);
			this.ResumeButton.TabIndex = 2;
			this.ResumeButton.UseVisualStyleBackColor = true;
			this.ResumeButton.Click += new System.EventHandler(this.ResumeButton_Click);
			// 
			// PauseButton
			// 
			this.PauseButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.PauseButton.Image = global::MajiroDebugListener.Properties.Resources.pause;
			this.PauseButton.Location = new System.Drawing.Point(379, 11);
			this.PauseButton.Name = "PauseButton";
			this.PauseButton.Size = new System.Drawing.Size(24, 24);
			this.PauseButton.TabIndex = 2;
			this.PauseButton.UseVisualStyleBackColor = true;
			this.PauseButton.Click += new System.EventHandler(this.PauseButton_Click);
			// 
			// DebuggerWindow
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(534, 261);
			this.Controls.Add(this.ProtocolPanel);
			this.Controls.Add(this.StatusLabel);
			this.Controls.Add(this.PauseButton);
			this.Controls.Add(this.ResumeButton);
			this.Controls.Add(this.StepInButton);
			this.Controls.Add(this.StepOverButton);
			this.Controls.Add(this.StepOutButton);
			this.Controls.Add(this.StopGameButton);
			this.Controls.Add(this.StartGameButton);
			this.Controls.Add(this.DebugFlagTextBox);
			this.MinimumSize = new System.Drawing.Size(550, 180);
			this.Name = "DebuggerWindow";
			this.Text = "Majiro Debugger";
			this.Load += new System.EventHandler(this.DebuggerWindow_Load);
			this.ProtocolPanel.ResumeLayout(false);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.TextBox DebugFlagTextBox;
		private System.Windows.Forms.Button StartGameButton;
		private System.Windows.Forms.Button StopGameButton;
		private System.Windows.Forms.Label StatusLabel;
		private System.Windows.Forms.RichTextBox ProtocolTextBox;
		private System.Windows.Forms.Panel ProtocolPanel;
		private System.Windows.Forms.Button StepOutButton;
		private System.Windows.Forms.Button StepOverButton;
		private System.Windows.Forms.Button StepInButton;
		private System.Windows.Forms.Button ResumeButton;
		private System.Windows.Forms.Button PauseButton;
	}
}

