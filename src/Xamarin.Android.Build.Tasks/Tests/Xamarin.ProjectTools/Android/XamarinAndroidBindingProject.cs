using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	public class XamarinAndroidBindingProject : XamarinAndroidProject
	{
		public override string ProjectTypeGuid {
			get { return "10368E6C-D01B-4462-8E8B-01FC667A7035"; }
		}

		public IList<BuildItem> Jars { get; private set; }
		public IList<BuildItem> Transforms { get; private set; }

		public XamarinAndroidBindingProject (string debugConfigurationName = "Debug", string releaseConfigurationName = "Release")
			: base (debugConfigurationName, releaseConfigurationName)
		{
			SetProperty ("MonoAndroidJavaPrefix", "Java");
			SetProperty ("MonoAndroidTransformPrefix", "Transforms");

			Jars = new List<BuildItem> ();
			Transforms = new List<BuildItem> ();
			ItemGroupList.Add (Jars);
			ItemGroupList.Add (Transforms);

			EnumFields = EnumMethods = MetadataXml = "<metadata/>";

			Transforms.Add (new AndroidItem.TransformFile ("Transforms\\EnumFields.xml") { TextContent = () => EnumFields });
			Transforms.Add (new AndroidItem.TransformFile ("Transforms\\EnumMethods.xml") { TextContent = () => EnumMethods });
			Transforms.Add (new AndroidItem.TransformFile ("Transforms\\Metadata.xml") { TextContent = () => MetadataXml });
		}

		public string EnumFields { get; set; }
		public string EnumMethods { get; set; }
		public string MetadataXml { get; set; }

		// MSBuild properties
		public string AndroidClassParser {
			get { return GetProperty (KnownProperties.AndroidClassParser); }
			set { SetProperty (KnownProperties.AndroidClassParser, value); }
		}
		
		public override ProjectRootElement Construct ()
		{
			var root = base.Construct ();
			foreach (var import in Imports) {
				var projectName = import.Project ();
				if (projectName != "Directory.Build.props" && projectName != "Directory.Build.targets")
					root.AddImport (projectName);
			}
			return root;
		}
	}

}
