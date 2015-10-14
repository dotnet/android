using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Java.Interop;
using Java.Interop.SafeHandles;
using Java.Interop.IntPtrs;

using SafeEnv = Java.Interop.SafeHandles.JniEnvironment;
using IntPtrEnv = Java.Interop.IntPtrs.JniEnvironment;

namespace Java.Interop {
	public enum JniObjectReferenceType {
		Invalid     = 0,
		Local       = 1,
		Global      = 2,
		WeakGlobal  = 3,
	}

	public struct JniObjectReference
	{
		public  IntPtr                      Handle  {get; private set;}
		public  JniObjectReferenceType      Type    {get; private set;}

		public JniObjectReference (IntPtr handle, JniObjectReferenceType type = JniObjectReferenceType.Invalid)
		{
			Handle  = handle;
			Type    = type;
		}
	}

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

		public static JValue Zero = new JValue ((JniReferenceSafeHandle) null);

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
#if XA
		public JValue (IntPtr value)
		{
			this = new JValue ();
			l = value;
		}
#endif
		public JValue (JniObjectReference value)
		{
			this = new JValue ();
			l = value.Handle;
		}

		public JValue (JniReferenceSafeHandle value)
		{
			this = new JValue ();
			l = value == null ? IntPtr.Zero : value.DangerousGetHandle ();
		}

