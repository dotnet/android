using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	public abstract class DotNetXamarinProject : XamarinProject
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

			AddReferences ("System"); // default

			SetProperty (KnownProperties.Configuration, debugConfigurationName, "'$(Configuration)' == ''");
			SetProperty ("Platform", "AnyCPU", "'$(Platform)' == ''");
			SetProperty ("ErrorReport", "prompt");
			SetProperty ("WarningLevel", "4");
			SetProperty ("ConsolePause", "false");
			SetProperty ("RootNamespace", () => RootNamespace ?? ProjectName);
			SetProperty ("AssemblyName", () => AssemblyName ?? ProjectName);
			SetProperty ("BaseIntermediateOutputPath", "obj\\", " '$(BaseIntermediateOutputPath)' == '' ");

			SetProperty (DebugProperties, "DebugSymbols", "true");
			SetProperty (DebugProperties, "DebugType", "full");
			SetProperty (DebugProperties, "Optimize", "false");
			SetProperty (DebugProperties, KnownProperties.OutputPath, Path.Combine ("bin", debugConfigurationName));
			SetProperty (DebugProperties, "DefineConstants", "DEBUG;");
			SetProperty (DebugProperties, KnownProperties.IntermediateOutputPath, Path.Combine ("obj", debugConfigurationName));

			SetProperty (ReleaseProperties, "Optimize", "true");
			SetProperty (ReleaseProperties, "ErrorReport", "prompt");
			SetProperty (ReleaseProperties, "WarningLevel", "4");
			SetProperty (ReleaseProperties, "ConsolePause", "false");
			SetProperty (ReleaseProperties, KnownProperties.OutputPath, Path.Combine ("bin", releaseConfigurationName));
			SetProperty (ReleaseProperties, KnownProperties.IntermediateOutputPath, Path.Combine ("obj", releaseConfigurationName));

			Sources.Add (new BuildItem.Source (() => "Properties\\AssemblyInfo" + Language.DefaultExtension) { TextContent = () => ProcessSourceTemplate (AssemblyInfo ?? Language.DefaultAssemblyInfo) });
		}

		void AddProperties (IList<Property> list, string condition, KeyValuePair<string, string> [] props)
		{
			foreach (var p in props)
				list.Add (new Property (condition, p.Key, p.Value));
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

		public BuildItem GetItem (string include)
		{
			return ItemGroupList.SelectMany (g => g).First (i => i.Include ().Equals (include, StringComparison.OrdinalIgnoreCase));
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

		public void Touch (params string [] itemPaths)
		{
			foreach (var item in itemPaths)
				GetItem (item).Timestamp = DateTimeOffset.UtcNow;
		}

		public virtual ProjectRootElement Construct ()
		{
			var root = ProjectRootElement.Create ();
			if (Packages.Any ())
				root.AddItemGroup ().AddItem (BuildActions.None, "packages.config");
			foreach (var pkg in Packages.Where (p => p.AutoAddReferences))
				foreach (var reference in pkg.References)
					if (!References.Any (r => r.Include == reference.Include))
						References.Add (reference);
			
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
			var root = Construct ();
			var sw = new StringWriter ();
			root.Save (sw);
			var document = XDocument.Parse (sw.ToString ());
			var pn = XName.Get ("Project", "http://schemas.microsoft.com/developer/msbuild/2003");
			var p = document.Element (pn);
			if (p != null) {
				var referenceGroup = p.Elements ().FirstOrDefault (x => x.Name.LocalName == "ItemGroup" &&  x.HasElements && x.Elements ().Any (e => e.Name.LocalName == "Reference"));
				if (referenceGroup != null) {
					foreach (var pr in PackageReferences) {
						//NOTE: without the namespace it puts xmlns=""
						XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
						var e = XElement.Parse ($"<PackageReference Include=\"{pr.Id}\" Version=\"{pr.Version}\"/>");
						e.Name = ns + e.Name.LocalName;
						referenceGroup.Add (e);
					}
					sw = new StringWriter ();
					document.Save (sw);
				}
			}
			return sw.ToString ();
		}
	}
}
