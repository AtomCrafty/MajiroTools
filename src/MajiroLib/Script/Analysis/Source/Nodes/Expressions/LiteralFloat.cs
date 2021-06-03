namespace Majiro.Script.Analysis.Source.Nodes.Expressions {
	public class LiteralFloat : Expression {
		public float Value;

		public LiteralFloat(float value) {
			Type = MjoType.Float;
			Value = value;
		}

		public override TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg) =>
			visitor.Visit(this, arg);
	}
}
