// Originally from: https://github.com/dotnet/java-interop/blob/dd3c1d0514addfe379f050627b3e97493e985da6/src/Java.Runtime.Environment/Java.Interop/JreRuntime.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Android.Runtime;

namespace Java.Interop {

	struct JavaVMInitArgs {
		#pragma warning disable CS0649 // Field is never assigned to;
		public  JniVersion                      version;    /*				 use JNI_VERSION_1_2 or later */

		public  int                             nOptions;
		public  IntPtr /* JavaVMOption[] */     options;
		public  byte                            ignoreUnrecognized;
		#pragma warning restore CS0649
	}

	class NativeAotRuntimeOptions : JniRuntime.CreationOptions {

		public  bool        IgnoreUnrecognizedOptions   {get; set;}

		public NativeAotRuntimeOptions ()
		{
			JniVersion  = JniVersion.v1_2;
		}

		/// <safety>
		/// This options instance must contain a live VM and/or current-thread environment with the JNI ABI.
		/// The VM must outlive the runtime, and any class-loader reference must remain live during initialization.
		/// </safety>
		public unsafe JreRuntime CreateJreVM ()
		{
			unsafe {
				// SAFETY: the caller retains the supplied VM, thread environment and loader through runtime initialization.
				return new JreRuntime (this);
			}
		}
	}

	class JreRuntime : JniRuntime
	{
		static JreRuntime ()
		{
		}

		static NativeAotRuntimeOptions CreateJreVM (NativeAotRuntimeOptions builder)
		{
			if (builder == null)
				throw new ArgumentNullException ("builder");
			if (builder.InvocationPointer == IntPtr.Zero &&
					builder.EnvironmentPointer == IntPtr.Zero &&
					string.IsNullOrEmpty (builder.JvmLibraryPath))
				throw new InvalidOperationException ($"Member `{nameof (NativeAotRuntimeOptions)}.{nameof (NativeAotRuntimeOptions.JvmLibraryPath)}` must be set.");

			builder.TypeManager     ??= CreateDefaultTypeManager ();

			builder.ValueManager            ??= CreateDefaultValueManager ();
			builder.ObjectReferenceManager  ??= new Android.Runtime.ManagedObjectReferenceManager ();

			if (builder.InvocationPointer != IntPtr.Zero || builder.EnvironmentPointer != IntPtr.Zero)
				return builder;

			throw new NotImplementedException ();
		}

		/// <safety>
		/// <paramref name="builder"/> must supply a live VM and/or current-thread JNI environment with the JNI ABI,
		/// retaining the VM for the runtime lifetime and the class-loader reference through initialization.
		/// </safety>
		internal protected unsafe JreRuntime (NativeAotRuntimeOptions builder)
			: base (CreateJreVM (builder))
		{
		}

		static JniRuntime.JniTypeManager CreateDefaultTypeManager ()
		{
			return new TrimmableTypeMapTypeManager ();
		}

		static JniRuntime.JniValueManager CreateDefaultValueManager ()
		{
			return new TrimmableTypeMapValueManager ();
		}

		public override Exception? GetExceptionForThrowable (ref JniObjectReference reference, JniObjectReferenceOptions options)
		{
			return Android.Runtime.AndroidExceptionManager.GetExceptionForThrowable (ref reference, options);
		}

		public override void RaisePendingException (Exception pendingException)
		{
			Android.Runtime.AndroidExceptionManager.RaisePendingException (pendingException);
		}

		public override string? GetCurrentManagedThreadName ()
		{
			return Thread.CurrentThread.Name;
		}

		public override string GetCurrentManagedThreadStackTrace (int skipFrames, bool fNeedFileInfo)
		{
			return new StackTrace (skipFrames, fNeedFileInfo)
				.ToString ();
		}
	}
}
