using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using Majiro.Script.Analysis.ControlFlow;
using Majiro.Script.Analysis.StackTransition;
using Majiro.Util;
using VToolBase.Core;

namespace Majiro.Script {
	public static class Disassembler {

		public static MjoScript DisassembleScript(BinaryReader reader) {
			string signature = reader.ReadSizedString(16);
			bool isEncrypted = signature == "MajiroObjX1.000\0";
			Debug.Assert(isEncrypted ^ (signature == "MajiroObjV1.000\0"));

			uint entryPointOffset = reader.ReadUInt32();
			uint readMarkSize = reader.ReadUInt32();
			int functionCount = reader.ReadInt32();
			var functionIndex = new List<FunctionIndexEntry>(functionCount);
			for(int i = 0; i < functionCount; i++) {
				functionIndex.Add(new FunctionIndexEntry {
					NameHash = reader.ReadUInt32(),
					Offset = reader.ReadUInt32()
				});
			}

			int byteCodeSize = reader.ReadInt32();
			var byteCode = reader.ReadBytes(byteCodeSize);
			if(isEncrypted) Crc.Crypt32(byteCode);
			using var ms = new MemoryStream(byteCode);

			var script = new MjoScript {
				Representation = MjoScriptRepresentation.InstructionList,
				EntryPointOffset = entryPointOffset,
				FunctionIndex = functionIndex,
				EnableReadMark = readMarkSize != 0
			};
			try {
				DisassembleByteCode(ms, script.Instructions);
			}
			catch(Exception e) {
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("Failed to disassemble script: " + e.Message);
				if(script.Instructions.Any()) {
					Console.WriteLine("Last parsed instructions:");
					for(int i = Math.Max(0, script.Instructions.Count - 5); i < script.Instructions.Count; i++) {
						PrintInstruction(script.Instructions[i], IColoredWriter.Console);
					}
				}
				throw;
			}
			script.SanityCheck();
			return script;
		}

		public static void DisassembleByteCode(Stream s, List<Instruction> instructions) {
			var reader = new BinaryReader(s);

			while(s.Position != s.Length) {
				var instruction = ReadInstruction(reader, (uint)s.Position);
				instructions.Add(instruction);
			}
		}

		public static Instruction ReadInstruction(BinaryReader reader, uint offset) {
			ushort opcodeValue = reader.ReadUInt16();
			if(!Opcode.ByValue.ContainsKey(opcodeValue)) {
				throw new Exception($"Invalid opcode at offset 0x{offset:X8}: 0x{opcodeValue:X4}");
			}
			var opcode = Opcode.ByValue[opcodeValue];
			var instruction = new Instruction(opcode, offset);
			string encoding = opcode.Encoding;

			foreach(char operand in encoding) {
				switch(operand) {
					case 't':
						// type list
						ushort count = reader.ReadUInt16();
						instruction.TypeList = reader
							.ReadBytes(count)
							.Cast<MjoType>()
							.ToArray();
						break;

					case 's':
						// string data
						ushort size = reader.ReadUInt16();
						var bytes = reader.ReadBytes(size - 1);
						byte nullTerminator = reader.ReadByte();
						Debug.Assert(nullTerminator == 0);
						instruction.String = Helpers.ShiftJis.GetString(bytes);
						break;

					case 'f':
						// flags
						instruction.Flags = (MjoFlags)reader.ReadUInt16();
						break;

					case 'h':
						// name hash
						instruction.Hash = reader.ReadUInt32();
						break;

					case 'o':
						// variable offset
						instruction.VarOffset = reader.ReadInt16();
						break;

					case '0':
						// 4 byte address placeholder
						uint addressPlaceholder = reader.ReadUInt32();
						Debug.Assert(addressPlaceholder == 0);
						break;

					case 'i':
						// integer constant
						instruction.IntValue = reader.ReadInt32();
						break;

					case 'r':
						// float constant
						instruction.FloatValue = reader.ReadSingle();
						break;

					case 'a':
						// argument count
						instruction.ArgumentCount = reader.ReadUInt16();
						break;

					case 'j':
						// jump offset
						instruction.JumpOffset = reader.ReadInt32();
						break;

					case 'l':
						// line number
						instruction.LineNumber = reader.ReadUInt16();
						break;

					case 'c':
						// switch case table
						count = reader.ReadUInt16();
						instruction.SwitchOffsets = Enumerable
							.Range(0, count)
							.Select(_ => reader.ReadInt32())
							.ToArray();
						break;

					default:
						throw new Exception("Unrecognized encoding specifier: " + operand);
				}
			}

			instruction.Size = (uint)(reader.BaseStream.Position - offset);

			return instruction;
		}

