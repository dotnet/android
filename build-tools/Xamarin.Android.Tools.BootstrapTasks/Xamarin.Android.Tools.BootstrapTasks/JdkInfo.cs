using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class JdkInfo : Task
	{
		[Required]
		public ITaskItem Output { get; set; }

		public string AndroidNdkPath { get; set; }

		public string AndroidSdkPath { get; set; }

		public string JavaSdkPath { get; set; }

		public string MaxJdkVersion { get; set; }

		[Output]
		public string JavaSdkDirectory { get; set; }

		public override bool Execute ()
		{
			var logger        = CreateTaskLogger (this);
			var androidSdk    = new AndroidSdkInfo (logger, AndroidSdkPath, AndroidNdkPath, JavaSdkPath);
			try {
				Log.LogMessage (MessageImportance.Low, $"  {nameof (androidSdk.JavaSdkPath)}: {androidSdk.JavaSdkPath}");

				Version maxVersion;
				if (string.IsNullOrEmpty (MaxJdkVersion)) {
					maxVersion = new Version ("8.0");
				} else {
					maxVersion = new Version (MaxJdkVersion);
				}

				var defaultJdk = new [] { new Tools.JdkInfo (androidSdk.JavaSdkPath) };
				var jdk = defaultJdk.Concat (Tools.JdkInfo.GetKnownSystemJdkInfos (logger))
					.Where (j => maxVersion != null ? j.Version <= maxVersion : true)
					.Where (j => j.IncludePath.Any ())
					.FirstOrDefault ();

				if (jdk == null) {
					Log.LogError ($"Could not determine a valid JavaSdkPath, `{androidSdk.JavaSdkPath}` was not compatible with the .NET for Android build.");
					return false;
				} else {
					Log.LogMessage (MessageImportance.Low, $"  {nameof (jdk.HomePath)}: {jdk.HomePath}");
				}

				var includes = new List<string> (jdk.IncludePath);
				var includeXmlTags = new StringBuilder ();
				foreach (var include in includes) {
					includeXmlTags.AppendLine ($"<JdkIncludePath Include=\"{include}\" />");
				}

				Directory.CreateDirectory (Path.GetDirectoryName (Output.ItemSpec));
				File.WriteAllText (Output.ItemSpec, $@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <Choose>
    <When Condition="" '$(JdkJvmPath)' == '' "">
      <PropertyGroup>
        <JdkJvmPath>{jdk.JdkJvmPath}</JdkJvmPath>
      </PropertyGroup>
      <ItemGroup>
        {includeXmlTags}
      </ItemGroup>
    </When>
  </Choose>
  <PropertyGroup>
    <JavaCPath Condition="" '$(JavaCPath)' == '' "">{jdk.JavacPath}</JavaCPath>
    <JarPath Condition="" '$(JarPath)' == '' "">{jdk.JarPath}</JarPath>
  </PropertyGroup>
</Project>");

				JavaSdkDirectory = jdk.HomePath;
				Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (JavaSdkDirectory)}: {JavaSdkDirectory}");

				return !Log.HasLoggedErrors;
			}
			finally {
			}
		}

		static Action<TraceLevel, string> CreateTaskLogger (Task task)
		{
			Action<TraceLevel, string> logger = (level, value) => {
				switch (level) {
				case TraceLevel.Error:
					task.Log.LogError (value);
					break;
				case TraceLevel.Warning:
					task.Log.LogWarning (value);
					break;
				default:
					task.Log.LogMessage (MessageImportance.Low, "{0}", value);
					break;
				}
			};
			return logger;
		}
	}
}
