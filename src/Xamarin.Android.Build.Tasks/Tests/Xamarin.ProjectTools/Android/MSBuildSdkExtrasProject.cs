using System;
using System.IO;

namespace Xamarin.ProjectTools
{
	public class MSBuildSdkExtrasProject : DotNetStandard
	{
		public MSBuildSdkExtrasProject ()
		{
			Sdk = "MSBuild.Sdk.Extras/2.0.41";
			Sources.Add (new BuildItem.Source ("Class1.cs") {
				TextContent = () => "public class Class1 { }"
			});
			Sources.Add (new AndroidItem.AndroidResource ("Resources\\values\\Strings.xml") {
				TextContent = () => StringsXml.Replace ("${PROJECT_NAME}", ProjectName)
			});
			StringsXml = XamarinAndroidLibraryProject.default_strings_xml;
			TargetFrameworks = "MonoAndroid10.0";
		}

		public string StringsXml { get; set; }

		public string TargetFrameworks {
			get => GetProperty (nameof (TargetFrameworks));
			set => SetProperty (nameof (TargetFrameworks), value);
		}

		public bool IsBindingProject {
			get => string.Equals (GetProperty (nameof (IsBindingProject)), "True", StringComparison.OrdinalIgnoreCase);
			set => SetProperty (nameof (IsBindingProject), value.ToString ());
		}

		string Configuration => IsRelease ? "Release" : "Debug";

		public string TargetFrameworkDirectory {
			get {
				int index = TargetFrameworks.IndexOf (";", StringComparison.Ordinal);
				if (index != -1) {
					return TargetFrameworks.Substring (0, TargetFrameworks.Length - index).ToLowerInvariant ();
				}
				return TargetFrameworks.ToLowerInvariant ();
			}
		}

		public string OutputPath => Path.Combine ("bin", Configuration, TargetFrameworkDirectory);

		public string IntermediateOutputPath => Path.Combine ("obj", Configuration, TargetFrameworkDirectory);
	}
}
