namespace Majiro.Script.Analysis.Source.Nodes.Expressions {
	public class LiteralInt : Expression {
		public int Value;

		public LiteralInt(int value) {
			Type = MjoType.Int;
			Value = value;
		}

		public override TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg) =>
			visitor.Visit(this, arg);
	}
}
