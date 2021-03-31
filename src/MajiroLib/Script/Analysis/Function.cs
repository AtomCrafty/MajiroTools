using System.Collections.Generic;
using System.Linq;

namespace Majiro.Script.Analysis {
	public class Function {

		public readonly MjoScript Script;
		public readonly uint NameHash;

		public int FirstInstructionIndex = -1;
		public int LastInstructionIndex = -1;
		public int InstructionCount => FirstInstructionIndex != -1 && LastInstructionIndex != -1 
			? LastInstructionIndex - FirstInstructionIndex + 1 : -1;

		public BasicBlock EntryBlock;
		public List<BasicBlock> ExitBlocks;
		public List<BasicBlock> BasicBlocks;

		public MjoType[] ParameterTypes;

		public IEnumerable<Instruction> Instructions => Enumerable
			.Range(FirstInstructionIndex, InstructionCount)
			.Select(index => Script.Instructions[index]);

		public Function(MjoScript script, uint nameHash) {
			Script = script;
			NameHash = nameHash;
		}

		public BasicBlock BasicBlockFromOffset(uint offset) {
			return BasicBlocks.Find(block => Script.Instructions[block.FirstInstructionIndex].Offset == offset);
		}
	}
}
