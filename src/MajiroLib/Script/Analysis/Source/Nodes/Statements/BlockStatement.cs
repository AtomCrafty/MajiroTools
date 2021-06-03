using System.Collections.Generic;

namespace Majiro.Script.Analysis.Source.Nodes.Statements {
	public class BlockStatement : Statement {
		public List<Statement> Statements;

		public BlockStatement(List<Statement> statements) {
			Statements = statements;
		}

		public override TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg) =>
			visitor.Visit(this, arg);
	}

	public class DestructorStatement : BlockStatement {
		public DestructorStatement(List<Statement> statements) : base(statements) { }

		public override TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg) =>
			visitor.Visit(this, arg);
	}
}
