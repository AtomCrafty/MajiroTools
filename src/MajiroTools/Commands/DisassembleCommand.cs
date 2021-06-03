using System;
using System.IO;
using System.Linq;
using Majiro.Project;
using Majiro.Script;
using Majiro.Script.Analysis.ControlFlow;
using Majiro.Util;
using VToolBase.Cli;
using VToolBase.Core;

namespace MajiroTools.Commands {
	public class DisassembleCommand : Command {
		public DisassembleCommand(CommandParameters parameters) : base(parameters) { }

		public override string Name => "disassemble";

		public override string[] Description => new[] {
			"Disassembles a binary script file (.mjo) into an intermediate representation (.mjil)"
		};

		public override (string syntax, string description)[] Usage => new[] {
			("\absource", "Disassemble the \absource\a- script")
		};

		public override (char shorthand, string name, string fallback, string description)[] Flags => new[] {
			('c', "cfg", "true", "Whether to perform a control flow analysis pass. This is highly recommended and required if you plan to add or remove instructions."),
			('e', "externalize", "true", "Whether to externalize message strings. Valid values are \abfalse\a-, \abtrue\a- and \aball\a-. \abtrue\a- only externalizes message strings, while \aball\a- exports all string literals."),
			('p', "print", "false", "Whether to print the disassembly to the console"),
			('f', "file", "true", "Whether to print the disassembly to a file"),
			('q', "quiet", "false", "Disable user-friendly output"),
			('w', "wait", "false", "Whether to wait after completing the command")
		};

		public override bool Execute() {
			if(Arguments.Length != 1)
				throw new Exception("Not enough arguments");

			string sourcePath = Arguments[0];
			string targetPath = Path.ChangeExtension(sourcePath, ".mjil");

			using var reader = File.OpenRead(sourcePath).NewReader();
			var script = Disassembler.DisassembleScript(reader);

			string projectPath = Parameters.GetString("project", null);
			if(projectPath != null) {
				script.Project = MjProject.Load(projectPath);
			}

			bool cfg = Parameters.GetBool("cfg", 'c', true);
			bool file = Parameters.GetBool("file", 'f', true);
			bool print = Parameters.GetBool("print", 'p', false);
			bool externalize = Parameters.GetBool("externalize", 'e', true);
			bool externalizeAll = Parameters.GetString("externalize", 'e', null) == "all";

			if(externalize && !cfg)
				throw new Exception("\acString externalization can't be used without cfg analysis. Either set \ab--cfg\ac to \abtrue\ac or \ab--externalize\ac to \abfalse\ac!");

			if(cfg) ControlFlowPass.ToControlFlowGraph(script);

			if(print) Disassembler.PrintScript(script, IColoredWriter.Console);

			if(file) {
				if(externalize) {
					script.ExternalizeStrings(externalizeAll);
					if(script.ExternalizedStrings.Any()) {
						string resourcePath = Path.ChangeExtension(sourcePath, ".mjres");
						using var s = File.Open(resourcePath, FileMode.Create);
						Disassembler.WriteResourceTable(script, s);
					}
				}

				using var stream = File.Open(targetPath, FileMode.Create).NewTextWriter();
				Disassembler.PrintScript(script, new StreamColorWriter(stream));
			}

			Wait();
			return true;
		}
	}
}
