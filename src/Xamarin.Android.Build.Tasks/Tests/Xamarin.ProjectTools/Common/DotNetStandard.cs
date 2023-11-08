using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Xamarin.ProjectTools
{
	public class DotNetStandard : XamarinProject, IShortFormProject
	{
		public override string ProjectTypeGuid {
			get {
				return string.Empty;
			}
		}

		public DotNetStandard ()
		{
			ProjectName = "UnnamedProject";
			Sources = new List<BuildItem> ();
			OtherBuildItems = new List<BuildItem> ();
			ItemGroupList.Add (Sources);
			ItemGroupList.Add (OtherBuildItems);
			Language = XamarinAndroidProjectLanguage.CSharp;
		}

		// NetStandard projects always need to restore
		public override bool ShouldRestorePackageReferences => true;

		public string PackageTargetFallback {
			get { return GetProperty ("PackageTargetFallback"); }
			set { SetProperty ("PackageTargetFallback", value); }
		}

		public string Sdk { get; set; }

		public IList<BuildItem> OtherBuildItems { get; private set; }
		public IList<BuildItem> Sources { get; private set; }

		public bool EnableDefaultItems => true;

		public override string SaveProject ()
		{
			return XmlUtils.ToXml (this);
		}
	}
}
