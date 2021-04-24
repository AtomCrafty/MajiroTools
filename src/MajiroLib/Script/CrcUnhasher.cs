using System;
using System.Collections;
using System.Collections.Generic;

namespace Majiro.Script {

	public class CrcUnhasher : IEnumerator<string>, IEnumerable<string> {
		public const string DefaultCharset = "abcdefghijklmnopqrstuvwxyz_0123456789";
		private static readonly byte[] DefaultCharsetBytes = Helpers.ShiftJis.GetBytes(DefaultCharset);

		private uint target = 0;
		private string prefix = "";
		private string postfix = "";
		private string charset = DefaultCharset;
		private int length = 0; // number of characters in the pattern
		private uint init = 0; // crc of prefix
		private uint expected = 0; // inverse crc of postfix with target as init value
		private byte[] charsetBytes = DefaultCharsetBytes;
		// levels[] tracks the current charset index of every element in the buffer.
		// It's used to save time comparing actual chars, and makes incrementing easier.
		private int[] levels = Array.Empty<int>();
		private byte[] buffer = Array.Empty<byte>();
		private string current = null;
		private long combinations = 0; // number of tried combinations, this is only for vanity and not required
		private bool finished = false; // not truly finished until current == null

		public CrcUnhasher() { }

		public long Combinations => combinations;
		public bool IsFinished => current == null && finished;
		public string Pattern => string.Concat(prefix, Helpers.ShiftJis.GetString(buffer), postfix);

		public uint Target {
			get => target;
			set {
				target = value;
				expected = Crc.HashInverse32(postfix, value);
				Reset();
			}
		}
		public string Prefix {
			get => prefix;
			set {
				prefix = value;
				init = Crc.Hash32(value);
				Reset();
			}
		}
		public string Postfix {
			get => postfix;
			set {
				postfix = value;
				expected = Crc.HashInverse32(value, target);
				Reset();
			}
		}
		public string Charset {
			get => charset;
			set {
				charset = value;
				charsetBytes = Helpers.ShiftJis.GetBytes(value);
				Reset();
			}
		}
		public int Length {
			get => length;
			set {
				length = value;
				levels = new int[length];
				buffer = new byte[length];
				Reset();
			}
		}

		public void Reset() {
			// Prepare initial buffer states: Fill levels and buffer, with all 0 and charset[0]
			for(int i = 0; i < length; i++) {
				levels[i] = 0;
				buffer[i] = charsetBytes[0];
			}
			current = null;
			finished = false;
		}

		public bool MoveNext() {
			if(finished) {
				current = null;
				return false;
			}

			// Check current pattern
			uint result = Crc.Hash32(buffer, init);
			if(result == expected) {
				current = Pattern;

				// Special handling: there is only ONE 4-byte combination to transform one accum A into another B
				// We can give up the current first 4 bytes.
				// This is only a minor optimization, since 4 ASCII bytes of depth is pretty fast to scan through.
				// (when length == 4, this behaves the same as a break;)
				// Next for(bufferIndex)-loop iteration will push out first 4 chars from any combination:
				//  charset = "ABC"
				//  [ACBA]AA -> [AAAA]BA
				for(int i = 0; i < Math.Min(4, length); i++) {
					levels[i] = charsetBytes.Length - 1;
				}
			}
			else {
				current = null;
			}

			// Change to next pattern
			//  charset = "ABC"
			//  AAAA -> BAAA -> CAAA -> ABAA -> BBAA -> CBAA -> ACAA ...
			int bufferIndex;
			for(bufferIndex = 0; bufferIndex < length; bufferIndex++) {
				int charsetIndex = ++levels[bufferIndex]; // next character at current buffer index
				if(charsetIndex != charsetBytes.Length) {
					// Letter incremented, break and hash new pattern
					buffer[bufferIndex] = charsetBytes[charsetIndex];
					break;
				}
				else {
					// Wrap letter back to charset[0], continue to next index
					levels[bufferIndex] = 0;
					buffer[bufferIndex] = charsetBytes[0];
				}
			}
			combinations++;
			finished = (bufferIndex >= length);
			return true;
		}

		public string Current => current;
		object IEnumerator.Current => current;

		public IEnumerator<string> GetEnumerator() => this;
		IEnumerator IEnumerable.GetEnumerator() => this;

		void IDisposable.Dispose() { }
	}
}