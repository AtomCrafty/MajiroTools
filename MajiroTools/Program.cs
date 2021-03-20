using System;
using System.Linq;
using System.Text;

namespace MajiroTools {
	static class Program {
		private static readonly Encoding ShiftJis;
		private static readonly uint[] CrcTable;

		static Program() {
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			ShiftJis = Encoding.GetEncoding("Shift-JIS");
			CrcTable = Enumerable.Range(0, 256).Select(Crc32).ToArray();
		}

		static uint Crc32(int seed) {
			const uint mask = 0xEDB88320;
			uint value = (uint) seed;
			for(int i = 0; i < 8; i++) {
				value = (value & 1) != 0 ? (value >> 1) ^ mask : value >> 1;
			}
			return value;
		}

		static uint Hash(string s) {
			var bytes = ShiftJis.GetBytes(s);
			uint result = 0xFFFFFFFF;
			foreach(byte b in bytes) {
				result = (result >> 8) ^ CrcTable[(byte)(result ^ b)];
			}
			return ~result;
		}

		static void PrintHash(string name) {
			Console.WriteLine($"{Hash(name):X8} {name}");
		}

		static void Main(string[] args) {
			PrintHash("$init@GLOBAL");
			PrintHash("get_variable");
			PrintHash("$get_variable");
			PrintHash("get_variable@");
			PrintHash("$get_variable@");
			PrintHash("get_variable$");
			PrintHash("$get_variable$");
			PrintHash("get_variable@GLOBAL");
			PrintHash("$get_variable@GLOBAL");
		}
	}
}
