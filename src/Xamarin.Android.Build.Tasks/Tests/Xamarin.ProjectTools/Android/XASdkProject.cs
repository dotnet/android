using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

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

		/// <summary>
		/// Save a NuGet.config file to a directory, with sources to support a local build of Microsoft.Android.Sdk
		/// </summary>
		public static void SaveNuGetConfig (string directory)
		{
			var doc = XDocument.Load (Path.Combine (XABuildPaths.TopDirectory, "NuGet.config"));
			var project = new XASdkProject ();
			project.AddNuGetConfigSources (doc);
			doc.Save (Path.Combine (directory, "NuGet.config"));
		}

		/// <summary>
		/// Save a global.json to a directory, with version number to support a local build of  Microsoft.Android.Sdk
		/// </summary>
		public static void SaveGlobalJson (string directory)
		{
			File.WriteAllText (Path.Combine (directory, "global.json"),
$@"{{
    ""sdk"": {{
        ""version"": ""5.0"",
        ""rollForward"": ""latestMajor""
    }},
    ""msbuild-sdks"": {{
        ""Microsoft.Android.Sdk"": ""{SdkVersion}""
    }}
}}");
		}

		public string PackageName { get; set; }
		public string JavaPackageName { get; set; }

		public XASdkProject (string outputType = "Exe")
		{
			Sdk = $"Microsoft.Android.Sdk/{SdkVersion}";
			TargetFramework = "net5.0";

			TargetSdkVersion = AndroidSdkResolver.GetMaxInstalledPlatform ().ToString ();
			PackageName = PackageName ?? string.Format ("{0}.{0}", ProjectName);
			JavaPackageName = JavaPackageName ?? PackageName.ToLowerInvariant ();
			GlobalPackagesFolder = Path.Combine (XABuildPaths.TopDirectory, "packages");
			SetProperty (KnownProperties.OutputType, outputType);

			// Add relevant Android content to our project without writing it to the .csproj file
			if (outputType == "Exe") {
				Sources.Add (new BuildItem.Source ("Properties\\AndroidManifest.xml") {
					TextContent = ProcessManifestTemplate
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

		public string AndroidManifest { get; set; } = default_android_manifest;

		/// <summary>
		/// Defaults to AndroidSdkResolver.GetMaxInstalledPlatform ()
		/// </summary>
		public string TargetSdkVersion { get; set; }

		/// <summary>
		/// Defaults to API 19
		/// </summary>
		public string MinSdkVersion { get; set; } = "19";

		public override void Populate (string directory, IEnumerable<ProjectResource> projectFiles)
		{
			base.Populate (directory, projectFiles);

			SaveGlobalJson (Path.Combine (Root, directory));
		}

		public virtual string ProcessManifestTemplate ()
		{
			var uses_sdk = new StringBuilder ("<uses-sdk ");
			if (!string.IsNullOrEmpty (MinSdkVersion)) {
				uses_sdk.Append ("android:minSdkVersion=\"");
				uses_sdk.Append (MinSdkVersion);
				uses_sdk.Append ("\" ");
			}
			if (!string.IsNullOrEmpty (TargetSdkVersion)) {
				uses_sdk.Append ("android:targetSdkVersion=\"");
				uses_sdk.Append (TargetSdkVersion);
				uses_sdk.Append ("\" ");
			}
			uses_sdk.Append ("/>");

			return AndroidManifest
				.Replace ("${PROJECT_NAME}", ProjectName)
				.Replace ("${PACKAGENAME}", PackageName)
				.Replace ("${USES_SDK}", uses_sdk.ToString ());
		}

		public override string ProcessSourceTemplate (string source)
		{
			return source.Replace ("${ROOT_NAMESPACE}", RootNamespace ?? ProjectName)
				.Replace ("${PROJECT_NAME}", ProjectName)
				.Replace ("${PACKAGENAME}", PackageName)
				.Replace ("${JAVA_PACKAGENAME}", JavaPackageName);
		}
	}
}
