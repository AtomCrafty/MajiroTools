using System;

namespace Majiro.Script {

	public enum MjoFlags : ushort { }

#pragma warning disable format
	[Flags]
	public enum MjoFlagMask : ushort {
		Dim      = 0b00011000_00000000,
		Type     = 0b00000111_00000000,
		Scope    = 0b00000000_11100000,
		Invert   = 0b00000000_00011000,
		Modifier = 0b00000000_00000111
	}
#pragma warning restore format

	public enum MjoType : byte {
		Int = 0,
		Float = 1,
		String = 2,
		IntArray = 3,
		FloatArray = 4,
		StringArray = 5,
		Unknown = 255
	}

	[Flags]
	public enum MjoTypeMask : byte {
		Int = 1 << MjoType.Int,
		Float = 1 << MjoType.Float,
		String = 1 << MjoType.String,
		IntArray = 1 << MjoType.IntArray,
		FloatArray = 1 << MjoType.FloatArray,
		StringArray = 1 << MjoType.StringArray,

		Numeric = Int | Float,
		Primitive = Int | Float | String,
		Array = IntArray | FloatArray | StringArray,
		All = Primitive | Array,
		None = 0
	}

	public enum MjoScope : byte {
		Persistent = 0,
		SaveFile = 1,
		Thread = 2,
		Local = 3
	}

	public enum MjoInvertMode : byte {
		None = 0,
		Numeric = 1,
		Boolean = 2,
		Bitwise = 3
	}

	public enum MjoModifier : byte {
		None = 0,
		PreIncrement = 1,
		PreDecrement = 2,
		PostIncrement = 3,
		PostDecrement = 4
	}

	public static class FlagHelpers {
		public static int Dimension(this MjoFlags flags) => ((ushort)flags & (ushort)MjoFlagMask.Dim) >> 11;
		public static MjoType Type(this MjoFlags flags) => (MjoType)(((ushort)flags & (ushort)MjoFlagMask.Type) >> 8);
		public static MjoScope Scope(this MjoFlags flags) => (MjoScope)(((ushort)flags & (ushort)MjoFlagMask.Scope) >> 5);
		public static MjoInvertMode InvertMode(this MjoFlags flags) => (MjoInvertMode)(((ushort)flags & (ushort)MjoFlagMask.Invert) >> 3);
		public static MjoModifier Modifier(this MjoFlags flags) => (MjoModifier)((ushort)flags & (ushort)MjoFlagMask.Modifier);

		public static MjoTypeMask ToMask(this MjoType type) => type == MjoType.Unknown ? MjoTypeMask.All : (MjoTypeMask)(1 << (byte)type);
		public static bool Matches(this MjoType type, MjoTypeMask mask) => type == MjoType.Unknown || (type.ToMask() & mask) != 0;

		public static MjoType Array(this MjoType type) => type.Matches(MjoTypeMask.Primitive)
			? type + 3
			: throw new Exception($"Can't create array type from {type}");

		public static MjoType ElementType(this MjoType type) => type.Matches(MjoTypeMask.Array)
			? type - 3
			: throw new Exception($"Can't resolve element type of {type}");

		public static MjoFlags Build(MjoType type, MjoScope scope, MjoModifier modifier, MjoInvertMode invertMode, int dimension) =>
			(MjoFlags)(dimension << 11 | (ushort)type << 8 | (ushort)scope << 5 | (ushort)invertMode << 3 | (ushort)modifier);
	}
}
