using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Xamarin.ProjectTools
{
	public class DotNetStandard : XamarinProject
	{

		public override string ProjectTypeGuid {
			get {
				return string.Empty;
			}
		}

		public DotNetStandard ()
		{
			Sources = new List<BuildItem> ();
			OtherBuildItems = new List<BuildItem> ();
			SetProperty (CommonProperties, "DebugType", "full");
			ItemGroupList.Add (Sources);
		}
		public string PackageTargetFallback {
			get { return GetProperty ("PackageTargetFallback"); }
			set { SetProperty ("PackageTargetFallback", value); }
		}
		public string TargetFramework {
			get { return GetProperty ("TargetFramework"); }
			set { SetProperty ("TargetFramework", value); }
		}
		public string Sdk { get; set; }

		public IList<BuildItem> OtherBuildItems { get; private set; }
		public IList<BuildItem> Sources { get; private set; }

		public override string SaveProject ()
		{
			var sb = new StringBuilder ();
			sb.AppendLine ("\t<PropertyGroup>");
			foreach (var pg in PropertyGroups) {
				if (!pg.Properties.Any ())
					continue;
				foreach (var p in pg.Properties) {
					var conditon = string.IsNullOrEmpty (p.Condition) ? "" : $" Conditon=\"{p.Condition}\"";
					sb.AppendLine ($"\t\t<{p.Name}{conditon}>{p.Value ()}</{p.Name}>");
				}
			}
			sb.AppendLine ("\t</PropertyGroup>");
			sb.AppendLine ("\t<ItemGroup>");
			foreach (var pr in PackageReferences) {
				sb.AppendLine ($"\t\t<PackageReference Include=\"{pr.Id}\" Version=\"{pr.Version}\"/>");
			}
			sb.AppendLine ("\t</ItemGroup>");
			sb.AppendLine ("\t<ItemGroup>");
			foreach (var bi in OtherBuildItems) {
				sb.Append ($"\t\t<{bi.BuildAction} ");
				if (bi.Include != null) sb.Append ($"Include=\"{bi.Include ()}\" ");
				if (bi.Update != null) sb.Append ($"Update=\"{bi.Update ()}\" ");
				if (bi.Remove != null) sb.Append ($"Remove=\"{bi.Remove ()}\" ");
				if (bi.Generator != null) sb.Append ($"Generator=\"{bi.Generator ()}\" ");
				if (bi.DependentUpon != null) sb.Append ($"DependentUpon=\"{bi.DependentUpon ()}\" ");
				if (bi.Version != null) sb.Append ($"Version=\"{bi.Version ()}\" ");
				if (bi.SubType != null) sb.Append ($"SubType=\"{bi.SubType ()}\" ");
				if (bi.Metadata.Any ()) {
					sb.AppendLine ($"\t\t/>");
				} else {
					sb.AppendLine ($">");
					foreach (var kvp in bi.Metadata) {
						sb.AppendLine ($"\t\t\t<{kvp.Key}>{kvp.Value}</{kvp.Key}>");
					}
					sb.AppendLine ($"\t\t</{bi.BuildAction}>");
				}
			}
			sb.AppendLine ("\t</ItemGroup>");
			return $"<Project Sdk=\"{Sdk}\">\r\n{sb.ToString ()}\r\n</Project>";
		}

		public override void NuGetRestore (string directory, string packagesDirectory = null)
		{
		}
	}
}
