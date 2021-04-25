using System;
using System.Linq;
using Majiro;
using Majiro.Script;
using VToolBase.Cli;

namespace MajiroTools.Commands {
	public class HashCommand : Command {
		public HashCommand(CommandParameters parameters) : base(parameters) { }

		public override string Name => "hash";

		public override string[] Description => new[] {
			"Hashes a string using CRC32"
		};

		public override (string syntax, string description)[] Usage => new[] {
			("\abstring", "The string to hash")
		};

		public override (char shorthand, string name, string fallback, string description)[] Flags => new[] {
			('i', "interactive", "false", "Whether to read additional names from the console"),
			('f', "function", "false", "Whether to prepend a $ sign if it is missing"),
			('s', "syscall", "true", "Whether to append @MAJIRO_INTER if no namespace is specified"),
			('q', "quiet", "false", "Disable user-friendly output"),
			('w', "wait", "false", "Whether to wait after completing the command")
		};

		public override bool Execute() {

			bool prependDollar = Parameters.GetBool("function", 'f', false);
			bool appendSyscallNamespace = Parameters.GetBool("syscall", 's', true);

			void PrintHash(string str) {
				uint hash = Crc.Hash32(str);
				bool isSyscall = Data.SyscallHashes.Contains(hash);
				Console.ForegroundColor = isSyscall ? ConsoleColor.Red : ConsoleColor.Blue;
				Console.Write(hash.ToString("x8"));
				Console.ResetColor();
				Console.Write(' ');
				Console.WriteLine(str);
			}

			string Process(string name) {
				if(prependDollar && !name.StartsWith('$'))
					name = '$' + name;

				if(appendSyscallNamespace && !name.Contains('@'))
					name += "@MAJIRO_INTER";

				return name;
			}

			foreach(string argument in Arguments) {
				PrintHash(Process(argument)); 
			}

			bool interactive = Parameters.GetBool("interactive", 'i', false);
			if(interactive) {
				while(true) {
					Console.ForegroundColor = ConsoleColor.Blue;
					Console.Write("???????? ");
					Console.ForegroundColor = ConsoleColor.Green;
					string input = Console.ReadLine();
					Console.CursorTop--;
					Console.Write(new string(' ', Console.WindowWidth));
					Console.CursorLeft = 0;

					if(string.IsNullOrWhiteSpace(input))
						break;

					PrintHash(Process(input));
				}
			}

			return true;
		}
	}
}
