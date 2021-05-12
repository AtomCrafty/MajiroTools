using System.Collections.Generic;
using System.IO;
using Majiro.Script;
using Newtonsoft.Json;
using VToolBase.Core;

namespace Majiro.Project {
	public class MjFunction {
		public uint Hash { get; set; }
		public string Name { get; set; }
		public string DeclaringScript { get; set; }
		public MjoType[] ParameterTypes { get; set; }
	}

	public class MjProject {
		public List<string> ScriptFiles { get; set; } = new List<string>();
		public Dictionary<string, List<MjFunction>> ScriptFunctions { get; set; } = new Dictionary<string, List<MjFunction>>();
		public Dictionary<uint, List<MjFunction>> FunctionMap { get; set; } = new Dictionary<uint, List<MjFunction>>();

		public bool TryGetFunctionName(uint hash, out string name) {
			name = null;
			if(!FunctionMap.TryGetValue(hash, out var functions)) return false;
			if(functions.Count == 0) return false;
			name = functions[0].Name;
			return name != null;
		}

		public void Save(string path) {
			using var writer = File.Open(path, FileMode.Create).NewTextWriter();
			new JsonSerializer { Formatting = Formatting.Indented }.Serialize(writer, this);
		}

		public static MjProject Load(string path) {
			using var reader = File.OpenText(path);
			return new JsonSerializer().Deserialize<MjProject>(new JsonTextReader(reader));
		}
	}
}
