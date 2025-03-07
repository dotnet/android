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

		public static string? GetJvmLibraryPath (Action<TraceLevel, string>? logger = null) => GetJdkInfo (logger).JdkJvmPath;

		static (JdkInfo? JdkInfo, string? JdkJvmPath) GetJdkInfo (Action<TraceLevel, string>? logger = null)
		{
			var info    = ReadJavaSdkDirectoryFromJdkInfoProps (logger);
			if (info.JdkJvmPath != null) {
				return (JdkInfo: info.JavaSdkDirectory == null ? null : new JdkInfo (info.JavaSdkDirectory), JdkJvmPath: info.JdkJvmPath);
			}
			var jdk = JdkInfo.GetKnownSystemJdkInfos (logger)
				.FirstOrDefault ();
			return (jdk, jdk?.JdkJvmPath);
		}

		static (string? JavaSdkDirectory, string? JdkJvmPath) ReadJavaSdkDirectoryFromJdkInfoProps (Action<TraceLevel, string>? logger)
		{
			var jdkPropFile = TryProbeForJdkInfoProps (logger);
			logger?.Invoke (TraceLevel.Verbose, $"TestJVM: jdkPropFile? {jdkPropFile}");
			if (!File.Exists (jdkPropFile)) {
				return (null, null);
			}

			logger?.Invoke (TraceLevel.Verbose, $"TestJVM: Extracting $(JdkJvmPath) from: {jdkPropFile}");
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

			logger?.Invoke (TraceLevel.Verbose, $"TestJVM: $(JavaSdkDirectory)={jdkPath?.Value}; $(JdkJvmPath)={jdkJvmPath.Value}");
			return (JavaSdkDirectory: jdkPath?.Value, JdkJvmPath: jdkJvmPath.Value);
		}

		static string? TryProbeForJdkInfoProps (Action<TraceLevel, string>? logger)
		{
			for (var probing = Path.GetDirectoryName (typeof (TestJVM).Assembly.Location); probing != null; probing = Path.GetDirectoryName (probing)) {
				logger?.Invoke (TraceLevel.Verbose, $"TestJVM: probing for JdkInfo.props around {probing}");
				if (File.Exists (Path.Combine (probing, "Java.Interop.sln"))) {
					// we've hit the root of the repo checkout
					return ProbeFromRootDir (probing);
				}

				var dirName = Path.GetFileName (probing);
				if (dirName.StartsWith ("Test", StringComparison.OrdinalIgnoreCase)) {
					var buildName   = dirName.Replace ("Test", "Build");
					if (buildName.Contains ('-')) {
						buildName   = buildName.Substring (0, buildName.IndexOf ('-'));
					}
					return Path.Combine (Path.GetDirectoryName (probing)!, buildName, "JdkInfo.props");
				}
			}
			return null;

			string ProbeFromRootDir (string location)
			{
				var buildDebug      = Path.Combine (location, "bin", "BuildDebug");
				var buildRelease    = Path.Combine (location, "bin", "BuildRelease");
				if (Directory.Exists (buildDebug) && !Directory.Exists (buildRelease)) {
					return Path.Combine (buildDebug, "JdkInfo.props");
				}
				if (Directory.Exists (buildRelease) && !Directory.Exists (buildDebug)) {
					return Path.Combine (buildRelease, "JdkInfo.props");
				}
				var dir = Directory.GetLastWriteTime (buildDebug) > Directory.GetLastWriteTime (buildRelease)
					? buildDebug
					: buildRelease;
				return Path.Combine (dir, "JdkInfo.props");
			}
		}

		public TestJVM (string[]? jars = null, Dictionary<string, Type>? typeMappings = null)
			: this (CreateOptions (jars, Assembly.GetCallingAssembly (), typeMappings))
		{
		}

		static TestJVMOptions CreateOptions (string[]? jarFiles, Assembly callingAssembly, Dictionary<string, Type>? typeMappings)
		{
			var o = new TestJVMOptions {
				CallingAssembly = callingAssembly,
			};
			if (typeMappings != null) {
				foreach (var e in typeMappings) {
					o.TypeMappings.Add (e.Key, e.Value);
				}
			}
			if (jarFiles != null) {
				foreach (var jar in jarFiles) {
					o.JarFilePaths.Add (jar);
				}
			}
			return o;
		}
	}

	public class TestJvmTypeManager : JreTypeManager
	{

		public TestJvmTypeManager (IDictionary<string, Type>? typeMappings)
			: base (typeMappings)
		{
		}
	}
}

