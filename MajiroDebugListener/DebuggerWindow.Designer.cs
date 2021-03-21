
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
			this.DebugFlagTextBox.Size = new System.Drawing.Size(200, 22);
			this.DebugFlagTextBox.TabIndex = 0;
			this.DebugFlagTextBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
			// 
			// StartGameButton
			// 
			this.StartGameButton.Location = new System.Drawing.Point(218, 11);
			this.StartGameButton.Name = "StartGameButton";
			this.StartGameButton.Size = new System.Drawing.Size(43, 24);
			this.StartGameButton.TabIndex = 2;
			this.StartGameButton.Text = "▶";
			this.StartGameButton.UseVisualStyleBackColor = true;
			this.StartGameButton.Click += new System.EventHandler(this.StartGameButton_Click);
			// 
			// StopGameButton
			// 
			this.StopGameButton.Location = new System.Drawing.Point(267, 11);
			this.StopGameButton.Name = "StopGameButton";
			this.StopGameButton.Size = new System.Drawing.Size(43, 24);
			this.StopGameButton.TabIndex = 2;
			this.StopGameButton.Text = "⬛";
			this.StopGameButton.UseVisualStyleBackColor = true;
			this.StopGameButton.Click += new System.EventHandler(this.StopGameButton_Click);
			// 
			// StatusLabel
			// 
			this.StatusLabel.AutoSize = true;
			this.StatusLabel.Location = new System.Drawing.Point(316, 16);
			this.StatusLabel.Name = "StatusLabel";
			this.StatusLabel.Size = new System.Drawing.Size(0, 15);
			this.StatusLabel.TabIndex = 3;
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
			this.ProtocolTextBox.Size = new System.Drawing.Size(510, 168);
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
			this.ProtocolPanel.Size = new System.Drawing.Size(510, 168);
			this.ProtocolPanel.TabIndex = 5;
			// 
			// DebuggerWindow
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(534, 221);
			this.Controls.Add(this.ProtocolPanel);
			this.Controls.Add(this.StatusLabel);
			this.Controls.Add(this.StopGameButton);
			this.Controls.Add(this.StartGameButton);
			this.Controls.Add(this.DebugFlagTextBox);
			this.MinimumSize = new System.Drawing.Size(337, 180);
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
	}
}