		public static void PrintFunctionHeader(Function function, IColoredWriter writer) {
			writer.ForegroundColor = ConsoleColor.Blue;
			writer.Write("func ");
			writer.Write($"${function.NameHash:x8}");
			writer.ResetColor();
			writer.Write("(");
			bool first = true;
			foreach(var type in function.ParameterTypes) {
				if(!first) {
					writer.Write(", ");
				}
				first = false;
				writer.ForegroundColor = ConsoleColor.Cyan;
				writer.Write(type.ToString().ToLower());
				writer.ResetColor();
			}
			writer.Write(") ");
			if(function.IsEntryPoint) {
				writer.ForegroundColor = ConsoleColor.DarkYellow;
				writer.Write("entrypoint ");
				writer.ResetColor();
			}
			writer.Write("{");
			if(function.Script.Project != null && function.Script.Project.TryGetFunctionName(function.NameHash, out string name)
			   || Data.KnownFunctionNamesByHash.TryGetValue(function.NameHash, out name)) {
				writer.ForegroundColor = ConsoleColor.DarkGray;
				writer.Write(" ; ");
				writer.Write(name);
				writer.ResetColor();
			}
			writer.WriteLine();
		}

		public static void PrintLabel(BasicBlock block, IColoredWriter writer) {
			writer.ForegroundColor = ConsoleColor.Magenta;
			writer.WriteLine($"{block.Name}:");
			writer.ResetColor();
		}

