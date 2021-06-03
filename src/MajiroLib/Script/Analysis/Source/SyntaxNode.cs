using System.Text;
using Majiro.Util;

namespace Majiro.Script.Analysis.Source {
	public abstract class SyntaxNode {
		public abstract TRes Accept<TArg, TRes>(ISyntaxVisitor<TArg, TRes> visitor, TArg arg);

		public override string ToString() {
			var sb = new StringBuilder();
			Accept(new DumpVisitor(), new StringBuilderColorWriter(sb));
			return sb.ToString();
		}
	}
}
