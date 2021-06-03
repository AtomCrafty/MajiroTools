namespace Majiro.Script.Analysis.Source.Nodes.Expressions {
	public class UnaryExpression : Expression {
		public Expression Operand;
		public UnaryOperation Operation;

		public UnaryExpression(Expression operand, UnaryOperation operation, MjoType type) {
			Operand = operand;
			Operation = operation;
			Type = type;
		}

		public override TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg) =>
			visitor.Visit(this, arg);
	}

	public enum UnaryOperation {
		LogicalNot,
		BitwiseNot,
		UnaryMinus,
		UnaryPlus
	}
}
