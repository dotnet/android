#nullable enable

using System;
using System.Runtime.InteropServices;

#if INTEROP
namespace Java.Interop
#else
namespace Android.Runtime
#endif
{

	[StructLayout(LayoutKind.Explicit)]
	public struct JniArgumentValue : IEquatable<JniArgumentValue> {
#pragma warning disable 0414
		[FieldOffset(0)] bool z;
		[FieldOffset(0)] sbyte b;
		[FieldOffset(0)] char c;
		[FieldOffset(0)] short s;
		[FieldOffset(0)] int i;
		[FieldOffset(0)] long j;
		[FieldOffset(0)] float f;
		[FieldOffset(0)] double d;
		[FieldOffset(0)] IntPtr l;
#pragma warning restore 0414

		public JniArgumentValue (bool value)
		{
			this = new JniArgumentValue ();
			z = value;
		}

		public JniArgumentValue (sbyte value)
		{
			this = new JniArgumentValue ();
			b = value;
		}

		public JniArgumentValue (byte value) : this ((sbyte)value) { }

		public JniArgumentValue (char value)
		{
			this = new JniArgumentValue ();
			c = value;
		}

		public JniArgumentValue (short value)
		{
			this = new JniArgumentValue ();
			s = value;
		}

		public JniArgumentValue (ushort value) : this ((short)value) { }

		public JniArgumentValue (int value)
		{
			this = new JniArgumentValue ();
			i = value;
		}

		public JniArgumentValue (uint value) : this ((int)value) { }

		public JniArgumentValue (long value)
		{
			this = new JniArgumentValue ();
			j = value;
		}

		public JniArgumentValue (ulong value) : this ((long) value) { }

		public JniArgumentValue (float value)
		{
			this = new JniArgumentValue ();
			f = value;
		}

		public JniArgumentValue (double value)
		{
			this = new JniArgumentValue ();
			d = value;
		}

		public JniArgumentValue (IntPtr value)
		{
			this = new JniArgumentValue ();
			l = value;
		}

		public JniArgumentValue (JniObjectReference value)
		{
			this = new JniArgumentValue ();
			l = value.Handle;
		}

		public JniArgumentValue (IJavaPeerable? value)
		{
			this = new JniArgumentValue ();
			if (value != null)
				l = value.PeerReference.Handle;
			else
				l = IntPtr.Zero;
		}

		public override int GetHashCode ()
		{
			return j.GetHashCode ();
		}

		public override bool Equals (object? obj)
		{
			var o   = obj as JniArgumentValue?;
			if (!o.HasValue)
				return false;
			return Equals (o.Value);
		}

		public bool Equals (JniArgumentValue other)
		{
			return j == other.j;
		}

		public static bool operator==(JniArgumentValue lhs, JniArgumentValue rhs)
		{
			return lhs.j == rhs.j;
		}

		public static bool operator!=(JniArgumentValue lhs, JniArgumentValue rhs)
		{
			return lhs.j != rhs.j;
		}

		public override string ToString ()
		{
			return string.Format ("JniArgumentValue(z={0},b={1},c={2},s={3},i={4},j={5},f={6},d={7},l=0x{8})",
					z, b, c, s, i, j, f, d, l.ToString ("x"));
		}
	}
}

