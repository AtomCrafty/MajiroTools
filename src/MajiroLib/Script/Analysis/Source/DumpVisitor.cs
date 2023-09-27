using System;
using System.Globalization;
using Majiro.Script.Analysis.Source.Nodes;
using Majiro.Script.Analysis.Source.Nodes.Expressions;
using Majiro.Script.Analysis.Source.Nodes.Statements;
using Majiro.Util;
using VToolBase.Core;

namespace Majiro.Script.Analysis.Source {
	public class DumpVisitor : SyntaxVisitor<IColoredWriter, bool> {

		private string _indent = "";

		private void WriteIdentifier(uint hash, MjoFlags flags, IColoredWriter writer) {
			writer.ForegroundColor = ConsoleColor.Red;
			char scope = flags.Scope() switch {
				MjoScope.Persistent => '#',
				MjoScope.SaveFile => '@',
				MjoScope.Thread => '%',
				MjoScope.Local => '_',
				_ => throw new ArgumentOutOfRangeException()
			};
			string type = flags.Type() switch {
				MjoType.Int => "",
				MjoType.Float => "%",
				MjoType.String => "$",
				MjoType.IntArray => "#",
				MjoType.FloatArray => "%#",
				MjoType.StringArray => "$#",
				MjoType.Unknown => "?",
				_ => throw new ArgumentOutOfRangeException()
			};
			writer.Write($"{scope}{{{hash:x8}}}{type}");
		}

		private void WriteLoadPrefix(MjoFlags flags, IColoredWriter writer) {
			WritePunctuation(flags.InvertMode() switch {
				MjoInvertMode.Numeric => "-",
				MjoInvertMode.Boolean => "!",
				MjoInvertMode.Bitwise => "~",
				_ => ""
			}, writer);

			WritePunctuation(flags.Modifier() switch {
				MjoModifier.PreIncrement => "++",
				MjoModifier.PreDecrement => "--",
				_ => ""
			}, writer);
		}

		private void WriteLoadSuffix(MjoFlags flags, IColoredWriter writer) {
			WritePunctuation(flags.Modifier() switch {
				MjoModifier.PostIncrement => "++",
				MjoModifier.PostDecrement => "--",
				_ => ""
			}, writer);
		}

		private void WritePunctuation(string text, IColoredWriter writer) {
			writer.ResetColor();
			writer.Write(text);
		}

		private void WriteNewline(IColoredWriter writer) {
			writer.WriteLine();
			writer.Write(_indent);
		}

		private void Indent() => _indent += "  ";

		private void Unindent() => _indent = _indent[..^2];

		private static string GetBinaryOperator(BinaryOperation op) {
			return op switch {
				BinaryOperation.None => "",
				BinaryOperation.Addition => "+",
				BinaryOperation.Subtraction => "-",
				BinaryOperation.Multiplication => "*",
				BinaryOperation.Division => "/",
				BinaryOperation.Modulo => "%",
				BinaryOperation.ShiftLeft => "<<",
				BinaryOperation.ShiftRight => ">>",
				BinaryOperation.BitwiseAnd => "&",
				BinaryOperation.BitwiseOr => "|",
				BinaryOperation.BitwiseXor => "^",
				BinaryOperation.LogicalAnd => "&&",
				BinaryOperation.LogicalOr => "||",
				BinaryOperation.CompareLessEqual => "<=",
				BinaryOperation.CompareLessThan => "<",
				BinaryOperation.CompareGreaterEqual => ">=",
				BinaryOperation.CompareGreaterThan => ">",
				BinaryOperation.CompareEqual => "==",
				BinaryOperation.CompareNotEqual => "!=",
				_ => throw new ArgumentOutOfRangeException()
			};
		}

