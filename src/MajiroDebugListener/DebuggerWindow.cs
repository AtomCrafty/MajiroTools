using System;
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
			UpdateStatusDisplay(_debugger.State);
		}

		private void UpdateDebuggerControls(DebuggerState state) {
			bool isRunning = state == DebuggerState.Attached;
			bool isSuspended = state == DebuggerState.Suspended;
			PauseButton.Enabled = isRunning;
			ResumeButton.Enabled = isSuspended;
			StepInButton.Enabled = isSuspended;
			StepOverButton.Enabled = isSuspended;
			StepOutButton.Enabled = isSuspended;
		}

		private void UpdateStatusDisplay(DebuggerState state) {
			Invoke(() => {
				StatusLabel.Text = $@"State: {state}";
				switch(state) {
					case DebuggerState.Idle:
						StartGameButton.Enabled = true;
						StopGameButton.Enabled = false;
						break;

					case DebuggerState.Waiting:
					case DebuggerState.Attached:
					case DebuggerState.Suspended:
						StartGameButton.Enabled = false;
						StopGameButton.Enabled = true;
						break;

					default:
						StartGameButton.Enabled = false;
						StopGameButton.Enabled = false;
						break;
				}

				UpdateDebuggerControls(state);
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
				if(selectionLength > 0)
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
				_debugger.ProcessMessage((DebugMessage)m.Msg, m.WParam.ToInt32(), m.LParam.ToInt32());
			}
			base.WndProc(ref m);
		}

		private void HandleCopyData(ref Message m) {
			var hWnd = m.WParam;
			var copyData = Marshal.PtrToStructure<CopyData>(m.LParam);
			_debugger.ReceiveData(hWnd, copyData.dwData, copyData.cbData, copyData.lpData);
			//var bytes = new byte[copyData.cbData];
			//Marshal.Copy(copyData.lpData, bytes, 0, bytes.Length);
			//using var stream = new MemoryStream(bytes, false);
			//_debugger.ReceiveData(hWnd, copyData.dwData, stream);
		}

		private void StartGameButton_Click(object sender, EventArgs e) => _debugger.StartProcess();
		private void StopGameButton_Click(object sender, EventArgs e) => _debugger.TerminateProcess(true);
		private void PauseButton_Click(object sender, EventArgs e) => _debugger.Pause();
		private void ResumeButton_Click(object sender, EventArgs e) => _debugger.Resume();
		private void StepInButton_Click(object sender, EventArgs e) => _debugger.StepIn();
		private void StepOverButton_Click(object sender, EventArgs e) => _debugger.StepOver();
		private void StepOutButton_Click(object sender, EventArgs e) => _debugger.StepOut();
	}
}
