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
	public class TestJVM : JreRuntime {

		static JreRuntimeOptions CreateBuilder (string[] jars, Assembly caller)
		{
			var dir = Path.GetDirectoryName (typeof (TestJVM).Assembly.Location);
			var builder = new JreRuntimeOptions () {
				JvmLibraryPath                                  = GetJvmLibraryPath (),
				JniAddNativeMethodRegistrationAttributePresent  = true,
				JniGlobalReferenceLogWriter                     = GetLogOutput ("JAVA_INTEROP_GREF_LOG", "g-", caller),
				JniLocalReferenceLogWriter                      = GetLogOutput ("JAVA_INTEROP_LREF_LOG", "l-", caller),
			};
			if (jars != null) {
				foreach (var jar in jars)
					builder.ClassPath.Add (Path.Combine (dir, jar));
			}
			builder.AddOption ("-Xcheck:jni");
			builder.TypeManager                 = new JreTypeManager ();

			return builder;
		}

		static TextWriter GetLogOutput (string envVar, string prefix, Assembly caller)
		{
			var path    = Environment.GetEnvironmentVariable (envVar);
			if (!string.IsNullOrEmpty (path))
				return null;
			path        = Path.Combine (
					Path.GetDirectoryName (typeof (TestJVM).Assembly.Location),
					prefix + Path.GetFileName (caller.Location) + ".txt");
			return new StreamWriter (path, append: false, encoding: new UTF8Encoding (encoderShouldEmitUTF8Identifier: false));
		}

		static string GetJvmLibraryPath ()
		{
			var jdkDir  = ReadJavaSdkDirectoryFromJdkInfoProps ();
			if (jdkDir != null) {
				return jdkDir;
			}
			var jdk = JdkInfo.GetKnownSystemJdkInfos ()
				.FirstOrDefault ();
			return jdk?.JdkJvmPath;
		}

		static string ReadJavaSdkDirectoryFromJdkInfoProps ()
		{
			var location    = typeof (TestJVM).Assembly.Location;
			var binDir      = Path.GetDirectoryName (Path.GetDirectoryName (location));
			var testDir     = Path.GetFileName (Path.GetDirectoryName (location));
			if (!testDir.StartsWith ("Test", StringComparison.OrdinalIgnoreCase)) {
				return null;
			}
			var buildName   = testDir.Replace ("Test", "Build");
			if (buildName.Contains ('-')) {
				buildName   = buildName.Substring (0, buildName.IndexOf ('-'));
			}
			var jdkPropFile = Path.Combine (binDir, buildName, "JdkInfo.props");
			if (!File.Exists (jdkPropFile)) {
				return null;
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
				return null;
			}
			return jdkJvmPath.Value;
		}

		Dictionary<string, Type> typeMappings;

		public TestJVM (string[] jars = null, Dictionary<string, Type> typeMappings = null)
			: base (CreateBuilder (jars, Assembly.GetCallingAssembly ()))
		{
			this.typeMappings = typeMappings;
		}

		class JreTypeManager : JniTypeManager {

			protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
			{
				foreach (var t in base.GetTypesForSimpleReference (jniSimpleReference))
					yield return t;
				var mappings = ((TestJVM) Runtime).typeMappings;
				Type target;
				if (mappings != null && mappings.TryGetValue (jniSimpleReference, out target))
					yield return target;
			}

			protected override IEnumerable<string> GetSimpleReferences (Type type)
			{
				return base.GetSimpleReferences (type)
					.Concat (CreateSimpleReferencesEnumerator (type));
			}

			IEnumerable<string> CreateSimpleReferencesEnumerator (Type type)
			{
				var mappings = ((TestJVM) Runtime).typeMappings;
				if (mappings == null)
					yield break;
				foreach (var e in mappings) {
					if (e.Value == type)
						yield return e.Key;
				}
			}
		}
	}
}

