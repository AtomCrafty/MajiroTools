using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Majiro.Script {

	public class CrcUnhasher : IEnumerator<string>, IEnumerable<string> {
		public const string DefaultCharset = "a-z_0-9";
		public const string DefaultCharsetExpanded = "abcdefghijklmnopqrstuvwxyz_0123456789";
		private static readonly byte[] DefaultCharsetBytes = Helpers.ShiftJis.GetBytes(DefaultCharsetExpanded);

		private uint target = 0;
		private HashSet<uint> targetSet = null;
		private string prefix = null;//"";
		private string postfix = null;//"";
		private bool depthFirst = false; // false: AAA->AAB->AAC->ABA, true: AAA->BAA->CAA->ABA
		private int length = 0; // number of characters in the pattern
		private string charsetRange = DefaultCharset;
		private string charsetExpanded = DefaultCharsetExpanded;
		private byte[] charsetBytes = DefaultCharsetBytes;
		private uint init = 0; // crc of prefix
		private uint expected = 0; // inverse crc of postfix with target as init value
		private HashSet<uint> expectedSet = null;
		// levels[] tracks the current charset index of every element in the buffer.
		// It's used to save time comparing actual chars, and makes incrementing easier.
		private int[] levels = Array.Empty<int>();
		private byte[] buffer = Array.Empty<byte>();
		private string current = null;
		private uint? result = null;
		private bool finished = false; // this will be true the iteration BEFORE MoveNext returns true
		private long combinations = 0; // number of tried combinations, this is only for vanity and not required

		public CrcUnhasher() { }

		// Configuration Properties:

		/// <summary>Gets or sets the search order (false: AAA->AAB->AAC->ABA, true: AAA->BAA->CAA->ABA).</summary>
		public bool DepthFirst {
			get => depthFirst;
			set {
				depthFirst = value;
				Reset();
			}
		}
		/// <summary>Gets or sets the target CRC-32 hash value.</summary>
		public uint Target {
			get => target;
			set {
				target = value;
				UpdateExpected(true, false); // update expected "single" values only
				Reset();
			}
		}
		public HashSet<uint> TargetSet {
			get => targetSet;
			set {
				targetSet = value;
				UpdateExpected(false, true); // update expected "set" values only
				Reset();
			}
		}
		/// <summary>Gets or sets the constant unhashed prefix string (preserves null).</summary>
		public string Prefix {
			get => prefix;
			set {
				prefix = value;// ?? throw new ArgumentNullException(nameof(Prefix));
				UpdateInit(); // update initial value for CRC-32 input
				Reset();
			}
		}
		/// <summary>Gets or sets the constant unhashed postfix string (preserves null).</summary>
		public string Postfix {
			get => postfix;
			set {
				postfix = value;// ?? throw new ArgumentNullException(nameof(Postfix));
				UpdateExpected(true, true); // update both expected "single" and "set" values
				Reset();
			}
		}
		/// <summary>Gets or sets the character set with range syntax (a-z_0-9).</summary>
		public string Charset {
			get => charsetRange;
			set {
				if(string.IsNullOrEmpty(value))
					throw new ArgumentException("Empty: Charset is null or empty", nameof(Charset));
				charsetExpanded = ParseCharsetRange(value, true); // duplicates are stripped by default
				charsetRange = value;
				charsetBytes = Helpers.ShiftJis.GetBytes(charsetExpanded);
				Reset();
			}
		}
		/// <summary>Gets or sets the character set without range syntax.</summary>
		public string CharsetExpanded {
			get => charsetExpanded;
			set {
				if(string.IsNullOrEmpty(value))
					throw new ArgumentException("Empty: CharsetExpanded is null or empty", nameof(CharsetExpanded));
				charsetRange = value.Replace("\\", "\\\\").Replace("-", "\\-"); // make sure this is a valid range
				charsetExpanded = value;
				charsetBytes = Helpers.ShiftJis.GetBytes(charsetExpanded);
				Reset();
			}
		}
		/// <summary>Gets or sets the pattern length to test against (0 is valid).</summary>
		public int Length {
			get => length;
			set {
				if(length < 0)
					throw new ArgumentOutOfRangeException("InvalidLength: Length is less than zero");
				length = value;
				levels = new int[length];
				buffer = new byte[length];
				Reset();
			}
		}

		// Information and Resuming Properties:

		/// <summary>Gets the total number of combinations tested against since this class's instantiation.</summary>
		public long Combinations => combinations;
		/// <summary>Gets if all permutations have been tested against for the current length.</summary>
		public bool IsFinished => finished; // current == null && finished;
		/// <summary>Returns total permutations for current charset.Length^length.</summary>
		public BigInteger PermutationsBig => CountPermutationsBig(length, charsetBytes.Length);
		/// <summary>Returns total permutations for current charset.Length^length (ulong.MaxValue on overflow).</summary>
		public ulong Permutations => CountPermutations(length, charsetBytes.Length);
		/// <summary>Gets the final CRC-32 result of the last check, including postfix (null if checks have not started).</summary>
		public uint? Result => (result.HasValue ? Crc.Hash32(postfix??"", result.Value) : null);

		/// <summary>Returns pattern of the last-checked match (null if checks have not started).</summary>
		public string Pattern {
			get {
				if(current != null)
					return current; // current == Pattern if it's a match, skip the extra steps
				byte[] patternBuffer = (byte[])buffer.Clone();
				bool notStarted = MoveBufferLast(patternBuffer, (int[])levels.Clone());
				return (!notStarted ? Helpers.ShiftJis.GetString(patternBuffer) : null); // not run yet
			}
		}
		/// <summary>Returns the Prefix+Pattern+Postfix of the last-checked match (null if checks have not started).</summary>
		public string FullPattern {
			get {
				string pattern = Pattern;
				return (pattern != null ? string.Concat(prefix??"", pattern, postfix??"") : null); // not run yet
			}
		}
		/// <summary>Returns the pattern of the next check (null if checks have finished).</summary>
		/// <remarks>
		/// If last check matched, all remaining permutations for the current 4 chars are skipped (which may result in an unusual return).
		/// </remarks>
		public string PatternNext => (!finished ? Helpers.ShiftJis.GetString(buffer) : null);
		/// <summary>Returns the Prefix+PatternNext+Postfix of the next check (null if checks have finished).</summary>
		/// <remarks>
		/// If last check matched, all remaining permutations for the current 4 chars are skipped (which may result in an unusual return).
		/// </remarks>
		public string FullPatternNext => (!finished ? string.Concat(prefix??"", PatternNext, postfix??"") : null);

		/// <summary>Resumes current position in checks to pattern (null starts from beginning).</summary>
		/// <remarks>If value is PatternNext, moveNext should be false. Otherwise specify moveNext if you want to skip the passed pattern.</remarks>
		public void ResumePattern(string value, bool moveNext) {
			Reset(); // Lazy: always reset first, to clean up and prepare everything
			// Null handles as if resuming from beginning
			if(value != null) {
				byte[] newBuffer = Helpers.ShiftJis.GetBytes(value);
				if(newBuffer.Length != length)
					throw new ArgumentException("InvalidPattern: New Pattern length does not match current length");
				for(int i = 0; i < length; i++) {
					byte b = newBuffer[i];
					int index = Array.IndexOf(charsetBytes, b);
					if(index == -1)
						throw new ArgumentException($"InvalidPattern: Character {(char) b} at index {i} not in charset");
					levels[i] = index;
					buffer[i] = b;
				}
			}
			
			if(moveNext) {
				// Lazy: Move to next pattern internally (this also loads up the Current property)
				MoveNext();
				combinations--; // Lazy: decrement count since the goal was to skip this pattern
			}
		}


		// IEnumerator Methods:

		/// <summary>Resets pattern checks back to the beginning of the current length.</summary>
		public void Reset() {
			// Prepare initial buffer states: Fill levels and buffer, with all 0 and charset[0]
			for(int i = 0; i < length; i++) {
				levels[i] = 0;
				buffer[i] = charsetBytes[0];
			}
			result = null;
			current = null;
			finished = false;
		}

		/// <summary>Checks the upcoming pattern in the buffer and moves to the next pattern.</summary>
		public bool MoveNext() {
			if(finished) {
				current = null;
				return false;
			}

			// Check current pattern
			//uint result = Crc.Hash32(buffer, init);
			uint result = Crc.Hash32(buffer, init);
			if(result == expected || (expectedSet?.Contains(result) ?? false)) {
				current = Helpers.ShiftJis.GetString(buffer);

				// Special handling: there is only ONE 4-byte combination to transform one accum A into another B
				// We can give up the current first 4 bytes.
				// This is only a minor optimization, since 4 ASCII bytes of depth is pretty fast to scan through.
				// (when length == 4, this behaves the same as a break;)
				// Next for(bufferIndex)-loop iteration will push out first 4 chars from any combination:
				//  charset = "ABC"
				//  [ACBA]AA -> [AAAA]BA  (when depthFirst==true)
				if(expectedSet == null) {
					// we can't use this behavior when targetting multiple hashes
					if(depthFirst) {
						for(int i = 0; i < Math.Min(4, length); i++)
							levels[i] = charsetBytes.Length - 1;
					}
					else {
						for(int i = length - 1; i >= Math.Max(0, length - 4); i--)
							levels[i] = charsetBytes.Length - 1;
					}
				}
			}
			else {
				current = null;
			}
			this.result = result;

			// Change to next pattern
			finished = MoveBufferNext(buffer, levels);
			combinations++;
			return true;
		}

		// IEnumerator<> + IEnumerable<> Simple Methods and Properties:

		/// <summary>Returns the current pattern of the last check (null if no match).</summary>
		public string Current => current;
		object IEnumerator.Current => current;
		/// <summary>Returns the current Prefix+Current+Postfix of the last check (null if no match).</summary>
		public string FullCurrent => (current != null ? string.Concat(prefix??"", current, postfix??"") : null);

		public IEnumerator<string> GetEnumerator() => this;
		IEnumerator IEnumerable.GetEnumerator() => this;

		void IDisposable.Dispose() { }


		// Private Update Methods:

		/// <summary>Updates the init field based on changes to prefix.</summary>
		private void UpdateInit() {
			init = Crc.Hash32(prefix??"");
		}
		/// <summary>Updates the expected/expectedSet fields based on changes to target/targetSet/postfix.</summary>
		private void UpdateExpected(bool single, bool multiple) {
			if(single) {
				expected = Crc.HashInverse32(postfix??"", target);
			}
			if(multiple) {
				if(targetSet == null)
					expectedSet = null;
				else
					expectedSet = new HashSet<uint>(targetSet.Select(t => Crc.HashInverse32(postfix??"", t)));
			}
		}

		// Change to next (or last) pattern
		//  charset = "ABC"
		//  AAAA -> BAAA -> CAAA -> ABAA -> BBAA -> CBAA -> ACAA ... (example for depthFirst=true)
		/// <summary>Changes buffer and levels arrays to next pattern (returns true on finished).</summary>
		private bool MoveBufferNext(byte[] buffer, int[] levels) {
			int bufferIndex;
			if(depthFirst) {
				for(bufferIndex = 0; bufferIndex < length; bufferIndex++) {
					int charsetIndex = ++levels[bufferIndex]; // next character at current buffer index
					if(charsetIndex != charsetBytes.Length) {
						// Letter incremented, break with new pattern
						buffer[bufferIndex] = charsetBytes[charsetIndex];
						break;
					}
					else {
						// Wrap letter back to charset[0], continue to next index
						levels[bufferIndex] = 0;
						buffer[bufferIndex] = charsetBytes[0];
					}
				}
				return bufferIndex == length;
			}
			else {
				for(bufferIndex = length - 1; bufferIndex >= 0; bufferIndex--) {
					int charsetIndex = ++levels[bufferIndex]; // next character at current buffer index
					if(charsetIndex != charsetBytes.Length) {
						// Letter incremented, break with new pattern
						buffer[bufferIndex] = charsetBytes[charsetIndex];
						break;
					}
					else {
						// Wrap letter back to charset[0], continue to previous index
						levels[bufferIndex] = 0;
						buffer[bufferIndex] = charsetBytes[0];
					}
				}
				return bufferIndex == -1;
			}
		}
		/// <summary>Changes buffer and levels arrays to last pattern (returns true on not started).</summary>
		private bool MoveBufferLast(byte[] buffer, int[] levels) {
			int bufferIndex;
			if(depthFirst) {
				for(bufferIndex = 0; bufferIndex < length; bufferIndex++) {
					int charsetIndex = --levels[bufferIndex]; // next character at current buffer index
					if(charsetIndex != -1) {
						// Letter decremented, break with new pattern
						buffer[bufferIndex] = charsetBytes[charsetIndex];
						break;
					}
					else {
						// Wrap letter back to charset[n-1], continue to next index
						levels[bufferIndex] = charsetBytes.Length - 1;
						buffer[bufferIndex] = charsetBytes[charsetBytes.Length - 1];
					}
				}
				return bufferIndex == length;
			}
			else {
				for(bufferIndex = length - 1; bufferIndex >= 0; bufferIndex--) {
					int charsetIndex = --levels[bufferIndex]; // next character at current buffer index
					if(charsetIndex != -1) {
						// Letter decremented, break with new pattern
						buffer[bufferIndex] = charsetBytes[charsetIndex];
						break;
					}
					else {
						// Wrap letter back to charset[n-1], continue to previous index
						levels[bufferIndex] = charsetBytes.Length - 1;
						buffer[bufferIndex] = charsetBytes[charsetBytes.Length - 1];
					}
				}
				return bufferIndex == -1;
			}
		}


		// Static Helper Methods:

		/// <summary>Counts total permutations for a given charsetLength^length.</summary>
		public static BigInteger CountPermutationsBig(int length, int charsetLength) {
			return (length != 0 ? BigInteger.Pow(new BigInteger(charsetLength), length) : BigInteger.One);
		}
		/// <summary>Counts total permutations for a given charsetLength^length (ulong.MaxValue on overflow).</summary>
		public static ulong CountPermutations(int length, int charsetLength) {
			BigInteger count = CountPermutationsBig(length, charsetLength);
			return (count < ulong.MaxValue ? (ulong)count : ulong.MaxValue);
		}

		/// <summary>Expands a range syntax character set (a-z_0-9) into its full form (includes error checking).</summary>
		/// <remarks>Dash '-' and backslash '\' characters must be escaped with '\' to be used literally.</remarks>
		public static string ParseCharsetRange(string pattern, bool stripDuplicates) {
			List<char> charset = new List<char>();
			bool lastRange = false;
			for(int i = 0; i < pattern.Length; i++) {
				char c = pattern[i];
				switch(c) {
				case '-': {  // Character range (functions off of previous literal character)
					if(lastRange)
						throw new ArgumentException($"InvalidRange: '-' after end of previous range, at index {i}");
					else if(i == 0)
						throw new ArgumentException($"Unescaped: '-' first character in charset, at index {i}");
					else if(i+1 == pattern.Length)
						throw new ArgumentException($"Unescaped: Trailing '-' in charset, at index {i}");

					char start = pattern[i-1]; // before '-'
					char end = pattern[++i]; // after '-'
					if(end == '-') {
						throw new ArgumentException($"Unescaped: '-' as range argument in charset, at index {i}");
					}
					else if(end == '\\') {
						if(i+1 == pattern.Length)
							throw new ArgumentException($"Trailing: '\\' escape in charset, at index {i}");
						end = pattern[++i];
					}
					if(end < start)
						throw new ArgumentException($"InvalidRange: [{start}-{end}], end char is less than start char, at index {i}");
					// Add range: start char already added, start at +1
					for(int cc = start + 1; cc <= end; cc++) {
						charset.Add((char) cc);
					}
					lastRange = true;
					break;
				}
				case '\\':  // Escape literal character
					if(i+1 == pattern.Length)
						throw new ArgumentException($"Trailing: '\\' escape in charset, at index {i}");
					charset.Add(pattern[++i]);
					lastRange = false;
					break;
				default:  // Literal character
					charset.Add(c);
					lastRange = false;
					break;
				}
			}

			// Error-check resulting charset:
			// List of duplicates to include in error message (does not contain duplicate duplicates)
			List<char> duplicates = (!stripDuplicates ? new List<char>() : null);
			string origCharsetStr = (!stripDuplicates ? new string(charset.ToArray()) : null);
			for(int i = 0; i < charset.Count; i++) {
				bool first = true;
				char c = charset[i];
				for(int j = i+1; j < charset.Count; j++) {
					if(c == charset[j]) {
						if(!stripDuplicates && first) {
							first = false;
							duplicates.Add(c);
						}
						charset.RemoveAt(j);
					}
				}
			}
			if(!stripDuplicates && duplicates.Count != 0) {
				string duplicatesStr = new string(duplicates.ToArray());
				string duplicatesEsc = duplicatesStr.Replace("\\", "\\\\").Replace("\"", "\\\"");
				throw new ArgumentException($"Duplicates: Found {duplicates.Count} duplicates in charset: \"{duplicatesEsc}\"");
			}
			// When origCharsetStr != null, we can use it here, as no duplicates were found when stripDuplicates = false
			return origCharsetStr ?? new string(charset.ToArray());
		}
	}
}