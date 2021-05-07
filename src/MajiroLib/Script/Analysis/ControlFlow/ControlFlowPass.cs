using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Majiro.Script.Analysis.ControlFlow {
	public static class ControlFlowPass {

		public static void ToControlFlowGraph(MjoScript script) {
			if(script.Representation == MjoScriptRepresentation.ControlFlowGraph) {
				script.SanityCheck();
				return;
			}

			if(script.Representation != MjoScriptRepresentation.InstructionList) {
				throw new Exception("Unable to convert script to control flow graph representation from current state: " + script.Representation);
			}

			script.Representation = MjoScriptRepresentation.InTransition;

			var functionStarts = new HashSet<int>();
			var functions = new List<Function>();
			script.Functions = functions;

			// mark function start indices
			foreach(var functionEntry in script.FunctionIndex) {
				uint offset = functionEntry.Offset;
				int index = script.InstructionIndexFromOffset(offset);
				if(index < 0) throw new Exception($"No instruction found at offset 0x{offset:x8}");

				var function = new Function(script, functionEntry.NameHash) {
					FirstInstructionIndex = index
				};

				if(functionEntry.Offset == script.EntryPointOffset)
					script.EntryPointFunction = function;

				functions.Add(function);
				functionStarts.Add(index);
			}

			// find function ends
			foreach(var function in functions) {
				for(int i = function.FirstInstructionIndex; i < script.Instructions.Count; i++) {
					if(i + 1 == script.Instructions.Count || functionStarts.Contains(i + 1)) {
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

			script.EntryPointOffset = null;
			script.FunctionIndex = null;

			foreach(var instruction in script.Instructions) {
				instruction.Offset = null;
				instruction.Size = null;
			}

			script.Representation = MjoScriptRepresentation.ControlFlowGraph;
			script.SanityCheck();
		}

		private static IEnumerable<uint> PossibleNextInstructionOffsets(Instruction instruction) {

			if(instruction.IsReturn)
				yield break;

			if(instruction.IsJump) {
				yield return (uint)(instruction.Offset!.Value + instruction.Size!.Value + instruction.JumpOffset!.Value);

				if(instruction.IsUnconditionalJump)
					yield break;
			}

			if(instruction.IsSwitch) {
				for(int i = 0; i < instruction.SwitchOffsets.Length; i++) {
					int caseOffset = instruction.SwitchOffsets[i];
					yield return (uint)(instruction.Offset!.Value
										+ 4     // opcode and case count operand
										+ 4 * i // offset of the case offset
										+ 4     // size of the case offset
										+ caseOffset);
				}
				yield break;
			}

			yield return instruction.Offset!.Value + instruction.Size!.Value;
		}

		private static void AnalyzeFunction(Function function) {
			var script = function.Script;
			var instructions = script.Instructions;

			var entryBlock = new BasicBlock(function, "entry") {
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
					string label = $"block_{offset:x4}";
					basicBlocks.Add(new BasicBlock(function, label) {
						FirstInstructionIndex = index
					});
				}
			}

			// mark basic block boundaries
			for(int i = function.FirstInstructionIndex; i <= function.LastInstructionIndex; i++) {
				var instruction = instructions[i];

				if(instruction.IsJump || instruction.IsSwitch) {
					foreach(uint offset in PossibleNextInstructionOffsets(instruction)) {
						MarkBasicBlockStart(offset);
					}
					// instruction after a jump is always a new basic block
					if(i != function.LastInstructionIndex)
						MarkBasicBlockStart(instruction.Offset!.Value + instruction.Size!.Value);
				}
				else if(instruction.IsArgCheck) {
					Debug.Assert(function.ParameterTypes == null);
					function.ParameterTypes = instruction.TypeList;
				}
				else if(instruction.IsAlloca) {
					Debug.Assert(function.LocalTypes == null);
					function.LocalTypes = instruction.TypeList;
				}
			}

			// find basic block ends
			foreach(var basicBlock in basicBlocks) {
				for(int i = basicBlock.FirstInstructionIndex; i <= function.LastInstructionIndex; i++) {
					instructions[i].Block = basicBlock;
					if(i == function.LastInstructionIndex || startIndices.Contains(i + 1)) {
						basicBlock.LastInstructionIndex = i;
						break;
					}
				}

				if(basicBlock.LastInstructionIndex == -1)
					throw new Exception("Unable to find last instruction");
			}

			basicBlocks.Sort((a, b) => a.FirstInstructionIndex - b.FirstInstructionIndex);
			function.Blocks = basicBlocks;

			// link consecutive blocks
			for(int i = 0; i < basicBlocks.Count - 1; i++) {
				var block = basicBlocks[i];
				if(block.IsUnreachable) continue;
				if(block.IsExitBlock) continue;

				var lastInstruction = instructions[block.LastInstructionIndex];
				if(lastInstruction.IsUnconditionalJump) continue;
				if(lastInstruction.IsSwitch) continue;

				var nextBlock = basicBlocks[i + 1];
				block.Successors.Add(nextBlock);
				nextBlock.Predecessors.Add(block);
			}

			foreach(var basicBlock in basicBlocks) {
				AnalyzeBasicBlock(basicBlock);
			}

			var unreachableBlocks = new Queue<BasicBlock>(basicBlocks.Where(block => block.IsUnreachable));
			while(unreachableBlocks.TryDequeue(out var block)) {
				foreach(var successor in block.Successors) {
					successor.Predecessors.Remove(block);
					if(successor.IsUnreachable)
						unreachableBlocks.Enqueue(successor);
				}
				block.Successors.Clear();
			}
		}

		private static void AnalyzeBasicBlock(BasicBlock basicBlock) {
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
				uint target = (uint)(lastInstruction.Offset!.Value + lastInstruction.Size!.Value + lastInstruction.JumpOffset!.Value);
				lastInstruction.JumpTarget = function.BasicBlockFromOffset(target);
				lastInstruction.JumpOffset = null;
			}
			else if(lastInstruction.IsSwitch) {
				lastInstruction.SwitchTargets = new BasicBlock[lastInstruction.SwitchOffsets.Length];
				for(int i = 0; i < lastInstruction.SwitchOffsets.Length; i++) {
					int caseOffset = lastInstruction.SwitchOffsets[i];
					uint target = (uint)(lastInstruction.Offset!.Value + 2 + 2 + 4 * (i + 1) + caseOffset);
					lastInstruction.SwitchTargets[i] = function.BasicBlockFromOffset(target);
				}
				lastInstruction.SwitchOffsets = null;
			}
		}

		public static void ToInstructionList(MjoScript script) {
			if(script.Representation == MjoScriptRepresentation.InstructionList) {
				script.SanityCheck();
				return;
			}

			if(script.Representation != MjoScriptRepresentation.ControlFlowGraph) {
				throw new Exception("Unable to convert script to instruction list representation from current state: " + script.Representation);
			}

			script.Representation = MjoScriptRepresentation.InTransition;

			// calculate size and offset for all instructions
			uint offset = 0;
			foreach(var instruction in script.Instructions) {
				uint size = Assembler.GetInstructionSize(instruction);
				instruction.Offset = offset;
				instruction.Size = size;
				instruction.Block = null;
				offset += size;
			}

			// resolve jump and switch targets to relative offsets
			foreach(var instruction in script.Instructions) {
				if(instruction.IsJump) {
					Debug.Assert(instruction.JumpTarget != null);
					long jumpSourceOffset = instruction.Offset!.Value + instruction.Size!.Value;
					long jumpTargetOffset = instruction.JumpTarget.StartOffset!.Value;
					instruction.JumpOffset = checked((int)(jumpTargetOffset - jumpSourceOffset));
					instruction.JumpTarget = null;
				}
				else if(instruction.IsSwitch) {
					Debug.Assert(instruction.SwitchTargets != null);
					instruction.SwitchOffsets = new int[instruction.SwitchTargets.Length];
					for(int i = 0; i < instruction.SwitchTargets.Length; i++) {
						var switchTarget = instruction.SwitchTargets[i];
						Debug.Assert(switchTarget != null);
						long jumpSourceOffset = instruction.Offset!.Value + 2 + 2 + (i + 1) * 4;
						long jumpTargetOffset = switchTarget.StartOffset!.Value;
						instruction.SwitchOffsets[i] = checked((int)(jumpTargetOffset - jumpSourceOffset));
					}
					instruction.SwitchTargets = null;
				}

				instruction.SanityCheck(MjoScriptRepresentation.InstructionList);
			}

			script.FunctionIndex = new List<FunctionIndexEntry>();
			foreach(var function in script.Functions) {
				script.FunctionIndex.Add(new FunctionIndexEntry {
					NameHash = function.NameHash,
					Offset = function.StartOffset!.Value
				});
			}
			script.Functions = null;

			script.EntryPointOffset = script.EntryPointFunction.StartOffset!.Value;
			script.EntryPointFunction = null;

			script.Representation = MjoScriptRepresentation.InstructionList;
			script.SanityCheck();
		}
	}
}
