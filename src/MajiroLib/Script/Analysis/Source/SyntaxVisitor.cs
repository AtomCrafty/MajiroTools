using System;
using Majiro.Script.Analysis.Source.Nodes;
using Majiro.Script.Analysis.Source.Nodes.Expressions;
using Majiro.Script.Analysis.Source.Nodes.Statements;

namespace Majiro.Script.Analysis.Source {
	public interface ISyntaxVisitor<in TArg, out TRes> {
		TRes Visit(SyntaxNode node, TArg arg);
		TRes Visit(FunctionNode node, TArg arg);

		// expressions
		TRes Visit(Expression node, TArg arg);
		TRes Visit(BinaryExpression node, TArg arg);
		TRes Visit(UnaryExpression node, TArg arg);
		TRes Visit(Cast node, TArg arg);
		TRes Visit(Identifier node, TArg arg);
		TRes Visit(ArrayAccess node, TArg arg);
		TRes Visit(LiteralInt node, TArg arg);
		TRes Visit(LiteralFloat node, TArg arg);
		TRes Visit(LiteralString node, TArg arg);
		TRes Visit(Call node, TArg arg);

		// statements
		TRes Visit(Statement node, TArg arg);
		TRes Visit(Assignment node, TArg arg);
		TRes Visit(ArrayAssignment node, TArg arg);
		TRes Visit(BlockStatement node, TArg arg);
		TRes Visit(CallStatement node, TArg arg);
		TRes Visit(DestructorStatement node, TArg arg);
		TRes Visit(ReturnStatement node, TArg arg);
		TRes Visit(IfStatement node, TArg arg);
		TRes Visit(TextStatement node, TArg arg);
		TRes Visit(CtrlStatement node, TArg arg);
		TRes Visit(ProcStatement node, TArg arg);
	}

	public abstract class SyntaxVisitor<TArg, TRes> : ISyntaxVisitor<TArg, TRes> {
		public virtual TRes Visit(SyntaxNode node, TArg arg) => throw new NotImplementedException(GetType().Name + " does not implement a visitor for type " + node.GetType().Name);
		public virtual TRes Visit(FunctionNode node, TArg arg) => Visit((SyntaxNode)node, arg);

		// expressions
		public virtual TRes Visit(Expression node, TArg arg) => Visit((SyntaxNode)node, arg);
		public virtual TRes Visit(BinaryExpression node, TArg arg) => Visit((Expression)node, arg);
		public virtual TRes Visit(UnaryExpression node, TArg arg) => Visit((Expression)node, arg);
		public virtual TRes Visit(Cast node, TArg arg) => Visit((Expression)node, arg);
		public virtual TRes Visit(Identifier node, TArg arg) => Visit((Expression)node, arg);
		public virtual TRes Visit(ArrayAccess node, TArg arg) => Visit((Expression)node, arg);
		public virtual TRes Visit(LiteralInt node, TArg arg) => Visit((Expression)node, arg);
		public virtual TRes Visit(LiteralFloat node, TArg arg) => Visit((Expression)node, arg);
		public virtual TRes Visit(LiteralString node, TArg arg) => Visit((Expression)node, arg);
		public virtual TRes Visit(Call node, TArg arg) => Visit((Expression)node, arg);

		// statements
		public virtual TRes Visit(Statement node, TArg arg) => Visit((SyntaxNode)node, arg);
		public virtual TRes Visit(Assignment node, TArg arg) => Visit((Statement)node, arg);
		public virtual TRes Visit(ArrayAssignment node, TArg arg) => Visit((Assignment)node, arg);
		public virtual TRes Visit(BlockStatement node, TArg arg) => Visit((Statement)node, arg);
		public virtual TRes Visit(CallStatement node, TArg arg) => Visit((Statement)node, arg);
		public virtual TRes Visit(DestructorStatement node, TArg arg) => Visit((BlockStatement)node, arg);
		public virtual TRes Visit(ReturnStatement node, TArg arg) => Visit((Statement)node, arg);
		public virtual TRes Visit(IfStatement node, TArg arg) => Visit((Statement)node, arg);
		public virtual TRes Visit(TextStatement node, TArg arg) => Visit((Statement)node, arg);
		public virtual TRes Visit(CtrlStatement node, TArg arg) => Visit((Statement)node, arg);
		public virtual TRes Visit(ProcStatement node, TArg arg) => Visit((Statement)node, arg);
	}
}
