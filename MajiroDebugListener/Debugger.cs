﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MajiroDebugListener {

	public delegate void StatusCallback(DebuggerState state);
	public delegate void LogCallback(LogSeverity severity, string message);

	public enum LogSeverity {
		Message, Info, Warn, Error
	}

	public interface IDebugger {

		LogCallback LogCallback { get; set; }
		StatusCallback StatusCallback { get; set; }
		DebuggerState State { get; }

		void ProcessMessage(DebugMessage message, long wParam, long lParam);
		void ReceiveData(IntPtr hWnd, IntPtr dwData, int cbData, IntPtr lpData);
		void StartProcess();
		void TerminateProcess(bool force);
		void Pause();
		void Resume();
		void StepIn();
		void StepOver();
		void StepOut();
	}

	public class Debugger : IDebugger {
		private const string GamePath = @"D:\Games\Private\[Jast] Closed Game [v16700]\ClosedGAME.exe";

		private readonly IntPtr _debuggerWindowHandle;
		private IntPtr _gameWindowHandle;
		private Process _gameProcess;
		private DebuggerState _state = DebuggerState.Idle;
		public Debugger(IntPtr debuggerWindowHandle) {
			_debuggerWindowHandle = debuggerWindowHandle;
		}

		public LogCallback LogCallback { get; set; }
		public StatusCallback StatusCallback { get; set; }

		public DebuggerState State {
			get => _state;
			private set {
				if(_state == value) return;
				_state = value;
				StatusCallback(value);
			}
		}

		public bool IsRunning => _state.IsOneOf(DebuggerState.Waiting, DebuggerState.Attached, DebuggerState.Suspended);

		public bool IsAttached => _state.IsOneOf(DebuggerState.Attached, DebuggerState.Suspended);

		#region Message Handling

		public void ProcessMessage(DebugMessage message, long wParam, long lParam) {
			LogMessageReceived(message, wParam, lParam);

			switch(message) {
				case DebugMessage.Attach:
					ProcessAttachMessage(message, wParam, lParam);
					break;
				case DebugMessage.Detach:
					ProcessDetachMessage(message, wParam, lParam);
					break;
				case DebugMessage.Handshake:
					SendMessage(DebugMessage.Acknowledge);
					break;
			}
		}

		private void ProcessAttachMessage(DebugMessage message, long wParam, long lParam) {
			Debug.Assert(wParam != 0);

			switch(_state) {
				case DebuggerState.Idle: {
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

						State = DebuggerState.Attached;
						break;
					}

				case DebuggerState.Waiting: {
						Debug.Assert(_gameWindowHandle == IntPtr.Zero);
						Debug.Assert(_gameProcess != null);

						_gameWindowHandle = new IntPtr(wParam);
						Native.GetWindowThreadProcessId(_gameWindowHandle, out uint processId);

						Debug.Assert(_gameProcess.Id == processId);

						State = DebuggerState.Attached;
						break;
					}

				default:
					Warn("Unexpected attach message");
					break;
			}
		}

		private void ProcessDetachMessage(DebugMessage message, long wParam, long lParam) {
			_gameWindowHandle = IntPtr.Zero;
			State = DebuggerState.Idle;
		}

		#endregion

		#region Data Processing

		public void ReceiveData(IntPtr hWnd, IntPtr dwData, int cbData, IntPtr lpData) {
			if(_state != DebuggerState.Attached && _state != DebuggerState.Suspended) {
				Error("Unexpected copy command");
				return;
			}

			Debug.Assert(hWnd == _gameWindowHandle);
			Log(LogSeverity.Message, $@"Copy:      {hWnd.ToInt32():X8} {dwData.ToInt32():X8} - {cbData} bytes received");

			switch(dwData.ToInt32()) {
				case 0:
					ProcessBreakpointMessage(Marshal.PtrToStructure<BreakpointMessage>(lpData));
					break;

				default:
					Warn("Unrecognized data type: " + dwData);
					break;
			}
		}

		[StructLayout(LayoutKind.Explicit, Size = 0x48)]
		private struct BreakpointMessage {
			[FieldOffset(0x00)]
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x40)]
			private readonly byte[] _scriptNameRaw;

			[FieldOffset(0x44)]
			public readonly int LineNumber;

			public string ScriptName => _scriptNameRaw.ToNullTerminatedString();
		}

		private void ProcessBreakpointMessage(BreakpointMessage message) {
			string scriptName = message.ScriptName;
			int lineNumber = message.LineNumber;

			Info($@"Triggered breakpoint in script {scriptName}, line {lineNumber}");
			State = DebuggerState.Suspended;
		}

		#endregion

		public void StartProcess() {
			if(IsRunning) {
				Error("A game process is already running");
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
				Info("Game process exited");
				_gameProcess = null;
				_gameWindowHandle = IntPtr.Zero;
				Info(@"---------------------------------------");
				State = DebuggerState.Idle;
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

			State = DebuggerState.Waiting;
		}

		public void TerminateProcess(bool force) {
			if(!IsRunning) {
				Error("There is no game process to terminate");
				return;
			}

			Debug.Assert(_state == DebuggerState.Waiting ^ _gameWindowHandle != IntPtr.Zero);
			Debug.Assert(_gameProcess != null);

			if(force)
				_gameProcess.Kill();
			else
				_gameProcess.CloseMainWindow();

			_gameWindowHandle = IntPtr.Zero;
			_gameProcess = null;
			State = DebuggerState.Idle;
		}

		public void Pause() {
			if(!IsAttached) {
				Error("No process is attached");
				return;
			}
			if(_state == DebuggerState.Suspended) {
				Warn("The process is already paused");
				return;
			}

			SendMessage(DebugMessage.Pause);
			State = DebuggerState.Suspended;
		}

		public void Resume() {
			if(!IsAttached) {
				Error("No process is attached");
				return;
			}
			if(_state != DebuggerState.Suspended) {
				Warn("The process is not paused");
				return;
			}

			SendMessage(DebugMessage.Resume);
			State = DebuggerState.Attached;
		}

		public void StepIn() {
			if(!IsAttached) {
				Error("No process is attached");
				return;
			}
			if(_state != DebuggerState.Suspended) {
				Warn("The process is not paused");
				return;
			}

			SendMessage(DebugMessage.StepIn);
			State = DebuggerState.Attached;
		}

		public void StepOver() {
			if(!IsAttached) {
				Error("No process is attached");
				return;
			}
			if(_state != DebuggerState.Suspended) {
				Warn("The process is not paused");
				return;
			}

			SendMessage(DebugMessage.Step);
			State = DebuggerState.Attached;
		}

		public void StepOut() {
			if(!IsAttached) {
				Error("No process is attached");
				return;
			}
			if(_state != DebuggerState.Suspended) {
				Warn("The process is not paused");
				return;
			}

			SendMessage(DebugMessage.StepOut);
			State = DebuggerState.Attached;
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
