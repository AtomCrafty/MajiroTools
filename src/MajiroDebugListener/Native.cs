using System;
using System.Runtime.InteropServices;

namespace MajiroDebugListener {
	public static class Native {

		[DllImport("user32.dll", EntryPoint = "SendMessage")]
		public static extern int SendMessage(int hWnd, uint uMsg, long wParam, long lParam);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct CopyData {
		public IntPtr dwData;
		public int cbData;
		public IntPtr lpData;
	}
}
