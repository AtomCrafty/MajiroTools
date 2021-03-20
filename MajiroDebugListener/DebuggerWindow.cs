using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MajiroDebugListener {
	public partial class DebuggerWindow : Form {
		private const string GamePath = @"D:\Games\Private\[Jast] Closed Game [v16700]\ClosedGAME.exe";

		[DllImport("user32.dll", EntryPoint = "SendMessage")]
		public static extern int SendMessageImpl(int hWnd, uint uMsg, long wParam, long lParam);

		private void SendMessage(DebugMessage uMsg, long wParam, long lParam) {
			SendMessageImpl(_gameWindowHandle.ToInt32(), (uint)uMsg, wParam, lParam);
			Log(_gameWindowHandle, uMsg, wParam, lParam, true);
		}

		private IntPtr _gameWindowHandle;
		private Process _gameProcess;

		private string DebugFlag => $@"/DEBUG:{Handle.ToInt32()}";

		public DebuggerWindow() {
			InitializeComponent();
		}

		private void DebuggerWindow_Load(object sender, EventArgs e) {
			DebugFlagTextBox.Text = DebugFlag;
			UpdateStatus();
		}

		private void Log(string text) {
			ProtocolTextBox.AppendText(text + Environment.NewLine);
		}

		private void Log(IntPtr hWnd, DebugMessage uMsg, long wParam, long lParam, bool send) {
			Log($"{(send ? "Send" : "Recv")}: {hWnd.ToInt32():X8} 0x{uMsg:X} {wParam:X8} {lParam:X8} - {uMsg}");
		}

		enum GameStatus {
			None,
			Running,
			Exited,
			Error
		}

		enum DebugMessage : ushort {
			Attach = 0xA000,
			Detach = 0xA001,
			Heartbeat = 0xA002,
			Respond = 0xA003,
			A004 = 0xA004,
			A005 = 0xA005,
			A006 = 0xA006,
			A007 = 0xA007,
			A008 = 0xA008,
			A009 = 0xA009,
			A00A = 0xA00A,
			A00B = 0xA00B,
			A00C = 0xA00C,
			A00D = 0xA00D,
			A00E = 0xA00E,
			A00F = 0xA00F,
			A010 = 0xA010,
			A011 = 0xA011,
			A012 = 0xA012,
			A013 = 0xA013,
		}

		private GameStatus GetStatus() {
			if(_gameProcess == null) return GameStatus.None;
			if(_gameProcess.HasExited) return GameStatus.Exited;
			return GameStatus.Running;
		}

		private void SetStatus(GameStatus status) {
			Invoke((Action)(() => {
				StatusLabel.Text = status == GameStatus.None ? "" : $@"Status: {status}";
				StartGameButton.Enabled = status != GameStatus.Running;
				StopGameButton.Enabled = status == GameStatus.Running;
			}));
		}

		private void UpdateStatus() => SetStatus(GetStatus());

		protected override void WndProc(ref Message m) {
			if(m.Msg < 0xA000 || m.Msg >= 0xC000) {
				base.WndProc(ref m);
				return;
			}

			var message = (DebugMessage)m.Msg;
			Log(m.HWnd, message, m.WParam.ToInt64(), m.LParam.ToInt64(), false);

			switch(message) {
				case DebugMessage.Attach:
					_gameWindowHandle = m.WParam;
					break;
				case DebugMessage.Detach:
					_gameWindowHandle = IntPtr.Zero;
					break;
				case DebugMessage.Heartbeat:
					SendMessage(DebugMessage.Respond, 0, 0);
					break;
			}

			base.WndProc(ref m);
		}

		private void StartGameButton_Click(object sender, EventArgs e) {
			_gameProcess = Process.Start(GamePath, DebugFlag);
			if(_gameProcess == null) {
				SetStatus(GameStatus.Error);
				return;
			}
			_gameProcess.EnableRaisingEvents = true;
			_gameProcess.Exited += (o, args) => {
				SetStatus(GameStatus.Exited);
				_gameProcess = null;
			};
			UpdateStatus();
		}

		private void StopGameButton_Click(object sender, EventArgs e) {
			if(_gameProcess == null) return;
			_gameProcess.Kill();
			UpdateStatus();
		}
	}
}
