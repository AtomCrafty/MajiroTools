using System;
using System.Drawing;
using System.Windows.Forms;

namespace MajiroDebugListener {
	public partial class DebuggerWindow : Form {
		private IDebugger _debugger;

		public DebuggerWindow() {
			InitializeComponent();
		}

		private void DebuggerWindow_Load(object sender, EventArgs e) {
			_debugger = new Debugger(Handle) {
				LogCallback = AppendLogMessage,
				StatusCallback = UpdateStatusDisplay
			};
			
			DebugFlagTextBox.Text = $@"/DEBUG:{Handle.ToInt32()}";
			UpdateStatusDisplay(_debugger.Status);
		}

		private void UpdateStatusDisplay(DebuggerStatus status) {
			Invoke((Action)(() => {
				StatusLabel.Text = $@"Status: {status}";
				switch(status) {
					case DebuggerStatus.Idle:
					case DebuggerStatus.Detached:
						StartGameButton.Enabled = true;
						StopGameButton.Enabled = false;
						break;

					case DebuggerStatus.Waiting:
					case DebuggerStatus.Attached:
						StartGameButton.Enabled = false;
						StopGameButton.Enabled = true;
						break;

					default:
						StartGameButton.Enabled = false;
						StopGameButton.Enabled = false;
						break;
				}
			}));
		}

		private void AppendLogMessage(LogSeverity severity, string message) {
			ProtocolTextBox.SuspendLayout();
			int selectionStart = ProtocolTextBox.SelectionStart;
			int selectionLength = ProtocolTextBox.SelectionLength;
			ProtocolTextBox.Select(ProtocolTextBox.TextLength, 0);
			ProtocolTextBox.SelectionColor = GetColorBySeverity(severity);
			ProtocolTextBox.AppendText(message + Environment.NewLine);
			ProtocolTextBox.Select(selectionStart, selectionLength);
			ProtocolTextBox.ResumeLayout();
		}

		private Color GetColorBySeverity(LogSeverity severity) {
			switch(severity) {
				case LogSeverity.Message:
					return Color.Black;
				case LogSeverity.Info:
					return Color.Black;
				case LogSeverity.Warn:
					return Color.Orange;
				case LogSeverity.Error:
					return Color.Red;
				default:
					return Color.Black;
			}
		}

		protected override void WndProc(ref Message m) {
			if(m.Msg >= 0xA000 && m.Msg < 0xC000) {
				_debugger.ProcessMessage((DebugMessage)m.Msg, m.WParam.ToInt64(), m.LParam.ToInt64());
			}
			base.WndProc(ref m);
		}

		private void StartGameButton_Click(object sender, EventArgs e) {
			_debugger.StartProcess();
		}

		private void StopGameButton_Click(object sender, EventArgs e) {
			_debugger.TerminateProcess(true);
		}
	}
}
