namespace Majiro.Script {
	public sealed class Instruction {
		public readonly Opcode Opcode;
		public uint Offset;

		public MjoFlags Flags;
		public uint Hash;
		public short VarOffset;
		public int JumpOffset;
		public MjoType[] TypeList;
		public string String;
		public int IntValue;
		public float FloatValue;
		public ushort ArgumentCount;
		public ushort LineNumber;
		public short[] SwitchCases;

		public Instruction(Opcode opcode, uint offset) {
			Opcode = opcode;
			Offset = offset;
		}

		public override string ToString() => Disassembler.FormatInstruction(this);
	}
}