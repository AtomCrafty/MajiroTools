using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Majiro.Script {
	public static class Disassembler {

		public static MjoScript DisassembleScript(BinaryReader reader) {
			string signature = reader.ReadSizedString(16);
			bool isEncrypted = signature == "MajiroObjX1.000\0";
			Debug.Assert(isEncrypted ^ (signature == "MajiroObjV1.000\0"));

			uint entryPointIndex = reader.ReadUInt32();
			uint unknown_14 = reader.ReadUInt32();
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
			if(isEncrypted) Crc32.Crypt(byteCode);
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
						var bytes = reader.ReadBytes(size);
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
							.Select(_ => reader.ReadInt16())
							.ToArray();
						break;

					default:
						throw new Exception("Unrecognized encoding specifier: " + operand);
				}
			}

			return instruction;
		}

		public static string FormatInstruction(Instruction instruction) {
			var sb = new StringBuilder();

			sb.Append($"{instruction.Offset:x4}: {instruction.Opcode.Mnemonic,-13}");

			foreach(char operand in instruction.Opcode.Encoding) {

				if(operand == '0') continue;
				sb.Append(' ');

				switch(operand) {
					case 't':
						// type list
						sb.Append('[');
						sb.Append(']');
						break;

					case 's':
						// string data
						sb.Append('"');
						sb.Append(instruction.String); // todo escape this properly
						sb.Append('"');
						break;

					case 'f':
						// flags
						sb.Append(instruction.Flags); // todo proper keywords
						break;

					case 'h':
						// name hash
						sb.Append('$');
						sb.Append(instruction.Hash.ToString("x8"));
						break;

					case 'o':
						// variable offset
						sb.Append(instruction.VarOffset);
						break;

					case '0':
						// 4 byte address placeholder
						break;

					case 'i':
						// integer constant
						sb.Append(instruction.IntValue);
						break;

					case 'r':
						// float constant
						sb.Append(instruction.FloatValue);
						break;

					case 'a':
						// argument count
						sb.Append('(');
						sb.Append(instruction.ArgumentCount);
						sb.Append(')');
						break;

					case 'j':
						// jump offset
						sb.Append('@');
						sb.Append(instruction.JumpOffset);
						break;

					case 'l':
						// line number
						sb.Append('#');
						sb.Append(instruction.LineNumber);
						break;

					case 'c':
						// switch case table
						bool first = true;
						foreach(short offset in instruction.SwitchCases) {
							if(!first) sb.Append(", ");
							sb.Append('@');
							sb.Append(instruction.JumpOffset);
							first = false;
						}
						break;
				}
			}

			return sb.ToString();
		}

		public static void PrintInstruction(Instruction instruction) {
			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.Write($"{instruction.Offset:x4}: ");
			Console.ForegroundColor = instruction.Opcode.Mnemonic != "line" ? ConsoleColor.White : ConsoleColor.DarkGray;
			Console.Write($"{instruction.Opcode.Mnemonic,-13}");
			Console.ResetColor();

			foreach(char operand in instruction.Opcode.Encoding) {

				if(operand == '0') continue;
				Console.Write(' ');

				switch(operand) {
					case 't':
						// type list
						Console.Write('[');
						bool first = true;
						foreach(MjoType type in instruction.TypeList) {
							if(!first) Console.Write(", ");
							Console.ForegroundColor = ConsoleColor.Cyan;
							Console.Write(type);
							Console.ResetColor();
							first = false;
						}
						Console.Write(']');
						break;

					case 's':
						// string data
						Console.ForegroundColor = ConsoleColor.DarkGreen;
						Console.Write('"');
						Console.Write(instruction.String); // todo escape this properly
						Console.Write('"');
						Console.ResetColor();
						break;

					case 'f':
						// flags
						Console.Write(instruction.Flags); // todo proper keywords
						break;

					case 'h':
						// name hash
						Console.ForegroundColor = ConsoleColor.Yellow;
						Console.Write('$');
						Console.Write(instruction.Hash.ToString("x8"));
						Console.ResetColor();
						break;

					case 'o':
						// variable offset
						Console.Write(instruction.VarOffset);
						break;

					case '0':
						// 4 byte address placeholder
						break;

					case 'i':
						// integer constant
						Console.Write(instruction.IntValue);
						break;

					case 'r':
						// float constant
						Console.Write(instruction.FloatValue);
						break;

					case 'a':
						// argument count
						Console.Write('(');
						Console.Write(instruction.ArgumentCount);
						Console.Write(')');
						break;

					case 'j':
						// jump offset
						Console.ForegroundColor = ConsoleColor.Magenta;
						Console.Write('@');
						Console.Write(instruction.JumpOffset);
						Console.ResetColor();
						break;

					case 'l':
						// line number
						Console.ForegroundColor = ConsoleColor.DarkGray;
						Console.Write('#');
						Console.Write(instruction.LineNumber);
						Console.ResetColor();
						break;

					case 'c':
						// switch case table
						first = true;
						foreach(short offset in instruction.SwitchCases) {
							if(!first) Console.Write(", ");
							Console.ForegroundColor = ConsoleColor.Magenta;
							Console.Write('@');
							Console.Write(offset);
							Console.ResetColor();
							first = false;
						}
						break;
				}
			}

			Console.WriteLine();
		}
	}
}
