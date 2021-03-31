using System;
using System.Collections.Generic;

namespace Majiro.Script.Analysis {
	public class ControlFlowGraph {

		public readonly List<Function> Functions;
		public ControlFlowGraph(List<Function> functions) {
			Functions = functions;
		}

		public static ControlFlowGraph BuildFromScript(MjoScript script) {
			var startIndices = new HashSet<int>();
			var functions = new List<Function>();

			// mark function start indices
			foreach(var functionEntry in script.Functions) {
				uint offset = functionEntry.Offset;
				int index = script.InstructionIndexFromOffset(offset);
				if(index < 0) throw new Exception($"No instruction found at offset 0x{offset:x8}");

				functions.Add(new Function(script, functionEntry.NameHash) {
					FirstInstructionIndex = index
				});
				startIndices.Add(index);
			}

			// find function ends
			foreach(var function in functions) {
				for(int i = function.FirstInstructionIndex; i < script.Instructions.Count; i++) {
					if(i + 1 == script.Instructions.Count || startIndices.Contains(i + 1)) {
						function.LastInstructionIndex = i;
						break;
					}
				}

				if(function.LastInstructionIndex == -1)
					throw new Exception("Unable to find last instruction");
			}

			foreach(var function in functions) {
				AnalyzeFunction(function);
			}

			return new ControlFlowGraph(functions);
		}

		public static IEnumerable<uint> PossibleNextInstructionOffsets(Instruction instruction) {

			yield return instruction.Offset + instruction.Size;

			if(instruction.Opcode.IsJump) {
				yield return (uint)(instruction.Offset + instruction.Size + instruction.JumpOffset);
				yield break;
			}

			if(instruction.SwitchCases != null) {
				foreach(short caseOffset in instruction.SwitchCases) {
					yield return (uint)(instruction.Offset + instruction.Size + caseOffset);
				}
			}
		}

		public static void AnalyzeFunction(Function function) {
			var script = function.Script;
			var instructions = script.Instructions;

			var entryBlock = new BasicBlock(function) {
				FirstInstructionIndex = function.FirstInstructionIndex,
				IsEntryBlock = true
			};
			function.EntryBlock = entryBlock;
			function.ExitBlocks = new List<BasicBlock>();

			var startIndices = new HashSet<int> { function.FirstInstructionIndex };
			var basicBlocks = new List<BasicBlock> { entryBlock };

			void MarkBasicBlockStart(uint offset) {
				int index = script.InstructionIndexFromOffset(offset);
				if(index == -1)
					throw new Exception("Unable to determine jump target");

				if(startIndices.Add(index)) {
					basicBlocks.Add(new BasicBlock(function) {
						FirstInstructionIndex = index
					});
				}
			}

			// mark basic block boundaries
			for(int i = function.FirstInstructionIndex; i < function.LastInstructionIndex; i++) {
				var instruction = instructions[i];

				if(instruction.IsJump || instruction.IsSwitch) {
					foreach(uint offset in PossibleNextInstructionOffsets(instruction)) {
						MarkBasicBlockStart(offset);
					}
				}
				else if(instruction.IsArgCheck) {
					function.ParameterTypes = instruction.TypeList;
				}
			}

			// find basic block ends
			foreach(var basicBlock in basicBlocks) {
				for(int i = basicBlock.FirstInstructionIndex; i <= function.LastInstructionIndex; i++) {
					if(i == function.LastInstructionIndex || startIndices.Contains(i + 1)) {
						basicBlock.LastInstructionIndex = i;
						break;
					}
				}

				if(basicBlock.LastInstructionIndex == -1)
					throw new Exception("Unable to find last instruction");
			}

			basicBlocks.Sort((a, b) => a.FirstInstructionIndex - b.FirstInstructionIndex);
			function.BasicBlocks = basicBlocks;

			foreach(var basicBlock in basicBlocks) {
				AnalyzeBasicBlock(basicBlock);
			}
		}

		public static void AnalyzeBasicBlock(BasicBlock basicBlock) {
			var function = basicBlock.Function;
			var script = function.Script;
			var instructions = script.Instructions;

			var lastInstruction = instructions[basicBlock.LastInstructionIndex];

			if(lastInstruction.IsReturn) {
				function.ExitBlocks.Add(basicBlock);
				basicBlock.IsExitBlock = true;
				return;
			}

			foreach(uint offset in PossibleNextInstructionOffsets(lastInstruction)) {
				var nextBlock = function.BasicBlockFromOffset(offset);
				if(nextBlock == null)
					throw new Exception("Invalid jump target");
				basicBlock.Successors.Add(nextBlock);
				nextBlock.Predecessors.Add(basicBlock);
			}

			if(lastInstruction.IsJump) {
				uint target = (uint)(lastInstruction.Offset + lastInstruction.Size + lastInstruction.JumpOffset);
				lastInstruction.JumpTarget = function.BasicBlockFromOffset(target);
			}
			else if(lastInstruction.IsSwitch) {
				lastInstruction.SwitchTargets = new BasicBlock[lastInstruction.SwitchCases.Length];
				for(int i = 0; i < lastInstruction.SwitchCases.Length; i++) {
					short caseOffset = lastInstruction.SwitchCases[i];
					uint target = (uint)(lastInstruction.Offset + lastInstruction.Size + caseOffset);
					lastInstruction.SwitchTargets[i] = function.BasicBlockFromOffset(target);
				}
			}
		}
	}
}
