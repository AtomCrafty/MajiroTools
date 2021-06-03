namespace Majiro.Script.Analysis.Source.Nodes.Expressions {
	public class ArrayAccess : Expression {
		public uint Hash;
		public MjoFlags Flags;
		public Expression[] Indices;

		public ArrayAccess(uint hash, MjoFlags flags, Expression[] indices) {
			Hash = hash;
			Flags = flags;
			Indices = indices;
		}

		public override TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg) =>
			visitor.Visit(this, arg);
	}
}
