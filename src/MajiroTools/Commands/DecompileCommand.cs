using System;
using System.IO;
using Majiro.Project;
using Majiro.Script;
using Majiro.Script.Analysis.ControlFlow;
using Majiro.Script.Analysis.Source;
using Majiro.Script.Analysis.StackTransition;
using Majiro.Util;
using VToolBase.Cli;
using VToolBase.Core;

namespace MajiroTools.Commands {
	public class DecompileCommand : Command {
		public DecompileCommand(CommandParameters parameters) : base(parameters) { }

		public override string Name => "decompile";

		public override string[] Description => new[] {
			"Decompile a binary script file (.mjo) into a source file (.mjs)"
		};

		public override (string syntax, string description)[] Usage => new[] {
			("\absource", "Decompile the \absource\a- script")
		};

		public override (char shorthand, string name, string fallback, string description)[] Flags => new[] {
			('p', "print", "false", "Whether to print the decompilation to the console"),
			('f', "file", "true", "Whether to print the decompilation to a file"),
			('q', "quiet", "false", "Disable user-friendly output"),
			('w', "wait", "false", "Whether to wait after completing the command")
		};

		public override bool Execute() {
			if(Arguments.Length != 1)
				throw new Exception("Not enough arguments");

			string sourcePath = Arguments[0];
			string targetPath = Path.ChangeExtension(sourcePath, ".mjs");

			using var reader = File.OpenRead(sourcePath).NewReader();
			var script = Disassembler.DisassembleScript(reader);

			string projectPath = Parameters.GetString("project", null);
			if(projectPath != null) {
				script.Project = MjProject.Load(projectPath);
			}

			bool file = Parameters.GetBool("file", 'f', true);
			bool print = Parameters.GetBool("print", 'p', false);

			ControlFlowPass.ToControlFlowGraph(script);
			StackTransitionPass.ToSsaGraph(script);
			DecompilerPass.ToSource(script);

			if(print) Disassembler.PrintScript(script, IColoredWriter.Console);

			if(file) {
				using var stream = File.Open(targetPath, FileMode.Create).NewTextWriter();
				Disassembler.PrintScript(script, new StreamColorWriter(stream));
			}

			Wait();
			return true;
		}
	}
}
