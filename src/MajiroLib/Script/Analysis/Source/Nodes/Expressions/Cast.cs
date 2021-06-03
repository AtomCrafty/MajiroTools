namespace Majiro.Script.Analysis.Source.Nodes.Expressions {
	public class Cast : Expression {
		public Expression Value;
		public MjoType TargetType;

		public Cast(Expression value, MjoType targetType) {
			Value = value;
			TargetType = targetType;
		}

		public override TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg) =>
			visitor.Visit(this, arg);
	}
}
