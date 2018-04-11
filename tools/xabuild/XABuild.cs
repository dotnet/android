using Microsoft.Build.CommandLine;
using System;
using System.IO;
using System.Linq;
using System.Threading;
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

				//Symbolic links to .NETFramework and .NETPortable directory
				var systemDirectories = Directory.EnumerateDirectories (paths.SystemProfiles)
					.Where (d => Path.GetFileName (d) != "MonoAndroid") //NOTE: this happened on one of our VSTS build agents
					.ToArray ();
				var symbolicLinks = systemDirectories
					.Select (d => Path.Combine (paths.FrameworksDirectory, Path.GetFileName (d)))
					.ToArray ();

				if (symbolicLinks.Any (d => !Directory.Exists (d))) {
					//Hold open the file while creating the symbolic links
					using (var writer = OpenSysLinksFile (paths)) {
						for (int i = 0; i < systemDirectories.Length; i++) {
							var symbolicLink = symbolicLinks [i];
							var systemDirectory = systemDirectories [i];
							Console.WriteLine ($"[xabuild] creating symbolic link '{symbolicLink}' -> '{systemDirectory}'");
							if (!SymbolicLink.Create (symbolicLink, systemDirectory)) {
								return 1;
							}
							writer.WriteLine (Path.GetFileName (symbolicLink));
						}
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

		static StreamWriter OpenSysLinksFile (XABuildPaths paths)
		{
			string path = Path.Combine (paths.FrameworksDirectory, ".__sys_links.txt");

			//NOTE: on Windows, the NUnit tests can throw IOException when running xabuild in parallel
			for (int i = 0;; i++) {
				try {
					return File.AppendText (path);
				} catch (IOException) {
					if (i == 2)
						throw; //Fail after 3 tries
					Thread.Sleep (100);
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
			SetProperty (toolsets, "NuGetProps", paths.NuGetProps);
			SetProperty (toolsets, "NuGetTargets", paths.NuGetTargets);
			SetProperty (toolsets, "NuGetRestoreTargets", paths.NuGetRestoreTargets);
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

			if (Directory.Exists (paths.MSBuildSdksPath)) {
				Environment.SetEnvironmentVariable ("MSBuildSDKsPath", paths.MSBuildSdksPath, EnvironmentVariableTarget.Process);
			}
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
