using System.Globalization;
using System.Linq;
using MajiroTools.Commands;
using VToolBase.Cli;
using VToolBase.Cli.Commands;

namespace MajiroTools {

	public class Program {
		public static void Main(string[] args) {
			CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
			HelpCommand.AppName = "MajiroTool Cli";
			HelpCommand.LibName = "MajiroLib";

			CommandManager.Register("hash", parameters => new HashCommand(parameters));
			CommandManager.Register("unhash", parameters => new UnhashCommand(parameters));
			CommandManager.Register("disassemble", parameters => new DisassembleCommand(parameters));
			CommandManager.Register("decompile", parameters => new DecompileCommand(parameters));
			CommandManager.Register("assemble", parameters => new AssembleCommand(parameters));
			CommandManager.Register("translate", parameters => new TranslateCommand(parameters));
			CommandManager.Register("project", parameters => new ProjectCommand(parameters));
			CommandManager.Register("find", parameters => new FindCommand(parameters));

			if(!CommandManager.TryRun(args)) {
				if(args.Length == 0)
					new HelpCommand(CommandParameters.Empty).Execute();
				else
					new AutoCommand(CommandParameters.ParseArguments(args.Prepend("auto"))).Execute();
			}
			Output.Terminate();
		}
	}
}