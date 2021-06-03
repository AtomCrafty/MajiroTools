using System.Collections.Generic;
using Majiro.Script.Analysis.Source.Nodes.Statements;

namespace Majiro.Script.Analysis.Source.Nodes {
	public class FunctionNode : BlockStatement {
		public uint Hash;

		public FunctionNode(uint hash, List<Statement> statements) : base(statements) {
			Hash = hash;
		}

		public override TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg) =>
			visitor.Visit(this, arg);
	}
}
