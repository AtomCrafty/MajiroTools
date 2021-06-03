namespace Majiro.Script.Analysis.Source.Nodes.Statements {
	public class IfStatement : Statement {
		public Expression Condition;
		public BlockStatement ThenBranch;
		public BlockStatement ElseBranch;

		public IfStatement(Expression condition, BlockStatement thenBranch, BlockStatement elseBranch = null) {
			Condition = condition;
			ThenBranch = thenBranch;
			ElseBranch = elseBranch;
		}

		public override TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg) =>
			visitor.Visit(this, arg);
	}
}
