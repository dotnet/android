using System;
using System.Diagnostics.CodeAnalysis;
using Java.Interop;

namespace Microsoft.Android.Runtime;

static class TrimmableValueMarshalerHelper
{
	public static bool IsPrimitiveJniValueType (Type type)
	{
		return type == typeof (bool) ||
			type == typeof (byte) ||
			type == typeof (sbyte) ||
			type == typeof (char) ||
			type == typeof (short) ||
			type == typeof (ushort) ||
			type == typeof (int) ||
			type == typeof (uint) ||
			type == typeof (long) ||
			type == typeof (ulong) ||
			type == typeof (float) ||
			type == typeof (double);
	}

	public static JniArgumentValue CreatePrimitiveArgumentValue (object? value, Type type)
	{
		return value switch {
			null => throw new ArgumentNullException (nameof (value), "Value cannot be null for primitive JNI value types."),
			bool v => new JniArgumentValue (v),
			byte v => new JniArgumentValue (v),
			sbyte v => new JniArgumentValue (v),
			char v => new JniArgumentValue (v),
			short v => new JniArgumentValue (v),
			ushort v => new JniArgumentValue (v),
			int v => new JniArgumentValue (v),
			uint v => new JniArgumentValue (v),
			long v => new JniArgumentValue (v),
			ulong v => new JniArgumentValue (v),
			float v => new JniArgumentValue (v),
			double v => new JniArgumentValue (v),
			_ => throw new NotSupportedException ($"Type '{type.AssemblyQualifiedName}' is not a JNI primitive value type."),
		};
	}
}
