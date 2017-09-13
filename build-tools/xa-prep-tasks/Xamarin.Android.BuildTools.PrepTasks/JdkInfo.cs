using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xamarin.Android.Tools;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public class JdkInfo : Task
	{
		[Required]
		public ITaskItem Output { get; set; }

		public string AndroidNdkPath { get; set; }

		public string AndroidSdkPath { get; set; }

		public string JavaSdkPath { get; set; }

		[Output]
		public string JavaSdkDirectory { get; set; }

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (JdkInfo)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Output)}: {Output}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (AndroidNdkPath)}: {AndroidNdkPath}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (AndroidSdkPath)}: {AndroidSdkPath}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (JavaSdkPath)}: {JavaSdkPath}");

			var androidSdk    = new AndroidSdkInfo (CreateTaskLogger (this), AndroidSdkPath, AndroidNdkPath, JavaSdkPath);
			try {
				var javaSdkPath = androidSdk.JavaSdkPath;
				if (string.IsNullOrEmpty(javaSdkPath)) {
					Log.LogError ("JavaSdkPath is blank");
					return false;
				}

				Log.LogMessage (MessageImportance.Low, $"  {nameof (androidSdk.JavaSdkPath)}: {javaSdkPath}");

				var jvmPath = Path.Combine (javaSdkPath, "jre", "bin", "server", "jvm.dll");
				if (!File.Exists (jvmPath)) {
					Log.LogError ($"JdkJvmPath not found at {jvmPath}");
					return false;
				}

				var javaIncludePath = Path.Combine (javaSdkPath, "include");
				var includes = new List<string> { javaIncludePath };
				includes.AddRange (Directory.GetDirectories (javaIncludePath)); //Include dirs such as "win32"

				var includeXmlTags = new StringBuilder ();
				foreach (var include in includes) {
					includeXmlTags.AppendLine ($"<JdkIncludePath Include=\"{include}\" />");
				}

				Directory.CreateDirectory (Path.GetDirectoryName (Output.ItemSpec));
				File.WriteAllText (Output.ItemSpec, $@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <Choose>
    <When Condition="" '$(JdkJvmPath)' == '' "">
      <PropertyGroup>
        <JdkJvmPath>{jvmPath}</JdkJvmPath>
      </PropertyGroup>
      <ItemGroup>
        {includeXmlTags}
      </ItemGroup>
    </When>
  </Choose>
  <PropertyGroup>
    <JavaCPath Condition="" '$(JavaCPath)' == '' "">{Path.Combine (javaSdkPath, "bin", "javac.exe")}</JavaCPath>
    <JarPath Condition="" '$(JarPath)' == '' "">{Path.Combine (javaSdkPath, "bin", "jar.exe")}</JarPath>
  </PropertyGroup>
</Project>");

				JavaSdkDirectory = javaSdkPath;
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
