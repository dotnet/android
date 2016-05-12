using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	public class XamarinAndroidLibraryProject : XamarinAndroidCommonProject
	{
		const string default_strings_xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""library_name"">${PROJECT_NAME}</string>
</resources>
";

		public XamarinAndroidLibraryProject ()
		{
			SetProperty ("AndroidApplication", "False");

			AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\values\\Strings.xml") { TextContent = () => StringsXml.Replace ("${PROJECT_NAME}", ProjectName) });
			StringsXml = default_strings_xml;
		}

		public string StringsXml { get; set; }
	}
}
