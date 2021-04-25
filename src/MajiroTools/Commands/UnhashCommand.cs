using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Majiro;
using Majiro.Script;
using VToolBase.Cli;

namespace MajiroTools.Commands {
	public class UnhashCommand : Command {
		public UnhashCommand(CommandParameters parameters) : base(parameters) { }

		public override string Name => "unhash";

		public override string[] Description => new[] {
			"Brute forces possible strings that could result in a specific hash"
		};

		public override (string syntax, string description)[] Usage => new[] {
			("\abhash", "Searches for names matching the given hash"),
			("\abhash prefix", "Searches for a string to replace the single \ab*\a- within the pattern such that the result matches the given hash"),
			("\abhash prefix charset", "Additionally specify the set of valid characters")
		};

		public override (char shorthand, string name, string fallback, string description)[] Flags => new[] {
			('n', "max-length", "10", "The maximum number of characters, excluding prefix and suffix"),
			('m', "min-length", "0", "The minimum number of characters, excluding prefix and suffix"),
			('p', "pattern", "\"*\"", "The name search pattern. Has to contain exactly one \ab*\a-, which represents the unknown part."),
			('c', "charset", "\"a-z_0-9\"", "The set of allowed characters. Allows simple range specifications."),
			('a', "alpha", null, "Overwrites the charset with \ab_\a- and the english alphabet."),

			('s', "syscall", "false", $"Shortcut for \ac--group={Data.SyscallGroup}"),
			('l', "local", "false", "Prepend \ab_\a- to the prefix and append \ab@\a- to the suffix"),
			('g', "group", null, "Prepend \ab$\a- to the prefix and append \ab@<group>\a- to the suffix"),

			('q', "quiet", "false", "Disable user-friendly output"),
			('w', "wait", "false", "Whether to wait after completing the command")
		};

		public override bool Execute() {

			if(Arguments.Length == 0)
				throw new ArgumentException("Not enough arguments");

			if(!uint.TryParse(Arguments[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint targetHash))
				throw new ArgumentException("Invalid hash. Please specify it as a 32 bit hex number");

			bool isSyscall = Data.SyscallHashes.Contains(targetHash);
			int minLength = Parameters.GetInt("min-length", 'm', 0);
			int maxLength = Parameters.GetInt("max-length", 'n', 10);

			string pattern = Parameters.GetString("pattern", 'p', Arguments.Length > 1 ? Arguments[1] : "*");
			string charset = Parameters.GetString("charset", 'c', Arguments.Length > 2 ? Arguments[2] : CrcUnhasher.DefaultCharset);

			string group = Parameters.GetString("group", 'g', null);
			bool isLocal = Parameters.GetBool("local", false);

			if(Parameters.GetBool("alpha", 'a', false)) {
				charset = "_abcdefghijklmnopqrstuvwxyz"; // ordered lexicographically
				charset = "_toiswcbphfmderlnagukvyjqxz"; // ordered by how likely a given character is to be the first in a word (text)
				charset = "_scpdbramtfiehgluwonvjkqyzx"; // ordered by how likely a given character is to be the first in a word (dictionary)
				charset = "_etaoinshrdlcumwfgypbvkjxqz"; // ordered by letter frequency (text)
				charset = "_esuarntolcdugpmhbqfvkwzxjq"; // ordered by letter frequency (dictionary)
			}

			if(Parameters.GetBool("syscall", 's', false))
				group = Data.SyscallGroup;

			if(group != null && isLocal)
				throw new ArgumentException("Can't have a local with a non-null group");

			if(isLocal) {
				pattern = '_' + pattern + '@';
			}

			if(group != null) {
				pattern = '$' + pattern + '@' + group;
			}

			var parts = pattern.Split('*');
			if(parts.Length != 2)
				throw new ArgumentException($"\acPattern must contain exactly one asterisk [\ab{pattern}\ac]");

			string prefix = parts[0];
			string suffix = parts[1];

			Log($"Target:  \ab{targetHash:x8}", true);
			Log($"Length:  \ab{minLength}\a- to \ab{maxLength}", true);
			Log($"Pattern: \ab{prefix}\ac*\ab{suffix}", true);
			Log($"Charset: \ab{charset}", true);
			Log("", true);

			var unhasher = new CrcUnhasher {
				Target = targetHash,
				Prefix = prefix,
				Postfix = suffix,
				Charset = charset,
			};

			ulong totalChecks = 0;
			uint totalMatches = 0;
			uint checksSinceLastUpdate = 0;

			var sw = Stopwatch.StartNew();
			const int updateInterval = 100;

			for(int length = minLength; length <= maxLength; length++) {
				Log($"Searching for matches of length \ab{length}\a-");
				unhasher.Length = length;

				uint lengthMatches = 0;

				foreach(string match in unhasher) {
					checksSinceLastUpdate++;
					if(sw.ElapsedMilliseconds > updateInterval) {
						totalChecks += checksSinceLastUpdate;
						Output.WriteColored($"Total checks: \ab{totalChecks}\a- Hashes per ms: \ab{(float)checksSinceLastUpdate / sw.ElapsedMilliseconds}\r");
						sw.Restart();
						checksSinceLastUpdate = 0;
					}
					if(match == null) continue;
					lengthMatches++;
					Output.ClearLine();
					Output.Write(targetHash.ToString("x8"), isSyscall ? ConsoleColor.Red : ConsoleColor.Blue);
					Output.WriteLine(' ' + match);
				}

				Output.ClearLine();
				if(lengthMatches != 0) {
					Log($"Found \ab{lengthMatches}\a- match{(lengthMatches != 1 ? "es" : "")} of length \ab{length}\a-");
					totalMatches += lengthMatches;
				}
			}

			return true;
		}
	}
}
