using System;
using System.Collections.Generic;
using System.IO;
using Majiro.Script;
using Majiro.Script.Analysis.ControlFlow;
using Majiro.Util;
using VToolBase.Cli;
using VToolBase.Core;

namespace MajiroTools.Commands {
	public class AssembleCommand : Command {
		public AssembleCommand(CommandParameters parameters) : base(parameters) { }

		public override string Name => "assemble";

		public override string[] Description => new[] {
			"Assembles an assembly file (.mjil) into a binary script (.mjo)"
		};

		public override (string syntax, string description)[] Usage => new[] {
			("\absource", "Assemble the \absource\a- script")
		};

		public override (char shorthand, string name, string fallback, string description)[] Flags => new[] {
			('e', "externalized", "true", "Whether to read strings from an external .mjres file."),
			('c', "encrypt", "true", "Whether to encrypt the script file."),
			('q', "quiet", "false", "Disable user-friendly output"),
			('w', "wait", "false", "Whether to wait after completing the command")
		};

		public override bool Execute() {
			if(Arguments.Length != 1)
				throw new Exception("Not enough arguments");

			string sourcePath = Arguments[0];
			string targetPath = Path.ChangeExtension(sourcePath, ".mjo");

			using var reader = File.OpenText(sourcePath);
			var script = Assembler.Parse(reader);

			if(Parameters.GetBool("externalized", 'e', true)) {
				string resourcePath = Path.ChangeExtension(sourcePath, ".mjres");
				if(File.Exists(resourcePath)) {
					using var s = File.OpenRead(resourcePath);
					script.ExternalizedStrings = Assembler.ReadResourceTable(s);
				}
			}

			using var writer = File.Open(targetPath, FileMode.Create).NewWriter();
			Assembler.AssembleScript(script, writer, Parameters.GetBool("encrypt", 'c', true));

			Wait();
			return true;
		}
	}
}