		public static void PrintInstruction(Instruction instruction, IColoredWriter writer) {
			if(instruction.Offset != null) {
				writer.ForegroundColor = ConsoleColor.DarkGray;
				writer.Write($"{instruction.Offset:x4}: ");
			}

			writer.ForegroundColor = instruction.Opcode.Mnemonic == "line"
				? ConsoleColor.DarkGray
				: instruction.Opcode.Mnemonic == "phi"
					? ConsoleColor.DarkYellow
					: ConsoleColor.White;
			writer.Write($"{instruction.Opcode.Mnemonic,-13}");
			writer.ResetColor();

			foreach(char operand in instruction.Opcode.Encoding) {

				if(operand == '0') continue;
				writer.Write(' ');

				if(operand == 'o' &&
				   instruction.Flags.Scope() != MjoScope.Local &&
				   instruction.VarOffset == -1)
					continue;

				switch(operand) {
					case 't':
						// type list
						writer.Write('[');
						bool first = true;
						foreach(var type in instruction.TypeList) {
							if(!first) writer.Write(", ");
							writer.ForegroundColor = ConsoleColor.Cyan;
							writer.Write(type.ToString().ToLower());
							writer.ResetColor();
							first = false;
						}
						writer.Write(']');
						break;

					case 's':
						// string data
						if(instruction.String != null) {
							writer.ForegroundColor = ConsoleColor.DarkGreen;
							writer.Write('"');
							writer.Write(instruction.String.Escape());
							writer.Write('"');
							writer.ResetColor();
						}
						else {
							Debug.Assert(instruction.ExternalKey != null);
							writer.ForegroundColor = ConsoleColor.Blue;
							writer.Write('%');
							writer.Write('{');
							writer.ForegroundColor = ConsoleColor.White;
							writer.Write(instruction.ExternalKey);
							writer.ForegroundColor = ConsoleColor.Blue;
							writer.Write('}');
							writer.ForegroundColor = ConsoleColor.DarkGray;
							if(instruction.Script != null) {
								writer.Write(" ; ");
								writer.Write('"');
								writer.Write(instruction.Script.ExternalizedStrings[instruction.ExternalKey].Escape());
								writer.Write('"');
								writer.ResetColor();
							}
						}
						break;

					case 'f': {
							// flags
							var flags = instruction.Flags;
							writer.ForegroundColor = ConsoleColor.Cyan;
							writer.Write(flags.Scope().ToString().ToLower());
							writer.Write(' ');
							writer.Write(flags.Type().ToString().ToLower());
							switch(flags.InvertMode()) {
								case MjoInvertMode.Numeric:
									writer.Write(" invert_numeric");
									break;
								case MjoInvertMode.Boolean:
									writer.Write(" invert_boolean");
									break;
								case MjoInvertMode.Bitwise:
									writer.Write(" invert_bitwise");
									break;
							}
							switch(flags.Modifier()) {
								case MjoModifier.PreIncrement:
									writer.Write(" preinc");
									break;
								case MjoModifier.PreDecrement:
									writer.Write(" predec");
									break;
								case MjoModifier.PostIncrement:
									writer.Write(" postinc");
									break;
								case MjoModifier.PostDecrement:
									writer.Write(" postdec");
									break;
							}
							switch(flags.Dimension()) {
								case 1:
									writer.Write(" dim1");
									break;
								case 2:
									writer.Write(" dim2");
									break;
								case 3:
									writer.Write(" dim3");
									break;
							}
							writer.ResetColor();
							break;
						}

					case 'h':
						// name hash
						if(instruction.IsSysCall)
							writer.ForegroundColor = ConsoleColor.Yellow;
						else if(instruction.IsCall)
							writer.ForegroundColor = ConsoleColor.Blue;
						else
							writer.ForegroundColor = ConsoleColor.Red;
						writer.Write('$');
						writer.Write(instruction.Hash.ToString("x8"));
						writer.ResetColor();
						break;

					case 'o':
						// variable offset
						writer.Write(instruction.VarOffset);
						break;

					case '0':
						// 4 byte address placeholder
						break;

					case 'i':
						// integer constant
						writer.Write(instruction.IntValue);
						break;

					case 'r':
						// float constant
						writer.Write(instruction.FloatValue.ToString(CultureInfo.InvariantCulture));
						break;

					case 'a':
						// argument count
						writer.Write('(');
						writer.Write(instruction.ArgumentCount);
						writer.Write(')');
						break;

					case 'j':
						// jump offset
						writer.ForegroundColor = ConsoleColor.Magenta;
						if(instruction.JumpTarget != null) {
							Debug.Assert(instruction.Block != null);
							writer.Write('@');
							writer.Write(instruction.JumpTarget.Name);
						}
						else {
							Debug.Assert(instruction.Block == null);
							PrintRelativeJump(instruction.JumpOffset!.Value, writer);
						}
						writer.ResetColor();
						break;

					case 'l':
						// line number
						writer.ForegroundColor = ConsoleColor.DarkGray;
						writer.Write('#');
						writer.Write(instruction.LineNumber);
						writer.ResetColor();
						break;

					case 'c':
						// switch case table
						first = true;
						if(instruction.SwitchTargets != null) {
							Debug.Assert(instruction.Block != null);
							foreach(var targetBlock in instruction.SwitchTargets) {
								if(!first) writer.Write(", ");
								writer.ForegroundColor = ConsoleColor.Magenta;
								writer.Write('@');
								writer.Write(targetBlock.Name);
								writer.ResetColor();
								first = false;
							}
						}
						else {
							Debug.Assert(instruction.Block == null);
							foreach(int offset in instruction.SwitchOffsets) {
								if(!first) writer.Write(", ");
								writer.ForegroundColor = ConsoleColor.Magenta;
								PrintRelativeJump(offset, writer);
								writer.ResetColor();
								first = false;
							}
						}
						break;

					case 'p':
						// phi node
						first = true;
						if(instruction is PhiInstruction phi) {
							foreach(var predecessor in phi.Block.Predecessors) {
								if(!first) writer.Write(", ");
								writer.ForegroundColor = ConsoleColor.Magenta;
								writer.Write('@');
								writer.Write(predecessor.Name);
								writer.ResetColor();
								first = false;
							}
						}
						else {
							throw new Exception("Only phi instructions are allowed to have the 'p' encoding specifier");
						}
						break;
				}
			}

			if(instruction.IsSysCall && Data.KnownSyscallNamesByHash.TryGetValue(instruction.Hash, out string name)) {
				writer.ForegroundColor = ConsoleColor.DarkGray;
				writer.Write(" ; $");
				writer.Write(name);
				writer.ResetColor();
			}
			else if(instruction.IsCall) {
				var project = instruction.Script?.Project;
				uint hash = instruction.Hash;
				if(project != null && project.TryGetFunctionName(hash, out string calleeName)
				   || Data.KnownFunctionNamesByHash.TryGetValue(hash, out calleeName)) {
					writer.ForegroundColor = ConsoleColor.DarkGray;
					writer.Write(" ; ");
					writer.Write(calleeName);
					writer.ResetColor();
				}
				else if(project != null && project.FunctionMap.TryGetValue(hash, out var functions) && functions.Any()) {
					writer.ForegroundColor = ConsoleColor.DarkGray;
					writer.Write(" ; declared in ");
					writer.Write(functions[0].DeclaringScript);
					writer.ResetColor();
				}
			}
			else if(instruction.IsLoad || instruction.IsStore) {
				uint hash = instruction.Hash;
				if(Data.KnownVariableNamesByHash.TryGetValue(hash, out string varName)) {
					writer.ForegroundColor = ConsoleColor.DarkGray;
					writer.Write(" ; ");
					writer.Write(varName);
					writer.ResetColor();
				}
			}

			writer.WriteLine();
		}

