using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.ProjectTools
{
	public class XASdkProject : DotNetStandard
	{
		const string default_strings_xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""hello"">Hello World, Click Me!</string>
	<string name=""app_name"">${PROJECT_NAME}</string>
</resources>
";

		readonly string default_layout_main;
		readonly string default_main_activity_cs;
		readonly string default_android_manifest;
		readonly byte [] icon_binary_mdpi;

		public string PackageName { get; set; }
		public string JavaPackageName { get; set; }

		public XASdkProject (string sdkVersion = "")
		{
			Sdk = string.IsNullOrEmpty (sdkVersion) ? "Xamarin.Android.Sdk" : $"Xamarin.Android.Sdk/{sdkVersion}";
			TargetFramework = "MonoAndroid10.0";

			PackageName = PackageName ?? string.Format ("{0}.{0}", ProjectName);
			JavaPackageName = JavaPackageName ?? PackageName.ToLowerInvariant ();
			ExtraNuGetConfigSources = new List<string> { Path.Combine (XABuildPaths.PrefixDirectory, "nupkgs") };
			GlobalPackagesFolder = Path.Combine (XABuildPaths.TopDirectory, "packages");

			using (var sr = new StreamReader (typeof (XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.AndroidManifest.xml")))
				default_android_manifest = sr.ReadToEnd ();
			using (var sr = new StreamReader (typeof (XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.MainActivity.cs")))
				default_main_activity_cs = sr.ReadToEnd ();
			using (var sr = new StreamReader (typeof (XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.LayoutMain.axml")))
				default_layout_main = sr.ReadToEnd ();
			using (var stream = typeof (XamarinAndroidCommonProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.Icon.png")) {
				icon_binary_mdpi = new byte [stream.Length];
				stream.Read (icon_binary_mdpi, 0, (int) stream.Length);
			}

			// Add relevant Android content to our project without writing it to the .csproj file
			Sources.Add (new BuildItem.Source ("Properties\\AndroidManifest.xml") {
				TextContent = () => default_android_manifest.Replace ("${PROJECT_NAME}", ProjectName).Replace ("${PACKAGENAME}", string.Format ("{0}.{0}", ProjectName))
			});
			Sources.Add (new BuildItem.Source ($"MainActivity{Language.DefaultExtension}") { TextContent = () => ProcessSourceTemplate (default_main_activity_cs) });
			Sources.Add (new BuildItem.Source ("Resources\\layout\\Main.axml") { TextContent = () => default_layout_main });
			Sources.Add (new BuildItem.Source ("Resources\\values\\Strings.xml") { TextContent = () => default_strings_xml.Replace ("${PROJECT_NAME}", ProjectName) });
			Sources.Add (new BuildItem.Source ("Resources\\drawable-mdpi\\Icon.png") { BinaryContent = () => icon_binary_mdpi });
			Sources.Add (new BuildItem.Source ($"Resources\\Resource.designer{Language.DefaultExtension}") { TextContent = () => string.Empty });
		}

		public string Configuration => IsRelease ? "Release" : "Debug";

		public string OutputPath => Path.Combine ("bin", Configuration, TargetFramework.ToLowerInvariant ());

		public string IntermediateOutputPath => Path.Combine ("obj", Configuration, TargetFramework.ToLowerInvariant ());

		public override string ProcessSourceTemplate (string source)
		{
			return source.Replace ("${ROOT_NAMESPACE}", RootNamespace ?? ProjectName)
				.Replace ("${PROJECT_NAME}", ProjectName)
				.Replace ("${PACKAGENAME}", PackageName)
				.Replace ("${JAVA_PACKAGENAME}", JavaPackageName);
		}

	}
}
