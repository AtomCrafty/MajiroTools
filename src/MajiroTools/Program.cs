using System;
using System.IO;
using Majiro.Script;
using Majiro.Script.Analysis.ControlFlow;
using Majiro.Script.Analysis.StackTransition;
using Majiro.Util;
using VToolBase.Cli;
using VToolBase.Cli.Commands;
using VToolBase.Cli.Util;

namespace MajiroTools {

	public class Program {
		public static void Main(string[] args) {
			if(!CommandManager.TryRun(args)) {
				new HelpCommand(CommandParameters.Empty).Execute();
			}
			Output.Flush();
		}

		static void PrintHash(string name) {
			Console.WriteLine($"{Crc.Hash32(name):x8} {name}");
		}

		static void Main2(string[] args) {
			var names = new[] {
				"X_CONTROL",
				"user_voice",
				"user_voice@GLOBAL",
			};

			foreach(string name in names) {
				//PrintHash(name);
			}

			/*
			foreach(var opcode in Opcode.List) {
				Console.Write($"{opcode.Value:X3} {opcode.Mnemonic.PadRight(13)}");
				Console.WriteLine(opcode.Aliases.Any() ? $" (aliases: {string.Join(", ", opcode.Aliases)})" : "");
			}
			//*/

			//*
			const string inPath = "start.mjo";//@"D:\Games\Private\[Jast] Closed Game [v16700]\scenario\0010101.mjo";
			using var reader = new BinaryReader(File.OpenRead(inPath));
			var script = Disassembler.DisassembleScript(reader);

			ControlFlowPass.Analyze(script);
			StackTransitionPass.Analyze(script);

			//Disassembler.PrintScript(script, IColoredWriter.Console);
			using(var fw = new StreamWriter("script1.mjil"))
				Disassembler.PrintScript(script, new StreamColorWriter(fw));

			using var ms = new MemoryStream();
			using(var sw = new StreamWriter(ms, null, -1, true)) {
				Disassembler.PrintScript(script, new StreamColorWriter(sw));
			}

			ms.Position = 0;
			using var sr = new StreamReader(ms);
			var script2 = Assembler.Parse(sr);

			ControlFlowPass.Analyze(script2);
			StackTransitionPass.Analyze(script2);

			using(var fw = new StreamWriter("script2.mjil"))
				Disassembler.PrintScript(script2, new StreamColorWriter(fw));

			using(var fw = File.Open("exported.mjo", FileMode.Create)) {
				Assembler.AssembleScript(script2, new BinaryWriter(fw), true, false);
				fw.Flush(true);
				fw.Close();
			}

			using var reader2 = new BinaryReader(File.OpenRead("exported.mjo"));
			var script3 = Disassembler.DisassembleScript(reader2);

			ControlFlowPass.Analyze(script3);

			using(var fw = new StreamWriter("script3.mjil"))
				Disassembler.PrintScript(script3, new StreamColorWriter(fw));
			Disassembler.PrintScript(script3, IColoredWriter.Console);
		}
	}
}