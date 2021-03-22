namespace MajiroDebugListener {
	public enum DebugMessage : ushort {
		Attach = 0xA000,
		Detach = 0xA001,
		Handshake = 0xA002,
		Acknowledge = 0xA003,
		AddBreak = 0xA004,
		ClearBreak = 0xA005,
		StepIn = 0xA007,
		Step = 0xA008,
		StepOut = 0xA009,
		Resume = 0xA00A,
		DelBreak = 0xA00C,
		Pause = 0xA00D,
		ReadVar = 0xA00E,
		ReadString = 0xA00F,
		ReadLocal = 0xA011,
		ReadLocalInt = 0xA012,
		ReadVarInt = 0xA013
	}
}
