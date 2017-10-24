using Microsoft.Build.CommandLine;
using System;
using System.IO;
using System.Runtime.CompilerServices;
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
			if (!Directory.Exists (paths.XamarinAndroidBuildOutput)) {
				Console.WriteLine ($"Unable to find Xamarin.Android build output at {paths.XamarinAndroidBuildOutput}");
				return 1;
			}

			//Create a custom xabuild.exe.config
			CreateConfig (paths);

			//Create link to .NETFramework and .NETPortable directory
			foreach (var dir in Directory.GetDirectories(paths.SystemProfiles)) {
				var name = Path.GetFileName(dir);
				if (!SymbolicLink.Create(Path.Combine(paths.FrameworksDirectory, name), dir)) {
					return 1;
				}
			}

			return MSBuildApp.Main ();
		}

		static void CreateConfig (XABuildPaths paths)
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
			SetProperty (toolsets, "AndroidSdkDirectory", paths.AndroidSdkDirectory);
			SetProperty (toolsets, "AndroidNdkDirectory", paths.AndroidNdkDirectory);
			SetProperty (toolsets, "MonoAndroidToolsDirectory", paths.MonoAndroidToolsDirectory);
			SetProperty (toolsets, "TargetFrameworkRootPath", paths.FrameworksDirectory + Path.DirectorySeparatorChar); //NOTE: Must include trailing \

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

			//NOTE: Since many xabuild.exe's could be running in parallel,
			//	some care should be taken when writing the config file.
			//	We need to open the file, and retry on IOException.
			//	We also need MSBuildApp's static ctor to run while the file is locked.
			FileStream stream = null;
			try {
				while (true) {
					try {
						stream = File.Create (paths.XABuildConfig);
						break;
					} catch (IOException) {
						Thread.Sleep (10);
					}
				}

				xml.Save (stream);
				stream.Flush ();

				//Run MSBuildApp's static ctor while the file is still opened
				RuntimeHelpers.RunClassConstructor(typeof (MSBuildApp).TypeHandle);
			} finally {
				if (stream != null)
					stream.Dispose ();
			}
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
