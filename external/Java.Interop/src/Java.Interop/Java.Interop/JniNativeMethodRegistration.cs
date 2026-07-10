#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Threading;

using Java.Interop;

namespace Java.Interop {

	public struct JniNativeMethodRegistration {

		public  string      Name;
		public  string      Signature;
		public  Delegate    Marshaler;

		public JniNativeMethodRegistration (string name, string signature, Delegate marshaler)
		{
			Name        = name      ?? throw new ArgumentNullException (nameof (name));
			Signature   = signature ?? throw new ArgumentNullException (nameof (signature));
			Marshaler   = marshaler ?? throw new ArgumentNullException (nameof (marshaler));
		}
	}

	/// <summary>
	/// Blittable JNI native method registration for use with raw function pointers.
	/// Layout matches JNI's <c>JNINativeMethod</c> struct exactly.
	/// </summary>
	[StructLayout (LayoutKind.Sequential)]
	public unsafe struct JniNativeMethod
	{
		byte* name;
		byte* signature;
		IntPtr functionPointer;

		public JniNativeMethod (byte* name, byte* signature, IntPtr functionPointer)
		{
			this.name = name;
			this.signature = signature;
			this.functionPointer = functionPointer;
		}
	}
}
