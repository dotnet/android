using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	public abstract class DotNetXamarinProject : XamarinProject, IShortFormProject
	{
		protected DotNetXamarinProject (string debugConfigurationName = "Debug", string releaseConfigurationName = "Release")
			: base (debugConfigurationName, releaseConfigurationName)
		{
			ProjectName = "UnnamedProject";

			Sources = new List<BuildItem> ();
			OtherBuildItems = new List<BuildItem> ();

			ItemGroupList.Add (References);
			ItemGroupList.Add (OtherBuildItems);
			ItemGroupList.Add (Sources);

			SetProperty ("RootNamespace", () => RootNamespace ?? ProjectName);
			SetProperty ("AssemblyName", () => AssemblyName ?? ProjectName);

			TargetFramework = "net9.0-android";
			EnableDefaultItems = false;
			AppendTargetFrameworkToOutputPath = false;

			// These are always set, so the OutputPath and IntermediateOutputPath properties work
			SetProperty (DebugProperties,   KnownProperties.OutputPath,             Path.Combine ("bin", debugConfigurationName));
			SetProperty (DebugProperties,   KnownProperties.IntermediateOutputPath, Path.Combine ("obj", debugConfigurationName));
			SetProperty (ReleaseProperties, KnownProperties.OutputPath,             Path.Combine ("bin", releaseConfigurationName));
			SetProperty (ReleaseProperties, KnownProperties.IntermediateOutputPath, Path.Combine ("obj", releaseConfigurationName));

			Sources.Add (new BuildItem.Source (() => "Properties\\AssemblyInfo" + Language.DefaultExtension) { TextContent = () => ProcessSourceTemplate (AssemblyInfo ?? Language.DefaultAssemblyInfo) });
		}

		public IList<BuildItem> OtherBuildItems { get; private set; }
		public IList<BuildItem> Sources { get; private set; }

		public IList<Property> ActiveConfigurationProperties {
			get { return IsRelease ? ReleaseProperties : DebugProperties; }
		}

		public string OutputPath {
			get { return GetProperty (ActiveConfigurationProperties, KnownProperties.OutputPath); }
			set { SetProperty (ActiveConfigurationProperties, KnownProperties.OutputPath, value); }
		}

		public string IntermediateOutputPath {
			get { return GetProperty (ActiveConfigurationProperties, KnownProperties.IntermediateOutputPath); }
			set { SetProperty (ActiveConfigurationProperties, KnownProperties.IntermediateOutputPath, value); }
		}

		public string Sdk { get; set; } = "Microsoft.NET.Sdk";

		public bool EnableDefaultItems {
			get { return string.Equals (GetProperty ("EnableDefaultItems"), "true", StringComparison.OrdinalIgnoreCase); }
			set { SetProperty ("EnableDefaultItems", value.ToString ()); }
		}

		public bool AppendTargetFrameworkToOutputPath {
			get { return string.Equals (GetProperty ("AppendTargetFrameworkToOutputPath"), "true", StringComparison.OrdinalIgnoreCase); }
			set { SetProperty ("AppendTargetFrameworkToOutputPath", value.ToString ()); }
		}

		public void AddReferences (params string [] references)
		{
			foreach (var s in references)
				References.Add (new BuildItem.Reference (s));
		}

		public void AddSources (params string [] sources)
		{
			foreach (var s in sources)
				Sources.Add (new BuildItem.Source (s));
		}

		public virtual ProjectRootElement Construct ()
		{
			ProjectRootElement root = ProjectRootElement.Create ();

			foreach (var pg in PropertyGroups)
				pg.AddElement (root);

			foreach (var ig in ItemGroupList) {
				var ige = root.AddItemGroup ();
				foreach (var i in ig) {
					if (i.Deleted)
						continue;
					ige.AddItem (i.BuildAction, i.Include (), i.Metadata);
				}
			}

			root.FullPath = ProjectName + Language.DefaultProjectExtension;

			return root;
		}

		public override string SaveProject ()
		{
			return XmlUtils.ToXml (this);
		}
	}
}
