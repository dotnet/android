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

		/// <remarks>
		/// This constructor stores addresses without reading their memory or invoking the callback.
		/// When registering this descriptor, <paramref name="name"/> and <paramref name="signature"/>
		/// must address readable NUL-terminated UTF-8 memory through registration. <paramref name="functionPointer"/>
		/// must have the JNI-compatible ABI for the signature, including the environment and
		/// receiver arguments, and remain callable and rooted for as long as JNI can invoke it,
		/// including after failed or partial registration. Prevent new calls to the registered
		/// native methods before unregistration. Release callback storage only after
		/// unregistration succeeds and all callbacks already running have finished.
		/// </remarks>
		public JniNativeMethod (byte* name, byte* signature, IntPtr functionPointer)
		{
			this.name = name;
			this.signature = signature;
			this.functionPointer = functionPointer;
		}
	}
}
