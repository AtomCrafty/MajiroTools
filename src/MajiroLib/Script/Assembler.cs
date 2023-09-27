using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using CsvHelper;
using Majiro.Script.Analysis.ControlFlow;
using Majiro.Script.Analysis.StackTransition;
using Majiro.Util;
using VToolBase.Core;
using static Majiro.Script.Assembler.TokenType;

namespace Majiro.Script {
	public static class Assembler {

		internal enum TokenType {
			ReadMark,
			Enable,
			Disable,
			Unknown,
			Address,
			Label,
			Name,
			Hash,
			IntLiteral,
			HexLiteral,
			FloatLiteral,
			StringLiteral,
			LineNo,
			Resource,
			Punctuation,
			Func,
			Index,
			EntryPoint,
			VarType,
			Scope,
			Modifier,
			InvertMode,
			Dimension,
			EndOfFile,
		}

		internal struct Token {
			public TokenType Type;
			public string Value;
			public int Row;
			public int Column;

			public Token(TokenType type, string value, int row, int column) {
				Type = type;
				Value = value;
				Row = row;
				Column = column;
			}

			public override string ToString() => $"{Type}: '{Value}'";
		}

		internal static IEnumerable<Token> Tokenize(TextReader reader) {
			var sb = new StringBuilder();

			const string punctuationChars = "([{,%}])";

			TokenType GetType(string text) {
				if(text.EndsWith(':') || text.StartsWith('@'))
					return int.TryParse(text[..^1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _) ? Address : TokenType.Label;
				if(text.StartsWith('#'))
					return LineNo;
				if(text.StartsWith('$'))
					return Hash;
				if(int.TryParse(text, out _))
					return IntLiteral;
				if((text.StartsWith("0x") || text.StartsWith("0X")) && int.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
					return HexLiteral;
				if(float.TryParse(text, out _))
					return FloatLiteral;
				if(text.Contains('"'))
					return StringLiteral;
				if(text.Length == 1 && punctuationChars.Contains(text[0]))
					return Punctuation;
				if(text == "func")
					return Func;
				if(text == "index")
					return TokenType.Index;
				if(text.IsOneOf("entry", "entrypoint"))
					return EntryPoint;
				if(text.ToLower().IsOneOf("int", "float", "string", "intarray", "floatarray", "stringarray"))
					return VarType;
				if(text.ToLower().IsOneOf("persist", "persistent", "save", "savefile", "thread", "local"))
					return Scope;
				if(text.ToLower().IsOneOf("dim1", "dim2", "dim3"))
					return Dimension;
				if(text.ToLower().IsOneOf("preinc", "predec", "postinc", "postdec"))
					return Modifier;
				if(text.ToLower().IsOneOf("invert_numeric", "invert_boolean", "invert_bitwise"))
					return InvertMode;
				if(text.ToLower() == "readmark")
					return ReadMark;
				if(text.ToLower().IsOneOf("enable", "enabled"))
					return Enable;
				if(text.ToLower().IsOneOf("disable", "disabled"))
					return Disable;
				return Name;
			}

			int row = 1;
			int column = 1;

			bool FinishToken(out Token token) {
				if(sb.Length > 0) {
					string text = sb.ToString();
					var type = GetType(text);
					token = new Token(type, text, row, column);
					sb.Clear();
					return true;
				}
				token = new Token();
				return false;
			}

			bool inString = false;
			bool inEscape = false;
			bool inComment = false;

			while(true) {
				int x = reader.Read();

				if(x == -1) {
					if(FinishToken(out var token))
						yield return token;
					yield return new Token(EndOfFile, "", row, column);
					yield break;
				}

				bool finishBefore = false;
				bool finishAfter = false;
				bool discard = false;

				char c = (char)x;

				if(c == '\n') {
					row++;
					column = 1;
				}
				else if(c == '\t') {
					const int tabSize = 4;
					column += tabSize - (column - 1) % tabSize;
				}
				else {
					column++;
				}

				if(inString) {
					if(inEscape) {
						inEscape = false;
					}
					else {
						switch(c) {
							case '\\':
								inEscape = true;
								break;

							case '"':
								inString = false;
								finishAfter = true;
								break;
						}
					}
				}
				else if(inComment) {
					discard = true;
					if(c == '\n')
						inComment = false;
				}
				else if(c == '"') {
					inString = true;
				}
				else if(c == ';') {
					discard = true;
					inComment = true;
				}
				else if(c == '%') {
					if(sb.Length == 0) {
						// this percent sign is at the start of a %{} construct,
						// so we treat it as a punctuation character.
						finishBefore = true;
						finishAfter = true;
					}
					else {
						// otherwise, the percent sign is part of an identifier like $sin%
						Debug.Assert(sb.ToString().StartsWith('$'));
					}
				}
				else if(punctuationChars.Contains(c)) {
					finishBefore = true;
					finishAfter = true;
				}
				else if(char.IsWhiteSpace(c)) {
					finishBefore = true;
					discard = true;
				}

				if(finishBefore) {
					if(FinishToken(out var token))
						yield return token;
				}

				if(!discard)
					sb.Append(c);

				if(finishAfter) {
					if(FinishToken(out var token))
						yield return token;
				}
			}
		}

		public static MjoScript Parse(TextReader reader) {
			var tokens = Tokenize(reader);
			using var enumerator = tokens.GetEnumerator();

			Token lt = default;
			Token ct = default;
			Advance();

			bool Advance() {
				if(!enumerator.MoveNext())
					throw new Exception("No more tokens to read");
				lt = ct;
				ct = enumerator.Current;
				return ct.Type != EndOfFile;
			}

			Token Consume(params TokenType[] allowedTypes) {
				var token = ct;
				if(allowedTypes.Any() && !allowedTypes.Contains(ct.Type))
					throw new Exception($"Unexpected token {ct.Type} '{ct.Value}' ({token.Row}, {token.Column}), expected one of these: {string.Join(", ", allowedTypes)}");
				Advance();
				return token;
			}

			void ConsumeIf(params TokenType[] allowedTypes) {
				TryConsume(out _, allowedTypes);
			}

			bool TryConsume(out Token token, params TokenType[] allowedTypes) {
				token = ct;
				if(allowedTypes.Any() && !allowedTypes.Contains(ct.Type))
					return false;
				return Advance();
			}

			Token ConsumePunctuation(params string[] allowedText) {
				var token = Consume(Punctuation);
				if(allowedText.Any() && !allowedText.Contains(token.Value))
					throw new Exception($"Unexpected token {token.Type} '{token.Value}' ({token.Row}, {token.Column}), expected one of these: {string.Join(", ", allowedText)}");
				return token;
			}

			bool TryConsumePunctuation(out Token token, params string[] allowedText) {
				if(!TryConsume(out token, Punctuation)) return false;
				if(allowedText.Any() && !allowedText.Contains(token.Value))
					return false;
				return true;
			}

			var blocks = new Dictionary<string, BasicBlock>();

			uint ParseHash() {
				string name = Consume(Hash).Value[1..];
				if(uint.TryParse(name, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hash))
					return hash;
				if(!name.Contains('@')) {
					//Console.WriteLine(">" + name + "<");
					Debug.Assert(Data.KnownSyscallNames.Contains(name));
					return Crc.Hash32('$' + name + Data.SyscallSuffix);
				}
				Debug.Assert(name.StartsWith('$'));
				hash = Crc.Hash32(name);
				Debug.Assert(Data.KnownFunctionNamesByHash.ContainsKey(hash));
				return hash;
			}

			MjoType ParseType() => Consume(VarType).Value.ToLower() switch {
				"int" => MjoType.Int,
				"float" => MjoType.Float,
				"string" => MjoType.String,
				"intarray" => MjoType.IntArray,
				"floatarray" => MjoType.FloatArray,
				"stringarray" => MjoType.StringArray,
				var invalid => throw new Exception($"invalid type name: {invalid}")
			};

			MjoScope ParseScope() => Consume(Scope).Value.ToLower() switch {
				"persistent" => MjoScope.Persistent,
				"persist" => MjoScope.Persistent,
				"savefile" => MjoScope.SaveFile,
				"save" => MjoScope.SaveFile,
				"thread" => MjoScope.Thread,
				"local" => MjoScope.Local,
				var invalid => throw new Exception($"invalid scope name: {invalid}")
			};

			MjoModifier ParseModifier() => Consume(Modifier).Value.ToLower() switch {
				"preinc" => MjoModifier.PreIncrement,
				"incx" => MjoModifier.PreIncrement,
				"predec" => MjoModifier.PreDecrement,
				"decx" => MjoModifier.PreDecrement,
				"postinc" => MjoModifier.PostIncrement,
				"xinc" => MjoModifier.PostIncrement,
				"postdec" => MjoModifier.PostDecrement,
				"xdec" => MjoModifier.PostDecrement,
				var invalid => throw new Exception($"invalid modifier name: {invalid}")
			};

			MjoInvertMode ParseInvertMode() => Consume(InvertMode).Value.ToLower() switch {
				"invert_numeric" => MjoInvertMode.Numeric,
				"neg" => MjoInvertMode.Numeric,
				"invert_bitwise" => MjoInvertMode.Bitwise,
				"not" => MjoInvertMode.Bitwise,
				"invert_boolean" => MjoInvertMode.Boolean,
				"notl" => MjoInvertMode.Boolean,
				var invalid => throw new Exception($"invalid invert mode: {invalid}")
			};

			int ParseDimension() => Consume(Dimension).Value.ToLower() switch {
				"dim1" => 1,
				"dim2" => 2,
				"dim3" => 3,
				var invalid => throw new Exception($"invalid dimension specifier: {invalid}")
			};

			MjoFlags ParseFlags() {
				MjoType type = 0;
				MjoScope scope = 0;
				MjoModifier modifier = 0;
				MjoInvertMode invertMode = 0;
				int dimension = 0;

				while(ct.Type.IsOneOf(VarType, Scope, Modifier, InvertMode, Dimension)) {
					switch(ct.Type) {
						case VarType:
							Debug.Assert(type == 0);
							type = ParseType();
							break;

						case Scope:
							Debug.Assert(scope == 0);
							scope = ParseScope();
							break;

						case Modifier:
							Debug.Assert(modifier == 0);
							modifier = ParseModifier();
							break;

						case InvertMode:
							Debug.Assert(invertMode == 0);
							invertMode = ParseInvertMode();
							break;

						case Dimension:
							Debug.Assert(dimension == 0);
							dimension = ParseDimension();
							break;
					}
				}

				return FlagHelpers.Build(type, scope, modifier, invertMode, dimension);
			}

			int ParseIntLiteral() {
				var token = Consume(IntLiteral, HexLiteral, Hash);
				return token.Type switch {
					IntLiteral => int.Parse(token.Value),
					HexLiteral => int.Parse(token.Value[2..], NumberStyles.HexNumber),
					Hash => int.Parse(token.Value[1..], NumberStyles.HexNumber),
					_ => throw new Exception("unreachable")
				};
			}

			static int ParseRelativeJumpLabel(string jumpLabel) {
				Debug.Assert(jumpLabel.StartsWith('~'));
				Debug.Assert(jumpLabel.Length >= 2);
				switch(jumpLabel[1]) {
					case '-':
						return -int.Parse(jumpLabel[2..], NumberStyles.HexNumber);
					case '+':
						return int.Parse(jumpLabel[2..], NumberStyles.HexNumber);
					default:
						return int.Parse(jumpLabel[1..], NumberStyles.HexNumber);
				}
			}

			BasicBlock GetBlock(Function function, string label) {
				if(!blocks.TryGetValue(label, out var block)) {
					block = new BasicBlock(function, label);
					blocks[label] = block;
				}
				return block;
			}

			Instruction ParseInstruction(BasicBlock block) {

				string mnemonic = Consume(Name).Value;
				var opcode = mnemonic == "phi"
					? PhiInstruction.PhiOpcode
					: Opcode.ByMnemonic[mnemonic];

				var instruction = new Instruction(opcode, block);

				foreach(char operand in opcode.Encoding) {
					switch(operand) {
						case 't':
							instruction.TypeList = ParseTypeList("[", "]");
							break;

						case 's':
							if(ct.Value == "%") {
								ConsumePunctuation("%");
								ConsumePunctuation("{");
								instruction.ExternalKey = Consume(Name).Value;
								ConsumePunctuation("}");
							}
							else {
								string value = Consume(StringLiteral).Value[1..^1].Unescape();
								instruction.String = value;
							}
							break;

						case 'f':
							instruction.Flags = ParseFlags();
							break;

						case 'h':
							instruction.Hash = ParseHash();
							break;

						case 'o':
							if(instruction.Flags.Scope() != MjoScope.Local && ct.Type != IntLiteral) {
								instruction.VarOffset = -1;
								break;
							}

							instruction.VarOffset = short.Parse(Consume(IntLiteral).Value);
							break;

						case '0':
							break;

						case 'i':
							instruction.IntValue = ParseIntLiteral();
							break;

						case 'r':
							instruction.FloatValue = float.Parse(Consume(IntLiteral, FloatLiteral).Value, CultureInfo.InvariantCulture);
							break;

						case 'a':
							ConsumePunctuation("(");
							instruction.ArgumentCount = ushort.Parse(Consume(IntLiteral).Value);
							ConsumePunctuation(")");
							break;

						case 'j':
							string jumpLabel = Consume(TokenType.Label).Value[1..];
							if(block != null) {
								Debug.Assert(!jumpLabel.StartsWith('~'));
								instruction.JumpTarget = GetBlock(block.Function, jumpLabel);
							}
							else {
								instruction.JumpOffset = ParseRelativeJumpLabel(jumpLabel);
							}
							break;

						case 'l':
							instruction.LineNumber = ushort.Parse(Consume(LineNo).Value[1..]);
							break;

						case 'c':
							if(block != null) {
								var switchTargets = new List<BasicBlock>();
								do {
									string switchLabel = Consume(TokenType.Label).Value[1..];
									Debug.Assert(!switchLabel.StartsWith('~'));
									switchTargets.Add(GetBlock(block.Function, switchLabel));
								} while(TryConsumePunctuation(out _, ","));
								instruction.SwitchTargets = switchTargets.ToArray();
							}
							else {
								var switchOffsets = new List<int>();
								do {
									string switchLabel = Consume(TokenType.Label).Value[1..];
									switchOffsets.Add(ParseRelativeJumpLabel(switchLabel));
								} while(TryConsumePunctuation(out _, ","));
								instruction.SwitchOffsets = switchOffsets.ToArray();
							}
							break;

						case 'p':
							do {
								Consume(TokenType.Label);
							} while(TryConsumePunctuation(out _, ","));
							break;
					}
				}

				return instruction;
			}

			BasicBlock ParseBlock(Function function) {

				string label = Consume(TokenType.Label).Value[..^1];
				var block = GetBlock(function, label);

				//block.PhiNodes = new List<PhiInstruction>();

				while(ct.Type != TokenType.Label && ct.Value != "}") {
					var instruction = ParseInstruction(block);
					//if(instruction is PhiInstruction phi)
					//	block.PhiNodes.Add(phi);
					//else
					block.Instructions.Add(instruction);
				}

				return block;
			}

			MjoType[] ParseTypeList(string start, string end) {
				var types = new List<MjoType>();

				ConsumePunctuation(start);
				if(ct.Value != end) {
					Token tok;
					do {
						types.Add(ParseType());
						tok = ConsumePunctuation(",", end);
					} while(tok.Value == ",");
				}
				else {
					ConsumePunctuation(end);
				}

				return types.ToArray();
			}

			Function ParseFunction(MjoScript script) {
				blocks.Clear();

				Consume(Func);
				uint hash = ParseHash();

				var types = ParseTypeList("(", ")");

				var function = new Function(script, hash) {
					ParameterTypes = types,
					Blocks = new List<BasicBlock>()
				};

				if(ct.Type == EntryPoint) {
					Consume();
					Debug.Assert(script.EntryPointFunction == null, $"Multiple entry points found ({script.EntryPointFunction?.NameHash:x8} and {hash:x8})");
					script.EntryPointFunction = function;
				}

				ConsumePunctuation("{");

				while(ct.Value != "}") {
					function.Blocks.Add(ParseBlock(function));
				}

				ConsumePunctuation("}");

				function.LocalTypes = function.Instructions.Single(inst => inst.IsAlloca).TypeList;
				return function;
			}

			MjoScript ParseScript() {
				var script = new MjoScript {
					EnableReadMark = true
				};

				if(TryConsume(out _, ReadMark)) {
					script.EnableReadMark = Consume(Enable, Disable).Type == Enable;
				}

				switch(ct.Type) {
					case Func:
						script.Representation = MjoScriptRepresentation.ControlFlowGraph;
						script.Functions = new List<Function>();
						while(ct.Type != EndOfFile) {
							var function = ParseFunction(script);
							script.Functions.Add(function);
						}
						break;

					case TokenType.Index:
						script.Representation = MjoScriptRepresentation.InstructionList;
						script.FunctionIndex = new List<FunctionIndexEntry>();
						while(TryConsume(out _, TokenType.Index)) {
							uint hash = ParseHash();
							uint offset = (uint)ParseIntLiteral();
							if(TryConsume(out _, EntryPoint)) {
								Debug.Assert(script.EntryPointOffset == null, "Multiple entry points found");
								script.EntryPointOffset = offset;
							}
							script.FunctionIndex.Add(new FunctionIndexEntry {
								NameHash = hash,
								Offset = offset
							});
						}

						while(TryConsume(out var addressToken, Address)) {
							var instruction = ParseInstruction(null);
							instruction.Offset = uint.Parse(addressToken.Value[..^1], NumberStyles.HexNumber);
							// this is valid because externalized strings are not allowed in instruction list mode
							instruction.Size = GetInstructionSize(instruction);
							script.Instructions.Add(instruction);
						}
						break;

					default:
						Consume(Func, TokenType.Index);
						throw new Exception("unreachable");
				}

				Debug.Assert(ct.Type == EndOfFile);

				script.SanityCheck();
				return script;
			}

			try {
				return ParseScript();
			}
			catch(Exception e) {
				throw new Exception($"Failed to parse script. Last token: {lt.Type} '{lt.Value}' in line {lt.Row}, column {lt.Column}", e);
			}
		}

		public static Dictionary<string, string> ReadResourceTable(Stream s) {
			using var reader = new CsvReader(s.NewTextReader(), CultureInfo.InvariantCulture);
			return reader.GetRecords(new { Key = "", Value = "" }).ToDictionary(pair => pair.Key, pair => pair.Value);
		}

		public static void AssembleToFile(MjoScript script, string path) {
			using var writer = File.Open(path, FileMode.Create).NewWriter();
			AssembleScript(script, writer);
		}

		public static void AssembleScript(MjoScript script, BinaryWriter writer, bool encrypt = true) {

			script.InternalizeStrings();
			script.ToInstructionList();

			using var ms = new MemoryStream();
			using var bw = new BinaryWriter(ms, Helpers.ShiftJis, true);

			AssembleByteCode(script, bw, out int readMarkCount);

			uint byteCodeSize = (uint)ms.Position;
			var byteCode = ms.GetBuffer();
			if(encrypt)
				Crc.Crypt32(byteCode);

			writer.Write(Encoding.ASCII.GetBytes(encrypt
				? "MajiroObjX1.000\0"
				: "MajiroObjV1.000\0"));

			writer.Write(script.EntryPointOffset!.Value);
			writer.Write(script.EnableReadMark ? readMarkCount : 0);
			writer.Write(script.FunctionIndex.Count);
			foreach(var entry in script.FunctionIndex) {
				writer.Write(entry.NameHash);
				writer.Write(entry.Offset);
			}

			writer.Write(byteCodeSize);
			writer.Write(byteCode, 0, (int)byteCodeSize);
		}

		private static void AssembleByteCode(MjoScript script, BinaryWriter writer, out int readMarkCount) {
			readMarkCount = 0;
			uint currentOffset = 0;
			foreach(var instruction in script.Instructions) {
				Debug.Assert(instruction.Offset == currentOffset);
				currentOffset += GetInstructionSize(instruction);
				AssembleInstruction(instruction, writer, ref readMarkCount);
			}
		}

		private static void AssembleInstruction(Instruction instruction, BinaryWriter writer, ref int readMarkCount) {
			writer.Write(instruction.Opcode.Value);

			foreach(char operand in instruction.Opcode.Encoding) {
				switch(operand) {
					case 't':
						// type list
						writer.Write((ushort)instruction.TypeList.Length);
						foreach(var type in instruction.TypeList) {
							writer.Write((byte)type);
						}
						break;

					case 's':
						// string data
						var bytes = Helpers.ShiftJis.GetBytes(instruction.String);
						writer.Write((ushort)(bytes.Length + 1));
						writer.Write(bytes);
						writer.Write((byte)0);
						break;

					case 'f':
						// flags
						writer.Write((ushort)instruction.Flags);
						break;

					case 'h':
						// name hash
						writer.Write(instruction.Hash);
						break;

					case 'o':
						// variable offset
						writer.Write(instruction.VarOffset);
						break;

					case '0':
						// 4 byte address placeholder
						writer.Write(0);
						break;

					case 'i':
						// integer constant
						writer.Write(instruction.IntValue);
						break;

					case 'r':
						// float constant
						writer.Write(instruction.FloatValue);
						break;

					case 'a':
						// argument count
						writer.Write(instruction.ArgumentCount);
						break;

					case 'j':
						// jump offset
						writer.Write(instruction.JumpOffset!.Value);
						break;

					case 'l':
						// line number
						readMarkCount = Math.Max(readMarkCount, instruction.LineNumber);
						writer.Write(instruction.LineNumber);
						break;

					case 'c':
						// switch case table
						writer.Write((ushort)instruction.SwitchOffsets.Length);
						foreach(int caseOffset in instruction.SwitchOffsets) {
							writer.Write(caseOffset);
						}
						break;

					default:
						throw new Exception("Unrecognized encoding specifier: " + operand);
				}
			}
		}

		public static uint GetInstructionSize(Instruction instruction) {
			uint size = 2;
			foreach(char operand in instruction.Opcode.Encoding) {
				switch(operand) {
					case 't':
						size += 2;
						size += (uint)instruction.TypeList.Length;
						break;

					case 's':
						if(instruction.ExternalKey != null)
							throw new Exception("Unable to determine size of an instruction with an externalized string. Internalize all strings before encoding the script!");
						Debug.Assert(instruction.String != null);
						size += 2;
						size += (uint)Helpers.ShiftJis.GetByteCount(instruction.String) + 1;
						break;

					case 'f':
					case 'o':
					case 'a':
					case 'l':
						size += 2;
						break;

					case 'h':
					case '0':
					case 'i':
					case 'r':
					case 'j':
						size += 4;
						break;

					case 'c':
						size += 2;
						size += (uint)(instruction.SwitchOffsets?.Length ?? instruction.SwitchTargets.Length) * 4;
						break;

					default:
						throw new Exception("Unknown encoding specifier: " + operand);
				}
			}

			return size;
		}
	}
}
