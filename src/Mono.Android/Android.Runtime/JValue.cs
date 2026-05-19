using System;
using System.Runtime.InteropServices;

namespace Android.Runtime {

	/// <summary>
	/// Represents a JNI <c>jvalue</c> union, used to pass arguments to Java methods
	/// via JNI function calls.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Each constructor corresponds to one of the JNI primitive types or an object reference.
	/// See the <see href="https://docs.oracle.com/javase/8/docs/technotes/guides/jni/spec/types.html">JNI Type Specification</see>.
	/// </para>
	/// </remarks>
	[StructLayout(LayoutKind.Explicit)]
	public struct JValue {
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

		/// <summary>
		/// A <see cref="JValue"/> representing a JNI <c>NULL</c> object reference.
		/// </summary>
		public static JValue Zero = new JValue (IntPtr.Zero);

		/// <summary>
		/// Creates a <see cref="JValue"/> from a <see cref="bool"/> value,
		/// corresponding to the JNI <c>jboolean</c> type.
		/// </summary>
		/// <param name="value">The Boolean value.</param>
		public JValue (bool value)
		{
			this = new JValue ();
			z = value;
		}

		/// <summary>
		/// Creates a <see cref="JValue"/> from an <see cref="sbyte"/> value,
		/// corresponding to the JNI <c>jbyte</c> type.
		/// </summary>
		/// <param name="value">The signed byte value.</param>
		public JValue (sbyte value)
		{
			this = new JValue ();
			b = value;
		}

		/// <summary>
		/// Creates a <see cref="JValue"/> from a <see cref="char"/> value,
		/// corresponding to the JNI <c>jchar</c> type.
		/// </summary>
		/// <param name="value">The character value.</param>
		public JValue (char value)
		{
			this = new JValue ();
			c = value;
		}

		/// <summary>
		/// Creates a <see cref="JValue"/> from a <see cref="short"/> value,
		/// corresponding to the JNI <c>jshort</c> type.
		/// </summary>
		/// <param name="value">The short integer value.</param>
		public JValue (short value)
		{
			this = new JValue ();
			s = value;
		}

		/// <summary>
		/// Creates a <see cref="JValue"/> from an <see cref="int"/> value,
		/// corresponding to the JNI <c>jint</c> type.
		/// </summary>
		/// <param name="value">The integer value.</param>
		public JValue (int value)
		{
			this = new JValue ();
			i = value;
		}

		/// <summary>
		/// Creates a <see cref="JValue"/> from a <see cref="long"/> value,
		/// corresponding to the JNI <c>jlong</c> type.
		/// </summary>
		/// <param name="value">The long integer value.</param>
		public JValue (long value)
		{
			this = new JValue ();
			j = value;
		}

		/// <summary>
		/// Creates a <see cref="JValue"/> from a <see cref="float"/> value,
		/// corresponding to the JNI <c>jfloat</c> type.
		/// </summary>
		/// <param name="value">The single-precision floating-point value.</param>
		public JValue (float value)
		{
			this = new JValue ();
			f = value;
		}

		/// <summary>
		/// Creates a <see cref="JValue"/> from a <see cref="double"/> value,
		/// corresponding to the JNI <c>jdouble</c> type.
		/// </summary>
		/// <param name="value">The double-precision floating-point value.</param>
		public JValue (double value)
		{
			this = new JValue ();
			d = value;
		}

		/// <summary>
		/// Creates a <see cref="JValue"/> from an <see cref="IntPtr"/> value,
		/// corresponding to a JNI <c>jobject</c> reference.
		/// </summary>
		/// <param name="value">The raw JNI object reference handle.</param>
		public JValue (IntPtr value)
		{
			this = new JValue ();
			l = value;
		}

		/// <summary>
		/// Creates a <see cref="JValue"/> from an <see cref="IJavaObject"/> instance,
		/// corresponding to a JNI <c>jobject</c> reference.
		/// </summary>
		/// <param name="value">
		/// The Java object instance. If <see langword="null"/>, the resulting
		/// <see cref="JValue"/> will contain <see cref="IntPtr.Zero"/>.
		/// </param>
		/// <remarks>
		/// This constructor extracts the <see cref="IJavaObject.Handle"/> from the
		/// provided object, or passes <see cref="IntPtr.Zero"/> if the object is <see langword="null"/>.
		/// </remarks>
		public JValue (IJavaObject value)
		{
			this = new JValue ();
			l = value == null ? IntPtr.Zero : value.Handle;
		}
	}
}

