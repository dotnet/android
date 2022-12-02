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
		public string TargetFramework {
			get { return GetProperty ("TargetFramework"); }
			set { SetProperty ("TargetFramework", value); }
		}

		/// <summary>
		/// Projects targeting net6.0/net7.0 require ref/runtime packs on NuGet.org or dotnet6/dotnet7
		/// </summary>
		public void AddNuGetSourcesForOlderTargetFrameworks (string targetFramework = null)
		{
			targetFramework ??= TargetFramework;
			if (targetFramework.IndexOf ("net6.0", StringComparison.OrdinalIgnoreCase) != -1) {
				ExtraNuGetConfigSources = new List<string> {
					"https://api.nuget.org/v3/index.json",
					"https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json",
				};
			} else if (targetFramework.IndexOf ("net7.0", StringComparison.OrdinalIgnoreCase) != -1) {
				ExtraNuGetConfigSources = new List<string> {
					"https://api.nuget.org/v3/index.json",
					"https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet7/nuget/v3/index.json",
				};
			}
		}

		public string Sdk { get; set; }

		public IList<BuildItem> OtherBuildItems { get; private set; }
		public IList<BuildItem> Sources { get; private set; }

		public bool EnableDefaultItems => true;

		public override string SaveProject ()
		{
			return XmlUtils.ToXml (this);
		}

		public override void NuGetRestore (string directory, string packagesDirectory = null)
		{
		}
	}
}
