// Originally from: https://github.com/dotnet/java-interop/blob/dd3c1d0514addfe379f050627b3e97493e985da6/src/Java.Runtime.Environment/Java.Interop/JreRuntime.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Android.Runtime;
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

		public  TextWriter? JniGlobalReferenceLogWriter {get; set;}
		public  TextWriter? JniLocalReferenceLogWriter  {get; set;}

		public NativeAotRuntimeOptions ()
		{
			JniVersion  = JniVersion.v1_2;
		}

		public JreRuntime CreateJreVM ()
		{
			return new JreRuntime (this);
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

#if NET
			// Only create type map and managers if not already provided
			// Both TypeManager AND ValueManager must be null to create a new TypeMap
			// (JavaInteropRuntime.init() sets both when calling CreateJreVM)
			if (builder.TypeManager == null && builder.ValueManager == null) {
				var typeMap = new TypeMapAttributeTypeMap ();
				builder.TypeManager     = new Android.Runtime.AndroidTypeManager (typeMap);
				builder.ValueManager    = new ManagedValueManager (typeMap);
				// Also set JNIEnvInit.TypeMap for legacy API compatibility (e.g., JNIEnv.GetArray)
				Android.Runtime.JNIEnvInit.TypeMap = typeMap;
			}
#endif  // NET

			builder.ObjectReferenceManager  ??= new Android.Runtime.AndroidObjectReferenceManager ();

			if (builder.InvocationPointer != IntPtr.Zero || builder.EnvironmentPointer != IntPtr.Zero)
				return builder;

			throw new NotImplementedException ();
		}

		internal protected JreRuntime (NativeAotRuntimeOptions builder)
			: base (CreateJreVM (builder))
		{
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

		// Override to use Android.Runtime.JavaProxyThrowable instead of Java.Interop.JavaProxyThrowable.
		// Java.Interop.JavaProxyThrowable uses net/dot/jni/internal/JavaProxyThrowable which requires
		// dynamic native member registration that is not supported in NativeAOT.
		// Android.Runtime.JavaProxyThrowable has a proper JCW (android/runtime/JavaProxyThrowable)
		// that doesn't require dynamic registration.
		public override void RaisePendingException (Exception pendingException)
		{
			var je = pendingException as JavaException;
			if (je == null) {
				je = Android.Runtime.JavaProxyThrowable.Create (pendingException);
			}
			JniEnvironment.Exceptions.Throw (je.PeerReference);
		}
	}
}
