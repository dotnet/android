using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	public abstract class XamarinAndroidCommonProject : XamarinAndroidProject
	{
		public IList<BuildItem> AndroidResources { get; private set; }

		public static readonly byte[] icon_binary_mdpi;
		public static readonly byte[] icon_binary_hdpi;
		public static readonly byte[] icon_binary_xhdpi;
		public static readonly byte[] icon_binary_xxhdpi;
		public static readonly byte[] icon_binary_xxxhdpi;

		BuildItem.Source resourceDesigner;

		static XamarinAndroidCommonProject ()
		{
			var stream = typeof(XamarinAndroidCommonProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.Icon.png");
			icon_binary_mdpi = new byte [stream.Length];
			stream.Read (icon_binary_mdpi, 0, (int) stream.Length);

			icon_binary_hdpi    = icon_binary_mdpi;
			icon_binary_xhdpi   = icon_binary_mdpi;
			icon_binary_xxhdpi  = icon_binary_mdpi;
			icon_binary_xxxhdpi = icon_binary_mdpi;
		}

		protected XamarinAndroidCommonProject (string debugConfigurationName = "Debug", string releaseConfigurationName = "Release")
			: base (debugConfigurationName, releaseConfigurationName)
		{
			AndroidResources = new List<BuildItem> ();
			ItemGroupList.Add (AndroidResources);
			resourceDesigner = new BuildItem.Source (() => "Resources\\Resource.designer" + Language.DefaultDesignerExtension) { TextContent = () => string.Empty };
			Sources.Add (resourceDesigner);
			AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\drawable-mdpi\\Icon.png") { BinaryContent = () => icon_binary_mdpi });
			AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\drawable-hdpi\\Icon.png") { BinaryContent = () => icon_binary_hdpi });
			AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\drawable-xhdpi\\Icon.png") { BinaryContent = () => icon_binary_xhdpi });
			AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\drawable-xxhdpi\\Icon.png") { BinaryContent = () => icon_binary_xxhdpi });
			AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\drawable-xxxhdpi\\Icon.png") { BinaryContent = () => icon_binary_xxxhdpi });
			//AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\drawable-nodpi\\Icon.png") { BinaryContent = () => icon_binary });
		}
		
		public override string ProjectTypeGuid {
			get { return "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC"; }
		}

		public override ProjectRootElement Construct ()
		{
			var root = base.Construct ();
			root.AddImport (XamarinAndroidLanguage.NormalProjectImport);
			foreach (var import in Imports) {
				var projectName = import.Project ();
				if (projectName != "Directory.Build.props" && projectName != "Directory.Build.targets")
					root.AddImport (projectName);
			}
			return root;
		}

		public override ProjectLanguage Language {
			get {
				return base.Language;
			}
			set {
				base.Language = value;
				if (value == XamarinAndroidProjectLanguage.FSharp) {
					// add the stuff needed for FSharp
					References.Add (new BuildItem.Reference ("System.Numerics"));
					PackageReferences.Add (KnownPackages.FSharp_Core_Latest);
					PackageReferences.Add (KnownPackages.Xamarin_Android_FSharp_ResourceProvider);
					Sources.Remove (resourceDesigner);
					OtherBuildItems.Add (new BuildItem.NoActionResource (() => "Resources\\Resource.designer" + Language.DefaultDesignerExtension) { TextContent = () => string.Empty });

					if (Builder.UseDotNet) {
						// Use KnownPackages.FSharp_Core_Latest for FSharp.Core
						SetProperty ("DisableImplicitFSharpCoreReference", "true");
					}
				}
			}
		}
	}
}
