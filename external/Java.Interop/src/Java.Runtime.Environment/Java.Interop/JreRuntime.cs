using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

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

		public  bool        IgnoreUnrecognizedOptions   {get; set;}

		public  Collection<string>  ClassPath           {get; private set;}

		public  TextWriter? JniGlobalReferenceLogWriter {get; set;}
		public  TextWriter? JniLocalReferenceLogWriter  {get; set;}

		internal    Dictionary<string, Type>?  	typeMappings;
		public      IDictionary<string, Type>   TypeMappings    => typeMappings ??= new ();

		internal    JvmLibraryHandler?  LibraryHandler  {get; set;}

		public JreRuntimeOptions ()
		{
			JniVersion  = JniVersion.v1_2;
			ClassPath   = new Collection<string> ();
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
		static JreRuntime ()
		{
		}

		static unsafe JreRuntimeOptions CreateJreVM (JreRuntimeOptions builder)
		{
			if (builder == null)
				throw new ArgumentNullException ("builder");
			if (builder.InvocationPointer == IntPtr.Zero &&
					builder.EnvironmentPointer == IntPtr.Zero &&
					string.IsNullOrEmpty (builder.JvmLibraryPath))
				throw new InvalidOperationException ($"Member `{nameof (JreRuntimeOptions)}.{nameof (JreRuntimeOptions.JvmLibraryPath)}` must be set.");

			builder.LibraryHandler  = JvmLibraryHandler.Create ();

#if NET
			builder.TypeManager     ??= new JreTypeManager (builder.typeMappings);
#endif  // NET

			bool onMono = Type.GetType ("Mono.RuntimeStructs", throwOnError: false) != null;
			if (onMono) {
				Console.WriteLine ($"MonoVM support enabled");
				builder.ValueManager            = builder.ValueManager              ?? new MonoRuntimeValueManager ();
				builder.ObjectReferenceManager  = builder.ObjectReferenceManager    ?? new MonoRuntimeObjectReferenceManager ();
			}
			else {
				builder.ValueManager            = builder.ValueManager              ?? new ManagedValueManager ();
				builder.ObjectReferenceManager  = builder.ObjectReferenceManager    ?? new ManagedObjectReferenceManager (builder.JniGlobalReferenceLogWriter, builder.JniLocalReferenceLogWriter);
			}

			if (builder.InvocationPointer != IntPtr.Zero || builder.EnvironmentPointer != IntPtr.Zero)
				return builder;

			builder.LibraryHandler.LoadJvmLibrary (builder.JvmLibraryPath!);

			if (!builder.ClassPath.Any (p => p.EndsWith ("java-interop.jar", StringComparison.OrdinalIgnoreCase))) {
				var loc = GetAssemblyLocation (typeof (JreRuntimeOptions).Assembly);
				var dir = string.IsNullOrEmpty (loc) ? null : Path.GetDirectoryName (loc);
				var jij = string.IsNullOrEmpty (dir) ? null : Path.Combine (dir, "java-interop.jar");
				if (!File.Exists (jij)) {
					throw new FileNotFoundException ($"`java-interop.jar` is required.  Please add to `JreRuntimeOptions.ClassPath`.  Tried to find it in `{jij}`.");
				}
				builder.ClassPath.Add (jij);
			}

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
					int r = builder.LibraryHandler.CreateJavaVM (out javavm, out jnienv, ref args);
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
					return builder;
				}
			} finally {
				for (int i = 0; i < options.Length; ++i)
					Marshal.FreeHGlobal (options [i].optionString);
			}
		}

		[UnconditionalSuppressMessage ("Trimming", "IL3000", Justification = "We check for a null Assembly.Location value!")]
		internal static string? GetAssemblyLocation (Assembly assembly)
		{
			var location = assembly.Location;
			if (!string.IsNullOrEmpty (location))
				return location;
			return null;
		}

		JvmLibraryHandler LibraryHandler;

		internal protected JreRuntime (JreRuntimeOptions builder)
			: base (CreateJreVM (builder))
		{
			LibraryHandler  = builder.LibraryHandler!;
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

		protected override void Dispose (bool disposing)
		{
			base.Dispose (disposing);
			LibraryHandler?.Dispose ();
			LibraryHandler = null!;
		}

		public new IEnumerable<IntPtr> GetAvailableInvocationPointers ()
		{
			return LibraryHandler.GetAvailableInvocationPointers ();
		}
	}

	internal abstract partial class JvmLibraryHandler : IDisposable {
		public  abstract    void                LoadJvmLibrary (string path);
		public  abstract    int                 CreateJavaVM (out IntPtr javavm, out IntPtr jnienv, ref JavaVMInitArgs args);
		public  abstract    IEnumerable<IntPtr> GetAvailableInvocationPointers ();

		public  abstract    void                Dispose ();

		public static JvmLibraryHandler Create ()
		{
			var handler = Environment.GetEnvironmentVariable ("JI_LOADER_TYPE");
			switch (handler?.ToLowerInvariant ()) {
#if !NET
			case "":
			case null:
#endif  // NET
			case "java-interop":
				return new JavaInteropLibJvmLibraryHandler ();
#if NET
			case "":
			case null:
			case "native-library":
				return new NativeLibraryJvmLibraryHandler ();
#endif  // NET
			default:
				Console.Error.WriteLine ($"Unsupported JI_LOADER_TYPE value of `{handler}`.");
				throw new NotSupportedException ();
			}
		}
	}

