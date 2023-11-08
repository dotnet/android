using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using Microsoft.Build.Construction;
using Xamarin.Android.Tools;

namespace Xamarin.ProjectTools
{
	public class XamarinAndroidApplicationProject : XamarinAndroidCommonProject
	{
		const string default_strings_xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""hello"">Hello World, Click Me!</string>
	<string name=""app_name"">${PROJECT_NAME}</string>
</resources>
";

		static readonly string default_layout_main;
		static readonly string default_main_activity_cs, default_main_activity_fs;
		static readonly string default_android_manifest;

		static XamarinAndroidApplicationProject ()
		{
			using (var sr = new StreamReader (typeof(XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ($"Xamarin.ProjectTools.Resources.DotNet.MainActivity.cs")))
				default_main_activity_cs = sr.ReadToEnd ();
			using (var sr = new StreamReader (typeof(XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.MainActivity.fs")))
				default_main_activity_fs = sr.ReadToEnd ();
			using (var sr = new StreamReader (typeof(XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.LayoutMain.axml")))
				default_layout_main = sr.ReadToEnd ();
			using (var sr = new StreamReader (typeof(XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.AndroidManifest.xml")))
				default_android_manifest = sr.ReadToEnd ();

		}

		public XamarinAndroidApplicationProject (string debugConfigurationName = "Debug", string releaseConfigurationName = "Release", [CallerMemberName] string packageName = "")
			: base (debugConfigurationName, releaseConfigurationName)
		{
			SetProperty (KnownProperties.OutputType, "Exe");
			SetProperty (KnownProperties.Nullable, "enable");
			SetProperty (KnownProperties.ImplicitUsings, "enable");
			SetProperty ("XamarinAndroidSupportSkipVerifyVersions", "True");
			SetProperty ("_FastDeploymentDiagnosticLogging", "True");
			SupportedOSPlatformVersion = "21.0";

			// Workaround for AndroidX, see: https://github.com/xamarin/AndroidSupportComponents/pull/239
			Imports.Add (new Import (() => "Directory.Build.targets") {
				TextContent = () =>
					@"<Project>
						<PropertyGroup>
							<VectorDrawableCheckBuildToolsVersionTaskBeforeTargets />
						</PropertyGroup>
					</Project>"
			});

			AndroidManifest = default_android_manifest;
			LayoutMain = default_layout_main;
			StringsXml = default_strings_xml;
			PackageName = $"com.xamarin.{(packageName ?? ProjectName).ToLower ()}";
			JavaPackageName = JavaPackageName ?? PackageName.ToLowerInvariant ();

			OtherBuildItems.Add (new BuildItem.NoActionResource ("Properties\\AndroidManifest.xml") {
				TextContent = ProcessManifestTemplate,
			});
			AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\Main.axml") { TextContent = () => LayoutMain });
			AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\values\\Strings.xml") { TextContent = () => StringsXml.Replace ("${PROJECT_NAME}", ProjectName) });

			Sources.Add (new BuildItem.Source (() => "MainActivity" + Language.DefaultExtension) { TextContent = () => ProcessSourceTemplate (MainActivity ?? DefaultMainActivity) });
		}

		// it is exposed as public because we may want to slightly modify this.
		public virtual string DefaultMainActivity {
			get { return Language == XamarinAndroidProjectLanguage.FSharp ? default_main_activity_fs : default_main_activity_cs; }
		}

		/// <summary>
		/// Defaults to AndroidSdkResolver.GetMaxInstalledPlatform ()
		/// </summary>
		public string TargetSdkVersion { get; set; }

		/// <summary>
		/// Set this to add the `android:minSdkVersion` attribute to the AndroidManifest.xml file
		/// </summary>
		public string MinSdkVersion { get; set; }

		/// <summary>
		/// Defaults to 21.0
		/// </summary>
		public string SupportedOSPlatformVersion {
			get { return GetProperty (KnownProperties.SupportedOSPlatformVersion); }
			set { SetProperty (KnownProperties.SupportedOSPlatformVersion, value); }
		}

		public bool AotAssemblies {
			get { return string.Equals (GetProperty (KnownProperties.RunAOTCompilation), "True", StringComparison.OrdinalIgnoreCase); }
			set { SetProperty (KnownProperties.RunAOTCompilation, value.ToString ()); }
		}

		public bool AndroidEnableProfiledAot {
			get { return string.Equals (GetProperty (KnownProperties.AndroidEnableProfiledAot), "True", StringComparison.OrdinalIgnoreCase); }
			set { SetProperty (KnownProperties.AndroidEnableProfiledAot, value.ToString ()); }
		}

		public bool EnableDesugar {
			get { return string.Equals (GetProperty (KnownProperties.AndroidEnableDesugar), "True", StringComparison.OrdinalIgnoreCase); }
			set { SetProperty (KnownProperties.AndroidEnableDesugar, value.ToString ()); }
		}

		public bool Deterministic {
			get { return string.Equals (GetProperty (KnownProperties.Deterministic), "True", StringComparison.OrdinalIgnoreCase); }
			set { SetProperty (KnownProperties.Deterministic, value.ToString ()); }
		}

		public bool EmbedAssembliesIntoApk {
			get { return string.Equals (GetProperty (KnownProperties.EmbedAssembliesIntoApk), "True", StringComparison.OrdinalIgnoreCase); }
			set { SetProperty (KnownProperties.EmbedAssembliesIntoApk, value.ToString ()); }
		}

		public string ManifestMerger {
			get { return GetProperty (KnownProperties.AndroidManifestMerger); }
			set { SetProperty (KnownProperties.AndroidManifestMerger, value); }
		}

		public string LinkTool {
			get { return GetProperty (KnownProperties.AndroidLinkTool); }
			set { SetProperty (KnownProperties.AndroidLinkTool, value); }
		}

		public string AndroidFastDeploymentType {
			get { return GetProperty (KnownProperties.AndroidFastDeploymentType); }
			set { SetProperty (KnownProperties.AndroidFastDeploymentType, value); }
		}

		public bool UseJackAndJill {
			get { return string.Equals (GetProperty (KnownProperties.UseJackAndJill), "True", StringComparison.OrdinalIgnoreCase); }
			set { SetProperty (KnownProperties.UseJackAndJill, value.ToString ()); }
		}

		public AndroidLinkMode AndroidLinkModeDebug {
			get {
				AndroidLinkMode m;
				return Enum.TryParse<AndroidLinkMode> (GetProperty (DebugProperties, KnownProperties.AndroidLinkMode), out m) ? m : AndroidLinkMode.None;
			}
			set { SetProperty (DebugProperties, KnownProperties.AndroidLinkMode, value.ToString ()); }
		}

		public AndroidLinkMode AndroidLinkModeRelease {
			get {
				AndroidLinkMode m;
				return Enum.TryParse<AndroidLinkMode> (GetProperty (ReleaseProperties, KnownProperties.AndroidLinkMode), out m) ? m : AndroidLinkMode.None;
			}
			set { SetProperty (ReleaseProperties, KnownProperties.AndroidLinkMode, value.ToString ()); }
		}

		public bool EnableMarshalMethods {
			get { return string.Equals (GetProperty (KnownProperties.AndroidEnableMarshalMethods), "True", StringComparison.OrdinalIgnoreCase); }
			set { SetProperty (KnownProperties.AndroidEnableMarshalMethods, value.ToString ()); }
		}

		public string AndroidManifest { get; set; }
		public string LayoutMain { get; set; }
		public string MainActivity { get; set; }
		public string StringsXml { get; set; }
		public string PackageName { get; set; }

		public string PackageNameJavaIntermediatePath { get { return PackageName.Replace ('.', Path.DirectorySeparatorChar).ToLower ();}}
		public string JavaPackageName { get; set; }

		public override BuildOutput CreateBuildOutput (ProjectBuilder builder)
		{
			return new AndroidApplicationBuildOutput (this) { Builder = builder };
		}

		public void SetDefaultTargetDevice ()
		{
			SetProperty ("AdbTarget", Environment.GetEnvironmentVariable ("ADB_TARGET"));
		}

		public virtual string ProcessManifestTemplate ()
		{
			var uses_sdk = new StringBuilder ();
			if (!string.IsNullOrEmpty (MinSdkVersion)) {
				uses_sdk.Append ("<uses-sdk ");
				uses_sdk.Append ("android:minSdkVersion=\"");
				uses_sdk.Append (MinSdkVersion);
				uses_sdk.Append ("\" ");
			}
			if (!string.IsNullOrEmpty (TargetSdkVersion)) {
				if (uses_sdk.Length == 0)
					uses_sdk.Append ("<uses-sdk ");
				uses_sdk.Append ("android:targetSdkVersion=\"");
				uses_sdk.Append (TargetSdkVersion);
				uses_sdk.Append ("\" ");
			}
			if (uses_sdk.Length > 0)
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
