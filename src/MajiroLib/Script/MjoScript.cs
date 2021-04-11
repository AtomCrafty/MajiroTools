using System.Collections.Generic;
using Majiro.Script.Analysis.ControlFlow;

namespace Majiro.Script {
	public class MjoScript {
		public uint EntryPointOffset;
		public readonly List<FunctionEntry> Index;
		public readonly List<Instruction> Instructions;

		public List<Function> Functions;

		public MjoScript(uint entryPointOffset, List<FunctionEntry> index, List<Instruction> instructions) {
			EntryPointOffset = entryPointOffset;
			Index = index;
			Instructions = instructions;
		}

		public int InstructionIndexFromOffset(uint offset) {
			return Instructions.FindIndex(instruction => instruction.Offset == offset);
		}
	}

	public struct FunctionEntry {
		public uint NameHash;
		public uint Offset;
	}
}