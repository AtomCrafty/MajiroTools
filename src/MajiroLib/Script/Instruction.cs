using Majiro.Script.Analysis;

namespace Majiro.Script {
	public sealed class Instruction {
		public readonly Opcode Opcode;
		public uint Offset;
		public uint Size;

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

		public BasicBlock JumpTarget;
		public BasicBlock[] SwitchTargets;

		public bool IsJump => Opcode.IsJump;
		public bool IsSwitch => Opcode.Mnemonic == "switch";
		public bool IsReturn => Opcode.Mnemonic == "return";
		public bool IsArgCheck => Opcode.Mnemonic == "argcheck";
		public bool IsSysCall => Opcode.Mnemonic == "syscall" || Opcode.Mnemonic == "syscallp";
		public bool IsCall => Opcode.Mnemonic == "call" || Opcode.Mnemonic == "callp";
		public bool IsLoad => Opcode.Mnemonic.StartsWith("ld");
		public bool IsStore => Opcode.Mnemonic.StartsWith("st");

		public Instruction(Opcode opcode, uint offset) {
			Opcode = opcode;
			Offset = offset;
		}

		public override string ToString() => Disassembler.FormatInstruction(this);
	}
}