#if NET

	class NativeLibraryJvmLibraryHandler : JvmLibraryHandler {
		IntPtr  handle;

		IntPtr _Create;
		IntPtr _GetCreated;

		public override void LoadJvmLibrary (string path)
		{
			handle = NativeLibrary.Load (path);
			Console.Error.WriteLine ($"# jonp: LoadJvmLibrary({path})={handle}");

			IntPtr create, getCreated;
			if (!NativeLibrary.TryGetExport (handle, "JNI_CreateJavaVM", out create) ||
					!NativeLibrary.TryGetExport (handle, "JNI_GetCreatedJavaVMs", out getCreated)) {
				NativeLibrary.Free (handle);
				handle = IntPtr.Zero;
				throw new NotSupportedException ("Library `{path}` does not export the required symbols `JNI_CreateJavaVM` or `JNI_GetCreatedJavaVMs`!");
			}

			Console.Error.WriteLine ($"# jonp: JNI_CreateJavaVM={create}; JNI_GetCreatedJavaVMs={getCreated}");
			_Create     = create;
			_GetCreated = getCreated;
		}

		public unsafe override int CreateJavaVM (out IntPtr javavm, out IntPtr jnienv, ref JavaVMInitArgs args)
		{
			Console.Error.WriteLine ($"# jonp: executing JNI_CreateJavaVM={_Create.ToString("x")}");
			// jint JNI_CreateJavaVM(JavaVM **p_vm, void **p_env, void *vm_args);

			var create = (delegate* unmanaged<out IntPtr, out IntPtr, ref Java.Interop.JavaVMInitArgs, int>) _Create;
			var r = create (out javavm, out jnienv, ref args);
			Console.Error.WriteLine ($"# jonp: r={r} javavm={javavm.ToString("x")} jnienv={jnienv.ToString ("x")}");
			return r;
		}

		public unsafe override IEnumerable<IntPtr> GetAvailableInvocationPointers ()
		{
			Console.Error.WriteLine ($"# jonp: executing _GetCreated fnptr={_GetCreated.ToString("x")}");
			var getCreated = (delegate* unmanaged<IntPtr*, int, out int, int>) _GetCreated;
			int nVMs;
			int r = getCreated (null, 0, out nVMs);
			if (r != 0) {
				throw new NotSupportedException ("JNI_GetCreatedJavaVMs() returned: " + r.ToString ());
			}
			var handles = new IntPtr [nVMs];
			fixed (IntPtr* h = handles) {
				r = getCreated (h, handles.Length, out nVMs);
			}
			if (r != 0)
				throw new InvalidOperationException ("JNI_GetCreatedJavaVMs() [take 2!] returned: " + r.ToString ());
			return handles;
		}

		public override void Dispose ()
		{
			NativeLibrary.Free (handle);
			handle      = IntPtr.Zero;
			_Create     = IntPtr.Zero;
			_GetCreated = IntPtr.Zero;
		}
	}

