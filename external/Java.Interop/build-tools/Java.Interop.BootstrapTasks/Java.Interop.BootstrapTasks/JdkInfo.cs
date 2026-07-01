using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Xamarin.Android.Tools;

using XATInfo   = Xamarin.Android.Tools.JdkInfo;

namespace Java.Interop.BootstrapTasks
{
	public class JdkInfo : Task
	{
		public  string  JdksRoot              { get; set; }

		public  string  PropertyNameModifier  { get; set; } = "";
		public  string  MinimumJdkVersion     { get; set; }
		public  string  MaximumJdkVersion     { get; set; }

		public  string  DotnetToolPath        { get; set; }

		static  Regex   VersionExtractor  = new Regex (@"(?<version>[\d]+(\.\d+)+)", RegexOptions.Compiled);

		[Required]
		public  ITaskItem       PropertyFile        { get; set; }

		public  ITaskItem       MakeFragmentFile    { get; set; }

		[Output]
		public  string          JavaHomePath        { get; set; }

		public override bool Execute ()
		{
			var minVersion      = GetVersion (MinimumJdkVersion);
			var maxVersion      = GetVersion (MaximumJdkVersion);

			var explicitJdks    = GetJdkRoots ();
			var defaultJdks     = XATInfo.GetKnownSystemJdkInfos (CreateLogger ())
				.Where (j => minVersion != null ? j.Version >= minVersion : true)
				.Where (j => maxVersion != null ? j.Version <= maxVersion : true)
				.Where (j => j.IncludePath.Any ());
			var jdk             = explicitJdks.Concat (defaultJdks)
				.Where (j => JdkRunsOnHost (j))
				.FirstOrDefault ();

			if (jdk == null) {
				Log.LogError ("Could not determine JAVA_HOME location. Please set JdksRoot or export the JAVA_HOME environment variable.");
				return false;
			}

			var rtJarPaths  = new[]{
				Path.Combine (Path.GetDirectoryName (jdk.JavacPath), "..", "jre", "lib", "rt.jar"),
			};
			var rtJarPath   = rtJarPaths.FirstOrDefault (p => File.Exists (p));

			JavaHomePath  = jdk.HomePath;

			Directory.CreateDirectory (Path.GetDirectoryName (PropertyFile.ItemSpec));
			WritePropertyFile (jdk, rtJarPath);

			if (MakeFragmentFile != null) {
				Directory.CreateDirectory (Path.GetDirectoryName (MakeFragmentFile.ItemSpec));
				WriteMakeFragmentFile (jdk, rtJarPath);
			}

			return !Log.HasLoggedErrors;
		}

		static bool JdkRunsOnHost (XATInfo jdk)
		{
			var cputype = RuntimeInformation.ProcessArchitecture;
			if (jdk.ReleaseProperties.TryGetValue ("OS_ARCH", out var arch)) {
				return (cputype, arch) switch {
					(Architecture.Arm64,    "aarch64")  => true,
					(Architecture.X64,      "x86_64")   => true,
					_ => false,
				};
			}
			return true;
		}

		XATInfo[] GetJdkRoots ()
		{
			XATInfo jdk = null;
			try {
				if (!string.IsNullOrEmpty (JdksRoot))
					jdk = new XATInfo (JdksRoot);
			} catch (Exception e) {
				Log.LogWarning ($"Could not get information about JdksRoot path `{JdksRoot}`: {e.Message}");
				Log.LogMessage (MessageImportance.Low, e.ToString ());
			}
			return jdk == null
				? Array.Empty<XATInfo>()
				: new[] { jdk };
		}

		Version GetVersion (string value)
		{
			if (string.IsNullOrEmpty (value))
				return null;
			if (!value.Contains (".")) {
				value += ".0";
			}
			Version v;
			if (Version.TryParse (value, out v))
				return v;
			return null;
		}

