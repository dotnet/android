#pragma warning disable
using System;
using System.Runtime.InteropServices;

namespace Android.Runtime {

	[StructLayout(LayoutKind.Explicit)]
	public struct JValue {
		[FieldOffset(0)] bool z;
		[FieldOffset(0)] sbyte b;
		[FieldOffset(0)] char c;
		[FieldOffset(0)] short s;
		[FieldOffset(0)] int i;
		[FieldOffset(0)] long j;
		[FieldOffset(0)] float f;
		[FieldOffset(0)] double d;
		[FieldOffset(0)] IntPtr l;

		public static JValue Zero = new JValue (IntPtr.Zero);

		public JValue (bool value)
		{
			this = new JValue ();
			z = value;
		}

		public JValue (sbyte value)
		{
			this = new JValue ();
			b = value;
		}

		public JValue (char value)
		{
			this = new JValue ();
			c = value;
		}

		public JValue (short value)
		{
			this = new JValue ();
			s = value;
		}

		public JValue (int value)
		{
			this = new JValue ();
			i = value;
		}

		public JValue (long value)
		{
			this = new JValue ();
			j = value;
		}

		public JValue (float value)
		{
			this = new JValue ();
			f = value;
		}

		public JValue (double value)
		{
			this = new JValue ();
			d = value;
		}

		public JValue (IntPtr value)
		{
			this = new JValue ();
			l = value;
		}

		public JValue (IJavaObject value)
		{
			this = new JValue ();
			l = value == null ? IntPtr.Zero : value.Handle;
		}
	}
}
#pragma warning restore
