using Majiro.Script.Analysis.Source.Nodes.Expressions;

namespace Majiro.Script.Analysis.Source.Nodes.Statements {
	public class ArrayAssignment : Assignment {
		public Expression[] Indices;

		public ArrayAssignment(Expression value, Expression[] indices, uint hash, MjoFlags flags, MjoType type, BinaryOperation operation)
		: base(value, hash, flags, type, operation) {
			Indices = indices;
		}

		public override TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg) =>
			visitor.Visit(this, arg);
	}
}