		public override bool Visit(FunctionNode node, IColoredWriter writer) {
			writer.ForegroundColor = ConsoleColor.Blue;
			writer.Write($"func ${node.Hash:x8}");
			WritePunctuation("(", writer);
			writer.ForegroundColor = ConsoleColor.DarkGray;
			writer.Write("..."); // todo
			WritePunctuation(") {", writer); // todo entrypoint
			Indent();
			foreach(var statement in node.Statements) {
				WriteNewline(writer);
				statement.Accept(this, writer);
			}
			Unindent();
			WriteNewline(writer);
			WritePunctuation("}", writer);
			WriteNewline(writer);
			return false;
		}

		public override bool Visit(BinaryExpression node, IColoredWriter writer) {
			string op = GetBinaryOperator(node.Operation);

			WritePunctuation("(", writer); // todo only parenthesize when necessary
			node.Left.Accept(this, writer);
			writer.ResetColor();
			writer.Write($" {op} ");
			node.Right.Accept(this, writer);
			WritePunctuation(")", writer);
			return false;
		}

		public override bool Visit(UnaryExpression node, IColoredWriter writer) {
			string op = node.Operation switch {
				UnaryOperation.LogicalNot => "!",
				UnaryOperation.BitwiseNot => "~",
				UnaryOperation.UnaryMinus => "-",
				UnaryOperation.UnaryPlus => "+",
				_ => throw new ArgumentOutOfRangeException()
			};

			writer.ResetColor();
			writer.Write($" {op}");
			WritePunctuation("(", writer);
			node.Operand.Accept(this, writer);
			WritePunctuation(")", writer);
			return false;
		}

		public override bool Visit(Cast node, IColoredWriter writer) {
			WritePunctuation("(", writer);
			writer.ForegroundColor = ConsoleColor.Cyan;
			writer.Write(node.TargetType.ToString().ToLower());
			WritePunctuation(")", writer);
			node.Value.Accept(this, writer);
			return false;
		}

		public override bool Visit(Identifier node, IColoredWriter writer) {
			WriteLoadPrefix(node.Flags, writer);
			WriteIdentifier(node.Hash, node.Flags, writer);
			WriteLoadSuffix(node.Flags, writer);
			return false;
		}

		public override bool Visit(ArrayAccess node, IColoredWriter writer) {
			WriteLoadPrefix(node.Flags, writer);
			WriteIdentifier(node.Hash, node.Flags, writer);
			WritePunctuation("[", writer);

			for(int i = 0; i < node.Indices.Length; i++) {
				if(i != 0) WritePunctuation(", ", writer);
				node.Indices[i].Accept(this, writer);
			}

			WritePunctuation("]", writer);
			WriteLoadSuffix(node.Flags, writer);
			return false;
		}

		public override bool Visit(LiteralInt node, IColoredWriter writer) {
			writer.ForegroundColor = ConsoleColor.Green;
			writer.Write(node.Value);
			return false;
		}

		public override bool Visit(LiteralFloat node, IColoredWriter writer) {
			writer.ForegroundColor = ConsoleColor.Green;
			writer.Write(node.Value.ToString("#.0#############", CultureInfo.InvariantCulture));
			return false;
		}

		public override bool Visit(LiteralString node, IColoredWriter writer) {
			writer.ForegroundColor = ConsoleColor.Green;
			writer.Write('"' + node.Value.Escape() + '"');
			return false;
		}

		public override bool Visit(Call node, IColoredWriter writer) {
			if(node.IsSyscall) {
				writer.ForegroundColor = ConsoleColor.Yellow;
				writer.Write(Data.KnownSyscallNamesByHash
					.TryGetValue(node.Hash, out string name)
					? $"${name}"
					: $"${node.Hash:x8}");
			}
			else {
				writer.ForegroundColor = ConsoleColor.Blue;
				writer.Write(Data.KnownFunctionNamesByHash
					.TryGetValue(node.Hash, out string name)
					? $"${name}"
					: $"${node.Hash:x8}");
			}
			WritePunctuation("(", writer);

			for(int i = 0; i < node.Arguments.Length; i++) {
				if(i != 0) WritePunctuation(", ", writer);
				node.Arguments[i].Accept(this, writer);
			}

			WritePunctuation(")", writer);
			return false;
		}

