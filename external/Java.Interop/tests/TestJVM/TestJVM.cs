using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Text;
using System.Xml.Linq;

using Xamarin.Android.Tools;

using Java.Interop;

namespace Java.InteropTests
{
	public class TestJVMOptions : JreRuntimeOptions {

		public TestJVMOptions (Assembly? callingAssembly = null)
		{
			CallingAssembly = callingAssembly ?? Assembly.GetCallingAssembly ();
		}

		public  ICollection<string>         JarFilePaths    {get;}      = new List<string> ();
		public  Assembly                    CallingAssembly {get; set;}
		public  Dictionary<string, Type>?   TypeMappings    {get; set;}

		internal    JdkInfo?                JdkInfo         {get; set;}
	}

	public class TestJVM : JreRuntime {

#if !__ANDROID__
		public JdkInfo? JdkInfo { get; private set; }
#endif  // !__ANDROID__

		public TestJVM (TestJVMOptions builder)
			: base (OverrideOptions (builder))
		{
#if !__ANDROID__
			this.JdkInfo    = builder.JdkInfo;
#endif  // !__ANDROID__

		}

		static TestJVMOptions OverrideOptions (TestJVMOptions builder)
		{
			var dir = GetOutputDirectoryName ();

			var info = GetJdkInfo ();

			builder.JvmLibraryPath                                  = info.JdkJvmPath;
			builder.JdkInfo                                         = info.JdkInfo;
			builder.JniAddNativeMethodRegistrationAttributePresent  = true;
			builder.JniGlobalReferenceLogWriter                     = GetLogOutput ("JAVA_INTEROP_GREF_LOG", "g-", builder.CallingAssembly);
			builder.JniLocalReferenceLogWriter                      = GetLogOutput ("JAVA_INTEROP_LREF_LOG", "l-", builder.CallingAssembly);

			foreach (var jar in builder.JarFilePaths)
				builder.ClassPath.Add (Path.Combine (dir, jar));
			builder.AddOption ("-Xcheck:jni");
			builder.TypeManager                 = builder.TypeManager ?? new TestJvmTypeManager (builder.TypeMappings);

			return builder;
		}

		static string GetOutputDirectoryName ()
		{
			return Path.GetDirectoryName (typeof (TestJVM).Assembly.Location) ??
				Environment.CurrentDirectory;
		}

		static TextWriter? GetLogOutput (string envVar, string prefix, Assembly caller)
		{
			var path    = Environment.GetEnvironmentVariable (envVar);
			if (!string.IsNullOrEmpty (path))
				return null;
			path        = Path.Combine (
					GetOutputDirectoryName (),
					prefix + Path.GetFileName (caller.Location) + ".txt");
			return new StreamWriter (path, append: false, encoding: new UTF8Encoding (encoderShouldEmitUTF8Identifier: false));
		}

		public static string? GetJvmLibraryPath () => GetJdkInfo ().JdkJvmPath;

		static (JdkInfo? JdkInfo, string? JdkJvmPath) GetJdkInfo ()
		{
			var info    = ReadJavaSdkDirectoryFromJdkInfoProps ();
			if (info.JdkJvmPath != null) {
				return (JdkInfo: info.JavaSdkDirectory == null ? null : new JdkInfo (info.JavaSdkDirectory), JdkJvmPath: info.JdkJvmPath);
			}
			var jdk = JdkInfo.GetKnownSystemJdkInfos ()
				.FirstOrDefault ();
			return (jdk, jdk?.JdkJvmPath);
		}

		static (string? JavaSdkDirectory, string? JdkJvmPath) ReadJavaSdkDirectoryFromJdkInfoProps ()
		{
			var location    = typeof (TestJVM).Assembly.Location;
			var binDir      = Path.GetDirectoryName (Path.GetDirectoryName (location)) ?? Environment.CurrentDirectory;
			var testDir     = Path.GetFileName (Path.GetDirectoryName (location));
			if (testDir == null) {
				return (null, null);
			}
			if (!testDir.StartsWith ("Test", StringComparison.OrdinalIgnoreCase)) {
				return (null, null);
			}
			var buildName   = testDir.Replace ("Test", "Build");
			if (buildName.Contains ('-')) {
				buildName   = buildName.Substring (0, buildName.IndexOf ('-'));
			}
			var jdkPropFile = Path.Combine (binDir, buildName, "JdkInfo.props");
			if (!File.Exists (jdkPropFile)) {
				return (null, null);
			}

			var msbuild     = XNamespace.Get ("http://schemas.microsoft.com/developer/msbuild/2003");

			var jdkProps    = XDocument.Load (jdkPropFile);
			var jdkJvmPath  = jdkProps.Elements ()
				.Elements (msbuild + "Choose")
				.Elements (msbuild + "When")
				.Elements (msbuild + "PropertyGroup")
				.Elements (msbuild + "JdkJvmPath")
				.FirstOrDefault ();
			if (jdkJvmPath == null) {
				return (null, null);
			}
			var jdkPath     = jdkProps.Elements ()
				.Elements (msbuild + "PropertyGroup")
				.Elements (msbuild + "JavaSdkDirectory")
				.FirstOrDefault ();

			return (JavaSdkDirectory: jdkPath?.Value, JdkJvmPath: jdkJvmPath.Value);
		}

		public TestJVM (string[]? jars = null, Dictionary<string, Type>? typeMappings = null)
			: this (CreateOptions (jars, Assembly.GetCallingAssembly (), typeMappings))
		{
		}

		static TestJVMOptions CreateOptions (string[]? jarFiles, Assembly callingAssembly, Dictionary<string, Type>? typeMappings)
		{
			var o = new TestJVMOptions {
				TypeMappings    = typeMappings,
				CallingAssembly = callingAssembly,
			};
			if (jarFiles != null) {
				foreach (var jar in jarFiles) {
					o.JarFilePaths.Add (jar);
				}
			}
			return o;
		}
	}

	public class TestJvmTypeManager :
#if NET
			JreTypeManager
#else   // !NET
			JniRuntime.JniTypeManager
#endif  // !NET
	{

		Dictionary<string, Type>? typeMappings;

		public TestJvmTypeManager (Dictionary<string, Type>? typeMappings)
		{
			this.typeMappings = typeMappings;
		}

		protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
		{
			foreach (var t in base.GetTypesForSimpleReference (jniSimpleReference))
				yield return t;
			if (typeMappings == null)
				yield break;
			Type target;
#pragma warning disable CS8600	// huh?
			if (typeMappings.TryGetValue (jniSimpleReference, out target))
				yield return target;
#pragma warning restore CS8600
		}

		protected override IEnumerable<string> GetSimpleReferences (Type type)
		{
			return base.GetSimpleReferences (type)
				.Concat (CreateSimpleReferencesEnumerator (type));
		}

		IEnumerable<string> CreateSimpleReferencesEnumerator (Type type)
		{
			if (typeMappings == null)
				yield break;
			foreach (var e in typeMappings) {
				if (e.Value == type)
					yield return e.Key;
			}
		}
	}
}

