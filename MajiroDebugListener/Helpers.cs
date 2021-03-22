using System.IO;
using System.Linq;
using System.Text;

namespace MajiroDebugListener {
	public static class Helpers {
		public static readonly Encoding ShiftJis;

		static Helpers() {
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			ShiftJis = Encoding.GetEncoding("Shift-JIS");
		}

		public static string ReadNullTerminatedString(this BinaryReader reader) {
			var sb = new StringBuilder(64);

			while(true) {
				char ch = reader.ReadChar();
				if(ch == 0) break;
				sb.Append(ch);
			}

			return sb.ToString();
		}

		public static string ToNullTerminatedString(this byte[] bytes, Encoding encoding = null) =>
			(encoding ?? ShiftJis).GetString(bytes).TrimEnd('\0');

		public static bool IsOneOf<T>(this T value, params T[] options) => options.Contains(value);
	}
}