		private static void PrintRelativeJump(int offset, IColoredWriter writer) {
			if(offset < 0)
				writer.Write($"@~-{-offset:x4}");
			else if(offset > 0)
				writer.Write($"@~+{offset:x4}");
			else writer.Write("@~0");
		}


		private const string Indent = " ";

		public static void PrintBasicBlock(BasicBlock block, IColoredWriter writer) {
			writer.Write(Indent);
			writer.ForegroundColor = ConsoleColor.Magenta;
			writer.Write(block.Name);
			writer.WriteLine(':');
			writer.ResetColor();

			foreach(var instruction in block.Instructions) {
				writer.Write(Indent);
				writer.Write(Indent);
				PrintInstruction(instruction, writer);
			}
		}

		public static void PrintFunction(Function function, IColoredWriter writer) {
			PrintFunctionHeader(function, writer);
			bool first = true;
			foreach(var block in function.Blocks) {
				if(!first)
					writer.WriteLine();
				first = false;
				PrintBasicBlock(block, writer);
			}
			writer.WriteLine('}');
		}

		public static void PrintScript(MjoScript script, IColoredWriter writer) {
			writer.ForegroundColor = ConsoleColor.DarkYellow;
			writer.Write("readmark ");
			writer.ForegroundColor = script.EnableReadMark ? ConsoleColor.Green : ConsoleColor.Red;
			writer.WriteLine(script.EnableReadMark ? "enable" : "disable");
			//writer.ForegroundColor = ConsoleColor.DarkYellow;
			//writer.Write("entrypoint ");
			//writer.ForegroundColor = ConsoleColor.Blue;
			//writer.WriteLine($"${script.EntryPointFunction.NameHash:x8}");
			writer.ResetColor();
			writer.WriteLine();

			switch(script.Representation) {
				case MjoScriptRepresentation.ControlFlowGraph:
					bool first = true;
					foreach(var function in script.Functions) {
						if(!first)
							writer.WriteLine();
						first = false;
						PrintFunction(function, writer);
					}
					break;
				default:
					foreach(var functionEntry in script.FunctionIndex) {
						writer.ForegroundColor = ConsoleColor.DarkYellow;
						writer.Write("index ");
						writer.ForegroundColor = ConsoleColor.Blue;
						writer.Write($"${functionEntry.NameHash:x8} ");
						writer.ForegroundColor = ConsoleColor.DarkGray;
						writer.Write($"0x{functionEntry.Offset:x4}");
						if(functionEntry.Offset == script.EntryPointOffset) {
							writer.ForegroundColor = ConsoleColor.DarkYellow;
							writer.Write(" entrypoint");
						}
						writer.ResetColor();
						writer.WriteLine();
					}
					writer.WriteLine();

					foreach(var instruction in script.Instructions) {
						PrintInstruction(instruction, writer);
					}
					break;
			}
		}

		public static void WriteResourceTable(MjoScript script, Stream s) {
			using var writer = new CsvWriter(new StreamWriter(s), CultureInfo.InvariantCulture);
			writer.WriteField("Key");
			writer.WriteField("Value");
			writer.NextRecord();
			foreach((string key, string value) in script.ExternalizedStrings.OrderBy(pair => int.Parse(pair.Key[1..]))) {
				writer.WriteField(key);
				writer.WriteField(value);
				writer.NextRecord();
			}
		}

		public static string DumpInstruction(Instruction instruction) {
			var sb = new StringBuilder();
			PrintInstruction(instruction, new StringBuilderColorWriter(sb));
			return sb.ToString();
		}
	}
}
