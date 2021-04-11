using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Majiro.Script.Analysis.ControlFlow;
using Majiro.Util;

namespace Majiro.Script.Analysis.StackTransition {
	public static class StackTransitionPass {

		public static void WriteStackState(StackState state) {
			for(int i = 0; i < state.Values.Count; i++) {
				var value = state.Values[i];

				if(i == 0 ||
				   i == state.StackBase ||
				   i == state.StackBase + state.LocalCount) {
					Console.ForegroundColor = ConsoleColor.White;
					Console.Write('|');
				}

				switch(value.Type) {
					case MjoType.Int:
						Console.ForegroundColor = ConsoleColor.Blue;
						Console.Write('i');
						break;
					case MjoType.Float:
						Console.ForegroundColor = ConsoleColor.Green;
						Console.Write('f');
						break;
					case MjoType.String:
						Console.ForegroundColor = ConsoleColor.Red;
						Console.Write('s');
						break;
					case MjoType.IntArray:
						Console.ForegroundColor = ConsoleColor.Yellow;
						Console.Write('I');
						break;
					case MjoType.FloatArray:
						Console.ForegroundColor = ConsoleColor.Yellow;
						Console.Write('F');
						break;
					case MjoType.StringArray:
						Console.ForegroundColor = ConsoleColor.Yellow;
						Console.Write('S');
						break;
					default:
						Console.ForegroundColor = ConsoleColor.DarkGray;
						Console.Write('?');
						break;
				}
			}

			if(state.StackTop == 0 ||
			   state.StackTop == state.StackBase ||
			   state.StackTop == state.StackBase + state.LocalCount) {
				Console.ForegroundColor = ConsoleColor.White;
				Console.Write('|');
			}
		}

		public static void Analyze(MjoScript script) => script.Functions.ForEach(Analyze);

		public static void Analyze(Function function) {
			//Disassembler.PrintFunctionHeader(function, IColoredWriter.Console);
			var blocks = function.BasicBlocks.ToList();
			blocks.PreOrderSort(block => block.Successors);

			foreach(var block in blocks) {
				//Disassembler.PrintLabel(block, IColoredWriter.Console);
				//Console.ForegroundColor = ConsoleColor.DarkGray;
				//Console.WriteLine("// predecessors: " + string.Join(", ", block.Predecessors.Select(p =>
				//	$"{p.Name} (end stack size {p.EndState?.StackTop.ToString() ?? "unknown"})")));

				var state = InitStartState(block);

				foreach(var phi in block.PhiNodes) {
					//WriteStackState(phi.StackState);
					//Console.CursorLeft = 40;//74;
					//Console.ForegroundColor = ConsoleColor.Red;
					//Disassembler.PrintInstruction(phi, IColoredWriter.Console);
				}

				//if(false)
				foreach(var instruction in block.Instructions) {
					//WriteStackState(state);
					//Console.CursorLeft = 35;
					//Console.ForegroundColor = ConsoleColor.DarkGray;
					//Console.Write(" -> ");

					SimulateTransition(state, instruction);

					//WriteStackState(state);
					//Console.CursorLeft = 40;//74;
					//Disassembler.PrintInstruction(instruction, IColoredWriter.Console);

					// the argcheck instruction determines the stack base
					if(instruction.IsArgCheck)
						state.StackBase = state.StackTop;
					if(instruction.IsAlloca)
						state.LocalCount = state.StackTop - state.StackBase;

					instruction.StackState = state.Clone();
				}

				CheckStateCompatibility(block.Successors.Select(pre => pre.StartState).Prepend(state));

				block.EndState = state;
				//Console.ForegroundColor = ConsoleColor.DarkGray;
				//Console.WriteLine("// successors: " + string.Join(", ", block.Successors.Select(s =>
				//	$"{s.Name} (start stack size {s.StartState?.StackTop.ToString() ?? "unknown"})")));
				//Console.WriteLine();
			}
		}

		private static StackState InitStartState(BasicBlock block) {
			block.PhiNodes = new List<PhiInstruction>();

			StackState startState;
			switch(block.Predecessors.Count) {
				case 0:
					startState = block.IsEntryBlock ? new StackState() : new StackState { StackBase = -1 };
					break;

				case 1:

					startState = block.Predecessors[0].EndState ?? throw new Exception("Predecessor state is unknown");
					break;

				default:
					CheckStateCompatibility(block, block.Predecessors.Select(pre => pre.EndState), out startState);
					break;
			}

			block.StartState = startState;
			return startState.Clone();
		}

