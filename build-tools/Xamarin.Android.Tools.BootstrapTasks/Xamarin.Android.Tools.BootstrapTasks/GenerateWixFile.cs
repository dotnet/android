using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	/// <summary>
	/// Generates a .wix file for the contents of bin/$(Configuration)/dotnet/packs
	/// The .wix file can be used to generate the .msi installer for Windows.
	/// </summary>
	public class GenerateWixFile : Task
	{
		[Required]
		public string Template { get; set; }

		[Required]
		public string DestinationFile { get; set; }

		[Required]
		public string DotNetPath { get; set; }

		[Required]
		public string MSIVersion { get; set; }

		public override bool Execute ()
		{
			var settings = new XmlWriterSettings {
				OmitXmlDeclaration = true,
				Indent = true,
			};

			var directories = new StringBuilder ();
			var components = new StringBuilder ();
			using (var packWriter = XmlWriter.Create (directories, settings))
			using (var componentWriter = XmlWriter.Create (components, settings)) {

				// Components
				componentWriter.WriteStartElement ("ComponentGroup");
				componentWriter.WriteAttributeString ("Id", "ProductComponents");

				// dotnet
				packWriter.WriteStartElement ("Directory");
				packWriter.WriteAttributeString ("Id", "dotnet");
				packWriter.WriteAttributeString ("Name", "dotnet");

				// sdk-manifests
				var sdk_manifests_root = Path.Combine (DotNetPath, "sdk-manifests");
				packWriter.WriteStartElement ("Directory");
				packWriter.WriteAttributeString ("Id", "sdk_manifests");
				packWriter.WriteAttributeString ("Name", "sdk-manifests");

				// $(DotNetPreviewVersionBand) such as 6.0.200
				var sdk_manifests = Directory.EnumerateDirectories (sdk_manifests_root).FirstOrDefault ();
				if (string.IsNullOrEmpty (sdk_manifests)) {
					Log.LogError ($"Cannot find child directory of: {sdk_manifests_root}");
					return false;
				}
				var version_band = Path.GetFileName (sdk_manifests);
				packWriter.WriteStartElement ("Directory");
				packWriter.WriteAttributeString ("Id", "DOTNETVERSIONBAND");
				packWriter.WriteAttributeString ("Name", version_band);
				packWriter.WriteAttributeString ("FileSource", sdk_manifests);
				var workload = Path.Combine (sdk_manifests, "Microsoft.NET.Sdk.Android");
				if (Directory.Exists (workload)) {
					RecurseDirectory (sdk_manifests, packWriter, componentWriter, workload);
				} else {
					Log.LogError ($"Cannot find directory: {workload}");
					return false;
				}
				packWriter.WriteEndElement (); // </Directory> version_band
				packWriter.WriteEndElement (); // </Directory> sdk-manifests

				// packs
				var packs_dir = Path.Combine (DotNetPath, "packs");
				packWriter.WriteStartElement ("Directory");
				packWriter.WriteAttributeString ("Id", "packs");
				packWriter.WriteAttributeString ("Name", "packs");
				packWriter.WriteAttributeString ("FileSource", packs_dir);
				foreach (var directory in Directory.EnumerateDirectories (packs_dir, "Microsoft.Android.*")) {
					RecurseDirectory (packs_dir, packWriter, componentWriter, directory);
				}
				packWriter.WriteEndElement (); // </Directory> packs

				// template-packs
				var templates_dir = Path.Combine (DotNetPath, "template-packs");
				packWriter.WriteStartElement ("Directory");
				packWriter.WriteAttributeString ("Id", "templatepacks");
				packWriter.WriteAttributeString ("Name", "template-packs");
				packWriter.WriteAttributeString ("FileSource", templates_dir);
				foreach (var file in Directory.EnumerateFiles (templates_dir, "Microsoft.Android.Templates.*.nupkg")) {
					AddFile (templates_dir, packWriter, componentWriter, file);
				}
				packWriter.WriteEndElement (); // </Directory> template-packs

				packWriter.WriteEndDocument (); // </Directory>
				componentWriter.WriteEndDocument (); // </ComponentGroup>
			}

			var template = File.ReadAllText (Template);
			var contents = template
				.Replace ("@MSIVERSION@", MSIVersion)
				.Replace ("@DIRECTORIES@", directories.ToString ())
				.Replace ("@COMPONENTS@", components.ToString ());

			Log.LogMessage (MessageImportance.Low, "Writing XML to {0}: {1}", DestinationFile, contents);
			File.WriteAllText (DestinationFile, contents);

			return !Log.HasLoggedErrors;
		}

		static void RecurseDirectory (string top_dir, XmlWriter packWriter, XmlWriter componentWriter, string directory)
		{
			var directoryId = GetId (top_dir, directory);
			packWriter.WriteStartElement ("Directory");
			packWriter.WriteAttributeString ("Id", directoryId);
			packWriter.WriteAttributeString ("Name", Path.GetFileName (directory));
			packWriter.WriteAttributeString ("FileSource", directory);
			foreach (var child in Directory.EnumerateDirectories (directory)) {
				var directoryName = Path.GetFileName (child);
				if (directoryName.StartsWith (".", StringComparison.Ordinal) || directoryName.StartsWith ("_", StringComparison.Ordinal))
					continue;
				RecurseDirectory (top_dir, packWriter, componentWriter, child);
			}
			foreach (var file in Directory.EnumerateFiles (directory)) {
				var fileName = Path.GetFileName (file);
				if (fileName.StartsWith (".", StringComparison.Ordinal) || fileName.StartsWith ("_", StringComparison.Ordinal))
					continue;
				AddFile (top_dir, packWriter, componentWriter, file);
			}
			packWriter.WriteEndElement (); // </Directory>
		}

		static void AddFile (string top_dir, XmlWriter packWriter, XmlWriter componentWriter, string file)
		{
			string componentId = GetId (top_dir, file);
			packWriter.WriteStartElement ("Component");
			packWriter.WriteAttributeString ("Id", componentId);
			packWriter.WriteStartElement ("File");
			packWriter.WriteAttributeString ("Id", componentId);
			packWriter.WriteAttributeString ("Name", Path.GetFileName (file));
			packWriter.WriteAttributeString ("KeyPath", "yes");
			packWriter.WriteEndElement (); // </File>
			packWriter.WriteEndElement (); // </Component>
			componentWriter.WriteStartElement ("ComponentRef");
			componentWriter.WriteAttributeString ("Id", componentId);
			componentWriter.WriteEndElement (); // </ComponentRef>
		}

		static string GetId (string top_dir, string path)
		{
			if (string.IsNullOrEmpty (path))
				return path;
			if (path.Length > top_dir.Length + 1) {
				path = path.Substring (top_dir.Length + 1);
			}
			return GetHashString (path);
		}

		static byte [] GetHash (string inputString)
		{
			using (var algorithm = SHA256.Create ())
				return algorithm.ComputeHash (Encoding.UTF8.GetBytes (inputString));
		}

		static string GetHashString (string inputString)
		{
			var sb = new StringBuilder ("S", 65);
			foreach (byte b in GetHash (inputString))
				sb.Append (b.ToString ("X2"));
			return sb.ToString ();
		}
	}
}
