using System;
using System.IO;
using System.Linq;
using System.Text;
using Majiro.Script;
using Majiro.Script.Analysis.ControlFlow;
using Majiro.Script.Analysis.StackTransition;

namespace MajiroTools {
	static class Program {
		private static readonly Encoding ShiftJis;

		static Program() {
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			ShiftJis = Encoding.GetEncoding("Shift-JIS");
		}

		static void PrintHash(string name) {
			Console.WriteLine($"{Crc.Hash32(name):x8} {name}");
		}

		static void Main(string[] args) {
			var names = new[] {
				"X_CONTROL",
				"CONSOLE_WROTE",
				"CONSOLE_CLS",
				"CONSOLE_ON",
				"CONSOLE_OFF",
				"PAUSE",
				"CRLF",
				"NAME_DISP",
				"HOT_RESET",
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
				"POPUP_SAVE",
				"POPUP_LOAD",
				"POPUP_SPEED",
				"POPUP_SPEEDCONFIG",
				"POPUP_SPEED_CONFIG",
				"__SYS__NumParams",
				};

			//foreach(string name in names) {
			//	PrintHash(name);
			//}

			/*
			foreach(var opcode in Opcode.List) {
				Console.Write($"{opcode.Value:X3} {opcode.Mnemonic.PadRight(13)}");
				Console.WriteLine(opcode.Aliases.Any() ? $" (aliases: {string.Join(", ", opcode.Aliases)})" : "");
			}
			//*/

			//*
			using var reader = new BinaryReader(File.OpenRead(@"start.mjo"));
			var script = Disassembler.DisassembleScript(reader);

			ControlFlowPass.Analyze(script);
			StackTransitionPass.Analyze(script);

			foreach(var function in script.Functions) {
				Disassembler.PrintFunctionHeader(function);
				foreach(var block in function.BasicBlocks) {
					Disassembler.PrintLabel(block);
					foreach(var instruction in block.PhiNodes.Concat(block.Instructions)) {
						StackTransitionPass.WriteStackState(instruction.StackState);
						Console.CursorLeft = 40;
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
