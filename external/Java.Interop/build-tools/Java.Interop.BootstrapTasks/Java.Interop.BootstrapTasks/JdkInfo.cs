using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Xamarin.Android.Tools;

using XATInfo   = Xamarin.Android.Tools.JdkInfo;

namespace Java.Interop.BootstrapTasks
{
	public class JdkInfo : Task
	{
		const string JARSIGNER = "jarsigner.exe";
		const string MDREG_KEY = @"SOFTWARE\Novell\Mono for Android";
		const string MDREG_JAVA_SDK = "JavaSdkDirectory";

		public  string  JdksRoot              { get; set; }

		public  string  MaximumJdkVersion     { get; set; }

		static  Regex   VersionExtractor  = new Regex (@"(?<version>[\d]+(\.\d+)+)", RegexOptions.Compiled);

		[Required]
		public  ITaskItem       PropertyFile        { get; set; }

		[Required]
		public  ITaskItem       MakeFragmentFile    { get; set; }

		[Output]
		public  string          JavaHomePath        { get; set; }

		public override bool Execute ()
		{
			var maxVersion      = GetVersion (MaximumJdkVersion);

			XATInfo jdk         = XATInfo.GetKnownSystemJdkInfos (CreateLogger ())
				.Where (j => maxVersion != null ? j.Version <= maxVersion : true)
				.Where (j => j.IncludePath.Any ())
				.FirstOrDefault ();

			if (jdk == null) {
				Log.LogError ("Could not determine JAVA_HOME location. Please set JdksRoot or export the JAVA_HOME environment variable.");
				return false;
			}

			JavaHomePath  = jdk.HomePath;

			Directory.CreateDirectory (Path.GetDirectoryName (PropertyFile.ItemSpec));
			Directory.CreateDirectory (Path.GetDirectoryName (MakeFragmentFile.ItemSpec));

			WritePropertyFile (jdk.JarPath, jdk.JavacPath, jdk.JdkJvmPath, jdk.IncludePath);
			WriteMakeFragmentFile (jdk.JarPath, jdk.JavacPath, jdk.JdkJvmPath, jdk.IncludePath);

			return !Log.HasLoggedErrors;
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

		void WritePropertyFile (string jarPath, string javacPath, string jdkJvmPath, IEnumerable<string> includes)
		{
			var msbuild = XNamespace.Get ("http://schemas.microsoft.com/developer/msbuild/2003");
			var project = new XElement (msbuild + "Project",
				new XElement (msbuild + "Choose",
					new XElement (msbuild + "When", new XAttribute ("Condition", " '$(JdkJvmPath)' == '' "),
						new XElement (msbuild + "PropertyGroup",
							new XElement (msbuild + "JdkJvmPath", jdkJvmPath)),
						new XElement (msbuild + "ItemGroup",
							includes.Select (i => new XElement (msbuild + "JdkIncludePath", new XAttribute ("Include", i)))))),
				new XElement (msbuild + "PropertyGroup",
					new XElement (msbuild + "JavaCPath", new XAttribute ("Condition", " '$(JavaCPath)' == '' "),
						javacPath),
					new XElement (msbuild + "JarPath", new XAttribute ("Condition", " '$(JarPath)' == '' "),
						jarPath)));
			project.Save (PropertyFile.ItemSpec);
		}

		void WriteMakeFragmentFile (string jarPath, string javacPath, string jdkJvmPath, IEnumerable<string> includes)
		{
			using (var o = new StreamWriter (MakeFragmentFile.ItemSpec)) {
				o.WriteLine ($"export  JI_JAR_PATH          := {jarPath}");
				o.WriteLine ($"export  JI_JAVAC_PATH        := {javacPath}");
				o.WriteLine ($"export  JI_JDK_INCLUDE_PATHS := {string.Join (" ", includes)}");
				o.WriteLine ($"export  JI_JVM_PATH          := {jdkJvmPath}");
			}
		}
	}
}
