using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Majiro.Project;
using Majiro.Script.Analysis.ControlFlow;

namespace Majiro.Script {
	public enum MjoScriptRepresentation {
		InstructionList,
		ControlFlowGraph,
		SsaGraph,
		SyntaxTree,

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
		public List<Instruction> Instructions;
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
					Debug.Assert(Instructions == null);
					Debug.Assert(FunctionIndex == null);
					Debug.Assert(EntryPointOffset == null);
					Debug.Assert(Functions != null);
					Debug.Assert(EntryPointFunction != null);
					break;
				case MjoScriptRepresentation.SsaGraph:
					Debug.Assert(Instructions == null);
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
			if(Representation == MjoScriptRepresentation.InstructionList)
				throw new Exception("Unable to perform string externalization in " + Representation + " representation");

			ExternalizedStrings = new Dictionary<string, string>();
			int messageCount = 0;

			foreach(var block in Blocks) {
				for(int i = 0; i < block.Instructions.Count; i++) {
					var instruction = block.Instructions[i];
					if(!instruction.IsText) continue;

					// process the entire (multi-line) text block
					var sb = new StringBuilder();
					var last = instruction;
					int nextIndex = i;
					while(true) {
						var next = block.Instructions[nextIndex++];
						if(next.IsText) {
							sb.Append(next.String);
						}
						else if(next.IsProc) {
							Debug.Assert(last.IsText);
						}
						else if(next.IsCtrl && next.String == "n") {
							Debug.Assert(last.IsProc);
							sb.Append('\n');
						}
						else if(next.IsCtrl && next.String == "p") {
							Debug.Assert(last.IsProc);
							break;
						}
						else if(next.IsLine) {
							continue;
						}
						else {
							//Debug.Fail("Unexpected instruction: " + next);
							break;
						}
						last = next;
					}

					// remove all additional instructions
					int instructionCount = nextIndex - i;
					Debug.Assert(instructionCount >= 3);
					if(nextIndex != 3) {
						block.Instructions.RemoveRange(i + 1, instructionCount - 3);
					}

					Debug.Assert(instruction.String != null);
					Debug.Assert(instruction.ExternalKey == null);

					string value = sb.ToString();
					string key = "L" + ++messageCount;
					ExternalizedStrings.Add(key, value);

					instruction.String = null;
					instruction.ExternalKey = key;
				}
			}

			foreach(var instruction in Functions.SelectMany(function => function.Instructions)) {
				if(instruction.String == null) continue;
				if(!externalizeLiterals && !instruction.IsText) continue;
			}
		}

		public void InternalizeStrings() {
			if(ExternalizedStrings == null) return;
			if(Representation == MjoScriptRepresentation.InstructionList)
				throw new Exception("Unable to perform string internalization in " + Representation + " representation");

			foreach(var block in Blocks) {
				for(int i = 0; i < block.Instructions.Count; i++) {
					var instruction = block.Instructions[i];
					if(instruction.ExternalKey == null) continue;
					if(!ExternalizedStrings.TryGetValue(instruction.ExternalKey, out string text))
						throw new Exception("Unable to resolve external string resource " + instruction.ExternalKey);
					
					Debug.Assert(instruction.String == null);
					Debug.Assert(instruction.ExternalKey != null);

					var lines = text.Split('\n');
					instruction.ExternalKey = null;
					instruction.String = lines[0];

					// insert "proc - ctrl n - text" sequence for each additional line
					foreach(string additionalLine in lines.Skip(1)) {
						block.Instructions.InsertRange(i + 1, new[] {
							new Instruction(Opcode.ByMnemonic["proc"], block),
							new Instruction(Opcode.ByMnemonic["ctrl"], block) { String = "n" },
							new Instruction(Opcode.ByMnemonic["text"], block) { String = additionalLine }
						});
						i += 3;
					}
				}
			}

			ExternalizedStrings = null;
		}

		public void ToInstructionList() => ControlFlowPass.ToInstructionList(this);
		public void ToControlFlowGraph() => ControlFlowPass.ToControlFlowGraph(this);
	}
}