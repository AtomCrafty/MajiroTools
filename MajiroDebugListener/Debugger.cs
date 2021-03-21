using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace MajiroDebugListener {

	public delegate void StatusCallback(DebuggerStatus status);
	public delegate void LogCallback(LogSeverity severity, string message);

	public enum LogSeverity {
		Message, Info, Warn, Error
	}

	public interface IDebugger {

		LogCallback LogCallback { get; set; }
		StatusCallback StatusCallback { get; set; }
		DebuggerStatus Status { get; }

		void ProcessMessage(DebugMessage message, long wParam, long lParam);
		void Detach();
		void StartProcess();
		void TerminateProcess(bool force);
	}

	public class Debugger : IDebugger {
		private const string GamePath = @"D:\Games\Private\[Jast] Closed Game [v16700]\ClosedGAME.exe";

		private readonly IntPtr _debuggerWindowHandle;
		private IntPtr _gameWindowHandle;
		private Process _gameProcess;
		private DebuggerStatus _status = DebuggerStatus.Idle;
		public Debugger(IntPtr debuggerWindowHandle) {
			_debuggerWindowHandle = debuggerWindowHandle;
		}

		public LogCallback LogCallback { get; set; }
		public StatusCallback StatusCallback { get; set; }

		public DebuggerStatus Status {
			get => _status;
			private set {
				if(_status == value) return;
				_status = value;
				StatusCallback(value);
			}
		}
		public void ProcessMessage(DebugMessage message, long wParam, long lParam) {
			LogMessageReceived(message, wParam, lParam);

			switch(message) {
				case DebugMessage.Attach:
					ProcessAttachMessage(message, wParam, lParam);
					break;
				case DebugMessage.Detach:
					ProcessDetachMessage(message, wParam, lParam);
					break;
				case DebugMessage.Heartbeat:
					SendMessage(DebugMessage.Respond, 0, 0);
					break;
			}
		}

		private void ProcessDetachMessage(DebugMessage message, long wParam, long lParam) {

			_gameWindowHandle = IntPtr.Zero;
			Info(@"---------------------------------------");
		}

		private void ProcessAttachMessage(DebugMessage message, long wParam, long lParam) {
			Debug.Assert(wParam != 0);

			switch(_status) {
				case DebuggerStatus.Idle: {
						Debug.Assert(_gameWindowHandle == IntPtr.Zero);
						Debug.Assert(_gameProcess == null);

						_gameWindowHandle = new IntPtr(wParam);
						try {
							Native.GetWindowThreadProcessId(_gameWindowHandle, out uint processId);
							_gameProcess = Process.GetProcessById((int)processId);
						}
						catch(ArgumentException) {
							Warn("Unable to detect game process");
						}

						Status = DebuggerStatus.Attached;
						break;
					}

				case DebuggerStatus.Waiting: {
						Debug.Assert(_gameWindowHandle == IntPtr.Zero);
						Debug.Assert(_gameProcess != null);

						_gameWindowHandle = new IntPtr(wParam);
						Native.GetWindowThreadProcessId(_gameWindowHandle, out uint processId);

						Debug.Assert(_gameProcess.Id == processId);

						Status = DebuggerStatus.Attached;
						break;
					}

				default:
					Warn("Unexpected attach message");
					break;
			}
		}

		public void Detach() {
			throw new NotImplementedException();
		}

		public void StartProcess() {
			if(_gameProcess != null) {
				Error("A game process is already running");
				return;
			}

			if(_status != DebuggerStatus.Idle) {
				Error("Can only start a game process while in idle state");
				return;
			}

			_gameProcess = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = GamePath,
					Arguments = $@"/DEBUG:{_debuggerWindowHandle.ToInt32()}"
				},
				EnableRaisingEvents = true
			};
			_gameProcess.Exited += (o, args) => {
				_gameProcess = null;
				_gameWindowHandle = IntPtr.Zero;
			};

			try {
				_gameProcess.Start();
			}
			catch(Exception e) {
				Error("Failed to start game process: " + e.Message);
				_gameProcess = null;
			}

			if(_gameProcess.HasExited) {
				Error("Game process exited unexpectedly");
				_gameProcess = null;
				return;
			}

			Status = DebuggerStatus.Waiting;
		}

		public void TerminateProcess(bool force) {
			switch(_status) {
				case DebuggerStatus.Waiting:
				case DebuggerStatus.Attached:
					Debug.Assert(_status == DebuggerStatus.Attached ^ _gameWindowHandle == IntPtr.Zero);
					Debug.Assert(_gameProcess != null);

					if(force)
						_gameProcess.Kill();
					else
						_gameProcess.CloseMainWindow();

					_gameWindowHandle = IntPtr.Zero;
					_gameProcess = null;
					Status = DebuggerStatus.Idle;
					break;
			}
		}

		public void SendMessage(DebugMessage message, long wParam = 0, long lParam = 0) {
			LogMessageSent(message, wParam, lParam);
			Native.SendMessage(_gameWindowHandle.ToInt32(), (uint)message, wParam, lParam);
		}

		#region Logging

		public void Log(LogSeverity severity, string text) => LogCallback?.Invoke(severity, text);
		public void Info(string text) => Log(LogSeverity.Info, text);
		public void Warn(string text) => Log(LogSeverity.Warn, text);
		public void Error(string text) => Log(LogSeverity.Error, text);

		public void LogMessageReceived(DebugMessage message, long wParam, long lParam) =>
			Log(LogSeverity.Message, $@"Recv: {(uint)message:X4} {wParam:X8} {lParam:X8} - {message}");

		public void LogMessageSent(DebugMessage message, long wParam, long lParam) =>
			Log(LogSeverity.Message, $@"Send: {(uint)message:X4} {wParam:X8} {lParam:X8} - {message}");

		#endregion
	}
}
