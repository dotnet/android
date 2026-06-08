using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Java.Interop;

using JIIntPtrEnv   = Java.Interop.JIIntPtrs.JniEnvironment;
using PinvokeEnv    = Java.Interop.JIPinvokes.JniEnvironment;
using XAIntPtrEnv   = Java.Interop.XAIntPtrs.JniEnvironment;
#if NET
using JIFuncPtrEnv  = Java.Interop.JIFunctionPointers.JniEnvironment;
#endif  // NET

public class XFieldInfo
{
	public IntPtr ID;
	public bool   IsStatic;
	public bool IsValid {get {return ID != IntPtr.Zero;}}
	public XFieldInfo (string name, string signature, IntPtr id, bool isStatic)
	{
		ID = id;
		IsStatic = isStatic;
	}
	public override string ToString ()
	{
		return string.Format ("{0}(0x{1})", GetType ().FullName, ID.ToString ("x"));
	}
}

public class XMethodInfo
{
	public IntPtr ID;
	public bool   IsStatic;
	public bool IsValid {get {return ID != IntPtr.Zero;}}
	public XMethodInfo (string name, string signature, IntPtr id, bool isStatic)
	{
		ID = id;
		IsStatic = isStatic;
	}
	public override string ToString ()
	{
		return string.Format ("{0}(0x{1})", GetType ().FullName, ID.ToString ("x"));
	}
}

namespace Java.Interop.JIIntPtrs {
	public class JniFieldInfo : XFieldInfo {
		public JniFieldInfo (string name, string signature, IntPtr id, bool isStatic)
			: base (name, signature, id, isStatic)
		{
		}
	}
	public class JniMethodInfo : XMethodInfo {
		public JniMethodInfo (string name, string signature, IntPtr id, bool isStatic)
			: base (name, signature, id, isStatic)
		{
		}
	}
	public struct JniObjectReference
	{
		public  IntPtr                      Handle  {get; private set;}
		public  JniObjectReferenceType      Type    {get; private set;}
		public  bool                        IsValid {
			get {
				return Handle != IntPtr.Zero;
			}
		}

		public JniObjectReference (IntPtr handle, JniObjectReferenceType type = JniObjectReferenceType.Invalid)
		{
			Handle  = handle;
			Type    = type;
		}
	}
	class JniEnvironmentInfo {
		public IntPtr EnvironmentPointer;
		public JniEnvironmentInvoker Invoker;
	}
	public static partial class JniEnvironment {
		internal static JniEnvironmentInfo CurrentInfo;

		internal static void LogCreateLocalRef (IntPtr value)
		{
		}
		public static Exception GetExceptionForLastThrowable ()
		{
			var v = JIIntPtrEnv.Exceptions.ExceptionOccurred ();
			var h = v.Handle;
			if (h == IntPtr.Zero)
				return null;
			JIIntPtrEnv.Exceptions.ExceptionClear ();
			LogCreateLocalRef (h);
			JIIntPtrEnv.References.DeleteLocalRef (h);
			return new Exception ("yada yada yada");
		}
	}
}

namespace Java.Interop.JIPinvokes {
	public class JniFieldInfo : XFieldInfo {
		public JniFieldInfo (string name, string signature, IntPtr id, bool isStatic)
			: base (name, signature, id, isStatic)
		{
		}
	}
	public class JniMethodInfo : XMethodInfo {
		public JniMethodInfo (string name, string signature, IntPtr id, bool isStatic)
			: base (name, signature, id, isStatic)
		{
		}
	}
	public struct JniObjectReference
	{
		public  IntPtr                      Handle  {get; private set;}
		public  JniObjectReferenceType      Type    {get; private set;}
		public  bool                        IsValid {
			get {
				return Handle != IntPtr.Zero;
			}
		}

		public JniObjectReference (IntPtr handle, JniObjectReferenceType type = JniObjectReferenceType.Invalid)
		{
			Handle  = handle;
			Type    = type;
		}
	}
	public static partial class JniEnvironment {
		public static IntPtr EnvironmentPointer;

		internal static void LogCreateLocalRef (IntPtr value)
		{
		}
		public static Exception GetExceptionForLastThrowable (IntPtr h)
		{
			if (h == IntPtr.Zero)
				return null;
			PinvokeEnv.Exceptions.ExceptionClear ();
			LogCreateLocalRef (h);
			var r = new JniObjectReference (h, JniObjectReferenceType.Local);
			PinvokeEnv.References.DeleteLocalRef (r.Handle);
			return new Exception ("yada yada yada");
		}
		public static Exception GetExceptionForLastThrowable ()
		{
			var v = JIIntPtrEnv.Exceptions.ExceptionOccurred ();
			var h = v.Handle;
			if (h == IntPtr.Zero)
				return null;
			PinvokeEnv.Exceptions.ExceptionClear ();
			LogCreateLocalRef (h);
			PinvokeEnv.References.DeleteLocalRef (h);
			return new Exception ("yada yada yada");
		}
	}
}
namespace Java.Interop.XAIntPtrs {
	public class JniFieldInfo : XFieldInfo {
		public JniFieldInfo (string name, string signature, IntPtr id, bool isStatic)
			: base (name, signature, id, isStatic)
		{
		}
	}
	public class JniMethodInfo : XMethodInfo {
		public JniMethodInfo (string name, string signature, IntPtr id, bool isStatic)
			: base (name, signature, id, isStatic)
		{
		}
	}
	public struct JniObjectReference
	{
		public  IntPtr                      Handle  {get; private set;}
		public  JniObjectReferenceType      Type    {get; private set;}
		public  bool                        IsValid {
			get {
				return Handle != IntPtr.Zero;
			}
		}

