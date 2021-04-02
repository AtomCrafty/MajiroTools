using System.Collections.Generic;
using System.Linq;

namespace Majiro.Script.Analysis.ControlFlow {
	public class BasicBlock {
		public readonly Function Function;

		public int FirstInstructionIndex = -1;
		public int LastInstructionIndex = -1;
		public int InstructionCount => FirstInstructionIndex != -1 && LastInstructionIndex != -1
			? LastInstructionIndex - FirstInstructionIndex + 1 : -1;

		public bool IsEntryBlock;
		public bool IsExitBlock;

		public List<BasicBlock> Predecessors = new List<BasicBlock>();
		public List<BasicBlock> Successors = new List<BasicBlock>();

		public IEnumerable<Instruction> Instructions => Enumerable
			.Range(FirstInstructionIndex, InstructionCount)
			.Select(index => Function.Script.Instructions[index]);

		public uint StartOffset => Instructions.First().Offset;

		public string Name =>
			IsEntryBlock ? "entry" : IsExitBlock ? $"exit_{StartOffset:x4}" : $"block_{StartOffset:x4}";

		public BasicBlock(Function function) {
			Function = function;
		}
	}
}
