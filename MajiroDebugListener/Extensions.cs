using System.IO;
using System.Text;

namespace MajiroDebugListener {
	public static class Extensions {

		public static string ReadNullTerminatedString(this BinaryReader reader) {
			var sb = new StringBuilder(64);

			while(true) {
				char ch = reader.ReadChar();
				if(ch == 0) break;
				sb.Append(ch);
			}

			return sb.ToString();
		}
	}
}
