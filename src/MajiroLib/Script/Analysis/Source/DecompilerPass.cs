using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Majiro.Script.Analysis.ControlFlow;
using Majiro.Script.Analysis.Source.Nodes;
using Majiro.Script.Analysis.Source.Nodes.Expressions;
using Majiro.Script.Analysis.Source.Nodes.Statements;
using Majiro.Script.Analysis.StackTransition;
using Majiro.Util;

namespace Majiro.Script.Analysis.Source {
	public static class DecompilerPass {

		public static void ToSource(MjoScript script) {
			if(script.Representation == MjoScriptRepresentation.SyntaxTree) {
				script.SanityCheck();
				return;
			}

			if(script.Representation != MjoScriptRepresentation.SsaGraph) {
				throw new Exception("Unable to convert script to source representation from current state: " + script.Representation);
			}

			script.Representation = MjoScriptRepresentation.InTransition;


			foreach(var function in script.Functions) {
				var node = new Decompiler(function).Decompile();
				node.Accept(new DumpVisitor(), IColoredWriter.Console);
				Console.WriteLine();
			}

			script.Representation = MjoScriptRepresentation.SyntaxTree;
			script.SanityCheck();
		}

		private class DBlock {
			public BasicBlock BasicBlock;
			public List<Statement> InternalStatements;
			public DBlock ImmediateDominator;
			public HashSet<DBlock> Dominators;
			public HashSet<DBlock> Dominatees;

			public Expression JumpCondition;
			public DBlock JumpTarget;
			public DBlock JumpFallthrough;
		}

		private sealed class Decompiler {

			private readonly Function Function;
			private readonly MjoScript Script;
			private readonly List<Instruction> Instructions;
			private readonly List<Expression> EvaluationStack = new List<Expression>();
			private readonly Dictionary<Instruction, SyntaxNode> Nodes = new Dictionary<Instruction, SyntaxNode>();
			private readonly Dictionary<BasicBlock, DBlock> DBlocks = new Dictionary<BasicBlock, DBlock>();
			private readonly Dictionary<BasicBlock, int> StartIndices = new Dictionary<BasicBlock, int>();

			public Decompiler(Function function) {
				Function = function;
				Script = function.Script;
				Instructions = function.Instructions.ToList();
			}

			private void BuildBlockList() {
				DBlocks.Clear();
				foreach(var basicBlock in Function.Blocks) {
					DBlocks.Add(basicBlock, new DBlock { BasicBlock = basicBlock });
					StartIndices.Add(basicBlock, Instructions.IndexOf(basicBlock.FirstInstruction));
				}
			}

			private int GetBlockStartIndex(BasicBlock block) {
				return StartIndices[block];
			}

			// https://www.cs.rice.edu/~keith/Embed/dom.pdf
			private void CalculateDominators() {

				// get blocks in reverse post-order
				var rpo = Function.Blocks.ToList();
				rpo.PostOrderSort(block => block.Successors);
				rpo.Reverse();
				Debug.Assert(rpo[0] == Function.EntryBlock);

				var idoms = new int[rpo.Count];
				Array.Fill(idoms, -1);
				idoms[0] = 0; // entry block dominates itself

				bool changed = true;
				while(changed) {
					changed = false;

					// start at 1 to skip the entry block
					for(int bi = 1; bi < rpo.Count; bi++) {
						var b = rpo[bi];
						int idom = rpo.IndexOf(b.Predecessors.First(p => idoms[rpo.IndexOf(p)] != -1));

						foreach(var p in b.Predecessors) {
							int pi = rpo.IndexOf(p);
							if(pi == idom) continue;
							if(idoms[pi] == -1) continue;
							idom = Intersect(pi, idom);
						}

						if(idoms[bi] != idom) {
							idoms[bi] = idom;
							changed = true;
						}
					}
				}

				// populate transitive dominator sets
				var entry = DBlocks[Function.EntryBlock];
				entry.ImmediateDominator = entry;
				entry.Dominators = new HashSet<DBlock> { entry };

				for(int i = 1; i < rpo.Count; i++) {
					var block = DBlocks[rpo[i]];
					int idomIndex = idoms[i];
					var idom = DBlocks[rpo[idomIndex]];
					block.ImmediateDominator = idom;
					block.Dominators = new HashSet<DBlock>();
					foreach(var idomDominator in idom.Dominators) {
						block.Dominators.Add(idomDominator);
					}
					block.Dominators.Add(block);
				}

				int Intersect(int b1, int b2) {
					while(b1 != b2) {
						while(b1 > b2)
							b1 = idoms[b1];
						while(b2 > b1)
							b2 = idoms[b2];
					}
					return b1;
				}
			}

