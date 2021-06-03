using Majiro.Script.Analysis.Source.Nodes.Expressions;

namespace Majiro.Script.Analysis.Source.Nodes.Statements {
	public class CallStatement : Statement {
		public Call Call;

		public CallStatement(Call call) {
			Call = call;
		}

		public override TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg) =>
			visitor.Visit(this, arg);
	}
}
