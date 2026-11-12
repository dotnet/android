@kotlin.ExperimentalUnsignedTypes
const val UINT_CONST: UInt = 3u

@kotlin.ExperimentalUnsignedTypes
const val USHORT_CONST: UShort = 3u

@kotlin.ExperimentalUnsignedTypes
const val ULONG_CONST: ULong = 3u

@kotlin.ExperimentalUnsignedTypes
const val UBYTE_CONST: UByte = 3u

@kotlin.ExperimentalUnsignedTypes
public class UnsignedTypes {
	public fun foo_uint (value : Int) : Int { return value; }
	public fun foo_uint (value : UInt) : UInt { return value; }
	public fun foo_ushort (value : Short) : Short { return value; }
	public fun foo_ushort (value : UShort) : UShort { return value; }
	public fun foo_ulong (value : Long) : Long { return value; }
	public fun foo_ulong (value : ULong) : ULong { return value; }
	public fun foo_ubyte (value : Byte) : Byte { return value; }
	public fun foo_ubyte (value : UByte) : UByte { return value; }
	public fun foo_uintarray (value : UIntArray) : UIntArray { return value; }
	public fun foo_ushortarray (value : UShortArray) : UShortArray { return value; }
	public fun foo_ulongarray (value : ULongArray) : ULongArray { return value; }
	public fun foo_ubytearray (value : UByteArray) : UByteArray { return value; }
	public fun foo_uintarrayarray (value : Array<UIntArray>) : Array<UIntArray> { return value; }
}