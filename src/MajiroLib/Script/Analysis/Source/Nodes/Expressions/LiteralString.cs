namespace Majiro.Script.Analysis.Source.Nodes.Expressions {
	public class LiteralString : Expression {
		public string Value;

		public LiteralString(string value) {
			Type = MjoType.String;
			Value = value;
		}

		public override TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg) =>
			visitor.Visit(this, arg);
	}
}
