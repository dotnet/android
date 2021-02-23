using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Text;

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
			var env = Environment.GetEnvironmentVariable ("JI_JVM_PATH");
			if (!string.IsNullOrEmpty (env))
				return env;
			var jdk = JdkInfo.GetKnownSystemJdkInfos ()
				.FirstOrDefault ();
			return jdk?.JdkJvmPath;
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

