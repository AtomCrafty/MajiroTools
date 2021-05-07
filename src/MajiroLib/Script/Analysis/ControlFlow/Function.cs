using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Majiro.Script.Analysis.ControlFlow {
	public class Function {

		public readonly MjoScript Script;
		public readonly uint NameHash;

		public int FirstInstructionIndex = -1;
		public int LastInstructionIndex = -1;
		public int InstructionCount => FirstInstructionIndex != -1 && LastInstructionIndex != -1
			? LastInstructionIndex - FirstInstructionIndex + 1 : -1;

		public BasicBlock EntryBlock;
		public List<BasicBlock> ExitBlocks;
		public List<BasicBlock> Blocks;

		public MjoType[] ParameterTypes;
		public MjoType[] LocalTypes;

		public uint? StartOffset => Script.Instructions[FirstInstructionIndex].Offset;
		public bool IsEntryPoint => this == Script.EntryPointFunction;

		public IEnumerable<Instruction> Instructions => Enumerable
			.Range(FirstInstructionIndex, InstructionCount)
			.Select(index => Script.Instructions[index]);

		public Function(MjoScript script, uint nameHash) {
			Script = script;
			NameHash = nameHash;
		}

		public BasicBlock BasicBlockFromOffset(uint offset) {
			return Blocks.Find(block => Script.Instructions[block.FirstInstructionIndex].Offset == offset)
				?? throw new Exception("No block found at offset " + offset);
		}

		public void SanityCheck(MjoScriptRepresentation representation) {
			switch(representation) {
				case MjoScriptRepresentation.InstructionList:
					Debug.Fail("Functions shouldn't exist in instruction list representation");
					break;
				case MjoScriptRepresentation.ControlFlowGraph:
					Debug.Assert(ParameterTypes != null);
					Debug.Assert(LocalTypes != null);
					break;
				case MjoScriptRepresentation.SsaGraph:
					Debug.Assert(ParameterTypes != null);
					Debug.Assert(LocalTypes != null);
					break;
			}

			foreach(var block in Blocks) {
				block.SanityCheck(representation);
			}
		}
	}
}