		public JniObjectReference (IntPtr handle, JniObjectReferenceType type = JniObjectReferenceType.Invalid)
		{
			Handle  = handle;
			Type    = type;
		}
	}
	class JniEnvironmentInfo {
		public IntPtr EnvironmentPointer;
		public JniEnvironmentInvoker Invoker;
	}
	public partial class JniEnvironment {
		internal static JniEnvironmentInfo CurrentInfo;

		internal static void LogCreateLocalRef (IntPtr value)
		{
		}
		public static Exception GetExceptionForLastThrowable ()
		{
			var h = XAIntPtrEnv.Exceptions.ExceptionOccurred ();
			if (h == IntPtr.Zero)
				return null;
			XAIntPtrEnv.Exceptions.ExceptionClear ();
			LogCreateLocalRef (h);
			XAIntPtrEnv.References.DeleteLocalRef (h);
			return new Exception ("yada yada yada");
		}
	}
}

#if NET
namespace Java.Interop.JIFunctionPointers {
	public class JniFieldInfo : XFieldInfo {
		public JniFieldInfo (string name, string signature, IntPtr id, bool isStatic)
			: base (name, signature, id, isStatic)
		{
		}
	}
	public class JniMethodInfo : XMethodInfo {
		public JniMethodInfo (string name, string signature, IntPtr id, bool isStatic)
			: base (name, signature, id, isStatic)
		{
		}
	}
	public struct JniObjectReference
	{
		public  IntPtr                      Handle  {get; private set;}
		public  JniObjectReferenceType      Type    {get; private set;}
		public  bool                        IsValid {
			get {
				return Handle != IntPtr.Zero;
			}
		}

		public JniObjectReference (IntPtr handle, JniObjectReferenceType type = JniObjectReferenceType.Invalid)
		{
			Handle  = handle;
			Type    = type;
		}
	}
	public static partial class JniEnvironment {
		public static IntPtr EnvironmentPointer;

		internal static void LogCreateLocalRef (IntPtr value)
		{
		}
		public static Exception GetExceptionForLastThrowable (IntPtr h)
		{
			if (h == IntPtr.Zero)
				return null;
			JIFuncPtrEnv.Exceptions.ExceptionClear ();
			LogCreateLocalRef (h);
			var r = new JniObjectReference (h, JniObjectReferenceType.Local);
			JIFuncPtrEnv.References.DeleteLocalRef (r.Handle);
			return new Exception ("yada yada yada");
		}
		public static Exception GetExceptionForLastThrowable ()
		{
			var v = JIIntPtrEnv.Exceptions.ExceptionOccurred ();
			var h = v.Handle;
			if (h == IntPtr.Zero)
				return null;
			JIFuncPtrEnv.Exceptions.ExceptionClear ();
			LogCreateLocalRef (h);
			JIFuncPtrEnv.References.DeleteLocalRef (h);
			return new Exception ("yada yada yada");
		}
	}
}
#endif  // NET

class App {

	public static void Main ()
	{
		var path = Environment.GetEnvironmentVariable ("JI_JVM_PATH");
		if (string.IsNullOrEmpty (path)) {
			Console.Error.WriteLine ($"error: must set `JI_JVM_PATH` environment variable to path of JVM library to use.");
			return;
		}
		var runtimeOptions  = new JreRuntimeOptions (){
			JvmLibraryPath          = Environment.GetEnvironmentVariable ("JI_JVM_PATH"),
		};
		var GlobalRuntime   = runtimeOptions.CreateJreVM ();
		IntPtr _env         = global::Java.Interop.JniEnvironment.EnvironmentPointer;

		XAIntPtrTiming (_env);
		JIIntPtrTiming (_env);
		JIPinvokeTiming (_env);
		JIFunctionPointersTiming (_env);

		GlobalRuntime.Dispose ();
	}

	const int C = 10000000;

