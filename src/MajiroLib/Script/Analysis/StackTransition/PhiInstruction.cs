using Majiro.Script.Analysis.ControlFlow;

namespace Majiro.Script.Analysis.StackTransition {
	public class PhiInstruction : Instruction {
		public static readonly Opcode PhiOpcode = new Opcode(0xFFFF, "phi", null, "p", null, null);

		public readonly int StackBaseOffset;

		public PhiInstruction(BasicBlock block, int stackBaseOffset) : base(PhiOpcode, block) {
			Block = block;
			StackBaseOffset = stackBaseOffset;
		}
	}
}
