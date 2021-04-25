using System;
using System.Linq;

namespace Majiro.Script {

	public static class Crc {

		private static readonly uint[] Crc32Table = Enumerable.Range(0, 256).Select(Calculate32).ToArray();
		private static readonly byte[] Crc32Index = Enumerable.Range(0, 256).Select(CalculateInverse32).ToArray();
		private static readonly byte[] CryptKey32 = Crc32Table.SelectMany(BitConverter.GetBytes).ToArray();

		private static readonly ulong[] Crc64Table = Enumerable.Range(0, 256).Select(Calculate64).ToArray();
		private static readonly byte[] CryptKey64 = Crc64Table.SelectMany(BitConverter.GetBytes).ToArray();

		public static uint Calculate32(int seed) {
			const uint poly = 0xEDB88320;
			uint value = (uint)seed;
			for(int i = 0; i < 8; i++) {
				value = (value & 1) != 0 ? (value >> 1) ^ poly : value >> 1;
			}
			return value;
		}
		public static ulong Calculate64(int seed) {
			// Assembly implementation uses poly: 0x85E1C3D753D46D27, and bitshifts after XOR with poly.
			// Behavior is identical to normal CRC-64 implementation with common poly: 0x42F0E1EBA9EA3693.
			//  (except the forward polynomial is used for a reverse implementation)
			const ulong poly = 0x42F0E1EBA9EA3693;
			ulong value = (ulong)seed;
			for(int i = 0; i < 8; i++) {
				value = (value & 1) != 0 ? (value >> 1) ^ poly : value >> 1;
			}
			return value;
		}

		public static byte CalculateInverse32(int seed) {
			uint msbyte = (uint)seed;
			for(int i = 0; i < 256; i++) {
				if((Calculate32(i) >> 24) == msbyte)
					return (byte) i;
			}
			throw new ArgumentException($"Most significant byte 0x{msbyte:x2} not in {nameof(Crc32Table)}");
		}

		public static void Crypt32(byte[] bytes, int keyOffset = 0) {
			for(int i = 0; i < bytes.Length; i++) {
				bytes[i] ^= CryptKey32[keyOffset++ & 0x3FF]; // (bitwise: % 1024)
			}
		}

		public static void Crypt64(byte[] bytes, int keyOffset = 0) {
			for(int i = 0; i < bytes.Length; i++) {
				bytes[i] ^= CryptKey64[keyOffset++ & 0x7FF]; // (bitwise: % 2048)
			}
		}

		public static uint Hash32(string s, uint init = 0u) => Hash32(Helpers.ShiftJis.GetBytes(s), init);

		public static uint Hash32(byte[] bytes, uint init = 0u) {
			uint result = ~init;
			foreach(byte b in bytes) {
				result = (result >> 8) ^ Crc32Table[(byte)(result ^ b)];
			}
			return ~result;
		}
		public static uint Hash32At(byte[] bytes, int startIndex, int count, uint init = 0u) {
			uint result = ~init;
			for(int i = 0; i < count; i++) {
				result = (result >> 8) ^ Crc32Table[(byte) (result ^ bytes[startIndex + i])];
			}
			return ~result;
		}

		public static uint HashInverse32(string s, uint init) => HashInverse32(Helpers.ShiftJis.GetBytes(s), init);

		public static uint HashInverse32(byte[] bytes, uint init) {
			uint result = ~init;
			foreach(byte b in bytes.Reverse()) {
				uint index = Crc32Index[result >> 24];
				result = ((result ^ Crc32Table[index]) << 8) | (index ^ b);
			}
			return ~result;
		}
		public static uint HashInverse32At(byte[] bytes, int startIndex, int count, uint init) {
			uint result = ~init;
			for(int i = 0; i < count; i++) {
				uint index = Crc32Index[result >> 24];
				result = ((result ^ Crc32Table[index]) << 8) | (index ^ bytes[startIndex + i]);
			}
			return ~result;
		}

		public static ulong Hash64(string s, ulong init = 0ul) => Hash64(Helpers.ShiftJis.GetBytes(s), init);

		public static ulong Hash64(byte[] bytes, ulong init = 0ul) {
			ulong result = ~init;
			foreach(byte b in bytes) {
				result = (result >> 8) ^ Crc64Table[(byte)(result ^ b)];
			}
			return ~result;
		}
		public static ulong Hash64At(byte[] bytes, int startIndex, int count, ulong init = 0ul) {
			ulong result = ~init;
			for(int i = 0; i < count; i++) {
				result = (result >> 8) ^ Crc64Table[(byte) (result ^ bytes[startIndex + i])];
			}
			return ~result;
		}
	}
}