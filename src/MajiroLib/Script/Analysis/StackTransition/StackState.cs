using System.Collections.Generic;
using System.Linq;

namespace Majiro.Script.Analysis.StackTransition {

	public class StackState {
		public readonly List<StackValue> Values;

		public int StackBase;
		public int StackTop => Values.Count;

		public int ArgCount => StackBase;
		public int LocalCount;
		public int TempCount => StackTop - ArgCount - LocalCount;

		public StackState(List<StackValue> values = null, int stackBase = 0) {
			Values = values ?? new List<StackValue>();
			StackBase = stackBase;
		}

		public void Push(StackValue value) => Values.Add(value);

		public StackValue Pop() {
			var value = Values.Last();
			Values.RemoveAt(Values.Count - 1);
			return value;
		}

		public StackValue this[int offset] {
			get => Values[StackBase + offset];
			set => Values[StackBase + offset] = value;
		}

		public StackState Clone() {
			return new StackState(Values.ToList(), StackBase) { LocalCount = LocalCount };
		}
	}
}