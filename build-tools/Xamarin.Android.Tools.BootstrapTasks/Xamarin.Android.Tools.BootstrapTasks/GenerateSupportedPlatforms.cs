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
		/// @(AndroidApiInfo) from .\bin\Build$(Configuration)\Mono.Android.Apis.projitems
		/// </summary>
		[Required]
		public ITaskItem [] AndroidApiInfo { get; set; }

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

		/// <summary>
		/// Default value for $(TargetPlatformVersion), defaults to MaxStableVersion.ApiLevel
		/// </summary>
		public int TargetApiLevel { get; set; }

		public override bool Execute ()
		{
			var versions = new AndroidVersions (AndroidApiInfo.Select (ToVersion));
			int targetApiLevel = TargetApiLevel > 0 ? TargetApiLevel : versions.MaxStableVersion.ApiLevel;
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
				writer.WriteString (targetApiLevel.ToString ("0.0", CultureInfo.InvariantCulture));
				writer.WriteEndElement (); // </TargetPlatformVersion>
				writer.WriteStartElement ("AndroidMinimumSupportedApiLevel");
				writer.WriteAttributeString ("Condition", " '$(AndroidMinimumSupportedApiLevel)' == '' ");
				writer.WriteString (MinimumApiLevel.ToString ());
				writer.WriteEndElement (); // </AndroidMinimumSupportedApiLevel>
				writer.WriteEndElement (); // </PropertyGroup>

				writer.WriteStartElement ("ItemGroup");
				foreach (int apiLevel in versions.InstalledBindingVersions
						.Where (v => v.ApiLevel >= MinimumApiLevel)
						.Select (v => v.ApiLevel)
						.Distinct ()
						.OrderBy (v => v)) {
					writer.WriteStartElement ("AndroidSdkSupportedTargetPlatformVersion");
					writer.WriteAttributeString ("Include", apiLevel.ToString ("0.0", CultureInfo.InvariantCulture));
					if (apiLevel < TargetApiLevel) {
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

		static AndroidVersion ToVersion (ITaskItem item)
		{
			/*
			<AndroidApiInfo Include="v12.0.99">
				<Name>Sv2</Name>
				<Level>32</Level>
				<Id>Sv2</Id>
				<Stable>False</Stable>
			</AndroidApiInfo>
			*/

			int.TryParse (item.GetMetadata ("Level"), out int apiLevel);
			bool.TryParse (item.GetMetadata ("Stable"), out bool stable);

			return new AndroidVersion (apiLevel, item.ItemSpec.TrimStart ('v'), item.GetMetadata ("Name"), item.GetMetadata ("Id"), stable);
		}
	}
}
