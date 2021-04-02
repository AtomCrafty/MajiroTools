using System.Collections.Generic;

namespace Majiro.Script.Analysis.StackTransition {
	public struct StackValue {

		public StackValueCategory Category;
		public MjoType Type;

		public Instruction Producer;
		public List<Instruction> Consumers;
	}

	public enum StackValueCategory {
		Argument,
		Local,
		Temp
	}
}
