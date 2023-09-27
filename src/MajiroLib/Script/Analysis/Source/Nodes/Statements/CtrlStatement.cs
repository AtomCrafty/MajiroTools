namespace Majiro.Script.Analysis.Source.Nodes.Statements {
	public class CtrlStatement : Statement {
		public string ControlCode;
		public Expression[] Operands;

		public CtrlStatement(string controlCode, Expression[] operands) {
			ControlCode = controlCode;
			Operands = operands;
		}

		public override TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg) =>
			visitor.Visit(this, arg);
	}
}