		public override string ToString ()
		{
			return string.Format ("Java.Interop.JValue(z={0},b={1},c={2},s={3},i={4},f={5},d={6},l=0x{7})",
					z, b, c, s, i, f, d, l.ToString ("x"));
		}
	}
	public abstract class JniFieldID : SafeHandle
	{
		internal JniFieldID ()
			: base (IntPtr.Zero, true)
		{
		}

		protected override bool ReleaseHandle ()
		{
			Console.WriteLine ("# {0}.ReleaseHandle()", GetType ().FullName);
			return true;
		}

		public override bool IsInvalid {
			get {
				return handle == IntPtr.Zero;
			}
		}

		public override string ToString ()
		{
			return string.Format ("{0}(0x{1})", GetType ().FullName, handle.ToString ("x"));
		}
	}

	public sealed class JniStaticFieldID : JniFieldID
	{
		JniStaticFieldID ()
		{
		}
	}
	public sealed class JniInstanceFieldID : JniFieldID
	{
		JniInstanceFieldID ()
		{
		}
	}
	public abstract class JniMethodID : SafeHandle
	{
		internal JniMethodID ()
			: base (IntPtr.Zero, true)
		{
		}

		protected override bool ReleaseHandle ()
		{
			Console.WriteLine ("# {0}.ReleaseHandle()", GetType ().FullName);
			return true;
		}

		public override bool IsInvalid {
			get {
				return handle == IntPtr.Zero;
			}
		}

		public override string ToString ()
		{
			return string.Format ("{0}(0x{1})", GetType ().FullName, handle.ToString ("x"));
		}
	}
	public sealed class JniStaticMethodID : JniMethodID
	{
		JniStaticMethodID ()
		{
		}
	}
	public sealed class JniInstanceMethodID : JniMethodID
	{
		JniInstanceMethodID ()
		{
		}
	}
	public struct JniNativeMethodRegistration {

		public  string      Name;
		public  string      Signature;
		public  Delegate    Marshaler;

		public JniNativeMethodRegistration (string name, string signature, Delegate marshaler)
		{
			Name        = name;
			Signature   = signature;
			Marshaler   = marshaler;
		}
	}

	public abstract class JniReferenceSafeHandle : SafeHandle
	{
		protected JniReferenceSafeHandle ()
			: this (ownsHandle:true)
		{
		}

		internal JniReferenceSafeHandle (bool ownsHandle)
			: base (IntPtr.Zero, ownsHandle)
		{
		}

		public override bool IsInvalid {
			get {return base.handle == IntPtr.Zero;}
		}

		public JniObjectReferenceType ReferenceType {
			get {
				if (IsInvalid)
					throw new ObjectDisposedException (GetType ().FullName);
				return SafeHandles.JniEnvironment.Handles.GetObjectRefType (this);
			}
		}

		internal IntPtr _GetAndClearHandle ()
		{
			var h   = handle;
			handle  = IntPtr.Zero;
			return h;
		}

		public override string ToString ()
		{
			return string.Format ("{0}(0x{1})", GetType ().FullName, handle.ToString ("x"));
		}
	}
	public class JniLocalReference : JniReferenceSafeHandle {

		internal JniLocalReference ()
		{
		}

		protected override bool ReleaseHandle ()
		{
			Console.WriteLine ("# {0}.ReleaseHandle()", GetType ().FullName);
			SafeHandles.JniEnvironment.Handles.DeleteLocalRef (handle);
			return true;
		}
	}
	public class JniWeakGlobalReference : JniReferenceSafeHandle {
		protected override bool ReleaseHandle ()
		{
			Console.WriteLine ("# {0}.ReleaseHandle()", GetType ().FullName);
			SafeHandles.JniEnvironment.Handles.DeleteWeakGlobalRef (handle);
			return true;
		}
	}
	public class JniGlobalReference : JniReferenceSafeHandle {
		protected override bool ReleaseHandle ()
		{
			Console.WriteLine ("# {0}.ReleaseHandle()", GetType ().FullName);
			SafeHandles.JniEnvironment.Handles.DeleteGlobalRef (handle);
			return true;
		}
	}
	public sealed class JniEnvironmentSafeHandle : SafeHandle
	{
		JniEnvironmentSafeHandle ()
			: base (IntPtr.Zero, ownsHandle:false)
		{
		}

		public JniEnvironmentSafeHandle (IntPtr handle)
			: this ()
		{
			SetHandle (handle);
		}

		public override bool IsInvalid {
			get {return handle == IntPtr.Zero;}
		}

		protected override bool ReleaseHandle ()
		{
			Console.WriteLine ("# {0}.ReleaseHandle()", GetType ().FullName);
			return false;
		}

		internal unsafe SafeHandles.JniEnvironmentInvoker CreateInvoker ()
		{
			IntPtr p = Marshal.ReadIntPtr (handle);
			return new SafeHandles.JniEnvironmentInvoker ((JniNativeInterfaceStruct*) p);
		}

		public override string ToString ()
		{
			return string.Format ("{0}(0x{1})", GetType ().FullName, handle.ToString ("x"));
		}
	}
	public sealed class JavaVMSafeHandle : SafeHandle {

		JavaVMSafeHandle ()
			: base (IntPtr.Zero, ownsHandle:false)
		{
		}

		public JavaVMSafeHandle (IntPtr handle)
			: this ()
		{
			SetHandle (handle);
		}

		public override bool IsInvalid {
			get {return handle == IntPtr.Zero;}
		}

		internal IntPtr Handle {
			get {return base.handle;}
		}

		protected override bool ReleaseHandle ()
		{
			Console.WriteLine ("# {0}.ReleaseHandle()", GetType ().FullName);
			return false;
		}

		public override string ToString ()
		{
			return string.Format ("{0}(0x{1})", GetType ().FullName, handle.ToString ("x"));
		}
	}
	struct JavaVMInitArgs {
		public  JniVersion                      version;    /*				 use JNI_VERSION_1_2 or later */

		public  int                             nOptions;
		public  IntPtr /* JavaVMOption[] */     options;
		public  byte                            ignoreUnrecognized;
	}

	struct JavaVMOption {
		public  IntPtr /* const char* */    optionString;
		public  IntPtr /* void * */         extraInfo;
	}
	public enum JniVersion {
		// v1_1    = 0x00010001,
		v1_2    = 0x00010002,
		v1_4    = 0x00010004,
		v1_6	= 0x00010006,
	}
}

namespace Java.Interop.SafeHandles {
	public partial class JniEnvironment {
		public static JniEnvironment Current;

		internal JniEnvironmentInvoker Invoker;
		public JniEnvironmentSafeHandle SafeHandle;

		public unsafe JniEnvironment (IntPtr v) {
			Current = this;
			SafeHandle = new JniEnvironmentSafeHandle (v);
			IntPtr p = Marshal.ReadIntPtr (v);
			Invoker = new JniEnvironmentInvoker ((JniNativeInterfaceStruct*) p);
		}
		internal void LogCreateLocalRef (JniLocalReference value)
		{
		}
		public Exception GetExceptionForLastThrowable ()
		{
			var v = SafeEnv.Errors.ExceptionOccurred ();
			if (v == null || v.IsInvalid)
				return null;
			SafeEnv.Errors.ExceptionClear ();
			LogCreateLocalRef (v);
			v.Dispose ();
			return new Exception ("yada yada yada");
		}
	}
}

namespace Java.Interop.IntPtrs {
	public partial class JniEnvironment {
		public static JniEnvironment Current;

		internal JniEnvironmentInvoker Invoker;
		public IntPtr SafeHandle;

