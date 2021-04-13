using Majiro.Script.Analysis.ControlFlow;
using Majiro.Script.Analysis.StackTransition;
using VToolBase.Core;

namespace Majiro.Script {
	public class Instruction {
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
		public int[] SwitchCases;

		public BasicBlock Block;
		public BasicBlock JumpTarget;
		public BasicBlock[] SwitchTargets;
		public StackState StackState;

		public bool IsJump => Opcode.IsJump;
		public bool IsUnconditionalJump => Opcode.Value == 0x82C;
		public bool IsSwitch => Opcode.Value == 0x850;
		public bool IsReturn => Opcode.Value == 0x82b;
		public bool IsArgCheck => Opcode.Value == 0x836;
		public bool IsAlloca => Opcode.Value == 0x829;
		public bool IsSysCall => Opcode.Value.IsOneOf((ushort)0x834, (ushort)0x835);
		public bool IsCall => Opcode.Value.IsOneOf((ushort)0x80f, (ushort)0x810);
		public bool IsPhi => Opcode.Mnemonic == "phi";

		public Instruction(Opcode opcode, uint offset) {
			Opcode = opcode;
			Offset = offset;
		}

		public override string ToString() => Disassembler.DumpInstruction(this);
	}
}