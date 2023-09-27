using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Majiro.Script.Analysis.ControlFlow;
using Majiro.Util;

namespace Majiro.Script.Analysis.StackTransition {
	public static class StackTransitionPass {
		public static bool Verbose = false;

		public static void WriteStackState(Function function, Instruction instruction, bool parameters = true,
			bool locals = true, bool temps = true) {
			void WriteTypeChar(MjoType type, bool color = true) {
				switch(type) {
					case MjoType.Int:
						if(color) Console.ForegroundColor = ConsoleColor.Blue;
						Console.Write('i');
						break;
					case MjoType.Float:
						if(color) Console.ForegroundColor = ConsoleColor.Green;
						Console.Write('f');
						break;
					case MjoType.String:
						if(color) Console.ForegroundColor = ConsoleColor.Red;
						Console.Write('s');
						break;
					case MjoType.IntArray:
						if(color) Console.ForegroundColor = ConsoleColor.Yellow;
						Console.Write('I');
						break;
					case MjoType.FloatArray:
						if(color) Console.ForegroundColor = ConsoleColor.Yellow;
						Console.Write('F');
						break;
					case MjoType.StringArray:
						if(color) Console.ForegroundColor = ConsoleColor.Yellow;
						Console.Write('S');
						break;
					default:
						if(color) Console.ForegroundColor = ConsoleColor.DarkGray;
						Console.Write('?');
						break;
				}
			}

			void WriteSeparator() {
				Console.ForegroundColor = ConsoleColor.White;
				Console.Write('|');
			}

			void WriteDash() {
				Console.ForegroundColor = ConsoleColor.DarkGray;
				Console.Write('-');
			}

			if(parameters) {
				WriteSeparator();
				if(function.ParameterTypes.Any()) {
					foreach(var type in function.ParameterTypes) {
						WriteTypeChar(type);
					}
				}
				else WriteDash();
			}

			if(locals) {
				WriteSeparator();
				if(function.LocalTypes.Any()) {
					foreach(var type in function.LocalTypes) {
						WriteTypeChar(type);
					}
				}
				else WriteDash();
			}

			if(temps) {
				WriteSeparator();

				for(int i = 0; i < instruction.BeforeValues.Length - instruction.PoppedValues.Length; i++) {
					WriteTypeChar(instruction.BeforeValues[i].Type);
				}

				if(instruction.PoppedValues.Any() || instruction.PushedValues.Any()) {
					Console.ForegroundColor = ConsoleColor.White;
					Console.Write('{');
					if(instruction.PoppedValues.Any()) {
						foreach(var value in instruction.PoppedValues) {
							WriteTypeChar(value.Type);
						}

						Console.ForegroundColor = ConsoleColor.White;
						Console.Write('>');
					}

					foreach(var value in instruction.PushedValues) {
						WriteTypeChar(value.Type);
					}

					Console.ForegroundColor = ConsoleColor.White;
					Console.Write('}');
				}
			}

			Console.ResetColor();
		}

		public static void ToSsaGraph(MjoScript script) {
			if(script.Representation == MjoScriptRepresentation.SsaGraph) {
				script.SanityCheck();
				return;
			}

			if(script.Representation != MjoScriptRepresentation.ControlFlowGraph) {
				throw new Exception("Unable to convert script to ssa graph representation from current state: " + script.Representation);
			}

			script.Representation = MjoScriptRepresentation.InTransition;

			foreach(var function in script.Functions) {
				ToSsaGraph(function);
			}

			script.Representation = MjoScriptRepresentation.SsaGraph;
			script.SanityCheck();
		}

		public static void ToControlFlowGraph(MjoScript script) {
			if(script.Representation == MjoScriptRepresentation.ControlFlowGraph) {
				script.SanityCheck();
				return;
			}

			if(script.Representation != MjoScriptRepresentation.SsaGraph) {
				throw new Exception("Unable to convert script to control flow graph representation from current state: " + script.Representation);
			}

			script.Representation = MjoScriptRepresentation.InTransition;

			foreach(var instruction in script.Instructions) {
				instruction.BeforeValues = null;
				instruction.PoppedValues = null;
				instruction.PushedValues = null;
			}

			foreach(var block in script.Blocks) {
				block.StartState = null;
				block.EndState = null;
				block.PhiNodes = null;
			}

			script.Representation = MjoScriptRepresentation.ControlFlowGraph;
			script.SanityCheck();
		}

