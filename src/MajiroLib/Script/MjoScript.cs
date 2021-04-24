using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Majiro.Script.Analysis.ControlFlow;

namespace Majiro.Script {
	public class MjoScript {
		public uint EntryPointOffset;
		public readonly List<FunctionEntry> Index;
		public readonly List<Instruction> Instructions;
		public bool EnableReadMark;

		public List<Function> Functions;
		public Dictionary<string, string> ExternalizedStrings;

		public Function EntryPointFunction => Functions?.Single(func => func.StartOffset == EntryPointOffset);

		public MjoScript(uint entryPointOffset, List<FunctionEntry> index, List<Instruction> instructions) {
			EntryPointOffset = entryPointOffset;
			Index = index;
			Instructions = instructions;
		}

		public int InstructionIndexFromOffset(uint offset) {
			return Instructions.FindIndex(instruction => instruction.Offset == offset);
		}

		public void ExternalizeStrings() {
			if(ExternalizedStrings != null) return;

			ExternalizedStrings = new Dictionary<string, string>();
			int messageCount = 0;

			foreach(var instruction in Instructions.Where(inst => inst.IsText)) {
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

			foreach(var instruction in Instructions.Where(inst => inst.IsText)) {
				Debug.Assert(instruction.String == null);
				Debug.Assert(instruction.ExternalKey != null);
				
				string key = instruction.ExternalKey;
				string value = ExternalizedStrings[key];

				instruction.String = value;
				instruction.ExternalKey = null;
			}
			
			ExternalizedStrings = null;
		}
	}

	public struct FunctionEntry {
		public uint NameHash;
		public uint Offset;
	}
}