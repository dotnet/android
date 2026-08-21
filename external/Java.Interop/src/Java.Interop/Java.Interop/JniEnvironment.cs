#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Java.Interop {

	public static partial class JniEnvironment {

		internal    static  readonly    ThreadLocal<JniEnvironmentInfo>     Info    = new ThreadLocal<JniEnvironmentInfo> (() => new JniEnvironmentInfo (), trackAllValues: true);

		internal    static  JniEnvironmentInfo      CurrentInfo {
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			get {
				var e = Info.Value!;
				if (!e.IsValid)
					throw new NotSupportedException ("JNI Environment Information has been invalidated on this thread.");
				return e;
			}
		}

		public      static  JniRuntime              Runtime {
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			get {return CurrentInfo.Runtime;}
		}

		public      static  IntPtr                  EnvironmentPointer {
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			get {return CurrentInfo.EnvironmentPointer;}
		}

		public      static  JniVersion              JniVersion {
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			get {return (JniVersion) Versions.GetVersion ();}
		}

		public      static  int                     LocalReferenceCount {
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			get {return CurrentInfo.LocalReferenceCount;}
		}

		public      static  bool                    WithinNewObjectScope {
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			get {return CurrentInfo.WithinNewObjectScope;}
			internal set {CurrentInfo.WithinNewObjectScope = value;}
		}

		[global::System.Diagnostics.CodeAnalysis.SuppressMessage (
			"Design",
			"CA1031:Do not catch general exception types",
			Justification = "Exceptions cannot cross a JNI boundary.")]
		public static bool BeginMarshalMethod (IntPtr jnienv, out JniTransition transition, [NotNullWhen (true)] out JniRuntime? runtime)
		{
			runtime = null;
			Exception?          ex  = null;
			try {
				runtime = Info.Value?.Runtime;
			}
			catch (Exception e) {
				ex  = e;
			}
			if (runtime == null || ex != null) {
				transition  = default;
				runtime     = null;
				Console.Error.WriteLine ("JNI Environment Information is not available on this thread.");
				if (ex != null) {
					Console.Error.WriteLine (ex);
				}
				return false;
			}

			try {
				runtime.OnEnterMarshalMethod ();
				transition  = new JniTransition (jnienv);
			}
			catch (Exception e) {
				runtime     = null;
				transition  = default;

				Console.Error.WriteLine ($"OnEnterMarshalMethod failed: {e}");
				return false;
			}

			return true;
		}

		public static void EndMarshalMethod (ref JniTransition transition)
		{
			transition.Dispose ();
		}

		internal    static  void    SetEnvironmentPointer (IntPtr environmentPointer)
		{
			CurrentInfo.EnvironmentPointer  = environmentPointer;
		}

		internal    static  void    SetEnvironmentPointer (IntPtr environmentPointer, JniRuntime runtime)
		{
			if (!Info.IsValueCreated) {
				Info.Value = new JniEnvironmentInfo (environmentPointer, runtime);
				return;
			}
			CurrentInfo.EnvironmentPointer  = environmentPointer;
		}

		internal    static  void    SetEnvironmentInfo (JniEnvironmentInfo info)
		{
			Info.Value  = info;
		}

		internal    static  Exception?  GetExceptionForLastThrowable ()
		{
			var e   = JniEnvironment.Exceptions.ExceptionOccurred ();
			if (!e.IsValid)
				return null;
			// JniEnvironment.Errors.ExceptionDescribe ();
			JniEnvironment.Exceptions.ExceptionClear ();
			JniEnvironment.LogCreateLocalRef (e);
			return Runtime.GetExceptionForThrowable (ref e, JniObjectReferenceOptions.CopyAndDispose);
		}

		internal    static  Exception?  GetExceptionForLastThrowable (IntPtr thrown)
		{
			if (thrown == IntPtr.Zero)
				return null;
			var e   = new JniObjectReference (thrown, JniObjectReferenceType.Local);
			// JniEnvironment.Errors.ExceptionDescribe ();
			JniEnvironment.Exceptions.ExceptionClear ();
			JniEnvironment.LogCreateLocalRef (e);
			return Runtime.GetExceptionForThrowable (ref e, JniObjectReferenceOptions.CopyAndDispose);
		}

		internal    static  Exception   CreateObjectDisposedException (IJavaPeerable value)
		{
			return new ObjectDisposedException (value.GetType ().FullName,
					$"Cannot access disposed object with JniIdentityHashCode={value.JniIdentityHashCode}.");
		}

		internal    static  void        LogCreateLocalRef (JniObjectReference value)
		{
			if (!value.IsValid)
				return;
			Runtime.ObjectReferenceManager.CreatedLocalReference (CurrentInfo, value);
		}

		internal    static  void    LogCreateLocalRef (IntPtr value)
		{
			if (value == IntPtr.Zero)
				return;
			var r = new JniObjectReference (value, JniObjectReferenceType.Local);
			LogCreateLocalRef (r);
		}

		partial class References {

			internal static unsafe int GetJavaVM (IntPtr jnienv, out IntPtr vm)
			{
				IntPtr _vm;
				int r       = JniNativeMethods.GetJavaVM (jnienv, &_vm);
				vm          = _vm;
				return r;
			}

			internal static void RawDeleteLocalRef (IntPtr env, IntPtr localRef)
			{
				JniNativeMethods.DeleteLocalRef (env, localRef);
			}
		}

	}

	sealed class JniEnvironmentInfo : IDisposable {

		const   int             NameBufferLength        = 512;

		IntPtr                  environmentPointer;
		char[]?                 nameBuffer;
		bool                    disposed;
		JniRuntime?             runtime;

		public      int                     LocalReferenceCount     {get; internal set;}
		public      bool                    WithinNewObjectScope    {get; set;}
		public      JniRuntime              Runtime {
			get => runtime ?? throw new NotSupportedException ();
			private set => runtime = value;
		}

		public IntPtr                  EnvironmentPointer {
			get {return environmentPointer;}
			set {
				if (disposed)
					throw new ObjectDisposedException (nameof (JniEnvironmentInfo));
				if (environmentPointer == value)
					return;

				environmentPointer  = value;
				IntPtr  vmh = IntPtr.Zero;
				int     r   = JniEnvironment.References.GetJavaVM (EnvironmentPointer, out vmh);
				if (r < 0)
					throw new InvalidOperationException ("JNIEnv::GetJavaVM() returned: " + r.ToString ());

				var vm = JniRuntime.GetRegisteredRuntime (vmh);
				if (vm == null)
					throw new NotSupportedException (
							string.Format ("No JavaVM registered with handle 0x{0}.",
								vmh.ToString ("x")));
				Runtime = vm;
			}
		}

		public      bool                    IsValid {
			get {return Runtime != null && environmentPointer != IntPtr.Zero;}
		}

		public JniEnvironmentInfo ()
		{
			Runtime             = JniRuntime.CurrentRuntime;
			EnvironmentPointer  = Runtime._AttachCurrentThread ();
		}

		internal    JniEnvironmentInfo (IntPtr environmentPointer, JniRuntime runtime)
		{
			EnvironmentPointer  = environmentPointer;
			Runtime             = runtime;
		}

		internal unsafe JniObjectReference ToJavaName (string jniTypeName)
		{
			int index = jniTypeName.IndexOf ("/", StringComparison.Ordinal);

			if (index == -1)
				return JniEnvironment.Strings.NewString (jniTypeName);

			int length = jniTypeName.Length;
			if (length > NameBufferLength)
				return JniEnvironment.Strings.NewString (jniTypeName.Replace ('/', '.'));

			if (nameBuffer == null)
				nameBuffer = new char [NameBufferLength];

			fixed (char* src = jniTypeName, dst = nameBuffer) {
				char* src_ptr = src;
				char* dst_ptr = dst;
				char* end_ptr = src + length;
				while (src_ptr < end_ptr) {
					*dst_ptr = (*src_ptr == '/') ? '.' : *src_ptr;
					src_ptr++;
					dst_ptr++;
				}
				return JniEnvironment.Strings.NewString (dst, length);
			}
		}

		public void Dispose ()
		{
			if (disposed)
				return;
			runtime                 = null;
			environmentPointer      = IntPtr.Zero;
			nameBuffer              = null;
			LocalReferenceCount     = 0;
			disposed                = true;
		}

	}
}
