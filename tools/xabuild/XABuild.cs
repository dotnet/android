using Microsoft.Build.CommandLine;
using System;
using System.IO;
using System.Xml;

namespace Xamarin.Android.Build
{
	class XABuild
	{
		[MTAThread]
		static int Main ()
		{
			var paths = new XABuildPaths ();
			try {
				if (!Directory.Exists (paths.XamarinAndroidBuildOutput)) {
					Console.WriteLine ($"Unable to find Xamarin.Android build output at {paths.XamarinAndroidBuildOutput}");
					return 1;
				}

				//Create a custom xabuild.exe.config
				var xml = CreateConfig (paths);

				//Create link to .NETFramework and .NETPortable directory
				foreach (var dir in Directory.GetDirectories (paths.SystemProfiles)) {
					var name = Path.GetFileName (dir);
					if (!SymbolicLink.Create (Path.Combine (paths.FrameworksDirectory, name), dir)) {
						return 1;
					}
				}

				int exitCode = MSBuildApp.Main ();
				if (exitCode != 0) {
					Console.WriteLine ($"MSBuildApp.Main exited with {exitCode}, xabuild configuration is:");

					var settings = new XmlWriterSettings {
						Indent = true,
						NewLineOnAttributes = true,
					};
					using (var writer = XmlTextWriter.Create (Console.Out, settings)) {
						xml.WriteTo (writer);
					}
				}
				return exitCode;
			} finally {
				//NOTE: these are temporary files
				foreach (var file in new [] { paths.MSBuildExeTempPath, paths.XABuildConfig }) {
					if (File.Exists (file)) {
						File.Delete (file);
					}
				}
			}
		}

		static XmlDocument CreateConfig (XABuildPaths paths)
		{
			var xml = new XmlDocument ();
			xml.Load (paths.MSBuildConfig);

			var toolsets = xml.SelectSingleNode ("configuration/msbuildToolsets/toolset");
			SetProperty (toolsets, "VsInstallRoot", paths.VsInstallRoot);
			SetProperty (toolsets, "MSBuildToolsPath", paths.MSBuildBin);
			SetProperty (toolsets, "MSBuildToolsPath32", paths.MSBuildBin);
			SetProperty (toolsets, "MSBuildToolsPath64", paths.MSBuildBin);
			SetProperty (toolsets, "MSBuildExtensionsPath", paths.MSBuildExtensionsPath);
			SetProperty (toolsets, "MSBuildExtensionsPath32", paths.MSBuildExtensionsPath);
			SetProperty (toolsets, "RoslynTargetsPath", Path.Combine (paths.MSBuildBin, "Roslyn"));
			SetProperty (toolsets, "MonoAndroidToolsDirectory", paths.MonoAndroidToolsDirectory);
			SetProperty (toolsets, "TargetFrameworkRootPath", paths.FrameworksDirectory + Path.DirectorySeparatorChar); //NOTE: Must include trailing \
			if (!string.IsNullOrEmpty (paths.AndroidSdkDirectory))
				SetProperty (toolsets, "AndroidSdkDirectory", paths.AndroidSdkDirectory);
			if (!string.IsNullOrEmpty (paths.AndroidNdkDirectory))
				SetProperty (toolsets, "AndroidNdkDirectory", paths.AndroidNdkDirectory);

			var projectImportSearchPaths = toolsets.SelectSingleNode ("projectImportSearchPaths");
			var searchPaths = projectImportSearchPaths.SelectSingleNode ($"searchPaths[@os='{paths.SearchPathsOS}']") as XmlElement;

			//NOTE: on Linux, the searchPaths XML element does not exist, so we have to create it
			if (searchPaths == null) {
				searchPaths = xml.CreateElement ("searchPaths");
				searchPaths.SetAttribute ("os", paths.SearchPathsOS);

				var property = xml.CreateElement ("property");
				property.SetAttribute ("name", "MSBuildExtensionsPath");
				property.SetAttribute ("value", "");
				searchPaths.AppendChild (property);

				property = xml.CreateElement ("property");
				property.SetAttribute ("name", "MSBuildExtensionsPath32");
				property.SetAttribute ("value", "");
				searchPaths.AppendChild (property);

				property = xml.CreateElement ("property");
				property.SetAttribute ("name", "MSBuildExtensionsPath64");
				property.SetAttribute ("value", "");
				searchPaths.AppendChild (property);

				projectImportSearchPaths.AppendChild (searchPaths);
			}

			foreach (XmlNode property in searchPaths.SelectNodes ("property[starts-with(@name, 'MSBuildExtensionsPath')]/@value")) {
				property.Value = string.Join (";", paths.ProjectImportSearchPaths);
			}

			xml.Save (paths.XABuildConfig);

			Environment.SetEnvironmentVariable ("MSBuildSDKsPath", paths.MSBuildSdksPath, EnvironmentVariableTarget.Process);
			Environment.SetEnvironmentVariable ("MSBUILD_EXE_PATH", paths.MSBuildExeTempPath, EnvironmentVariableTarget.Process);
			return xml;
		}

		/// <summary>
		/// If the value exists, sets value attribute, else creates the element
		/// </summary>
		static void SetProperty (XmlNode toolsets, string name, string value)
		{
			if (string.IsNullOrEmpty (value))
				return;

			var valueAttribute = toolsets.SelectSingleNode ($"property[@name='{name}']/@value");
			if (valueAttribute != null) {
				valueAttribute.Value = value;
			} else {
				var property = toolsets.OwnerDocument.CreateElement ("property");
				property.SetAttribute ("name", name);
				property.SetAttribute ("value", value);
				toolsets.PrependChild (property);
			}
		}
	}
}
