namespace Majiro.Script.Analysis.Source.Nodes.Expressions {
	public class BinaryExpression : Expression {
		public Expression Left;
		public Expression Right;
		public BinaryOperation Operation;

		public BinaryExpression(Expression left, Expression right, BinaryOperation operation, MjoType type) {
			Left = left;
			Right = right;
			Operation = operation;
			Type = type;
		}

		public override TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg) =>
			visitor.Visit(this, arg);
	}

	public enum BinaryOperation {
		None, 

		Addition,
		Subtraction,
		Multiplication,
		Division,
		Modulo,
		ShiftLeft,
		ShiftRight,
		BitwiseAnd,
		BitwiseOr,
		BitwiseXor,
		LogicalAnd,
		LogicalOr,

		CompareLessEqual,
		CompareLessThan,
		CompareGreaterEqual,
		CompareGreaterThan,
		CompareEqual,
		CompareNotEqual,
	}
}
