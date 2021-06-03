using Majiro.Script.Analysis.Source.Nodes.Expressions;

namespace Majiro.Script.Analysis.Source.Nodes.Statements {
	public class Assignment : Statement {
		public Expression Value;
		public uint Hash;
		public MjoFlags Flags;
		public MjoType Type;
		public BinaryOperation Operation;

		public Assignment(Expression value, uint hash, MjoFlags flags, MjoType type, BinaryOperation operation) {
			Value = value;
			Hash = hash;
			Flags = flags;
			Type = type;
			Operation = operation;
		}

		public override TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg) =>
			visitor.Visit(this, arg);
	}
}