		Action<TraceLevel, string> CreateLogger ()
		{
			Action<TraceLevel, string> logger = (level, value) => {
				switch (level) {
					case TraceLevel.Error:
						Log.LogError ("{0}", value);
						break;
					case TraceLevel.Warning:
						Log.LogWarning ("{0}", value);
						break;
					default:
						Log.LogMessage (MessageImportance.Low, "{0}", value);
						break;
				}
			};
			return logger;
		}

		void WritePropertyFile (XATInfo jdk, string rtJarPath)
		{
			var jarPath     = jdk.JarPath;
			var javacPath   = jdk.JavacPath;
			var javaPath    = jdk.JavaPath;
			var jdkJvmPath  = jdk.JdkJvmPath;
			var includes    = jdk.IncludePath;

			var msbuild = XNamespace.Get ("http://schemas.microsoft.com/developer/msbuild/2003");
			var jdkJvmP = $"JdkJvm{PropertyNameModifier}Path";
			var project = new XElement (msbuild + "Project",
				new XElement (msbuild + "Choose",
					new XElement (msbuild + "When", new XAttribute ("Condition", $" '$({jdkJvmP})' == '' "),
						new XElement (msbuild + "PropertyGroup",
							new XElement (msbuild + jdkJvmP, jdkJvmPath)),
						new XElement (msbuild + "ItemGroup",
							includes.Select (i => new XElement (msbuild + $"Jdk{PropertyNameModifier}IncludePath", new XAttribute ("Include", i)))))),
				new XElement (msbuild + "PropertyGroup",
					CreateProperty (msbuild, $"JavaApi{PropertyNameModifier}DefineConstants",
						string.Join (";", Enumerable.Range (11, jdk.Version.Major-11+1).Select (v => $"JAVA_API_{v}"))),
					CreateProperty (msbuild, $"Java{PropertyNameModifier}MajorVersion", jdk.Version.Major.ToString ()),
					CreateProperty (msbuild, $"Java{PropertyNameModifier}SdkDirectory", JavaHomePath),
					CreateProperty (msbuild, $"Java{PropertyNameModifier}Path", javaPath),
					CreateProperty (msbuild, $"JavaC{PropertyNameModifier}Path", javacPath),
					CreateProperty (msbuild, $"Jar{PropertyNameModifier}Path", jarPath),
					CreateProperty (msbuild, $"Dotnet{PropertyNameModifier}ToolPath", DotnetToolPath),
					CreateProperty (msbuild, $"Jre{PropertyNameModifier}RtJarPath", rtJarPath)));
			project.Save (PropertyFile.ItemSpec);
		}

		XElement CreateProperty (XNamespace msbuild, string propertyName, string propertyValue)
		{
			if (string.IsNullOrEmpty (propertyValue)) {
				return null;
			}

			return new XElement (msbuild + propertyName,
					new XAttribute ("Condition", $" '$({propertyName})' == '' "),
					propertyValue);
		}

		void WriteMakeFragmentFile (XATInfo jdk, string rtJarPath)
		{
			var jarPath     = jdk.JarPath;
			var javacPath   = jdk.JavacPath;
			var javaPath    = jdk.JavaPath;
			var jdkJvmPath  = jdk.JdkJvmPath;
			var includes    = jdk.IncludePath;

			using (var o = new StreamWriter (MakeFragmentFile.ItemSpec)) {
				o.WriteLine ($"export  JI_JAR_PATH          := {jarPath}");
				o.WriteLine ($"export  JI_JAVA_PATH         := {javaPath}");
				o.WriteLine ($"export  JI_JAVAC_PATH        := {javacPath}");
				o.WriteLine ($"export  JI_JDK_INCLUDE_PATHS := {string.Join (" ", includes)}");
				o.WriteLine ($"export  JI_JVM_PATH          := {jdkJvmPath}");
				o.WriteLine ($"export  JI_RT_JAR_PATH       := {rtJarPath}");
			}
		}
	}
}
