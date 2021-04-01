using System;
using System.Linq;

namespace Majiro.Script {

	public static class Crc {

		private static readonly uint[] Crc32Table = Enumerable.Range(0, 256).Select(Calculate32).ToArray();
		private static readonly byte[] CryptKey32 = Crc32Table.SelectMany(BitConverter.GetBytes).ToArray();

		private static readonly long[] Crc64Table = Enumerable.Range(0, 256).Select(Calculate64).ToArray();
		private static readonly byte[] CryptKey64 = Crc64Table.SelectMany(BitConverter.GetBytes).ToArray();

		public static uint Calculate32(int seed) {
			const uint mask = 0xEDB88320;
			uint value = (uint)seed;
			for(int i = 0; i < 8; i++) {
				value = (value & 1) != 0 ? (value >> 1) ^ mask : value >> 1;
			}
			return value;
		}
		public static long Calculate64(int seed) {
			const ulong mask = 0x85E1C3D753D46D27;
			ulong value = (ulong)seed;
			for(int i = 0; i < 8; i++) {
				value = (value & 1) != 0 ? (value ^ mask) >> 1 : value >> 1;
			}
			return (long)value;
		}

		public static void Crypt32(byte[] bytes, int keyOffset = 0) {
			for(int i = 0; i < bytes.Length; i++) {
				bytes[i] ^= CryptKey32[keyOffset++ & 0x3FF];
			}
		}

		public static void Crypt64(byte[] bytes, int keyOffset = 0) {
			for(int i = 0; i < bytes.Length; i++) {
				bytes[i] ^= CryptKey64[keyOffset++ & 0x7FF];
			}
		}

		public static uint Hash32(string s) => Hash32(Helpers.ShiftJis.GetBytes(s));

		public static uint Hash32(byte[] bytes) {
			uint result = 0xFFFFFFFF;
			foreach(byte b in bytes) {
				result = (result >> 8) ^ Crc32Table[(byte)(result ^ b)];
			}
			return ~result;
		}

		public static long Hash64(string s) => Hash64(Helpers.ShiftJis.GetBytes(s));

		public static long Hash64(byte[] bytes) {
			long result = 0xFFFFFFFF;
			foreach(byte b in bytes) {
				result = (result >> 8) ^ Crc64Table[(byte)(result ^ b)];
			}
			return ~result;
		}
	}
}