		public unsafe JniEnvironment (IntPtr v) {
			Current = this;
			SafeHandle = v;
			IntPtr p = Marshal.ReadIntPtr (v);
			Invoker = new JniEnvironmentInvoker ((JniNativeInterfaceStruct*) p);
		}
		internal void LogCreateLocalRef (IntPtr value)
		{
		}
		public Exception GetExceptionForLastThrowable ()
		{
			var v = IntPtrEnv.Errors.ExceptionOccurred ();
			#if XA
			var h = v;
			#else
			var h = v.Handle;
			#endif
			if (h == IntPtr.Zero)
				return null;
			SafeEnv.Errors.ExceptionClear ();
			LogCreateLocalRef (h);
			IntPtrEnv.Handles.DeleteLocalRef (h);
			return new Exception ("yada yada yada");
		}
	}
}

class App {
	const string LibraryName = "jvm.dll";

	[DllImport (LibraryName)]
	static extern int JNI_CreateJavaVM (out IntPtr javavm, out IntPtr jnienv, ref JavaVMInitArgs args);

	public static void Main ()
	{
		IntPtr _jvm, _env;
		CreateJavaVM (out _jvm, out _env);
		Console.WriteLine ("# _jvm: {0}", _jvm.ToString ("x"));
		Console.WriteLine ("# _env: {0}", _env.ToString ("x"));

		SafeTiming (_env);
		IntPtrTiming (_env);
	}

	static void CreateJavaVM (out IntPtr jvm, out IntPtr jnienv)
	{
		var args = new JavaVMInitArgs () {
			version             = JniVersion.v1_6,
			nOptions            = 0,
			ignoreUnrecognized  = (byte) 1,
		};
		int r = JNI_CreateJavaVM (out jvm, out jnienv, ref args);
		if (r != 0)
			throw new InvalidOperationException ("JNI_CreateJavaVM returned: " + r);
	}

	const int C = 10000000;

	static unsafe void SafeTiming (IntPtr _env)
	{
		var se = new SafeEnv (_env);
		var Arrays_class = SafeEnv.Types.FindClass ("java/util/Arrays");
		var Arrays_binarySearch = SafeEnv.Members.GetStaticMethodID (Arrays_class, "binarySearch", "([II)I");
		var intArray = SafeEnv.Arrays.NewIntArray (3);
		fixed (int* p = new int[]{1,2,3})
			SafeEnv.Arrays.SetIntArrayRegion (intArray, 0, 3, (IntPtr) p);

		var t = Stopwatch.StartNew ();
		var args = stackalloc JValue [2];
		args [0] = new JValue (intArray);
		args [1] = new JValue (2);
		for (int i = 0; i < C; ++i) {
			if (SafeEnv.Current.SafeHandle.DangerousGetHandle () == IntPtr.Zero)
				Console.WriteLine("wat?!");
			int r = SafeEnv.Members.CallStaticIntMethod (Arrays_class, Arrays_binarySearch, args);
		}
		t.Stop ();
		Console.WriteLine ("# SafeHandle timing: {0}", t.Elapsed);
		Console.WriteLine ("#\tAverage Invocation: {0}ms", t.Elapsed.TotalMilliseconds / C);
		GC.KeepAlive (se);
		GC.KeepAlive (intArray);
		GC.KeepAlive (Arrays_class);
		GC.KeepAlive (Arrays_binarySearch);
	}

	static unsafe void IntPtrTiming (IntPtr _env)
	{
		var pe = new IntPtrEnv (_env);
		var Arrays_class = IntPtrEnv.Types.FindClass ("java/util/Arrays");
		var Arrays_binarySearch = IntPtrEnv.Members.GetStaticMethodID (Arrays_class, "binarySearch", "([II)I");
		var intArray = IntPtrEnv.Arrays.NewIntArray (3);
		fixed (int* p = new int[]{1,2,3})
			IntPtrEnv.Arrays.SetIntArrayRegion (intArray, 0, 3, (IntPtr) p);

		var t = Stopwatch.StartNew ();
		var args = stackalloc JValue [2];
		args [0] = new JValue (intArray);
		args [1] = new JValue (2);
		for (int i = 0; i < C; ++i) {
			int r = IntPtrEnv.Members.CallStaticIntMethod (Arrays_class, Arrays_binarySearch, args);
		}
		t.Stop ();
		Console.WriteLine ("# JniObjectReference timing: {0}", t.Elapsed);
		Console.WriteLine ("#\tAverage Invocation: {0}ms", t.Elapsed.TotalMilliseconds / C);
	}
}