			public FunctionNode Decompile() {
				BuildBlockList();
				CalculateDominators();

				int instructionPointer = 0;
				var block = DecompileBlock(ref instructionPointer);
				//Debug.Assert(instructionPointer == Instructions.Count);
				return new FunctionNode(Function.NameHash, block);
			}

			private List<Statement> DecompileBlock(ref int instructionPointer) {
				var statements = new List<Statement>();

				bool merged = false;

				while(true) {
					var instruction = Instructions[instructionPointer];
					var block = instruction.Block;

					if(instruction.IsLine) {
						instructionPointer++;
						continue;
					}

					if(instruction.IsUnconditionalJump) {
						instructionPointer = GetBlockStartIndex(instruction.JumpTarget);
						break;
					}

					if(instructionPointer == GetBlockStartIndex(block) && !merged && block.Predecessors.Count > 1)
						break;
					merged = false;

					var statement = DecompileStatement(ref instructionPointer, ref merged);
					statements.Add(statement);

					if(statement is ReturnStatement)
						break;
				}

				return statements;
			}

			private BasicBlock FindMergeBlock(BasicBlock parent) {
				Debug.Assert(parent.Successors.Count == 2);
				var a = parent.Successors[0];
				var b = parent.Successors[1];

				// find the block where the two execution paths meet
				var reachableFromA = new HashSet<BasicBlock> { parent, a };
				var queue = new Queue<BasicBlock>();
				queue.Enqueue(a);

				while(queue.TryDequeue(out var next)) {
					if(reachableFromA.Contains(next)) continue;
					next.Successors.ForEach(queue.Enqueue);
				}

				queue.Enqueue(b);
				while(queue.TryDequeue(out var next)) {
					if(reachableFromA.Contains(next)) return next;
					next.Successors.ForEach(queue.Enqueue);
				}

				return null;
			}

			private void ProcessBinaryExpression(Instruction instruction, BinaryOperation operation, MjoType type) {
				Debug.Assert(EvaluationStack.Count >= 2);
				var left = EvaluationStack[^2];
				var right = EvaluationStack[^1];
				var expr = new BinaryExpression(left, right, operation, type);
				EvaluationStack.RemoveRange(EvaluationStack.Count - 2, 2);
				EvaluationStack.Add(expr);
				Nodes[instruction] = expr;
			}

			private void ProcessUnaryExpression(Instruction instruction, UnaryOperation operation, MjoType type) {
				Debug.Assert(EvaluationStack.Count >= 1);
				var operand = EvaluationStack[^1];
				var expr = new UnaryExpression(operand, operation, type);
				EvaluationStack[^1] = expr;
				Nodes[instruction] = expr;
			}

			private void ProcessCast(Instruction instruction, MjoType type) {
				Debug.Assert(EvaluationStack.Count >= 1);
				var operand = EvaluationStack[^1];
				var expr = new Cast(operand, type);
				EvaluationStack[^1] = expr;
				Nodes[instruction] = expr;
			}

			private Assignment ProcessAssignment(Instruction instruction, ref int pointer, MjoType type, BinaryOperation operation) {
				// we expect a pop instruction after an st.*
				Debug.Assert(Instructions[pointer++].IsPop);
				return ProcessAssignmentP(instruction, type, operation);
			}

			private Assignment ProcessAssignmentP(Instruction instruction, MjoType type, BinaryOperation operation) {
				Debug.Assert(EvaluationStack.Count == 1);
				Debug.Assert(instruction.Flags.Type() == type);
				var value = EvaluationStack[0];
				EvaluationStack.Clear();
				var stmt = new Assignment(value, instruction.Hash, instruction.Flags, type, operation);
				Nodes[instruction] = stmt;
				return stmt;
			}

			private ArrayAssignment ProcessArrayAssignment(Instruction instruction, ref int pointer, MjoType type, BinaryOperation operation) {
				// we expect a pop instruction after an st.*
				Debug.Assert(Instructions[pointer++].IsPop);
				return ProcessArrayAssignmentP(instruction, type, operation);
			}