		public override bool Visit(Assignment node, IColoredWriter writer) {
			string op = GetBinaryOperator(node.Operation);

			WriteIdentifier(node.Hash, node.Flags, writer);
			WritePunctuation($" {op}= ", writer);

			node.Value.Accept(this, writer);

			WritePunctuation(";", writer);
			return false;
		}

		public override bool Visit(ArrayAssignment node, IColoredWriter writer) {
			string op = GetBinaryOperator(node.Operation);

			WriteIdentifier(node.Hash, node.Flags, writer);
			WritePunctuation("[", writer);

			for(int i = 0; i < node.Indices.Length; i++) {
				if(i != 0) WritePunctuation(", ", writer);
				node.Indices[i].Accept(this, writer);
			}

			WritePunctuation($"] {op}= ", writer);

			node.Value.Accept(this, writer);

			WritePunctuation(";", writer);
			return false;
		}

		public override bool Visit(BlockStatement node, IColoredWriter writer) {
			WritePunctuation("{", writer);
			Indent();

			foreach(var statement in node.Statements) {
				WriteNewline(writer);
				statement.Accept(this, writer);
			}

			Unindent();
			WriteNewline(writer);
			WritePunctuation("}", writer);
			return false;
		}

		public override bool Visit(CallStatement node, IColoredWriter writer) {
			node.Call.Accept(this, writer);
			WritePunctuation(";", writer);
			return false;
		}

		public override bool Visit(DestructorStatement node, IColoredWriter writer) {
			writer.ForegroundColor = ConsoleColor.Magenta;
			writer.Write("destructor ");
			Visit((BlockStatement)node, writer);
			return false;
		}

		public override bool Visit(ReturnStatement node, IColoredWriter writer) {
			writer.ForegroundColor = ConsoleColor.Magenta;
			writer.Write("return");
			if(node.ReturnValue != null) {
				writer.Write(" ");
				node.ReturnValue.Accept(this, writer);
			}
			WritePunctuation(";", writer);
			return false;
		}

		public override bool Visit(IfStatement node, IColoredWriter writer) {
			writer.ForegroundColor = ConsoleColor.Magenta;
			writer.Write("if");
			WritePunctuation("(", writer);
			node.Condition.Accept(this, writer);
			WritePunctuation(") ", writer);
			node.ThenBranch.Accept(this, writer);
			if(node.ElseBranch != null) {
				WriteNewline(writer);
				writer.ForegroundColor = ConsoleColor.Magenta;
				writer.Write("else ");
				node.ElseBranch.Accept(this, writer);
			}
			return false;
		}

		public override bool Visit(TextStatement node, IColoredWriter writer) {
			writer.ForegroundColor = ConsoleColor.Magenta;
			writer.Write("text");
			WritePunctuation("(", writer);
			writer.ForegroundColor = ConsoleColor.Green;
			writer.Write('"' + node.Text.Escape() + '"');
			WritePunctuation(");", writer);
			return false;
		}

		public override bool Visit(CtrlStatement node, IColoredWriter writer) {
			writer.ForegroundColor = ConsoleColor.Magenta;
			writer.Write("ctrl");
			WritePunctuation("(", writer);
			writer.ForegroundColor = ConsoleColor.Green;
			writer.Write('"' + node.ControlCode.Escape() + '"');
			foreach(var operand in node.Operands) {
				WritePunctuation(", ", writer);
				operand.Accept(this, writer);
			}
			WritePunctuation(");", writer);
			return false;
		}

		public override bool Visit(ProcStatement node, IColoredWriter writer) {
			writer.ForegroundColor = ConsoleColor.Magenta;
			writer.Write("proc");
			WritePunctuation(";", writer);
			return false;
		}
	}
}
