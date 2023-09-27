namespace Majiro.Script.Analysis.Source.Nodes.Statements {
	public class ProcStatement : Statement {

		public ProcStatement() { }

		public override TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg) =>
			visitor.Visit(this, arg);
	}
}
