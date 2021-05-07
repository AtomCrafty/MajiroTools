using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Majiro.Project;
using Majiro.Script.Analysis.ControlFlow;

namespace Majiro.Script {
	public enum MjoScriptRepresentation {
		InstructionList,
		ControlFlowGraph,
		SsaGraph,

		InTransition
	}

	public struct FunctionIndexEntry {
		public uint NameHash;
		public uint Offset;
	}

	public class MjoScript {
		// general info
		public MjProject Project;
		public bool EnableReadMark;
		public readonly List<Instruction> Instructions = new List<Instruction>();
		public Dictionary<string, string> ExternalizedStrings;
		public MjoScriptRepresentation Representation;

		// InstructionList representation
		public List<FunctionIndexEntry> FunctionIndex;
		public uint? EntryPointOffset;

		// ControlFlowGraph representation
		public List<Function> Functions;
		public IEnumerable<BasicBlock> Blocks => Functions?.SelectMany(func => func.Blocks);
		public Function EntryPointFunction;

		public void SanityCheck() {
			switch(Representation) {
				case MjoScriptRepresentation.InstructionList:
					Debug.Assert(Instructions != null);
					Debug.Assert(FunctionIndex != null);
					Debug.Assert(EntryPointOffset != null);
					Debug.Assert(Functions == null);
					Debug.Assert(EntryPointFunction == null);
					break;
				case MjoScriptRepresentation.ControlFlowGraph:
					Debug.Assert(Instructions != null);
					Debug.Assert(FunctionIndex == null);
					Debug.Assert(EntryPointOffset == null);
					Debug.Assert(Functions != null);
					Debug.Assert(EntryPointFunction != null);
					break;
				case MjoScriptRepresentation.SsaGraph:
					Debug.Assert(Instructions != null);
					Debug.Assert(FunctionIndex == null);
					Debug.Assert(EntryPointOffset == null);
					Debug.Assert(Functions != null);
					Debug.Assert(EntryPointFunction != null);
					break;
			}

			if(Functions != null) {
				foreach(var function in Functions) {
					function.SanityCheck(Representation);
				}
			}

			if(Instructions != null) {
				foreach(var instruction in Instructions) {
					instruction.SanityCheck(Representation);
				}
			}
		}

		public int InstructionIndexFromOffset(uint offset) {
			return Instructions.FindIndex(instruction => instruction.Offset == offset);
		}

		public void ExternalizeStrings(bool externalizeLiterals) {
			if(ExternalizedStrings != null) return;

			ExternalizedStrings = new Dictionary<string, string>();
			int messageCount = 0;

			foreach(var instruction in Instructions) {
				if(instruction.String == null) continue;
				if(!externalizeLiterals && !instruction.IsText) continue;

				Debug.Assert(instruction.String != null);
				Debug.Assert(instruction.ExternalKey == null);

				string value = instruction.String;
				string key = "L" + ++messageCount;
				ExternalizedStrings.Add(key, value);

				instruction.String = null;
				instruction.ExternalKey = key;
			}
		}

		public void InternalizeStrings() {
			if(ExternalizedStrings == null) return;

			foreach(var instruction in Instructions) {
				if(instruction.ExternalKey == null) continue;

				Debug.Assert(instruction.String == null);
				Debug.Assert(instruction.ExternalKey != null);

				string key = instruction.ExternalKey;
				string value = ExternalizedStrings[key];

				instruction.String = value;
				instruction.ExternalKey = null;
			}

			ExternalizedStrings = null;
		}

		public void ToInstructionList() => ControlFlowPass.ToInstructionList(this);
	}
}