using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Java.Interop {

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

	public class JreRuntimeOptions : JniRuntime.CreationOptions {

		internal    List<string>    Options = new List<string> ();

		public  JniVersion  JniVersion                  {get; set;}
		public  bool        IgnoreUnrecognizedOptions   {get; set;}

		public  Collection<string>  ClassPath           {get; private set;}

		public JreRuntimeOptions ()
		{
			JniVersion  = JniVersion.v1_2;
			ClassPath   = new Collection<string> () {
				Path.Combine (
					Path.GetDirectoryName (typeof (JreRuntimeOptions).Assembly.Location),
					"java-interop.jar"),
			};
		}

		public JreRuntimeOptions AddOption (string option)
		{
			Options.Add (option);
			return this;
		}

		public JreRuntimeOptions AddSystemProperty (string name, string value)
		{
			if (name == null)
				throw new ArgumentNullException ("name");
			if (value == null)
				throw new ArgumentNullException ("value");
			if (name == "java.class.path")
				throw new ArgumentException ("Do not use AddSystemProperty() for the 'java.class.path' property. Add to the ClassPath collection instead.", "name");
			Options.Add (string.Format ("-D{0}={1}", name, value));
			return this;
		}

		public JreRuntime CreateJreVM ()
		{
			return new JreRuntime (this);
		}
	}

	public class JreRuntime : JniRuntime
	{
		const string LibraryName = "jvm.dll";

		[DllImport (LibraryName)]
		static extern int JNI_CreateJavaVM (out IntPtr javavm, out IntPtr jnienv, ref JavaVMInitArgs args);

		[DllImport (LibraryName)]
		static extern int JNI_GetCreatedJavaVMs ([Out] IntPtr[] handles, int bufLen, out int nVMs);

		public static new JniRuntime Current {
			get {
				if (JniRuntime.Current != null)
					return JniRuntime.Current;
				IntPtr              h       = IntPtr.Zero;
				int                 count   = 0;
				foreach (var vmh in GetCreatedJavaVMHandles ()) {
					if (count++ == 0)
						h = vmh;
				}
				if (count == 0)
					throw new InvalidOperationException ("No JavaVM has been created. Please use JreVMBuilder.CreateJreVM().");
				if (count > 1)
					throw new NotSupportedException (string.Format ("Found {0} JavaVMs. Don't know which to use. Use JavaVM.SetCurrent().", count));
				JniRuntime r = GetRegisteredRuntime (h);
				if (r != null)
					return r;
				return new JreRuntime (new JreRuntimeOptions () {
						InvocationPointer = h,
				});
			}
		}

		public static IEnumerable<IntPtr> GetCreatedJavaVMHandles ()
		{
			int nVMs;
			int r = JNI_GetCreatedJavaVMs (null, 0, out nVMs);
			if (r != 0)
				throw new NotSupportedException ("JNI_GetCreatedJavaVMs() returned: " + r);
			var handles = new IntPtr [nVMs];
			r = JNI_GetCreatedJavaVMs (handles, handles.Length, out nVMs);
			if (r != 0)
				throw new InvalidOperationException ("JNI_GetCreatedJavaVMs() [take 2!] returned: " + r);
			return handles;
		}

		static unsafe JreRuntimeOptions CreateJreVM (JreRuntimeOptions builder)
		{
			if (builder == null)
				throw new ArgumentNullException ("builder");

			if (builder.InvocationPointer != IntPtr.Zero)
				return builder;

			var args = new JavaVMInitArgs () {
				version             = builder.JniVersion,
				nOptions            = builder.Options.Count + 1,
				ignoreUnrecognized  = builder.IgnoreUnrecognizedOptions ? (byte) 1 : (byte) 0,
			};
			var options = new JavaVMOption [builder.Options.Count + 1];
			try {
				for (int i = 0; i < builder.Options.Count; ++i)
					options [i].optionString = Marshal.StringToHGlobalAnsi (builder.Options [i]);
				var classPath   = Marshal.StringToHGlobalAnsi (string.Format ("-Djava.class.path={0}", string.Join (Path.PathSeparator.ToString (), builder.ClassPath)));
				options [builder.Options.Count].optionString = classPath;
				fixed (JavaVMOption* popts = options) {
					args.options = (IntPtr) popts;
					IntPtr      javavm;
					IntPtr      jnienv;
					int r = JNI_CreateJavaVM (out javavm, out jnienv, ref args);
					if (r != 0) {
						var message = string.Format (
								"The JDK supports creating at most one JVM per process, ever; " +
								"do you have a JVM running already, or have you already created (and destroyed?) one? " +
								"(JNI_CreateJavaVM returned {0}).",
								r);
						throw new NotSupportedException (message);
					}
					builder.InvocationPointer            = javavm;
					builder.EnvironmentPointer   = jnienv;
					builder.DestroyRuntimeOnDispose  = true;
					return builder;
				}
			} finally {
				for (int i = 0; i < options.Length; ++i)
					Marshal.FreeHGlobal (options [i].optionString);
			}
		}

		internal protected JreRuntime (JreRuntimeOptions builder)
			: base (CreateJreVM (builder))
		{
		}

		protected override bool TryGC (IJavaPeerable value, ref JniObjectReference handle)
		{
			if (!handle.IsValid)
				return true;
			var wgref = handle.NewWeakGlobalRef ();
			JniEnvironment.References.Dispose (ref handle);
			JniGC.Collect ();
			handle = wgref.NewGlobalRef ();
			JniEnvironment.References.Dispose (ref wgref);
			return !handle.IsValid;
		}
	}
}

