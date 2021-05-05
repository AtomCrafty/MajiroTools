using System;
using System.Collections.Generic;
using System.IO;
using Majiro;
using Majiro.Project;
using Majiro.Script;
using Majiro.Script.Analysis.ControlFlow;
using Newtonsoft.Json;
using VToolBase.Cli;
using VToolBase.Core;

namespace MajiroTools.Commands {
	public class ProjectCommand : Command {
		public ProjectCommand(CommandParameters parameters) : base(parameters) { }

		public override string Name => "project";

		public override string[] Description => new[] {
			"Analyzes a game directory"
		};

		public override (string syntax, string description)[] Usage => new[] {
			("", "Analyzes the current directory"),
			("\abdirectory", "Specifies the path to the game directory")
		};

		public override (char shorthand, string name, string fallback, string description)[] Flags => new[] {
			('q', "quiet", "false", "Disable user-friendly output"),
			('w', "wait", "false", "Whether to wait after completing the command")
		};

		// TODO this is very basic atm
		public override bool Execute() {

			string projectRoot = Path.GetFullPath(Arguments.Length > 0 ? Arguments[0] : ".");
			string projectFile = Path.Combine(projectRoot, "project.json");

			var project = new MjProject();

			foreach(string file in Directory.GetFiles(projectRoot, "*.mjo", SearchOption.AllDirectories)) {
				string relativePath = Path.GetRelativePath(projectRoot, file);
				string relativeName = Path.ChangeExtension(relativePath, null).Replace('\\', '/');

				try {
					using var reader = File.OpenRead(file).NewReader();
					var script = Disassembler.DisassembleScript(reader);

					ControlFlowPass.ToControlFlowGraph(script);
					//StackTransitionPass.Analyze(script);

					var functions = new List<MjFunction>();

					foreach(var scriptFunction in script.Functions) {
						uint hash = scriptFunction.NameHash;
						Data.KnownFunctionNamesByHash.TryGetValue(hash, out string name);

						var function = new MjFunction {
							Hash = hash,
							Name = name,
							DeclaringScript = relativeName
							//ParameterTypes = scriptFunction.ParameterTypes
						};

						functions.Add(function);
						if(!project.FunctionMap.ContainsKey(hash))
							project.FunctionMap[hash] = new List<MjFunction>();
						project.FunctionMap[hash].Add(function);
					}

					project.ScriptFiles.Add(relativeName);
					project.ScriptFunctions[relativeName] = functions;
				}
				catch(Exception e) {
					Output.WriteLineColored($"Unable to analyze script \ab{relativeName}\a-:\n\ac{e.Message}");
				}
			}

			project.Save(projectFile);

			Wait();
			return true;
		}
	}
}
