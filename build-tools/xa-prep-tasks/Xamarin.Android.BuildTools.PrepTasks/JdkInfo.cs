using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xamarin.Android.Build.Utilities;

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

			AndroidLogger.Error += ErrorHandler;
			AndroidLogger.Warning += WarningHandler;
			AndroidLogger.Info += InfoHandler;
			try {
				AndroidSdk.Refresh (AndroidSdkPath, AndroidNdkPath, JavaSdkPath);

				var javaSdkPath = AndroidSdk.JavaSdkPath;
				if (string.IsNullOrEmpty(javaSdkPath)) {
					Log.LogError ("JavaSdkPath is blank");
					return false;
				}

				Log.LogMessage (MessageImportance.Low, $"  {nameof (AndroidSdk.JavaSdkPath)}: {javaSdkPath}");

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
				AndroidLogger.Error -= ErrorHandler;
				AndroidLogger.Warning -= WarningHandler;
				AndroidLogger.Info -= InfoHandler;
			}
		}

		private void ErrorHandler (string task, string message)
		{
			Log.LogError ($"{task}: {message}");
		}

		private void WarningHandler (string task, string message)
		{
			Log.LogWarning ($"{task}: {message}");
		}

		private void InfoHandler (string task, string message)
		{
			Log.LogMessage (MessageImportance.Low, $"{task}: {message}");
		}
	}
}
