namespace Majiro.Script.Analysis.Source.Nodes.Expressions {
	public class Call : Expression {
		public uint Hash;
		public bool IsSyscall;
		public Expression[] Arguments;

		public Call(uint hash, bool isSyscall, Expression[] arguments) {
			Hash = hash;
			IsSyscall = isSyscall;
			Arguments = arguments;
		}

		public override TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg) =>
			visitor.Visit(this, arg);
	}
}
