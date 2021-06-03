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

			var functionStarts = new Dictionary<int, Function>();
			var functions = new List<Function>();
			script.Functions = functions;

			// mark function start indices
			foreach(var functionEntry in script.FunctionIndex) {
				uint offset = functionEntry.Offset;
				int index = script.InstructionIndexFromOffset(offset);
				if(index < 0) throw new Exception($"No instruction found at offset 0x{offset:x8}");

				var function = new Function(script, functionEntry.NameHash);

				if(functionEntry.Offset == script.EntryPointOffset)
					script.EntryPointFunction = function;

				functions.Add(function);
				functionStarts.Add(index, function);
			}

			// find function ends
			foreach((int firstInstructionIndex, var function) in functionStarts) {
				int lastInstructionIndex = -1;
				for(int i = firstInstructionIndex; i < script.Instructions.Count; i++) {
					if(i + 1 == script.Instructions.Count || functionStarts.ContainsKey(i + 1)) {
						lastInstructionIndex = i;
						break;
					}
				}

				if(lastInstructionIndex == -1)
					throw new Exception("Unable to find last instruction");

				AnalyzeFunction(function, firstInstructionIndex, lastInstructionIndex);
			}

			foreach(var instruction in script.Instructions) {
				instruction.Offset = null;
				instruction.Size = null;
			}

			script.EntryPointOffset = null;
			script.FunctionIndex = null;
			script.Instructions = null;

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

		private static void AnalyzeFunction(Function function, int firstInstructionIndex, int lastInstructionIndex) {
			var script = function.Script;
			var instructions = script.Instructions;

			var entryBlock = new BasicBlock(function, "entry") {
				IsEntryBlock = true
			};
			function.EntryBlock = entryBlock;
			function.ExitBlocks = new List<BasicBlock>();

			var blockStarts = new Dictionary<int, BasicBlock> { { firstInstructionIndex, entryBlock } };
			var basicBlocks = new List<BasicBlock> { entryBlock };
			function.Blocks = basicBlocks;

			void MarkBasicBlockStart(uint offset) {
				int index = script.InstructionIndexFromOffset(offset);
				if(index == -1)
					throw new Exception("Unable to determine jump target");

				if(!blockStarts.ContainsKey(index)) {
					string label = $"block_{offset:x4}";
					var block = new BasicBlock(function, label);
					basicBlocks.Add(block);
					blockStarts.Add(index, block);
				}
			}

			// mark basic block boundaries
			for(int i = firstInstructionIndex; i <= lastInstructionIndex; i++) {
				var instruction = instructions[i];

				if(instruction.IsJump || instruction.IsSwitch) {
					foreach(uint offset in PossibleNextInstructionOffsets(instruction)) {
						MarkBasicBlockStart(offset);
					}
					// instruction after a jump is always a new basic block
					if(i != lastInstructionIndex)
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
			foreach(int startIndex in blockStarts.Keys.OrderBy(i => i)) {
				var basicBlock = blockStarts[startIndex];
				for(int i = startIndex; i <= lastInstructionIndex; i++) {
					instructions[i].Block = basicBlock;
					basicBlock.Instructions.Add(instructions[i]);
					if(i == lastInstructionIndex || blockStarts.ContainsKey(i + 1)) {
						break;
					}
				}
			}

			basicBlocks.Sort((a, b) => (int)a.FirstInstruction.Offset!.Value - (int)b.FirstInstruction.Offset!.Value);

			// link consecutive blocks
			for(int i = 0; i < basicBlocks.Count - 1; i++) {
				var block = basicBlocks[i];
				if(block.IsUnreachable) continue;
				if(block.IsExitBlock) continue;

				var lastInstruction = block.LastInstruction;
				if(lastInstruction.IsUnconditionalJump) continue;
				if(lastInstruction.IsSwitch) continue;

				var nextBlock = basicBlocks[i + 1];
				if(block.Successors.Contains(nextBlock))
					continue;
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
			var lastInstruction = basicBlock.LastInstruction;

			if(lastInstruction.IsReturn) {
				function.ExitBlocks.Add(basicBlock);
				basicBlock.IsExitBlock = true;
				return;
			}

			foreach(uint offset in PossibleNextInstructionOffsets(lastInstruction)) {
				var nextBlock = function.BasicBlockFromOffset(offset);
				if(nextBlock == null)
					throw new Exception("Invalid jump target");
				if(basicBlock.Successors.Contains(nextBlock))
					continue;
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
			script.Instructions = script.Functions.SelectMany(function => function.Instructions).ToList();

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
					Offset = function.FirstInstruction.Offset!.Value
				});
			}
			script.Functions = null;

			script.EntryPointOffset = script.EntryPointFunction.FirstInstruction.Offset!.Value;
			script.EntryPointFunction = null;

			script.Representation = MjoScriptRepresentation.InstructionList;
			script.SanityCheck();
		}
	}
}
