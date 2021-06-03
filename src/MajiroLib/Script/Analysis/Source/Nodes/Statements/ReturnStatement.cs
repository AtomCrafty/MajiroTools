namespace Majiro.Script.Analysis.Source.Nodes.Statements {
	public class ReturnStatement : Statement {
		public Expression ReturnValue;

		public ReturnStatement(Expression returnValue) {
			ReturnValue = returnValue;
		}

		public override TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg) =>
			visitor.Visit(this, arg);
	}
}
