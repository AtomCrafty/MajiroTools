using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Majiro.Script.Analysis.ControlFlow;
using Majiro.Script.Analysis.StackTransition;
using Majiro.Util;

namespace Majiro.Script {
	public static class Disassembler {

		public static MjoScript DisassembleScript(BinaryReader reader) {
			string signature = reader.ReadSizedString(16);
			bool isEncrypted = signature == "MajiroObjX1.000\0";
			Debug.Assert(isEncrypted ^ (signature == "MajiroObjV1.000\0"));

			uint entryPointIndex = reader.ReadUInt32();
			uint readMarkSize = reader.ReadUInt32();
			int functionCount = reader.ReadInt32();
			var functionIndex = new List<FunctionEntry>(functionCount);
			for(int i = 0; i < functionCount; i++) {
				functionIndex.Add(new FunctionEntry {
					NameHash = reader.ReadUInt32(),
					Offset = reader.ReadUInt32()
				});
			}

			int byteCodeSize = reader.ReadInt32();
			var byteCode = reader.ReadBytes(byteCodeSize);
			if(isEncrypted) Crc.Crypt32(byteCode);
			using var ms = new MemoryStream(byteCode);

			var instructions = DisassembleByteCode(ms);

			return new MjoScript(entryPointIndex, functionIndex, instructions);
		}

		public static List<Instruction> DisassembleByteCode(Stream s) {
			var reader = new BinaryReader(s);
			var list = new List<Instruction>();

			while(s.Position != s.Length) {
				var instruction = ReadInstruction(reader, (uint)s.Position);
				list.Add(instruction);
			}

			return list;
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
						Debug.Assert(reader.ReadByte() == 0);
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
						Debug.Assert(reader.ReadInt32() == 0);
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
						instruction.SwitchCases = Enumerable
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
			writer.WriteLine($"func ${function.NameHash:x8}({string.Join(", ", function.ParameterTypes).ToLower()})");
			writer.ResetColor();
		}

		public static void PrintLabel(BasicBlock block, IColoredWriter writer) {
			writer.ForegroundColor = ConsoleColor.Magenta;
			writer.WriteLine($"{block.Name}:");
			writer.ResetColor();
		}

		public static void PrintInstruction(Instruction instruction, IColoredWriter writer) {
			writer.ForegroundColor = ConsoleColor.DarkGray;
			writer.Write($"{instruction.Offset:x4}: ");
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

				switch(operand) {
					case 't':
						// type list
						writer.Write('[');
						bool first = true;
						foreach(var type in instruction.TypeList) {
							if(!first) writer.Write(", ");
							writer.ForegroundColor = ConsoleColor.Cyan;
							writer.Write(type);
							writer.ResetColor();
							first = false;
						}
						writer.Write(']');
						break;

					case 's':
						// string data
						writer.ForegroundColor = ConsoleColor.DarkGreen;
						writer.Write('"');
						writer.Write(instruction.String); // todo escape this properly
						writer.Write('"');
						writer.ResetColor();
						break;

					case 'f': {
							// flags
							var keywords = new List<string>();
							var flags = instruction.Flags;
							keywords.Add(flags.Scope().ToString().ToLower());
							keywords.Add(flags.Type().ToString().ToLower());
							var invert = flags.InvertMode();
							if(invert != MjoInvertMode.None) keywords.Add("invert_" + invert.ToString().ToLower());
							var modifier = flags.Modifier();
							if(modifier != MjoModifier.None) keywords.Add("modify_" + modifier.ToString().ToLower());
							int dimension = flags.Dimension();
							if(dimension != 0) keywords.Add("dim" + dimension);

							writer.ForegroundColor = ConsoleColor.Cyan;
							writer.Write(string.Join(" ", keywords));
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
							writer.Write('@');
							writer.Write(instruction.JumpTarget.Name);
						}
						else {
							writer.Write($"@~{instruction.JumpOffset:x8}");
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
							foreach(var targetBlock in instruction.SwitchTargets) {
								if(!first) writer.Write(", ");
								writer.Write('@');
								writer.Write(targetBlock.Name);
								first = false;
							}
						}
						else {
							foreach(int offset in instruction.SwitchCases) {
								if(!first) writer.Write(", ");
								writer.Write($"@~{offset:x8}");
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

			writer.WriteLine();
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
			writer.WriteLine($"func ${function.NameHash:x8}({string.Join(", ", function.ParameterTypes).ToLower()}) {{");
			bool first = true;
			foreach(var block in function.BasicBlocks) {
				if(!first)
					writer.WriteLine();
				first = false;
				PrintBasicBlock(block, writer);
			}
			writer.WriteLine('}');
		}

		public static void PrintScript(MjoScript script, IColoredWriter writer) {
			bool first = true;
			foreach(var function in script.Functions) {
				if(!first)
					writer.WriteLine();
				first = false;
				PrintFunction(function, writer);
			}
		}

		public static string DumpInstruction(Instruction instruction) {
			var sb = new StringBuilder();
			PrintInstruction(instruction, new StringBuilderColorWriter(sb));
			return sb.ToString();
		}
	}
}