		private static readonly Dictionary<char, MjoTypeMask> _charToTypeMask = new Dictionary<char, MjoTypeMask> {
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

		private static readonly Dictionary<char, MjoType> _charToType = new Dictionary<char, MjoType> {
			{'b', MjoType.Int},
			{'i', MjoType.Int},
			{'f', MjoType.Float},
			{'s', MjoType.String},
			{'I', MjoType.IntArray},
			{'F', MjoType.FloatArray},
			{'S', MjoType.StringArray},
		};

		private static List<MjoTypeMask> DecodeOperandMask(Instruction instruction, int stackTop, out int index) {
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
								while(list.Count < stackTop)
									list.Add(MjoTypeMask.All);
								break;

							case var typeChar
								when _charToTypeMask.ContainsKey(typeChar):
								var repeatType = _charToTypeMask[typeChar];
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
						when _charToTypeMask.ContainsKey(simple):
						list.Add(_charToTypeMask[simple]);
						break;

					default:
						throw new Exception($"Unexpected character '{c}' in stack transition descriptor");
				}
			}

			throw new Exception("Expected a '.' in the stack transition descriptor");
		}

		private static void PushResultValues(StackState state, Instruction instruction, int index, string transition, List<StackValue> popped) {

			void Push(MjoType type) {
				state.Push(new StackValue {
					Category = StackValueCategory.Temp,
					Type = type,
					Producer = instruction,
					Consumers = new List<Instruction>()
				});
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

					case '~': // ~#t
						Debug.Assert(transition[++i] == '#');
						Debug.Assert(transition[++i] == 't');
						Push(instruction.Flags.Type().ElementType());
						break;

					case '*':
						Push(MjoType.Unknown);
						break;

					case var digit
						when char.IsDigit(digit):
						// reuse the existing value
						state.Push(popped[(int)char.GetNumericValue(digit) - 1]);
						break;

					case var simple
						when _charToType.ContainsKey(simple):
						Push(_charToType[simple]);
						break;

					default:
						throw new Exception($"Unexpected character '{c}' in stack transition descriptor");
				}
			}
		}

		private static void SimulateTransition(StackState state, Instruction instruction) {
			string transition = instruction.Opcode.Transition;

			var mask = DecodeOperandMask(instruction, state.StackTop, out int index);
			var popped = new List<StackValue>();
			Debug.Assert(mask.Count <= state.StackTop);

			mask.Reverse();
			foreach(var expected in mask) {
				var value = state.Pop();
				Debug.Assert(value.Type.Matches(expected));
				value.Consumers.Add(instruction);
				popped.Add(value);
			}

			popped.Reverse();
			PushResultValues(state, instruction, index, transition, popped);
		}

		private static void CheckStateCompatibility(IEnumerable<StackState> states) =>
			CheckStateCompatibility(null, states.ToList(), false, out _);

		private static void CheckStateCompatibility(BasicBlock block, IEnumerable<StackState> states, out StackState merged) =>
			CheckStateCompatibility(block, states.ToList(), true, out merged);

		private static void CheckStateCompatibility(BasicBlock block, IList<StackState> statesWithNull, bool merge, out StackState merged) {

			var states = statesWithNull.Where(s => s != null).ToList();

			if(statesWithNull.Count == 1) {
				merged = statesWithNull[0].Clone();
				return;
			}

			Debug.Assert(states.Any());

			int stackBase = states[0].StackBase;
			int stackTop = states[0].StackTop;
			int argCount = states[0].ArgCount;
			int localCount = states[0].LocalCount;

			Debug.Assert(states.All(state => state.StackBase == stackBase));
			Debug.Assert(states.All(state => state.StackTop == stackTop));
			Debug.Assert(states.All(state => state.ArgCount == argCount));
			Debug.Assert(states.All(state => state.LocalCount == localCount));

			if(!merge) {
				merged = default;
				return;
			}

			merged = new StackState {
				StackBase = stackBase,
				LocalCount = localCount
			};
			for(int i = 0; i < stackTop; i++) {
				var values = states.Select(state => state[i - stackBase]).ToList();

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

				bool createdPhi;
				Instruction producer;
				if(statesWithNull.Count == 1 || i < argCount + localCount) {
					producer = values
						.Select(val => val.Producer)
						.Distinct()
						.Single();
					createdPhi = false;
				}
				else {
					producer = new PhiInstruction(block, i - stackBase);
					block.PhiNodes.Add((PhiInstruction)producer);
					createdPhi = true;
				}

				merged.Push(new StackValue {
					Category = category,
					Type = type,
					Producer = producer,
					Consumers = new List<Instruction>()
				});

				if(createdPhi) {
					producer.StackState = merged.Clone();
				}
			}
		}
	}
}