#endif  // NET

	class JavaInteropLibJvmLibraryHandler : JvmLibraryHandler {

		static JavaInteropLibJvmLibraryHandler ()
		{
		}

		const int JAVA_INTEROP_JVM_FAILED_ALREADY_LOADED = -1001;

		public override void LoadJvmLibrary (string path)
		{
			IntPtr errorPtr = IntPtr.Zero;
			int r = JreNativeMethods.java_interop_jvm_load_with_error_message (path, out errorPtr);
			if (r != 0) {
				string? error = Marshal.PtrToStringAnsi (errorPtr);
				JreNativeMethods.java_interop_free (errorPtr);
				if (r == JAVA_INTEROP_JVM_FAILED_ALREADY_LOADED) {
					return;
				}
				throw new NotSupportedException ($"Could not load JVM path `{path}`: {error} ({r})!");
			}
		}

		public override int CreateJavaVM (out IntPtr javavm, out IntPtr jnienv, ref JavaVMInitArgs args)
		{
			return JreNativeMethods.java_interop_jvm_create (out javavm, out jnienv, ref args);
		}

		public override IEnumerable<IntPtr> GetAvailableInvocationPointers ()
		{
			int nVMs;
			int r = JreNativeMethods.java_interop_jvm_list (null, 0, out nVMs);
			if (r != 0)
				throw new NotSupportedException ("JNI_GetCreatedJavaVMs() returned: " + r.ToString ());
			var handles = new IntPtr [nVMs];
			r = JreNativeMethods.java_interop_jvm_list (handles, handles.Length, out nVMs);
			if (r != 0)
				throw new InvalidOperationException ("JNI_GetCreatedJavaVMs() [take 2!] returned: " + r.ToString ());
			return handles;
		}

		public override void Dispose ()
		{
		}
	}

	partial class JreNativeMethods {

		static JreNativeMethods ()
		{
			if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
				var loc     = JreRuntime.GetAssemblyLocation (typeof (JreRuntime).Assembly) ?? throw new NotSupportedException ();
				var baseDir = Path.GetDirectoryName (loc) ?? throw new NotSupportedException ();
				var newDir  = Path.Combine (baseDir, Environment.Is64BitProcess ? "win-x64" : "win-x86");
				JreNativeMethods.AddDllDirectory (newDir);
			}
		}


		[DllImport (JavaInteropLib, CharSet=CharSet.Ansi, CallingConvention=CallingConvention.Cdecl)]
		internal static extern void java_interop_free (IntPtr p);

		[DllImport (JavaInteropLib, CharSet=CharSet.Ansi, CallingConvention=CallingConvention.Cdecl)]
		internal static extern int java_interop_jvm_load_with_error_message (string path, out IntPtr message);

		[DllImport (JavaInteropLib, CharSet=CharSet.Ansi, CallingConvention=CallingConvention.Cdecl)]
		internal static extern int java_interop_jvm_create (out IntPtr javavm, out IntPtr jnienv, ref JavaVMInitArgs args);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern int java_interop_jvm_list ([Out] IntPtr[]? handles, int bufLen, out int nVMs);

		[DllImport ("kernel32", CharSet=CharSet.Unicode)]
		internal static extern int AddDllDirectory (string NewDirectory);
	}
}

