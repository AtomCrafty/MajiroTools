using System;
using System.IO;
using System.Text;

namespace Majiro.Util {
	public interface IColoredWriter {
		ConsoleColor ForegroundColor { get; set; }
		ConsoleColor BackgroundColor { get; set; }
		void ResetColor();

		void Write(object o);
		void WriteLine(object o);
		void WriteLine();

		public static readonly IColoredWriter Console = new ColoredConsoleWriter();
	}

	public sealed class ColoredConsoleWriter : IColoredWriter {
		public ConsoleColor ForegroundColor {
			get => Console.ForegroundColor;
			set => Console.ForegroundColor = value;
		}

		public ConsoleColor BackgroundColor {
			get => Console.BackgroundColor;
			set => Console.BackgroundColor = value;
		}

		public void ResetColor() => Console.ResetColor();

		public void Write(object o) => Console.Write(o);

		public void WriteLine(object o) => Console.WriteLine(o);

		public void WriteLine() => Console.WriteLine();
	}

	public sealed class StreamColorWriter : IColoredWriter {
		private readonly TextWriter _baseWriter;
		public StreamColorWriter(TextWriter baseWriter) {
			_baseWriter = baseWriter;
		}

		public ConsoleColor ForegroundColor { get => default; set { } }

		public ConsoleColor BackgroundColor { get => default; set { } }

		public void ResetColor() { }

		public void Write(object o) => _baseWriter.Write(o);

		public void WriteLine(object o) => _baseWriter.WriteLine(o);

		public void WriteLine() => _baseWriter.WriteLine();
	}

	public sealed class StringBuilderColorWriter : IColoredWriter {
		private readonly StringBuilder _builder;
		public StringBuilderColorWriter(StringBuilder builder) {
			_builder = builder;
		}

		public ConsoleColor ForegroundColor { get => default; set { } }

		public ConsoleColor BackgroundColor { get => default; set { } }

		public void ResetColor() { }

		public void Write(object o) => _builder.Append(o);

		public void WriteLine(object o) => _builder.Append(o).AppendLine();

		public void WriteLine() => _builder.AppendLine();
	}
}
