using System.Collections.Generic;
using System.Linq;
using Majiro.Script.Analysis.StackTransition;

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
		public bool IsUnreachable => !Predecessors.Any();

		public bool IsDestructorEntryBlock =>
			Predecessors.Count == 1 && Predecessors[0].LastInstruction.Opcode.Value == 0x847;
		
		public Instruction FirstInstruction => Function.Script.Instructions[FirstInstructionIndex];
		public Instruction LastInstruction => Function.Script.Instructions[LastInstructionIndex];

		public List<PhiInstruction> PhiNodes;
		public StackState StartState;
		public StackState EndState;

		public IEnumerable<Instruction> Instructions => Enumerable
			.Range(FirstInstructionIndex, InstructionCount)
			.Select(index => Function.Script.Instructions[index]);

		public uint StartOffset => Instructions.First().Offset;

		public string Name => IsEntryBlock ? "entry" :
							  IsExitBlock ? $"exit_{StartOffset:x4}" :
							  IsUnreachable ? $"unreachable_{StartOffset:x4}" :
							  IsDestructorEntryBlock ? $"destructor_{StartOffset:x4}"
											: $"block_{StartOffset:x4}";

		public BasicBlock(Function function) {
			Function = function;
		}

		public override string ToString() => Name;
	}
}
