using System;
using VToolBase.Cli;

namespace MajiroTools.Commands {
	public class AutoCommand : Command {
		public AutoCommand(CommandParameters parameters) : base(parameters) { }

		public override string Name => "auto";

		public override string[] Description => new[] {
			"Automatically decide how to process a file"
		};

		public override (string syntax, string description)[] Usage => new[] {
			("\abscript.mjo", "Prints the disassembled script to the console")
		};

		public override (char shorthand, string name, string fallback, string description)[] Flags => new[] {
			('q', "quiet", "false", "Disable user-friendly output"),
			('w', "wait", "false", "Whether to wait after completing the command")
		};

		public override bool Execute() {
			if(Arguments.Length == 0) {
				return CommandManager.TryRun("help");
			}

			string file = Arguments[0];
			if(file.EndsWith(".mjo", StringComparison.InvariantCultureIgnoreCase)) {
				Output.WriteLine("Detected as mjo script. Disassembling.");
				return CommandManager.TryRun("disassemble", "--print", "--file=false", file);
			}

			Output.WriteLineColored("\acError\a-: AutoCommand was unable to determine an appropriate action");

			return false;
		}
	}
}
