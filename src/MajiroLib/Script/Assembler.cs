using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Majiro.Script.Analysis.ControlFlow;
using Majiro.Script.Analysis.StackTransition;
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
			FloatLiteral,
			StringLiteral,
			LineNo,
			Resource,
			Punctuation,
			Func,
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

			public Token(TokenType type, string value) {
				Type = type;
				Value = value;
			}

			public override string ToString() => $"{Type}: '{Value}'";
		}

		internal static IEnumerable<Token> Tokenize(TextReader reader) {
			var sb = new StringBuilder();

			TokenType GetType(string text) {
				if(text.EndsWith(':') || text.StartsWith('@'))
					return int.TryParse(text[..^1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _) ? Address : Label;
				if(text.StartsWith('#'))
					return LineNo;
				if(text.StartsWith('$'))
					return Hash;
				if(int.TryParse(text, out _))
					return IntLiteral;
				if(float.TryParse(text, out _))
					return FloatLiteral;
				if(text.Contains('"'))
					return StringLiteral;
				if(text.Length == 1 && "([{,}])".Contains(text[0]))
					return Punctuation;
				if(text == "func")
					return Func;
				if(text.IsOneOf("entry", "entrypoint"))
					return EntryPoint;
				if(text.ToLower().IsOneOf("int", "float", "string", "intarray", "floatarray", "stringarray"))
					return VarType;
				if(text.ToLower().IsOneOf("persist", "persistent", "save", "savefile", "thread", "local"))
					return Scope;
				if(text.ToLower().IsOneOf("dim1", "dim2", "dim3"))
					return Dimension;
				if(text.StartsWith("modifier_"))
					return Modifier;
				if(text.StartsWith("invert_"))
					return InvertMode;
				if(text.ToLower() == "readmark")
					return ReadMark;
				if(text.ToLower().IsOneOf("enable", "enabled"))
					return Enable;
				if(text.ToLower().IsOneOf("disable", "disabled"))
					return Disable;
				return Name;
			}

			bool FinishToken(out Token token) {
				if(sb.Length > 0) {
					string text = sb.ToString();
					var type = GetType(text);
					token = new Token(type, text);
					sb.Clear();
					return true;
				}
				token = new Token();
				return false;
			}

			bool inString = false;
			bool inEscape = false;

			while(true) {
				int x = reader.Read();

				if(x == -1) {
					if(FinishToken(out var token))
						yield return token;
					yield return new Token(EndOfFile, "");
					yield break;
				}

				bool finishBefore = false;
				bool finishAfter = false;
				bool discard = false;

				char c = (char)x;

				if(inString) {
					switch(c) {
						case '\\' when !inEscape:
							inEscape = true;
							continue;

						case '"' when !inEscape:
							inString = false;
							finishAfter = true;
							break;
					}
					// todo handle escape sequences
				}
				else if(c == '"') {
					inString = true;
				}
				else if("([{,}])".Contains(c)) {
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

			Token ct;
			Advance();

			bool Advance() {
				if(!enumerator.MoveNext())
					throw new Exception("No more tokens to read");
				ct = enumerator.Current;
				return ct.Type != EndOfFile;
			}

			Token Consume(params TokenType[] allowedTypes) {
				var token = ct;
				if(allowedTypes.Any() && !allowedTypes.Contains(ct.Type))
					throw new Exception($"Unexpected token {ct.Type} '{ct.Value}', expected one of these: {string.Join(", ", allowedTypes)}");
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
					throw new Exception($"Unexpected token {token.Type} '{token.Value}', expected one of these: {string.Join(", ", allowedText)}");
				return token;
			}

			bool TryConsumePunctuation(out Token token, params string[] allowedText) {
				if(!TryConsume(out token, Punctuation)) return false;
				if(allowedText.Any() && !allowedText.Contains(token.Value))
					return false;
				return true;
			}

			var instructions = new List<Instruction>();
			var blocks = new Dictionary<string, BasicBlock>();
			long currentOffset = 0;

			uint ParseHash() => uint.Parse(Consume(Hash).Value[1..], NumberStyles.HexNumber);

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

			int ParseDimension() => Consume(InvertMode).Value.ToLower() switch {
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

			BasicBlock GetBlock(Function function, string label) {
				if(!blocks.TryGetValue(label, out var block)) {
					block = new BasicBlock(function);
					blocks[label] = block;
				}
				return block;
			}

			Instruction ParseInstruction(BasicBlock block) {

				ConsumeIf(Address);

				string mnemonic = Consume(Name).Value;
				var opcode = mnemonic == "phi"
					? PhiInstruction.PhiOpcode
					: Opcode.ByMnemonic[mnemonic];

				var instruction = new Instruction(opcode, (uint)currentOffset);
				currentOffset += 2;

				foreach(char operand in opcode.Encoding) {
					switch(operand) {
						case 't':
							instruction.TypeList = ParseTypeList("[", "]");
							currentOffset += 2;
							currentOffset += instruction.TypeList.Length;
							break;

						case 's':
							instruction.String = Consume(StringLiteral).Value[1..^1];
							currentOffset += 2;
							currentOffset += Helpers.ShiftJis.GetByteCount(instruction.String) + 1;
							break;

						case 'f':
							instruction.Flags = ParseFlags();
							currentOffset += 2;
							break;

						case 'h':
							instruction.Hash = ParseHash();
							currentOffset += 4;
							break;

						case 'o':
							instruction.VarOffset = short.Parse(Consume(IntLiteral).Value);
							currentOffset += 2;
							break;

						case '0':
							currentOffset += 4;
							break;

						case 'i':
							instruction.IntValue = int.Parse(Consume(IntLiteral).Value);
							currentOffset += 4;
							break;

						case 'r':
							instruction.FloatValue = float.Parse(Consume(IntLiteral, FloatLiteral).Value, CultureInfo.InvariantCulture);
							currentOffset += 4;
							break;

						case 'a':
							ConsumePunctuation("(");
							instruction.ArgumentCount = ushort.Parse(Consume(IntLiteral).Value);
							ConsumePunctuation(")");
							currentOffset += 2;
							break;

						case 'j':
							string jumpLabel = Consume(Label).Value[1..];
							if(jumpLabel.StartsWith('~')) {
								instruction.JumpOffset = int.Parse(jumpLabel[1..], NumberStyles.HexNumber);
							}
							else {
								instruction.JumpTarget = GetBlock(block.Function, jumpLabel);
							}
							currentOffset += 4;
							break;

						case 'l':
							instruction.LineNumber = ushort.Parse(Consume(LineNo).Value[1..]);
							currentOffset += 2;
							break;

						case 'c':
							var switchTargets = new List<BasicBlock>();
							do {
								string switchLabel = Consume(Label).Value[1..];
								switchTargets.Add(GetBlock(block.Function, switchLabel));
							} while(TryConsumePunctuation(out _, ","));

							instruction.SwitchTargets = switchTargets.ToArray();
							currentOffset += 2;
							currentOffset += switchTargets.Count * 4;
							break;

						case 'p':
							do {
								Consume(Label);
							} while(TryConsumePunctuation(out _, ","));
							break;
					}
				}

				instruction.Size = (uint)(currentOffset - instruction.Offset);
				return instruction;
			}

			BasicBlock ParseBlock(Function function) {

				string label = Consume(Label).Value[..^1];
				var block = GetBlock(function, label);

				block.FirstInstructionIndex = instructions.Count;
				block.PhiNodes = new List<PhiInstruction>();

				while(ct.Type != Label && ct.Value != "}") {
					var instruction = ParseInstruction(block);
					if(instruction is PhiInstruction phi)
						block.PhiNodes.Add(phi);
					else
						instructions.Add(instruction);
				}

				block.LastInstructionIndex = instructions.Count - 1;
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
				Consume(Func);
				uint hash = ParseHash();

				var types = ParseTypeList("(", ")");

				if(ct.Type == EntryPoint) {
					Consume();
					script.EntryPointOffset = (uint)currentOffset;
				}

				ConsumePunctuation("{");

				var function = new Function(script, hash) {
					ParameterTypes = types,
					BasicBlocks = new List<BasicBlock>(),
					FirstInstructionIndex = instructions.Count
				};

				while(ct.Value != "}") {
					function.BasicBlocks.Add(ParseBlock(function));
				}

				ConsumePunctuation("}");

				function.LastInstructionIndex = instructions.Count - 1;
				return function;
			}

			var script = new MjoScript(uint.MaxValue, new List<FunctionEntry>(), instructions) {
				Functions = new List<Function>(),
				EnableReadMark = true
			};

			if(TryConsume(out _, ReadMark)) {
				script.EnableReadMark = Consume(Enable, Disable).Type == Enable;
			}

			while(ct.Type != EndOfFile) {
				var function = ParseFunction(script);
				script.Functions.Add(function);
				script.Index.Add(new FunctionEntry {
					NameHash = function.NameHash,
					Offset = function.StartOffset
				});
			}

			foreach(var instruction in instructions) {
				if(instruction.JumpTarget != null) {
					instruction.JumpOffset = (int)(instruction.JumpTarget.StartOffset - (instruction.Offset + instruction.Size));
				}
				if(instruction.SwitchTargets != null) {
					uint offset = instruction.Offset + 6;
					instruction.SwitchCases = new int[instruction.SwitchTargets.Length];
					for(int i = 0; i < instruction.SwitchTargets.Length; i++) {
						instruction.SwitchCases[i] = (int)(offset - instruction.SwitchTargets[i].StartOffset);
						offset += 4;
					}
				}
			}

			Debug.Assert(script.EntryPointOffset != uint.MaxValue);

			return script;
		}

		public static void AssembleScript(MjoScript script, BinaryWriter writer, bool encrypt = true, bool readMark = true) {

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

			writer.Write(script.EntryPointOffset);
			writer.Write(readMark ? readMarkCount : 0);
			writer.Write(script.Index.Count);
			foreach(var entry in script.Index) {
				writer.Write(entry.NameHash);
				writer.Write(entry.Offset);
			}

			writer.Write(byteCodeSize);
			writer.Write(byteCode, 0, (int)byteCodeSize);
		}

		private static void AssembleByteCode(MjoScript script, BinaryWriter writer, out int readMarkCount) {
			readMarkCount = 0;
			foreach(var instruction in script.Instructions) {
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
						writer.Write(instruction.JumpOffset);
						break;

					case 'l':
						// line number
						readMarkCount = Math.Max(readMarkCount, instruction.LineNumber);
						writer.Write(instruction.LineNumber);
						break;

					case 'c':
						// switch case table
						writer.Write((ushort)instruction.SwitchCases.Length);
						foreach(int caseOffset in instruction.SwitchCases) {
							writer.Write(caseOffset);
						}
						break;

					default:
						throw new Exception("Unrecognized encoding specifier: " + operand);
				}
			}
		}
	}
}
