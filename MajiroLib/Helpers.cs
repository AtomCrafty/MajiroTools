using System.IO;
using System.Text;

namespace Majiro {
	public static class Helpers {
		public static readonly Encoding ShiftJis;

		static Helpers() {
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			ShiftJis = Encoding.GetEncoding(932);
		}

		public static string ReadSizedString(this BinaryReader reader, int size, Encoding encoding = null) =>
			(encoding ?? ShiftJis).GetString(reader.ReadBytes(size));
	}
}