			private ArrayAssignment ProcessArrayAssignmentP(Instruction instruction, MjoType type, BinaryOperation operation) {
				int dimension = instruction.Flags.Dimension();
				Debug.Assert(EvaluationStack.Count == dimension + 1);
				Debug.Assert(dimension > 0 && dimension <= 3);
				Debug.Assert(instruction.Flags.Type() == type);
				var value = EvaluationStack[0];
				var indices = EvaluationStack.Take(dimension).ToArray();
				var stmt = new ArrayAssignment(value, indices, instruction.Hash, instruction.Flags, type, operation);
				Nodes[instruction] = stmt;
				return stmt;
			}

			private Expression[] PopArguments(int count) {
				Debug.Assert(EvaluationStack.Count >= count);
				var result = EvaluationStack.Skip(EvaluationStack.Count - count).ToArray();
				EvaluationStack.RemoveRange(EvaluationStack.Count - count, count);
				return result;
			}

			private Statement DecompileStatement(ref int instructionPointer, ref bool merged) {

				while(instructionPointer < Instructions.Count) {
					var instruction = Instructions[instructionPointer++];

					switch(instruction.Opcode.Value) {

						#region binary

						case 0x100: // mul.i
							ProcessBinaryExpression(instruction, BinaryOperation.Multiplication, MjoType.Int);
							break;
						case 0x101: // mul.r
							ProcessBinaryExpression(instruction, BinaryOperation.Multiplication, MjoType.Float);
							break;

						case 0x108: // div.i
							ProcessBinaryExpression(instruction, BinaryOperation.Division, MjoType.Int);
							break;
						case 0x109: // div.r
							ProcessBinaryExpression(instruction, BinaryOperation.Division, MjoType.Float);
							break;

						case 0x110: // rem
							ProcessBinaryExpression(instruction, BinaryOperation.Modulo, MjoType.Int);
							break;

						case 0x118: // add.i
							ProcessBinaryExpression(instruction, BinaryOperation.Addition, MjoType.Int);
							break;
						case 0x119: // add.r
							ProcessBinaryExpression(instruction, BinaryOperation.Addition, MjoType.Float);
							break;
						case 0x11a: // add.s
							ProcessBinaryExpression(instruction, BinaryOperation.Addition, MjoType.String);
							break;

						case 0x120: // sub.i
							ProcessBinaryExpression(instruction, BinaryOperation.Subtraction, MjoType.Int);
							break;
						case 0x121: // sub.r
							ProcessBinaryExpression(instruction, BinaryOperation.Subtraction, MjoType.Float);
							break;

						case 0x128: // shr
							ProcessBinaryExpression(instruction, BinaryOperation.ShiftRight, MjoType.Int);
							break;

						case 0x130: // shl
							ProcessBinaryExpression(instruction, BinaryOperation.ShiftLeft, MjoType.Int);
							break;

						case 0x138: // cle.i
							ProcessBinaryExpression(instruction, BinaryOperation.CompareLessEqual, MjoType.Int);
							break;
						case 0x139: // cle.r
							ProcessBinaryExpression(instruction, BinaryOperation.CompareLessEqual, MjoType.Float);
							break;
						case 0x13A: // cle.s
							ProcessBinaryExpression(instruction, BinaryOperation.CompareLessEqual, MjoType.String);
							break;

						case 0x140: // clt.i
							ProcessBinaryExpression(instruction, BinaryOperation.CompareLessThan, MjoType.Int);
							break;
						case 0x141: // clt.r
							ProcessBinaryExpression(instruction, BinaryOperation.CompareLessThan, MjoType.Float);
							break;
						case 0x142: // clt.s
							ProcessBinaryExpression(instruction, BinaryOperation.CompareLessThan, MjoType.String);
							break;

						case 0x148: // cge.i
							ProcessBinaryExpression(instruction, BinaryOperation.CompareGreaterEqual, MjoType.Int);
							break;
						case 0x149: // cge.r
							ProcessBinaryExpression(instruction, BinaryOperation.CompareGreaterEqual, MjoType.Float);
							break;
						case 0x14A: // cge.s
							ProcessBinaryExpression(instruction, BinaryOperation.CompareGreaterEqual, MjoType.String);
							break;

						case 0x150: // cgt.i
							ProcessBinaryExpression(instruction, BinaryOperation.CompareGreaterThan, MjoType.Int);
							break;
						case 0x151: // cgt.r
							ProcessBinaryExpression(instruction, BinaryOperation.CompareGreaterThan, MjoType.Float);
							break;
						case 0x152: // cgt.s
							ProcessBinaryExpression(instruction, BinaryOperation.CompareGreaterThan, MjoType.String);
							break;

						case 0x158: // ceq.i
							ProcessBinaryExpression(instruction, BinaryOperation.CompareEqual, MjoType.Int);
							break;
						case 0x159: // ceq.r
							ProcessBinaryExpression(instruction, BinaryOperation.CompareEqual, MjoType.Float);
							break;
						case 0x15A: // ceq.s
							ProcessBinaryExpression(instruction, BinaryOperation.CompareEqual, MjoType.String);
							break;

						case 0x160: // cne.i
							ProcessBinaryExpression(instruction, BinaryOperation.CompareNotEqual, MjoType.Int);
							break;
						case 0x161: // cne.r
							ProcessBinaryExpression(instruction, BinaryOperation.CompareNotEqual, MjoType.Float);
							break;
						case 0x162: // cne.s
							ProcessBinaryExpression(instruction, BinaryOperation.CompareNotEqual, MjoType.String);
							break;

						case 0x168: // xor
							ProcessBinaryExpression(instruction, BinaryOperation.BitwiseXor, MjoType.Int);
							break;

						case 0x170: // andl
							ProcessBinaryExpression(instruction, BinaryOperation.LogicalAnd, MjoType.Int);
							break;

						case 0x178: // orl
							ProcessBinaryExpression(instruction, BinaryOperation.LogicalOr, MjoType.Int);
							break;

						case 0x180: // and
							ProcessBinaryExpression(instruction, BinaryOperation.BitwiseAnd, MjoType.Int);
							break;

						case 0x188: // or
							ProcessBinaryExpression(instruction, BinaryOperation.BitwiseOr, MjoType.Int);
							break;

						#endregion

						#region unary

						case 0x190: // notl
							ProcessUnaryExpression(instruction, UnaryOperation.LogicalNot, MjoType.Int);
							break;

						case 0x198: // not
							ProcessUnaryExpression(instruction, UnaryOperation.BitwiseNot, MjoType.Int);
							break;

						case 0x1A0: // neg.i
							ProcessUnaryExpression(instruction, UnaryOperation.UnaryMinus, MjoType.Int);
							break;
						case 0x1A1: // neg.r
							ProcessUnaryExpression(instruction, UnaryOperation.UnaryMinus, MjoType.Float);
							break;

						case 0x1A8: // pos.i
							ProcessUnaryExpression(instruction, UnaryOperation.UnaryPlus, MjoType.Int);
							break;
						case 0x1A9: // pos.r
							ProcessUnaryExpression(instruction, UnaryOperation.UnaryPlus, MjoType.Float);
							break;

						case 0x83e: // conv.i
							ProcessCast(instruction, MjoType.Int);
							break;
						case 0x83f: // conv.r
							ProcessCast(instruction, MjoType.Float);
							break;

						#endregion

						#region ld*

						case 0x800: // ldc.i
							EvaluationStack.Add(new LiteralInt(instruction.IntValue));
							break;
						case 0x801: // ldstr
							EvaluationStack.Add(new LiteralString(instruction.String));
							break;
						case 0x802: // ld
							EvaluationStack.Add(new Identifier(instruction.Hash, instruction.Flags));
							break;
						case 0x803: // ldc.r
							EvaluationStack.Add(new LiteralFloat(instruction.FloatValue));
							break;
						case 0x837: // ldelem
							{
								int dimension = instruction.Flags.Dimension();
								Debug.Assert(EvaluationStack.Count >= dimension);
								Debug.Assert(dimension > 0 && dimension <= 3);
								var indices = PopArguments(dimension);
								EvaluationStack.Add(new ArrayAccess(instruction.Hash, instruction.Flags, indices));
							}
							break;

						#endregion

						#region st.*

						case 0x1b0: // st.i
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.None);
						case 0x1b1: // st.r
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.Float, BinaryOperation.None);
						case 0x1b2: // st.s
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.String, BinaryOperation.None);
						case 0x1b3: // st.iarr
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.IntArray, BinaryOperation.None);
						case 0x1b4: // st.rarr
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.FloatArray, BinaryOperation.None);
						case 0x1b5: // st.sarr
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.StringArray, BinaryOperation.None);

						case 0x1b8: // st.mul.i
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.Multiplication);
						case 0x1b9: // st.mul.r
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.Float, BinaryOperation.Multiplication);

						case 0x1c0: // st.div.i
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.Division);
						case 0x1c1: // st.div.r
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.Float, BinaryOperation.Division);

						case 0x1c8: // st.rem
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.Modulo);

						case 0x1d0: // st.add.i
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.Addition);
						case 0x1d1: // st.add.r
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.Float, BinaryOperation.Addition);
						case 0x1d2: // st.add.s
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.String, BinaryOperation.Addition);

						case 0x1d8: // st.sub.i
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.Subtraction);
						case 0x1d9: // st.sub.r
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.Float, BinaryOperation.Subtraction);

						case 0x1e0: // st.shl
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.ShiftLeft);

						case 0x1e8: // st.shr
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.ShiftRight);

						case 0x1f0: // st.and
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.BitwiseAnd);

						case 0x1f8: // st.xor
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.BitwiseXor);

						case 0x200: // st.or
							return ProcessAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.BitwiseOr);

						#endregion

						#region stp.*
						case 0x210: // stp.i
							return ProcessAssignmentP(instruction, MjoType.Int, BinaryOperation.None);
						case 0x211: // stp.r
							return ProcessAssignmentP(instruction, MjoType.Float, BinaryOperation.None);
						case 0x212: // stp.s
							return ProcessAssignmentP(instruction, MjoType.String, BinaryOperation.None);
						case 0x213: // stp.iarr
							return ProcessAssignmentP(instruction, MjoType.IntArray, BinaryOperation.None);
						case 0x214: // stp.rarr
							return ProcessAssignmentP(instruction, MjoType.FloatArray, BinaryOperation.None);
						case 0x215: // stp.sarr
							return ProcessAssignmentP(instruction, MjoType.StringArray, BinaryOperation.None);

						case 0x218: // stp.mul.i
							return ProcessAssignmentP(instruction, MjoType.Int, BinaryOperation.Multiplication);
						case 0x219: // stp.mul.r
							return ProcessAssignmentP(instruction, MjoType.Float, BinaryOperation.Multiplication);

						case 0x220: // stp.div.i
							return ProcessAssignmentP(instruction, MjoType.Int, BinaryOperation.Division);
						case 0x221: // stp.div.r
							return ProcessAssignmentP(instruction, MjoType.Float, BinaryOperation.Division);

						case 0x228: // stp.rem
							return ProcessAssignmentP(instruction, MjoType.Int, BinaryOperation.Modulo);

						case 0x230: // stp.add.i
							return ProcessAssignmentP(instruction, MjoType.Int, BinaryOperation.Addition);
						case 0x231: // stp.add.r
							return ProcessAssignmentP(instruction, MjoType.Float, BinaryOperation.Addition);
						case 0x232: // stp.add.s
							return ProcessAssignmentP(instruction, MjoType.String, BinaryOperation.Addition);

						case 0x238: // stp.sub.i
							return ProcessAssignmentP(instruction, MjoType.Int, BinaryOperation.Subtraction);
						case 0x239: // stp.sub.r
							return ProcessAssignmentP(instruction, MjoType.Float, BinaryOperation.Subtraction);

						case 0x240: // stp.shl
							return ProcessAssignmentP(instruction, MjoType.Int, BinaryOperation.ShiftLeft);

						case 0x248: // stp.shr
							return ProcessAssignmentP(instruction, MjoType.Int, BinaryOperation.ShiftRight);

						case 0x250: // stp.and
							return ProcessAssignmentP(instruction, MjoType.Int, BinaryOperation.BitwiseAnd);

						case 0x258: // stp.xor
							return ProcessAssignmentP(instruction, MjoType.Int, BinaryOperation.BitwiseXor);

						case 0x260: // stp.or
							return ProcessAssignmentP(instruction, MjoType.Int, BinaryOperation.BitwiseOr);

						#endregion

						#region stelem.*

						case 0x270: // stelem.i
							return ProcessArrayAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.None);
						case 0x271: // stelem.r
							return ProcessArrayAssignment(instruction, ref instructionPointer, MjoType.Float, BinaryOperation.None);
						case 0x272: // stelem.s
							return ProcessArrayAssignment(instruction, ref instructionPointer, MjoType.String, BinaryOperation.None);

						case 0x278: // stelem.mul.i
							return ProcessArrayAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.Multiplication);
						case 0x279: // stelem.mul.r
							return ProcessArrayAssignment(instruction, ref instructionPointer, MjoType.Float, BinaryOperation.Multiplication);

						case 0x280: // stelem.div.i
							return ProcessArrayAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.Division);
						case 0x281: // stelem.div.r
							return ProcessArrayAssignment(instruction, ref instructionPointer, MjoType.Float, BinaryOperation.Division);

						case 0x288: // stelem.rem
							return ProcessArrayAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.Modulo);

						case 0x290: // stelem.add.i
							return ProcessArrayAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.Addition);
						case 0x291: // stelem.add.r
							return ProcessArrayAssignment(instruction, ref instructionPointer, MjoType.Float, BinaryOperation.Addition);
						case 0x292: // stelem.add.s
							return ProcessArrayAssignment(instruction, ref instructionPointer, MjoType.String, BinaryOperation.Addition);

						case 0x298: // stelem.sub.i
							return ProcessArrayAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.Subtraction);
						case 0x299: // stelem.sub.r
							return ProcessArrayAssignment(instruction, ref instructionPointer, MjoType.Float, BinaryOperation.Subtraction);

						case 0x2a0: // stelem.shl
							return ProcessArrayAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.ShiftLeft);

						case 0x2a8: // stelem.shr
							return ProcessArrayAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.ShiftRight);

						case 0x2b0: // stelem.and
							return ProcessArrayAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.BitwiseAnd);

						case 0x2b8: // stelem.xor
							return ProcessArrayAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.BitwiseXor);

						case 0x2c0: // stelem.or
							return ProcessArrayAssignment(instruction, ref instructionPointer, MjoType.Int, BinaryOperation.BitwiseOr);

						#endregion

						#region stelemp.*
						case 0x2d0: // stelemp.i
							return ProcessArrayAssignmentP(instruction, MjoType.Int, BinaryOperation.None);
						case 0x2d1: // stelemp.r
							return ProcessArrayAssignmentP(instruction, MjoType.Float, BinaryOperation.None);
						case 0x2d2: // stelemp.s
							return ProcessArrayAssignmentP(instruction, MjoType.String, BinaryOperation.None);

						case 0x2d8: // stelemp.mul.i
							return ProcessArrayAssignmentP(instruction, MjoType.Int, BinaryOperation.Multiplication);
						case 0x2d9: // stelemp.mul.r
							return ProcessArrayAssignmentP(instruction, MjoType.Float, BinaryOperation.Multiplication);

						case 0x2e0: // stelemp.div.i
							return ProcessArrayAssignmentP(instruction, MjoType.Int, BinaryOperation.Division);
						case 0x2e1: // stelemp.div.r
							return ProcessArrayAssignmentP(instruction, MjoType.Float, BinaryOperation.Division);

						case 0x2e8: // stelemp.rem
							return ProcessArrayAssignmentP(instruction, MjoType.Int, BinaryOperation.Modulo);

						case 0x2f0: // stelemp.add.i
							return ProcessArrayAssignmentP(instruction, MjoType.Int, BinaryOperation.Addition);
						case 0x2f1: // stelemp.add.r
							return ProcessArrayAssignmentP(instruction, MjoType.Float, BinaryOperation.Addition);
						case 0x2f2: // stelemp.add.s
							return ProcessArrayAssignmentP(instruction, MjoType.String, BinaryOperation.Addition);

						case 0x2f8: // stelemp.sub.i
							return ProcessArrayAssignmentP(instruction, MjoType.Int, BinaryOperation.Subtraction);
						case 0x2f9: // stelemp.sub.r
							return ProcessArrayAssignmentP(instruction, MjoType.Float, BinaryOperation.Subtraction);

						case 0x300: // stelemp.shl
							return ProcessArrayAssignmentP(instruction, MjoType.Int, BinaryOperation.ShiftLeft);

						case 0x308: // stelemp.shr
							return ProcessArrayAssignmentP(instruction, MjoType.Int, BinaryOperation.ShiftRight);

						case 0x310: // stelemp.and
							return ProcessArrayAssignmentP(instruction, MjoType.Int, BinaryOperation.BitwiseAnd);

						case 0x318: // stelemp.xor
							return ProcessArrayAssignmentP(instruction, MjoType.Int, BinaryOperation.BitwiseXor);

						case 0x320: // stelemp.or
							return ProcessArrayAssignmentP(instruction, MjoType.Int, BinaryOperation.BitwiseOr);

						#endregion

						#region calls

						case 0x80f: // call
							EvaluationStack.Add(new Call(instruction.Hash, false, PopArguments(instruction.ArgumentCount)));
							break;
						case 0x810: // callp
							Debug.Assert(EvaluationStack.Count == instruction.ArgumentCount);
							return new CallStatement(new Call(instruction.Hash, false, PopArguments(instruction.ArgumentCount)));

						case 0x834: // syscall
							EvaluationStack.Add(new Call(instruction.Hash, true, PopArguments(instruction.ArgumentCount)));
							break;
						case 0x835: // syscallp
							Debug.Assert(EvaluationStack.Count == instruction.ArgumentCount);
							return new CallStatement(new Call(instruction.Hash, true, PopArguments(instruction.ArgumentCount)));

						#endregion

						case 0x82b: // ret
							Debug.Assert(EvaluationStack.Count <= 1);
							return new ReturnStatement(EvaluationStack.Count > 0 ? EvaluationStack[0] : null);

						case 0x829: // alloca
						case 0x836: // argcheck
						case 0x83a: // line
							break;

						case 0x847: // bsel.5
							{
								var branch = Instructions[instructionPointer++];
								Debug.Assert(branch.IsUnconditionalJump);

								Debug.Assert(GetBlockStartIndex(instruction.JumpTarget) == instructionPointer);

								var clear = Instructions[instructionPointer++];
								Debug.Assert(clear.IsBselClr);

								var block = DecompileBlock(ref instructionPointer);

								Debug.Assert(instructionPointer == GetBlockStartIndex(branch.JumpTarget));
								return new DestructorStatement(block);
							}

						//case 0x82e: // brtrue

						case 0x82e: // brfalse
							{
								merged = true;
								var condition = PopArguments(1)[0];

								if(Instructions[instructionPointer].IsUnconditionalJump) {
									// loop

									var conditionBlock = instruction.Block;
									Debug.Assert(conditionBlock.Predecessors.Count == 2);
									var backJumpBlock = conditionBlock.Predecessors.Single(block => block.FirstInstruction.IsUnconditionalJump);
									var bodyJump = Instructions[instructionPointer];
									var bodyJumpBlock = bodyJump.Block;
									Debug.Assert(bodyJumpBlock.FirstInstruction == bodyJump);
									var bodyStartBlock = bodyJump.JumpTarget;

									int bodyIp = GetBlockStartIndex(bodyStartBlock);
									var body = DecompileBlock(ref bodyIp);

								}

								int thenIp = instructionPointer;
								var thenBranch = DecompileBlock(ref thenIp);

								int elseIp = GetBlockStartIndex(instruction.JumpTarget);

								if(thenIp == elseIp) {
									instructionPointer = thenIp;
									return new IfStatement(condition, new BlockStatement(thenBranch));
								}

								var elseBranch = DecompileBlock(ref elseIp);
								Debug.Assert(thenIp == elseIp);

								instructionPointer = elseIp;
								return new IfStatement(condition, new BlockStatement(thenBranch), new BlockStatement(elseBranch));
							}

						case 0x840:
							Debug.Assert(EvaluationStack.Count == 0);
							return new TextStatement(instruction.String);

						case 0x841:
							Debug.Assert(EvaluationStack.Count == 0);
							return new ProcStatement();

						case 0x842:
							Debug.Assert(EvaluationStack.Count == 0);
							Expression[] operands;
							switch(instruction.String) {
								case "s":
								case "t":
								case "x":
								case "d":
									operands = PopArguments(1);
									break;

								case "c":
								case "l":
								case "o":
									operands = PopArguments(2);
									break;

								case "f":
									operands = PopArguments(5);
									break;

								case "g":
									operands = PopArguments(6);
									break;

								case "n":
								case "N":
								case "p":
								case "w":
									operands = new Expression[0];
									break;

								default:
									throw new Exception("Unrecognized control code: " + instruction.String);
							}
							return new CtrlStatement(instruction.String, operands.ToArray());

						default:
							Debug.Fail("Unrecognized instruction: " + instruction);
							break;
					}
				}

				throw new Exception("Failed to decompile statement");
			}
		}
	}
}
