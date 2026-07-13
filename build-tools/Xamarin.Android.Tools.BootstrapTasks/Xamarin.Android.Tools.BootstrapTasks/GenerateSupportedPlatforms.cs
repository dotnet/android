#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	/// <summary>
	/// Generates Microsoft.Android.Sdk.SupportedPlatforms.props
	/// Similar to: https://github.com/dotnet/sdk/blob/18ee4eac8b3abe6d554d2e0c39d8952da0f23ce5/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.WindowsSupportedTargetPlatforms.props
	/// </summary>
	public class GenerateSupportedPlatforms : Task
	{
		/// <summary>
		/// @(AndroidApiInfo) from src\Mono.Android\Mono.Android.Apis.projitems
		/// </summary>
		[Required]
		public ITaskItem [] AndroidApiInfo { get; set; } = [];

		/// <summary>
		/// The output file to generate
		/// </summary>
		[Required]
		public string OutputFile { get; set; } = "";

		/// <summary>
		/// $(AndroidMinimumDotNetApiLevel) from Configuration.props
		/// </summary>
		[Required]
		public string? MinimumApiLevel { get; set; }

		/// <summary>
		/// Default value for $(TargetPlatformVersion), defaults to MaxStableVersion.ApiLevel
		/// </summary>
		public string? TargetApiLevel { get; set; }

		public override bool Execute ()
		{

			var minVersion        = ToVersion (MinimumApiLevel);
			var targetVersion     = ToVersion (TargetApiLevel);
			var versions          = AndroidApiInfo.Select (ToAndroidVersion).ToArray ();
			var maxStableVersion  = versions
				.Where (version => version.Stable)
				.OrderBy (version => version.TargetFrameworkVersion)
				.Last ();
			var targetApiLevel    = targetVersion != null && targetVersion.Major > 0
				? targetVersion
				: maxStableVersion.VersionCodeFull;
			var settings = new XmlWriterSettings {
				OmitXmlDeclaration = true,
				Indent = true,
			};
			using (var writer = XmlWriter.Create (OutputFile, settings)) {

				writer.WriteComment ($@"
***********************************************************************************************
{Path.GetFileName (OutputFile)}

Specifies the supported Android platform versions for this SDK.

***********************************************************************************************
");
				writer.WriteStartElement ("Project");

				writer.WriteStartElement ("PropertyGroup");
				writer.WriteStartElement ("TargetPlatformSupported");
				writer.WriteString ("true");
				writer.WriteEndElement (); // </TargetPlatformSupported>
				writer.WriteStartElement ("TargetPlatformVersion");
				writer.WriteAttributeString ("Condition", " '$(TargetPlatformVersion)' == '' ");
				writer.WriteString (targetApiLevel.Major.ToString ());
				writer.WriteString (".");
				writer.WriteString (targetApiLevel.Minor.ToString ());
				writer.WriteEndElement (); // </TargetPlatformVersion>
				writer.WriteStartElement ("AndroidMinimumSupportedApiLevel");
				writer.WriteAttributeString ("Condition", " '$(AndroidMinimumSupportedApiLevel)' == '' ");
				writer.WriteString (MinimumApiLevel?.ToString () ?? "");
				writer.WriteEndElement (); // </AndroidMinimumSupportedApiLevel>
				writer.WriteEndElement (); // </PropertyGroup>

				writer.WriteStartElement ("ItemGroup");
				foreach (Version versionCode in versions
						.Select (version => version.VersionCodeFull)
						.Where (version => version >= minVersion)
						.Distinct ()
						.OrderBy (version => version)) {
					writer.WriteStartElement ("AndroidSdkSupportedTargetPlatformVersion");
					writer.WriteAttributeString ("Include", versionCode.ToString ());
					if (versionCode < targetVersion) {
						writer.WriteAttributeString ("DefineConstantsOnly", "true");
					}
					writer.WriteEndElement (); // </AndroidSdkSupportedTargetPlatformVersion>
				}
				writer.WriteStartElement ("SdkSupportedTargetPlatformVersion");
				writer.WriteAttributeString ("Include", "@(AndroidSdkSupportedTargetPlatformVersion)");

				writer.WriteEndDocument (); // </Project>
			}

			return !Log.HasLoggedErrors;
		}

		static Version? ToVersion (string? value)
		{
			if (string.IsNullOrEmpty (value)) {
				return null;
            }
			if (Version.TryParse (value, out var version)) {
				return version;
			}
			if (int.TryParse (value, out var major)) {
				return new Version (major, 0);
			}
			return null;
		}

		static (Version VersionCodeFull, Version TargetFrameworkVersion, bool Stable) ToAndroidVersion (ITaskItem item)
		{
			/*
			<AndroidApiInfo Include="v16.0.99">
				<Name>CANARY</Name>
				<Level>36</Level>
				<VersionCodeFull>36.1</VersionCodeFull>
				<Id>CANARY</Id>
				<Stable>False</Stable>
			</AndroidApiInfo>
			*/

			if (!Version.TryParse (item.GetMetadata ("VersionCodeFull"), out var versionCodeFull)) {
				throw new ArgumentException ($"Invalid VersionCodeFull '{item.GetMetadata ("VersionCodeFull")}' for item '{item.ItemSpec}'");
			}
			if (!Version.TryParse (item.ItemSpec.TrimStart ('v'), out var targetFrameworkVersion)) {
				throw new ArgumentException ($"Invalid framework version '{item.ItemSpec}'");
			}
			bool.TryParse (item.GetMetadata ("Stable"), out bool stable);

			return (versionCodeFull, targetFrameworkVersion, stable);
		}
	}
}
