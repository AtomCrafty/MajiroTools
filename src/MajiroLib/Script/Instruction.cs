using System.Diagnostics;
using System.Linq;
using Majiro.Script.Analysis.ControlFlow;
using Majiro.Script.Analysis.StackTransition;
using VToolBase.Core;

namespace Majiro.Script {
	public class Instruction {
		// shared info
		public readonly Opcode Opcode;
		public MjoFlags Flags;
		public uint Hash;
		public short VarOffset;
		public MjoType[] TypeList;
		public string String;
		public string ExternalKey;
		public int IntValue;
		public float FloatValue;
		public ushort ArgumentCount;
		public ushort LineNumber;

		// InstructionList representation
		public uint? Offset;
		public uint? Size;
		public int? JumpOffset;
		public int[] SwitchOffsets;

		// ControlFlowGraph representation
		public BasicBlock Block;
		public BasicBlock JumpTarget;
		public BasicBlock[] SwitchTargets;
		public Function Function => Block?.Function;
		public MjoScript Script => Function?.Script;

		// SsaGraph representations
		public StackValue[] BeforeValues;
		public StackValue[] PoppedValues;
		public StackValue[] PushedValues;

		public bool IsJump => Opcode.IsJump;
		public bool IsUnconditionalJump => Opcode.Value == 0x82C;
		public bool IsSwitch => Opcode.Value == 0x850;
		public bool IsReturn => Opcode.Value == 0x82b;
		public bool IsArgCheck => Opcode.Value == 0x836;
		public bool IsAlloca => Opcode.Value == 0x829;
		public bool IsText => Opcode.Value == 0x840;
		public bool IsProc => Opcode.Value == 0x841;
		public bool IsCtrl => Opcode.Value == 0x842;
		public bool IsPop => Opcode.Value == 0x82f;
		public bool IsBselClr => Opcode.Value == 0x844;
		public bool IsLine => Opcode.Value == 0x83a;
		public bool IsSysCall => Opcode.Value.IsOneOf((ushort)0x834, (ushort)0x835);
		public bool IsCall => Opcode.Value.IsOneOf((ushort)0x80f, (ushort)0x810);
		public bool IsLoad => Opcode.Mnemonic.StartsWith("ld");
		public bool IsStore => Opcode.Mnemonic.StartsWith("st");
		public bool IsPhi => Opcode.Mnemonic == "phi";

		public Instruction(Opcode opcode, uint offset) {
			Opcode = opcode;
			Offset = offset;
		}

		public Instruction(Opcode opcode, BasicBlock block) {
			Opcode = opcode;
			Block = block;
		}

		public void SanityCheck(MjoScriptRepresentation representation) {
			Debug.Assert(Opcode != null);
			switch(representation) {
				case MjoScriptRepresentation.InstructionList:
					Debug.Assert(Block == null);
					Debug.Assert(JumpTarget == null);
					Debug.Assert(SwitchTargets == null);
					Debug.Assert(BeforeValues == null);
					Debug.Assert(PoppedValues == null);
					Debug.Assert(PushedValues == null);
					Debug.Assert(Offset != null);
					Debug.Assert(Size != null && Size != 0);
					Debug.Assert(IsJump ^ JumpOffset == null);
					Debug.Assert(IsSwitch ^ SwitchOffsets == null);
					break;
				case MjoScriptRepresentation.ControlFlowGraph:
					Debug.Assert(Offset == null);
					Debug.Assert(Size == null && Size != 0);
					Debug.Assert(JumpOffset == null);
					Debug.Assert(SwitchOffsets == null);
					Debug.Assert(BeforeValues == null);
					Debug.Assert(PoppedValues == null);
					Debug.Assert(PushedValues == null);
					Debug.Assert(Block != null);
					Debug.Assert(IsJump ^ JumpTarget == null);
					Debug.Assert(IsSwitch ^ SwitchTargets == null);
					break;
				case MjoScriptRepresentation.SsaGraph:
					Debug.Assert(Offset == null);
					Debug.Assert(Size == null && Size != 0);
					Debug.Assert(JumpOffset == null);
					Debug.Assert(SwitchOffsets == null);
					Debug.Assert(BeforeValues != null);
					Debug.Assert(PoppedValues != null);
					Debug.Assert(PushedValues != null);
					Debug.Assert(PushedValues.All(val => val.Producer == this));
					Debug.Assert(Block != null);
					Debug.Assert(IsJump ^ JumpTarget == null);
					Debug.Assert(IsSwitch ^ SwitchTargets == null);
					break;
			}
		}

		public override string ToString() => Disassembler.DumpInstruction(this);
	}
}