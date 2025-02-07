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

	class JreRuntimeOptions : JniRuntime.CreationOptions {

		internal    List<string>    Options = new List<string> ();

		public  bool        IgnoreUnrecognizedOptions   {get; set;}

		public  Collection<string>  ClassPath           {get; private set;}

		public  TextWriter? JniGlobalReferenceLogWriter {get; set;}
		public  TextWriter? JniLocalReferenceLogWriter  {get; set;}

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
			Options.Add (string.Format (CultureInfo.InvariantCulture, "-D{0}={1}", name, value));
			return this;
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

		static JreRuntimeOptions CreateJreVM (JreRuntimeOptions builder)
		{
			if (builder == null)
				throw new ArgumentNullException ("builder");
			if (builder.InvocationPointer == IntPtr.Zero &&
					builder.EnvironmentPointer == IntPtr.Zero &&
					string.IsNullOrEmpty (builder.JvmLibraryPath))
				throw new InvalidOperationException ($"Member `{nameof (JreRuntimeOptions)}.{nameof (JreRuntimeOptions.JvmLibraryPath)}` must be set.");

			builder.LibraryHandler  = JvmLibraryHandler.Create ();

#if NET
			builder.TypeManager     ??= new NativeAotTypeManager ();
#endif  // NET

			builder.ValueManager            = builder.ValueManager              ?? new NativeAotValueManager (builder.TypeManager);
			builder.ObjectReferenceManager  = builder.ObjectReferenceManager    ?? new ManagedObjectReferenceManager (builder.JniGlobalReferenceLogWriter, builder.JniLocalReferenceLogWriter);

			if (builder.InvocationPointer != IntPtr.Zero || builder.EnvironmentPointer != IntPtr.Zero)
				return builder;

			throw new NotImplementedException ();
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
			case "":
			case null:
			case "native-library":
				return new NativeLibraryJvmLibraryHandler ();
			default:
				Console.Error.WriteLine ($"Unsupported JI_LOADER_TYPE value of `{handler}`.");
				throw new NotSupportedException ();
			}
		}
	}

#if NET

	class NativeLibraryJvmLibraryHandler : JvmLibraryHandler {

		public override void LoadJvmLibrary (string path) =>
			throw new NotImplementedException ();

		public override int CreateJavaVM (out IntPtr javavm, out IntPtr jnienv, ref JavaVMInitArgs args) =>
			throw new NotImplementedException ();

		public override IEnumerable<IntPtr> GetAvailableInvocationPointers () =>
			throw new NotImplementedException ();

		public override void Dispose () { }
	}

#endif  // NET
}
