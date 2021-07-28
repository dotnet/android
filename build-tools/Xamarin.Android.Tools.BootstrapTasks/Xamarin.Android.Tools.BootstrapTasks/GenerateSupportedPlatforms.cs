using System.IO;
using System.Globalization;
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
		/// A list of AndroidApiInfo.xml files produced by Mono.Android.targets
		/// </summary>
		[Required]
		public string [] AndroidApiInfo { get; set; }

		/// <summary>
		/// The output file to generate
		/// </summary>
		[Required]
		public string OutputFile { get; set; }

		/// <summary>
		/// $(AndroidMinimumDotNetApiLevel) from Configuration.props
		/// </summary>
		[Required]
		public int MinimumApiLevel { get; set; }

		public override bool Execute ()
		{
			if (AndroidApiInfo.Length == 0) {
				Log.LogError ("This task requires at least one AndroidApiInfo.xml file!");
				return false;
			}

			var versions = new AndroidVersions (
				AndroidApiInfo.Select (d => Path.GetDirectoryName (d)));
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
				writer.WriteString (versions.MaxStableVersion.ApiLevel.ToString ("0.0", CultureInfo.InvariantCulture));
				writer.WriteEndElement (); // </TargetPlatformVersion>
				writer.WriteStartElement ("AndroidMinimumSupportedApiLevel");
				writer.WriteAttributeString ("Condition", " '$(AndroidMinimumSupportedApiLevel)' == '' ");
				writer.WriteString (MinimumApiLevel.ToString ());
				writer.WriteEndElement (); // </AndroidMinimumSupportedApiLevel>
				writer.WriteEndElement (); // </PropertyGroup>

				writer.WriteStartElement ("ItemGroup");
				foreach (AndroidVersion version in versions.InstalledBindingVersions
						.Where (v => v.ApiLevel >= MinimumApiLevel)
						.OrderBy (v => v.ApiLevel)) {
					writer.WriteStartElement ("AndroidSdkSupportedTargetPlatformVersion");
					writer.WriteAttributeString ("Include", version.ApiLevel.ToString ("0.0", CultureInfo.InvariantCulture));
					writer.WriteEndElement (); // </AndroidSdkSupportedTargetPlatformVersion>
				}
				writer.WriteStartElement ("SdkSupportedTargetPlatformVersion");
				writer.WriteAttributeString ("Include", "@(AndroidSdkSupportedTargetPlatformVersion)");

				writer.WriteEndDocument (); // </Project>
			}

			return !Log.HasLoggedErrors;
		}
	}
}