	static unsafe void JIIntPtrTiming (IntPtr _env)
	{
		JIIntPtrEnv.CurrentInfo = new Java.Interop.JIIntPtrs.JniEnvironmentInfo {
			EnvironmentPointer  = _env,
			Invoker             = new Java.Interop.JIIntPtrs.JniEnvironmentInvoker ((JniNativeInterfaceStruct*) Marshal.ReadIntPtr (_env)),
		};
		var Arrays_class = JIIntPtrEnv.Types._FindClass ("java/util/Arrays");
		var Arrays_binarySearch = JIIntPtrEnv.StaticMethods.GetStaticMethodID (Arrays_class, "binarySearch", "([II)I");
		var intArray = JIIntPtrEnv.Arrays.NewIntArray (3);
		fixed (int* p = new int[]{1,2,3})
			JIIntPtrEnv.Arrays.SetIntArrayRegion (intArray, 0, 3, p);

		var t = Stopwatch.StartNew ();
		var args = stackalloc JniArgumentValue [2];
		args [0] = new JniArgumentValue (intArray.Handle);
		args [1] = new JniArgumentValue (2);
		for (int i = 0; i < C; ++i) {
			int r = JIIntPtrEnv.StaticMethods.CallStaticIntMethod (Arrays_class, Arrays_binarySearch, args);
		}
		t.Stop ();
		Console.WriteLine ("# {0} timing: {1}", nameof (JIIntPtrTiming), t.Elapsed);
		Console.WriteLine ("#\tAverage Invocation: {0}ms", t.Elapsed.TotalMilliseconds / C);
	}

	static unsafe void JIPinvokeTiming (IntPtr _env)
	{
		PinvokeEnv.EnvironmentPointer = _env;
		var Arrays_class = PinvokeEnv.Types._FindClass ("java/util/Arrays");
		var Arrays_binarySearch = PinvokeEnv.StaticMethods.GetStaticMethodID (Arrays_class, "binarySearch", "([II)I");
		var intArray = PinvokeEnv.Arrays.NewIntArray (3);
		fixed (int* p = new int[]{1,2,3})
			PinvokeEnv.Arrays.SetIntArrayRegion (intArray, 0, 3, p);

		var t = Stopwatch.StartNew ();
		var args = stackalloc JniArgumentValue [2];
		args [0] = new JniArgumentValue (intArray.Handle);
		args [1] = new JniArgumentValue (2);
		for (int i = 0; i < C; ++i) {
			int r = PinvokeEnv.StaticMethods.CallStaticIntMethod (Arrays_class, Arrays_binarySearch, args);
		}
		t.Stop ();
		Console.WriteLine ("# {0} timing: {1}", nameof (JIPinvokeTiming), t.Elapsed);
		Console.WriteLine ("#\tAverage Invocation: {0}ms", t.Elapsed.TotalMilliseconds / C);
	}

	static unsafe void XAIntPtrTiming (IntPtr _env)
	{
		XAIntPtrEnv.CurrentInfo = new Java.Interop.XAIntPtrs.JniEnvironmentInfo {
			EnvironmentPointer  = _env,
			Invoker             = new Java.Interop.XAIntPtrs.JniEnvironmentInvoker ((JniNativeInterfaceStruct*) Marshal.ReadIntPtr (_env)),
		};
		var Arrays_class = XAIntPtrEnv.Types._FindClass ("java/util/Arrays");
		var Arrays_binarySearch = XAIntPtrEnv.StaticMethods.GetStaticMethodID (Arrays_class, "binarySearch", "([II)I");
		var intArray = XAIntPtrEnv.Arrays.NewIntArray (3);
		fixed (int* p = new int[]{1,2,3})
			XAIntPtrEnv.Arrays.SetIntArrayRegion (intArray, 0, 3, p);

		var t = Stopwatch.StartNew ();
		var args = stackalloc JniArgumentValue [2];
		args [0] = new JniArgumentValue (intArray);
		args [1] = new JniArgumentValue (2);
		for (int i = 0; i < C; ++i) {
			int r = XAIntPtrEnv.StaticMethods.CallStaticIntMethod (Arrays_class, Arrays_binarySearch, args);
		}
		t.Stop ();
		Console.WriteLine ("# {0} timing: {1}", nameof (XAIntPtrTiming), t.Elapsed);
		Console.WriteLine ("#\tAverage Invocation: {0}ms", t.Elapsed.TotalMilliseconds / C);
	}

	static unsafe void JIFunctionPointersTiming (IntPtr _env)
	{
#if NET
		JIFuncPtrEnv.EnvironmentPointer = _env;
		var Arrays_class = JIFuncPtrEnv.Types._FindClass ("java/util/Arrays");
		var Arrays_binarySearch = JIFuncPtrEnv.StaticMethods.GetStaticMethodID (Arrays_class, "binarySearch", "([II)I");
		var intArray = JIFuncPtrEnv.Arrays.NewIntArray (3);
		fixed (int* p = new int[]{1,2,3})
			JIFuncPtrEnv.Arrays.SetIntArrayRegion (intArray, 0, 3, p);

		var t = Stopwatch.StartNew ();
		var args = stackalloc JniArgumentValue [2];
		args [0] = new JniArgumentValue (intArray.Handle);
		args [1] = new JniArgumentValue (2);
		for (int i = 0; i < C; ++i) {
			int r = JIFuncPtrEnv.StaticMethods.CallStaticIntMethod (Arrays_class, Arrays_binarySearch, args);
		}
		t.Stop ();
		Console.WriteLine ("# {0} timing: {1}", nameof (JIFunctionPointersTiming), t.Elapsed);
		Console.WriteLine ("#\tAverage Invocation: {0}ms", t.Elapsed.TotalMilliseconds / C);
#endif  // NET
	}
}
