using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Xamarin.ProjectTools
{
	public class XASdkProject : DotNetStandard
	{
		public static readonly string SdkVersion = typeof (XASdkProject).Assembly
			.GetCustomAttributes<AssemblyMetadataAttribute> ()
			.Where (attr => attr.Key == "SdkVersion")
			.Select (attr => attr.Value)
			.FirstOrDefault () ?? "0.0.1";

		const string default_strings_xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""hello"">Hello World, Click Me!</string>
	<string name=""app_name"">${PROJECT_NAME}</string>
</resources>
";

		static readonly string default_layout_main;
		static readonly string default_main_activity_cs;
		static readonly string default_android_manifest;
		static readonly byte [] icon_binary_mdpi;

		static XASdkProject ()
		{
			var assembly = typeof (XASdkProject).Assembly;
			using (var sr = new StreamReader (assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.AndroidManifest.xml")))
				default_android_manifest = sr.ReadToEnd ();
			using (var sr = new StreamReader (assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.MainActivity.cs")))
				default_main_activity_cs = sr.ReadToEnd ();
			using (var sr = new StreamReader (assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.LayoutMain.axml")))
				default_layout_main = sr.ReadToEnd ();
			using (var stream = assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.Icon.png")) {
				icon_binary_mdpi = new byte [stream.Length];
				stream.Read (icon_binary_mdpi, 0, (int) stream.Length);
			}
		}

		public string PackageName { get; set; }
		public string JavaPackageName { get; set; }
		public string AndroidManifest { get; set; }

		public XASdkProject (string outputType = "Exe")
		{
			Sdk = $"Microsoft.Android.Sdk/{SdkVersion}";
			TargetFramework = "net5.0";

			PackageName = PackageName ?? string.Format ("{0}.{0}", ProjectName);
			JavaPackageName = JavaPackageName ?? PackageName.ToLowerInvariant ();
			AndroidManifest = default_android_manifest;
			GlobalPackagesFolder = Path.Combine (XABuildPaths.TopDirectory, "packages");
			SetProperty (KnownProperties.OutputType, outputType);

			// Add relevant Android content to our project without writing it to the .csproj file
			if (outputType == "Exe") {
				Sources.Add (new BuildItem.Source ("Properties\\AndroidManifest.xml") {
					TextContent = () => AndroidManifest.Replace ("${PROJECT_NAME}", ProjectName).Replace ("${PACKAGENAME}", string.Format ("{0}.{0}", ProjectName))
				});
			}
			Sources.Add (new BuildItem.Source ($"MainActivity{Language.DefaultExtension}") { TextContent = () => ProcessSourceTemplate (MainActivity ?? DefaultMainActivity) });
			Sources.Add (new BuildItem.Source ("Resources\\layout\\Main.axml") { TextContent = () => default_layout_main });
			Sources.Add (new BuildItem.Source ("Resources\\values\\Strings.xml") { TextContent = () => default_strings_xml.Replace ("${PROJECT_NAME}", ProjectName) });
			Sources.Add (new BuildItem.Source ("Resources\\drawable-mdpi\\Icon.png") { BinaryContent = () => icon_binary_mdpi });
			Sources.Add (new BuildItem.Source ($"Resources\\Resource.designer{Language.DefaultExtension}") { TextContent = () => string.Empty });
		}

		protected override bool SetExtraNuGetConfigSources => true;

		public string OutputPath => Path.Combine ("bin", Configuration, TargetFramework.ToLowerInvariant ());

		public string IntermediateOutputPath => Path.Combine ("obj", Configuration, TargetFramework.ToLowerInvariant ());

		public string DefaultMainActivity => default_main_activity_cs;

		public string MainActivity { get; set; }

		public override string ProcessSourceTemplate (string source)
		{
			return source.Replace ("${ROOT_NAMESPACE}", RootNamespace ?? ProjectName)
				.Replace ("${PROJECT_NAME}", ProjectName)
				.Replace ("${PACKAGENAME}", PackageName)
				.Replace ("${JAVA_PACKAGENAME}", JavaPackageName);
		}
	}
}