		private static void ToSsaGraph(Function function) {
			if(Verbose) Disassembler.PrintFunctionHeader(function, IColoredWriter.Console);

			var blocks = function.Blocks.ToList();
			blocks.PreOrderSort(block => block.Successors);

			foreach(var block in blocks) {
				if(Verbose) Disassembler.PrintLabel(block, IColoredWriter.Console);

				var state = InitStartState(block).ToList();

				if(Verbose) {
					Console.ForegroundColor = ConsoleColor.DarkGray;
					Console.WriteLine("; predecessors: " + string.Join(", ", block.Predecessors.Select(p =>
						$"{p.Name} (end stack size {p.EndState?.Length.ToString() ?? "unknown"})")));
				}

				foreach(var phi in block.PhiNodes) {
					WriteStackState(function, phi, false, false);
					Console.CursorLeft = 40;//74;
					Console.ForegroundColor = ConsoleColor.Red;
					Disassembler.PrintInstruction(phi, IColoredWriter.Console);
				}

				foreach(var instruction in block.Instructions) {
					Debug.Assert(instruction.BeforeValues == null);
					instruction.BeforeValues = state.ToArray();

					SimulateTransition(state, instruction);

					if(Verbose) {
						WriteStackState(function, instruction, false, false);
						Console.CursorLeft = 40; //74;
						Disassembler.PrintInstruction(instruction, IColoredWriter.Console);
					}
				}

				block.EndState = state.ToArray();
				CheckStateCompatibility(block.Successors.Select(pre => pre.StartState).Prepend(block.EndState));

				if(Verbose) {
					Console.ForegroundColor = ConsoleColor.DarkGray;
					Console.WriteLine("; successors: " + string.Join(", ", block.Successors.Select(s =>
						$"{s.Name} (start stack size {s.StartState?.Length.ToString() ?? "unknown"})")));
					Console.WriteLine();
				}
			}
		}

		private static StackValue[] InitStartState(BasicBlock block) {
			block.PhiNodes = new List<PhiInstruction>();

			StackValue[] startState;
			switch(block.Predecessors.Count) {
				case 0:
					startState = new StackValue[0];
					break;

				case 1:
					startState = block.Predecessors[0].EndState ?? throw new Exception("Predecessor state is unknown");
					break;

				default:
					CheckStateCompatibility(block, block.Predecessors.Select(pre => pre.EndState), out startState);
					break;
			}

			return block.StartState = startState;
		}

		private static readonly Dictionary<char, MjoTypeMask> CharToTypeMask = new Dictionary<char, MjoTypeMask> {
			{'b', MjoTypeMask.Int},
			{'i', MjoTypeMask.Int},
			{'f', MjoTypeMask.Float},
			{'s', MjoTypeMask.String},
			{'n', MjoTypeMask.Numeric},
			{'p', MjoTypeMask.Primitive},
			{'I', MjoTypeMask.IntArray},
			{'F', MjoTypeMask.FloatArray},
			{'S', MjoTypeMask.StringArray},
			{'*', MjoTypeMask.All},
		};

		private static readonly Dictionary<char, MjoType> CharToType = new Dictionary<char, MjoType> {
			{'b', MjoType.Int},
			{'i', MjoType.Int},
			{'f', MjoType.Float},
			{'s', MjoType.String},
			{'I', MjoType.IntArray},
			{'F', MjoType.FloatArray},
			{'S', MjoType.StringArray},
		};

		private static List<MjoTypeMask> DecodeOperandMask(Instruction instruction, int stackSize, out int index) {
			string transition = instruction.Opcode.Transition;

			var list = new List<MjoTypeMask>();

			for(int i = 0; i < transition.Length; i++) {
				char c = transition[i];
				switch(c) {
					case '.':
						index = ++i;
						return list;

					case '[':
						switch(transition[++i]) {

							case '#'
								when transition[i + 1] == 's':
								Debug.Assert(transition[++i] == 's');
								Debug.Assert(transition[++i] == ']');
								switch(instruction.String) {
									case "c":
									case "l":
									case "o":
										list.Add(MjoTypeMask.Int);
										list.Add(MjoTypeMask.Int);
										break;
									case "s":
									case "t":
										list.Add(MjoTypeMask.Int);
										break;
									case "x":
										list.Add(MjoTypeMask.String);
										break;
									case "d":
										list.Add(MjoTypeMask.Primitive);
										break;
									case "f":
										list.Add(MjoTypeMask.Int);
										list.Add(MjoTypeMask.Int);
										list.Add(MjoTypeMask.Int);
										list.Add(MjoTypeMask.Int);
										list.Add(MjoTypeMask.String);
										break;
									case "g":
										list.Add(MjoTypeMask.Int);
										list.Add(MjoTypeMask.Int);
										list.Add(MjoTypeMask.Int);
										list.Add(MjoTypeMask.Int);
										list.Add(MjoTypeMask.Int);
										list.Add(MjoTypeMask.Int);
										break;
									case "n":
									case "N":
									case "p":
									case "w":
										break;
									default:
										throw new Exception("Unrecognized control code: " + instruction.String);
								}
								break;

							case '#':
								Debug.Assert(transition[++i] == 't');
								Debug.Assert(transition[++i] == ']');
								list.AddRange(instruction.TypeList.Select(FlagHelpers.ToMask));
								break;

							case '*'
								when transition[i + 1] == ']':
								Debug.Assert(transition[++i + 1] == '.');
								while(list.Count < stackSize)
									list.Add(MjoTypeMask.All);
								break;

							case var typeChar
								when CharToTypeMask.ContainsKey(typeChar):
								var repeatType = CharToTypeMask[typeChar];
								Debug.Assert(transition[++i] == '#');
								int repeatCount = transition[++i] switch {
									'a' => instruction.ArgumentCount,
									'd' => instruction.Flags.Dimension(),
									var x => throw new Exception($"Invalid repeat count specifier '{x}' in stack transition descriptor")
								};
								Debug.Assert(transition[++i] == ']');
								list.AddRange(Enumerable.Repeat(repeatType, repeatCount));
								break;

							case var x:
								throw new Exception($"Invalid repeat type specifier '{x}' in stack transition descriptor");
						}
						break;

					case var digit
						when char.IsDigit(digit):
						list.Add(list[(int)char.GetNumericValue(digit) - 1]);
						break;

					case var simple
						when CharToTypeMask.ContainsKey(simple):
						list.Add(CharToTypeMask[simple]);
						break;

					default:
						throw new Exception($"Unexpected character '{c}' in stack transition descriptor");
				}
			}

			throw new Exception("Expected a '.' in the stack transition descriptor");
		}

