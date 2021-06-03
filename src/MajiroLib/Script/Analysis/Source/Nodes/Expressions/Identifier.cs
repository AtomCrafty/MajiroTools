namespace Majiro.Script.Analysis.Source.Nodes.Expressions {
	public class Identifier : Expression {
		public uint Hash;
		public MjoFlags Flags;

		public Identifier(uint hash, MjoFlags flags) {
			Hash = hash;
			Flags = flags;
		}

		public override TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg) =>
			visitor.Visit(this, arg);
	}
}
