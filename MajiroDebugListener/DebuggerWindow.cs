using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MajiroDebugListener {
	public partial class DebuggerWindow : Form {
		private IDebugger _debugger;

		private void Invoke(Action action) => base.Invoke(action);

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
			Invoke(() => {
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
			});
		}

		private void AppendLogMessage(LogSeverity severity, string message) {
			Invoke(() => {
				ProtocolTextBox.SuspendLayout();
				int selectionStart = ProtocolTextBox.SelectionStart;
				int selectionLength = ProtocolTextBox.SelectionLength;
				ProtocolTextBox.Select(ProtocolTextBox.TextLength, 0);
				ProtocolTextBox.SelectionColor = GetColorBySeverity(severity);
				ProtocolTextBox.AppendText(message + Environment.NewLine);
				ProtocolTextBox.Select(selectionStart, selectionLength);
				ProtocolTextBox.ResumeLayout();
			});
		}

		private Color GetColorBySeverity(LogSeverity severity) {
			switch(severity) {
				case LogSeverity.Message:
					return Color.Black;
				case LogSeverity.Info:
					return Color.DarkBlue;
				case LogSeverity.Warn:
					return Color.Orange;
				case LogSeverity.Error:
					return Color.Red;
				default:
					return Color.Black;
			}
		}

		protected override void WndProc(ref Message m) {
			if(m.Msg == 0x004A) { // WM_COPYDATA
				HandleCopyData(ref m);
			}
			if(m.Msg >= 0xA000 && m.Msg < 0xC000) {
				_debugger.ProcessMessage((DebugMessage)m.Msg, m.WParam.ToInt64(), m.LParam.ToInt64());
			}
			base.WndProc(ref m);
		}

		private void HandleCopyData(ref Message m) {
			var hWnd = m.WParam;
			var copyData = Marshal.PtrToStructure<CopyData>(m.LParam);
			var bytes = new byte[copyData.cbData];
			Marshal.Copy(copyData.lpData, bytes, 0, bytes.Length);
			using var stream = new MemoryStream(bytes, false);
			_debugger.ReceiveData(hWnd, copyData.dwData, stream);
		}

		private void StartGameButton_Click(object sender, EventArgs e) {
			_debugger.StartProcess();
		}

		private void StopGameButton_Click(object sender, EventArgs e) {
			_debugger.TerminateProcess(true);
		}
	}
}
