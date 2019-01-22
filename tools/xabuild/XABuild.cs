using Microsoft.Build.CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
				//HACK: running on Mono, MSBuild cannot resolve System.Reflection.Metadata
				if (!paths.IsWindows) {
					AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
						var name = new AssemblyName (args.Name);
						if (name.Name == "System.Reflection.Metadata") {
							var path = Path.Combine (paths.MSBuildBin, $"{name.Name}.dll");
							return Assembly.LoadFile (path);
						}
						//Return null, to revert to default .NET behavior
						return null;
					};
				}
				if (!Directory.Exists (paths.XamarinAndroidBuildOutput)) {
					Console.WriteLine ($"Unable to find Xamarin.Android build output at {paths.XamarinAndroidBuildOutput}");
					return 1;
				}

				//Create a custom xabuild.exe.config
				var xml = CreateConfig (paths);

				//Symbolic links to be created: key=system, value=in-tree
				var symbolicLinks = new Dictionary<string, string> ();
				foreach (var dir in Directory.EnumerateDirectories (paths.SystemFrameworks)) {
					if (Path.GetFileName (dir) != "MonoAndroid") {
						symbolicLinks [dir] = Path.Combine (paths.FrameworksDirectory, Path.GetFileName (dir));
					}
				}
				foreach (var dir in paths.SystemTargetsDirectories) {
					symbolicLinks [dir] = Path.Combine (paths.MSBuildExtensionsPath, Path.GetFileName (dir));
				}
				if (symbolicLinks.Values.Any (d => !Directory.Exists (d))) {
					//Hold open the file while creating the symbolic links
					using (var writer = OpenSysLinksFile (paths)) {
						foreach (var pair in symbolicLinks) {
							var systemDirectory = pair.Key;
							var symbolicLink = pair.Value;
							Console.WriteLine ($"[xabuild] creating symbolic link '{symbolicLink}' -> '{systemDirectory}'");
							if (!SymbolicLink.Create (symbolicLink, systemDirectory)) {
								return 1;
							}
							writer.WriteLine (Path.GetFileName (symbolicLink));
						}
					}
				}

				return MSBuildApp.Main ();
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
			if (searchPaths != null) {
				foreach (XmlNode property in searchPaths.SelectNodes ("property[starts-with(@name, 'MSBuildExtensionsPath')]/@value")) {
					property.Value = "";
				}
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