		private static List<StackValue> PushResultValues(List<StackValue> state, Instruction instruction, int index, string transition, StackValue[] popped) {

			var pushed = new List<StackValue>();

			// these are handled on function level
			if(instruction.IsAlloca || instruction.IsArgCheck) {
				return pushed;
			}

			void Push(MjoType type) {
				var value = new StackValue {
					Category = StackValueCategory.Temp,
					Type = type,
					Producer = instruction,
					Consumers = new List<Instruction>()
				};

				pushed.Add(value);
				state.Add(value);
			}

			for(int i = index; i < transition.Length; i++) {
				char c = transition[i];
				switch(c) {
					case '[': // [#t]
						Debug.Assert(transition[++i] == '#');
						Debug.Assert(transition[++i] == 't');
						Debug.Assert(transition[++i] == ']');
						foreach(var type in instruction.TypeList) {
							Push(type);
						}

						break;

					case '#': // #t
						Debug.Assert(transition[++i] == 't');
						Push(instruction.Flags.Type());
						break;

					case '*':
						Push(MjoType.Unknown);
						break;

					case var digit
						when char.IsDigit(digit):
						// reuse the existing value
						state.Add(popped[(int)char.GetNumericValue(digit) - 1]);
						break;

					case var simple
						when CharToType.ContainsKey(simple):
						Push(CharToType[simple]);
						break;

					default:
						throw new Exception($"Unexpected character '{c}' in stack transition descriptor");
				}
			}

			return pushed;
		}

		private static void SimulateTransition(List<StackValue> stack, Instruction instruction) {
			string transition = instruction.Opcode.Transition;

			var mask = DecodeOperandMask(instruction, stack.Count, out int index);
			Debug.Assert(mask.Count <= stack.Count);

			int offset = stack.Count - mask.Count;
			var popped = new StackValue[mask.Count];
			for(int i = 0; i < mask.Count; i++) {
				var expected = mask[i];
				var value = stack[offset + i];
				Debug.Assert(value.Type.Matches(expected));
				value.Consumers.Add(instruction);
				popped[i] = value;
			}
			stack.RemoveRange(offset, mask.Count);

			var pushed = PushResultValues(stack, instruction, index, transition, popped);

			Debug.Assert(instruction.PoppedValues == null);
			Debug.Assert(instruction.PushedValues == null);
			instruction.PoppedValues = popped.ToArray();
			instruction.PushedValues = pushed.ToArray();
		}

		private static void CheckStateCompatibility(IEnumerable<StackValue[]> states) =>
			CheckStateCompatibility(null, states.ToList(), false, out _);

		private static void CheckStateCompatibility(BasicBlock block, IEnumerable<StackValue[]> states, out StackValue[] merged) =>
			CheckStateCompatibility(block, states.ToList(), true, out merged);

		private static void CheckStateCompatibility(BasicBlock block, IList<StackValue[]> statesWithNull, bool merge, out StackValue[] merged) {

			var states = statesWithNull.Where(s => s != null).ToList();

			if(states.Count == 1) {
				merged = states[0].ToArray();
				return;
			}

			Debug.Assert(states.Any());
			Debug.Assert(states.All(stack => stack.Length == states[0].Length));

			if(!merge) {
				merged = default;
				return;
			}

			// no merge necessary for an empty stack
			if(states[0].Length == 0) {
				merged = states[0];
				return;
			}

			merged = states[0].ToArray();
			for(int i = 0; i < merged.Length; i++) {
				var values = states.Select(state => state[i]).ToList();

				if(!values.Distinct().Skip(1).Any())
					continue;

				var types = values
					.Select(val => val.Type)
					.Where(t => t != MjoType.Unknown)
					.DefaultIfEmpty(MjoType.Unknown)
					.Distinct()
					.ToList();

				var type = types.Count == 1
					? types[0]
					: MjoType.Unknown;

				var category = values
					.Select(val => val.Category)
					.Distinct()
					.Single();

				var producer = new PhiInstruction(block, i);
				block.PhiNodes.Add(producer);

				merged[i] = new StackValue {
					Category = category,
					Type = type,
					Producer = producer,
					Consumers = new List<Instruction>()
				};
			}
		}
	}
}
