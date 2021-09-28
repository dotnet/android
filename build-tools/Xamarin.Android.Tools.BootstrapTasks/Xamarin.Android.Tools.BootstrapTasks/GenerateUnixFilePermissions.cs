using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	/// <summary>
	/// Creates data/UnixFilePermissions.xml
	/// NOTE: not currently intended to run on Windows
	/// </summary>
	public class GenerateUnixFilePermissions : Task
	{
		[Required]
		public string Output { get; set; }

		public string PackagePath { get; set; } = "";

		public ITaskItem [] Files { get; set; }

		public override bool Execute ()
		{
			var settings = new XmlWriterSettings {
				OmitXmlDeclaration = true,
				Indent = true,
			};

			/*
			 <FileList>
			   <File Path="tools/Darwin/aapt2" Permission="755" />
			 </FileList>
			*/

			using var xml = XmlWriter.Create (Output, settings);
			xml.WriteStartElement ("FileList");
			if (Files != null) {
				var files =
					from f in Files
					let path = f.GetMetadata ("RelativePath")
					let permission = f.GetMetadata ("Permission")
					where !string.IsNullOrEmpty (path) && !string.IsNullOrEmpty (permission)
					orderby path
					select (path, permission);
				foreach (var (path, permission) in files) {
					xml.WriteStartElement ("File");
					xml.WriteAttributeString ("Path", Path.Combine (PackagePath, path));
					xml.WriteAttributeString ("Permission", permission);
					xml.WriteEndElement ();
				}
			}
			xml.WriteEndDocument ();

			return !Log.HasLoggedErrors;
		}
	}
}
