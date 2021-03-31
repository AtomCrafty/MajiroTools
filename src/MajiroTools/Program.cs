using System;
using System.IO;
using System.Linq;
using System.Text;
using Majiro.Script;
using Majiro.Script.Analysis;

namespace MajiroTools {
	static class Program {
		private static readonly Encoding ShiftJis;

		static Program() {
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			ShiftJis = Encoding.GetEncoding("Shift-JIS");
		}

		static void PrintHash(string name) {
			Console.WriteLine($"{Crc32.Hash(name):x8} {name}");
		}

		static void Main(string[] args) {
			var names = new[] {
				"X_CONTROL", "CONSOLE_WROTE", "CONSOLE_CLS", "CONSOLE_ON", "CONSOLE_OFF", "CONSOLE_OFF", "PAUSE", "CRLF", "NAME_DISP", "CONSOLE_CLS", "CONSOLE_OFF", "CONSOLE_ON", "HOT_RESET",
				"$init@GLOBAL",
				"get_variable",
				"$get_variable",
				"get_variable@",
				"$get_variable@",
				"get_variable$",
				"$get_variable$",
				"get_variable@GLOBAL",
				"$get_variable@GLOBAL",
				"event_hook",
				"event_hook@MAJIRO",
				"$event_hook",
				"$event_hook@MAJIRO",
			};

			foreach(string name in names) {
				PrintHash(name);
			}

			/*
			foreach(var opcode in Opcode.List) {
				Console.Write($"{opcode.Value:X3} {opcode.Mnemonic.PadRight(13)}");
				Console.WriteLine(opcode.Aliases.Any() ? $" (aliases: {string.Join(", ", opcode.Aliases)})" : "");
			}
			//*/

			//*
			using var reader = new BinaryReader(File.OpenRead(@"start.mjo"));
			var script = Disassembler.DisassembleScript(reader);
			//foreach(var instruction in script.Instructions) {
			//	Disassembler.PrintInstruction(instruction);
			//}

			var cfg = ControlFlowGraph.BuildFromScript(script);
			foreach(var function in cfg.Functions) {
				Console.ForegroundColor = ConsoleColor.Blue;
				Console.WriteLine($"func ${function.NameHash:x8}({string.Join(", ", function.ParameterTypes)})");
				Console.ResetColor();
				foreach(var basicBlock in function.BasicBlocks) {
					Console.ForegroundColor = ConsoleColor.Magenta;
					Console.WriteLine($"{basicBlock.Name}:");
					Console.ResetColor();
					foreach(var instruction in basicBlock.Instructions) {
						Disassembler.PrintInstruction(instruction);
					}
					Console.WriteLine();
				}
				Console.WriteLine();
			}

			//*/
		}
	}
}
