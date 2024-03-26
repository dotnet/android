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
			AndroidJavaSources = new List<BuildItem> ();

			ItemGroupList.Add (References);
			ItemGroupList.Add (OtherBuildItems);
			ItemGroupList.Add (Sources);
			ItemGroupList.Add (AndroidJavaSources);

			SetProperty ("RootNamespace", () => RootNamespace ?? ProjectName);
			SetProperty ("AssemblyName", () => AssemblyName ?? ProjectName);

			if (Builder.UseDotNet) {
				TargetFramework = "net8.0-android";
				EnableDefaultItems = false;
				AppendTargetFrameworkToOutputPath = false;
			} else {
				AddReferences ("System"); // default
				SetProperty ("Platform", "AnyCPU", "'$(Platform)' == ''");
				SetProperty ("ErrorReport", "prompt");
				SetProperty ("WarningLevel", "4");
				SetProperty ("ConsolePause", "false");

				SetProperty ("BaseIntermediateOutputPath", "obj\\", " '$(BaseIntermediateOutputPath)' == '' ");

				SetProperty (DebugProperties, "DebugSymbols", "true");
				SetProperty (DebugProperties, "DebugType", "portable");
				SetProperty (DebugProperties, "Optimize", "false");
				SetProperty (DebugProperties, "DefineConstants", "DEBUG;");

				SetProperty (ReleaseProperties, "Optimize", "true");
				SetProperty (ReleaseProperties, "ErrorReport", "prompt");
				SetProperty (ReleaseProperties, "WarningLevel", "4");
				SetProperty (ReleaseProperties, "ConsolePause", "false");
			}

			// These are always set, so the OutputPath and IntermediateOutputPath properties work
			SetProperty (DebugProperties,   KnownProperties.OutputPath,             Path.Combine ("bin", debugConfigurationName));
			SetProperty (DebugProperties,   KnownProperties.IntermediateOutputPath, Path.Combine ("obj", debugConfigurationName));
			SetProperty (ReleaseProperties, KnownProperties.OutputPath,             Path.Combine ("bin", releaseConfigurationName));
			SetProperty (ReleaseProperties, KnownProperties.IntermediateOutputPath, Path.Combine ("obj", releaseConfigurationName));

			Sources.Add (new BuildItem.Source (() => "Properties\\AssemblyInfo" + Language.DefaultExtension) { TextContent = () => ProcessSourceTemplate (AssemblyInfo ?? Language.DefaultAssemblyInfo) });
		}

		public IList<BuildItem> OtherBuildItems { get; private set; }
		public IList<BuildItem> Sources { get; private set; }
		public IList<BuildItem> AndroidJavaSources { get; private set; }

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
			// Workaround for https://github.com/dotnet/msbuild/issues/2554 when using Microsoft.Build.Construction.ProjectRootElement.Create
			var msbuildExePathVarName = "MSBUILD_EXE_PATH";
			if (!Builder.UseDotNet && !TestEnvironment.IsWindows) {
				Environment.SetEnvironmentVariable (msbuildExePathVarName, typeof (DotNetXamarinProject).Assembly.Location);
			}
			ProjectRootElement root = null;
			try {
				root = ProjectRootElement.Create ();
			} finally {
				if (!Builder.UseDotNet && !TestEnvironment.IsWindows) {
					Environment.SetEnvironmentVariable (msbuildExePathVarName, null);
				}
			}

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
			if (Builder.UseDotNet) {
				return XmlUtils.ToXml (this);
			}
			XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
			var encoding = Encoding.UTF8;
			var root = Construct ();
			XDocument document;
			using (var stream = new MemoryStream ())
			using (var sw = new StreamWriter (stream, encoding, 8 * 1024, leaveOpen: true)) {
				root.Save (sw);
				stream.Position = 0;
				document = XDocument.Load (stream);
				document.Declaration.Encoding = encoding.HeaderName;
				var pn = XName.Get ("Project", ns.NamespaceName);
				var p = document.Element (pn);
				if (p != null) {
					//NOTE: when running tests inside VS 2019 "Current" was set here
					p.SetAttributeValue ("ToolsVersion", "15.0");

					var referenceGroup = p.Elements ().FirstOrDefault (x => x.Name.LocalName == "ItemGroup" && x.HasElements && x.Elements ().Any (e => e.Name.LocalName == "Reference"));
					if (referenceGroup != null) {
						foreach (var pr in PackageReferences) {
							//NOTE: without the namespace it puts xmlns=""
							var e = XElement.Parse ($"<PackageReference Include=\"{pr.Id}\" Version=\"{pr.Version}\"/>");
							e.Name = ns + e.Name.LocalName;
							referenceGroup.Add (e);
						}
					}
				}
				return document.ToString ();
			}
		}
	}
}
