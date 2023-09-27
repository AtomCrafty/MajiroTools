using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Majiro.Script;
using Majiro.Script.Analysis.ControlFlow;
using VToolBase.Cli;
using VToolBase.Core;

namespace MajiroTools.Commands {
	public class FindCommand : Command {
		public FindCommand(CommandParameters parameters) : base(parameters) { }

		public override string Name => "find";

		public override string[] Description => new[] {
			"Find references to a function or variable hash."
		};

		public override (string syntax, string description)[] Usage => new[] {
			("\abhash", "Searches for references to \abhash\a- in all .mjo files in the current directory"),
			("\abhash script.mjo", "Searches for references to \abhash\a- in the file \abscript.mjo\a-"),
			("\abhash folder", "Searches for references to \abhash\a- in all .mjo files in the \abfolder\a-")
		};

		public override (char shorthand, string name, string fallback, string description)[] Flags => new[] {
			('q', "quiet", "false", "Disable user-friendly output"),
			('w', "wait", "false", "Whether to wait after completing the command")
		};

		public override bool Execute() {
			if(Arguments.Length == 0) {
				return CommandManager.TryRun("help");
			}

			var hashStrings = Arguments[0].Split(',');
			var hashes = new uint[hashStrings.Length];
			for(int i = 0; i < hashStrings.Length; i++) {
				if(!uint.TryParse(hashStrings[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hashes[i])) {
					Error($"Invalid hash string: {hashStrings[i]}");
					return true;
				}
			}

			string[] files;
			string searchPath;
			if(Arguments.Length == 1) {
				Log("Searching cwd for script files", true);
				searchPath = Directory.GetCurrentDirectory();
				files = Directory.GetFiles(searchPath, "*.mjo", SearchOption.AllDirectories);
			}
			else {
				string path = Arguments[1];
				if(File.Exists(path)) {
					Log("Detected as single file", true);
					searchPath = Path.GetDirectoryName(path);
					files = new[] { path };
				}
				else if(Directory.Exists(path)) {
					Log("Detected as script folder", true);
					searchPath = path;
					files = Directory.GetFiles(path, "*.mjo", SearchOption.AllDirectories);
				}
				else {
					throw new FileNotFoundException("Path is neither a valid file nor a valid directory", path);
				}
			}

			if(!files.Any()) {
				Log("No script files found");
				return true;
			}

			foreach(string file in files) {
				string scriptName = string.IsNullOrEmpty(searchPath) ? file : Path.GetRelativePath(searchPath, file);
				try {
					using var reader = File.OpenRead(file).NewReader();
					var script = Disassembler.DisassembleScript(reader);

					ControlFlowPass.ToControlFlowGraph(script);

					// analysis
					bool interestingScript = false;
					void MarkInterestingScript() {
						if(interestingScript) return;
						interestingScript = true;
						Output.WriteLineColored($"Script \ab{scriptName}\a-:");
					}

					foreach(var function in script.Functions) {
						bool interestingFunction = false;
						void MarkInterestingFunction() {
							if(interestingFunction) return;
							interestingFunction = true;
							MarkInterestingScript();
							if(hashes.Contains(function.NameHash)) {
								Output.WriteLineColored($" Function \ae{function.NameHash:x8}\a-:");
							}
							else {
								Output.WriteLine($" Function {function.NameHash:x8}:");
							}
						}

						if(hashes.Contains(function.NameHash)) {
							MarkInterestingFunction();
							Output.WriteLineColored("  Declaration matching search hash");
						}

						foreach(var instruction in function.Instructions) {
							if(instruction.Opcode.Encoding.Contains('h') && hashes.Contains(instruction.Hash)) {
								uint hash = instruction.Hash;
								MarkInterestingFunction();
								if(instruction.IsSysCall) {
									Output.WriteLineColored($"  Syscall \ae{hash:x8}\a-");
								}
								else if(instruction.IsCall) {
									Output.WriteLineColored($"  Call to function \ae{hash:x8}\a-");
								}
								else if(instruction.IsLoad) {
									Output.WriteLineColored($"  Loading variable \ae{hash:x8}\a-");
								}
								else if(instruction.IsStore) {
									Output.WriteLineColored($"  Storing variable \ae{hash:x8}\a-");
								}
							}
							else if(instruction.Opcode.Value == (ushort)OpcodeValues.Ldc_I &&
									hashes.Contains((uint)instruction.IntValue)) {
								MarkInterestingFunction();
								Output.WriteLineColored($"  Loading constant value 0x\ae{(uint)instruction.IntValue:x8}\a-");
							}
						}
					}
				}
				catch(Exception e) {
					Output.WriteLineColored($"Failed to process script {scriptName}:");
					Output.WriteException(e);
				}
			}

			Wait();
			return true;
		}
	}
}
