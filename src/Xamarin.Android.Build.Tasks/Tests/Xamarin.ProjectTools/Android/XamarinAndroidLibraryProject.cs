using System;
using System.IO;

namespace Xamarin.ProjectTools
{
	public class XamarinAndroidLibraryProject : XamarinAndroidCommonProject
	{
		internal const string default_strings_xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""library_name"">${PROJECT_NAME}</string>
</resources>
";

		public XamarinAndroidLibraryProject (string debugConfigurationName = "Debug", string releaseConfigurationName = "Release")
			: base (debugConfigurationName, releaseConfigurationName)
		{
			SetProperty (KnownProperties.Nullable, "enable");
			SetProperty (KnownProperties.ImplicitUsings, "enable");
			AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\values\\Strings.xml") { TextContent = () => StringsXml.Replace ("${PROJECT_NAME}", ProjectName) });
			StringsXml = default_strings_xml;
		}

		public string StringsXml { get; set; }
	}
}
