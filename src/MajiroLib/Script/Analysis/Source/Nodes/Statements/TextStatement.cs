namespace Majiro.Script.Analysis.Source.Nodes.Statements {
	public class TextStatement : Statement {
		public string Text;

		public TextStatement(string text) {
			Text = text;
		}

		public override TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg) =>
			visitor.Visit(this, arg);
	}
}
