using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using Majiro.Script;
using Majiro.Script.Analysis.ControlFlow;
using Majiro.Util;
using VToolBase.Cli;
using VToolBase.Core;

namespace MajiroTools.Commands {
	public class TranslateCommand : Command {
		public TranslateCommand(CommandParameters parameters) : base(parameters) { }

		public override string Name => "translate";

		public override string[] Description => new[] {
			"Translates multiple script files"
		};

		public override (string syntax, string description)[] Usage => new[] {
			("\absheet", "Inserts translation strings from \absheet\a- into scripts in the current folder"),
			("\absheet infolder", "Inserts translation strings from \absheet\a- into scripts in the \abinfolder\a-"),
			("\absheet infolder outfolder", "Places the translated scripts in a different directory")
		};

		public override (char shorthand, string name, string fallback, string description)[] Flags => new[] {
			('l', "line-width", "80", "The number of characters per line used for word wrapping"),
			('q', "quiet", "false", "Disable user-friendly output"),
			('w', "wait", "false", "Whether to wait after completing the command")
		};

		class TranslationLine {
			public string LocalId;
			public string ScriptName;
			public string OriginalSpeaker;
			public string OriginalText;
			public string TranslatedSpeaker;
			public string TranslatedText;
		}

		public override bool Execute() {
			if(Arguments.Length < 1)
				throw new Exception("Not enough arguments");

			string translationPath = Arguments[0];
			string scriptInPath = Arguments.Length > 1 ? Arguments[1] : ".";
			string scriptOutPath = Arguments.Length > 2 ? Arguments[2] : scriptInPath;

			var translation = new Dictionary<string, List<TranslationLine>>();
			ReadTranslation(translationPath, translation);

			int charsPerLine = Parameters.GetInt("line-width", 'l', 80);
			string wrapPattern = $@"(?![^\n]{{1,{charsPerLine}}}$)([^\n]{{1,{charsPerLine}}})\s";
			foreach(var translationLine in translation.Values.SelectMany(x => x)) {
				translationLine.TranslatedText = Regex.Replace(translationLine.TranslatedText, wrapPattern, "$1\n").Replace('\u2013', '-').Replace('\u2014', '-').Replace('\u2015', '-');
				//Console.WriteLine(translationLine.TranslatedText + "\n");
			}

			foreach(string file in Directory.GetFiles(scriptInPath, "*.mjo")) {
				string scriptName = Path.GetFileNameWithoutExtension(file).ToLower();
				string outFile = Path.Combine(scriptOutPath, Path.GetRelativePath(scriptInPath, file));

				if(!translation.ContainsKey(scriptName)) {
					if(file != outFile)
						File.Copy(file, outFile, true);
					continue;
				}

				var script = Disassembler.DisassembleFromFile(file);

				script.ToControlFlowGraph();
				script.ExternalizeStrings(false);

				var strings = script.ExternalizedStrings;
				var tlstrings = translation[scriptName];

				Output.WriteLine(scriptName, ConsoleColor.Yellow);

				foreach(var tlline in tlstrings) {
					//Output.WriteLine($"{tlline.LocalId}: {strings[tlline.LocalId]}", ConsoleColor.Red);
					//Output.WriteLine($"{tlline.LocalId}: {tlline.TranslatedText}", ConsoleColor.Green);
					//Output.WriteLine();
					//Output.Flush();
					if(!strings.ContainsKey(tlline.LocalId)) {
						Output.WriteLine($"Unable to insert {tlline.LocalId}: " + tlline.TranslatedText, ConsoleColor.Red);
					}
				}

				if(strings.Count > tlstrings.Count) {
					foreach(var tlline in tlstrings) {
						Output.WriteLine($"{tlline.LocalId}: {strings[tlline.LocalId]}", ConsoleColor.Red);
						Output.WriteLine(
							$"{tlline.LocalId}: {(string.IsNullOrEmpty(tlline.TranslatedSpeaker) ? "" : $"{tlline.TranslatedSpeaker}「「") + tlline.TranslatedText}",
							ConsoleColor.Green);
						Output.WriteLine();
						Output.Flush();
					}
				}

				if(tlstrings.Count < strings.Count) {
					throw new Exception($"{scriptName}: Found only {tlstrings.Count} translation entries, but the script contains {strings.Count} lines");
				}

				foreach(var translationLine in tlstrings) {
					if(!strings.ContainsKey(translationLine.LocalId)) continue;
					strings[translationLine.LocalId] = translationLine.TranslatedSpeaker + "「「" + translationLine.TranslatedText;
				}

				script.InternalizeStrings();
				script.ToInstructionList();

				Assembler.AssembleToFile(script, outFile);
			}

			Wait();
			return true;
		}

		private void ReadTranslation(string translationPath, Dictionary<string, List<TranslationLine>> translation) {
			using var reader = new CsvReader(File.OpenText(translationPath), CultureInfo.InvariantCulture);
			reader.Read();
			reader.ReadHeader();
			while(reader.Read()) {
				string scriptName = Path.GetFileNameWithoutExtension(reader.GetField("Filename"))?.ToLower() ?? "";
				if(!translation.TryGetValue(scriptName, out var list))
					translation[scriptName] = list = new List<TranslationLine>();
				string translated = reader.GetField("Final");
				string speaker = reader.GetField("Name");
				if(string.IsNullOrWhiteSpace(translated)) continue;
				string id = "L" + (list.Count + 1);
				list.Add(new TranslationLine {
					LocalId = id,
					ScriptName = scriptName,
					TranslatedSpeaker = speaker,
					TranslatedText = translated
				});
			}
		}
	}
}
