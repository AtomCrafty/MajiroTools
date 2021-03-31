using System;
using System.Linq;

namespace Majiro.Script {

	public static class Crc32 {

		private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(Calculate).ToArray();
		private static readonly byte[] CryptKey = CrcTable.SelectMany(BitConverter.GetBytes).ToArray();

		public static uint Calculate(int seed) {
			const uint mask = 0xEDB88320;
			uint value = (uint)seed;
			for(int i = 0; i < 8; i++) {
				value = (value & 1) != 0 ? (value >> 1) ^ mask : value >> 1;
			}
			return value;
		}

		public static void Crypt(byte[] bytes, int keyOffset = 0) {
			for(int i = 0; i < bytes.Length; i++) {
				bytes[i] ^= CryptKey[keyOffset++ & 0x3FF];
			}
		}

		public static uint Hash(string s) => Hash(Helpers.ShiftJis.GetBytes(s));

		public static uint Hash(byte[] bytes) {
			uint result = 0xFFFFFFFF;
			foreach(byte b in bytes) {
				result = (result >> 8) ^ CrcTable[(byte)(result ^ b)];
			}
			return ~result;
		}
	}
}