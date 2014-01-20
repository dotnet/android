using System;
using System.Collections.Generic;
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

	public class JreVMBuilder : JavaVMOptions {

		internal    List<string>    Options = new List<string> ();

		public  JniVersion  JniVersion                  {get; set;}
		public  bool        IgnoreUnrecognizedOptions   {get; set;}

		public JreVMBuilder ()
		{
			JniVersion  = JniVersion.v1_2;
		}

		public JreVMBuilder AddOption (string option)
		{
			Options.Add (option);
			return this;
		}

		public JreVMBuilder AddSystemProperty (string name, string value)
		{
			if (name == null)
				throw new ArgumentNullException ("name");
			if (value == null)
				throw new ArgumentNullException ("value");
			Options.Add (string.Format ("-D{0}={1}", name, value));
			return this;
		}

		public JreVM CreateJreVM ()
		{
			return new JreVM (this);
		}
	}

	public class JreVM : JavaVM
	{
		const string LibraryName = "jvm.dll";

		[DllImport (LibraryName)]
		static extern int JNI_CreateJavaVM (out JavaVMSafeHandle javavm, out JniEnvironmentSafeHandle jnienv, ref JavaVMInitArgs args);

		[DllImport (LibraryName)]
		static extern int JNI_GetCreatedJavaVMs ([Out] IntPtr[] handles, int bufLen, out int nVMs);

		public static new JavaVM Current {
			get {
				if (JavaVM.Current != null)
					return JavaVM.Current;
				JavaVMSafeHandle    h       = null;
				int                 count   = 0;
				foreach (var vmh in GetCreatedJavaVMHandles ()) {
					if (count++ == 0)
						h = vmh;
				}
				if (count == 0)
					throw new InvalidOperationException ("No JavaVM has been created. Please use JreVMBuilder.CreateJreVM().");
				if (count > 1)
					throw new NotSupportedException (string.Format ("Found {0} JavaVMs. Don't know which to use. Use JavaVM.SetCurrent().", count));
				JavaVM r = GetRegisteredJavaVM (h);
				if (r != null)
					return r;
				return new JreVM (new JreVMBuilder () {
						VMHandle = h,
				});
			}
		}

		public static IEnumerable<JavaVMSafeHandle> GetCreatedJavaVMHandles ()
		{
			int nVMs;
			int r = JNI_GetCreatedJavaVMs (null, 0, out nVMs);
			if (r != 0)
				throw new NotSupportedException ("JNI_GetCreatedJavaVMs() returned: " + r);
			var handles = new IntPtr [nVMs];
			r = JNI_GetCreatedJavaVMs (handles, handles.Length, out nVMs);
			if (r != 0)
				throw new InvalidOperationException ("JNI_GetCreatedJavaVMs() [take 2!] returned: " + r);
			return handles.Select (h => new JavaVMSafeHandle (h));
		}

		static unsafe JreVMBuilder CreateJreVM (JreVMBuilder builder)
		{
			if (builder == null)
				throw new ArgumentNullException ("builder");

			if (builder.VMHandle != null && !builder.VMHandle.IsInvalid)
				return builder;

			var args = new JavaVMInitArgs () {
				version             = builder.JniVersion,
				nOptions            = builder.Options.Count,
				ignoreUnrecognized  = builder.IgnoreUnrecognizedOptions ? (byte) 1 : (byte) 0,
			};
			var options = new JavaVMOption [builder.Options.Count];
			try {
				for (int i = 0; i < options.Length; ++i)
					options [i].optionString = Marshal.StringToHGlobalAnsi (builder.Options [i]);
				fixed (JavaVMOption* popts = options) {
					args.options = (IntPtr) popts;
					JavaVMSafeHandle            javavm;
					JniEnvironmentSafeHandle    jnienv;
					int r = JNI_CreateJavaVM (out javavm, out jnienv, ref args);
					if (r != 0) {
						var message = string.Format (
								"The JDK supports creating at most one JVM per process, ever; " +
								"do you have a JVM running already, or have you already created (and destroyed?) one? " +
								"(JNI_CreateJavaVM returned {0}).",
								r);
						throw new NotSupportedException (message);
					}
					builder.VMHandle            = javavm;
					builder.EnvironmentHandle   = jnienv;
					builder.DestroyVMOnDispose  = true;
					return builder;
				}
			} finally {
				for (int i = 0; i < options.Length; ++i)
					Marshal.FreeHGlobal (options [i].optionString);
			}
		}

		internal protected JreVM (JreVMBuilder builder)
			: base (CreateJreVM (builder))
		{
		}

		protected override bool TryGC (IJavaObject value, ref JniReferenceSafeHandle handle)
		{
			System.Diagnostics.Debug.WriteLine ("# JreVM.TryGC");
			if (handle == null || handle.IsInvalid)
				return true;
			var wgref = handle.NewWeakGlobalRef ();
			System.Diagnostics.Debug.WriteLine ("# JreVM.TryGC: wgref=0x{0}", wgref.DangerousGetHandle().ToString ("x"));;
			handle.Dispose ();
			JniGC.Collect ();
			handle = wgref.NewGlobalRef ();
			System.Diagnostics.Debug.WriteLine ("# JreVM.TryGC: handle.IsInvalid={0}", handle.IsInvalid);
			return handle == null || handle.IsInvalid;
		}
	}
}

