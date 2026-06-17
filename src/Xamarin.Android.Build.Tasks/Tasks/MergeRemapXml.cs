#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class MergeRemapXml : AndroidTask
	{
		public  override    string      TaskPrefix      => "MRX";

		public  ITaskItem[]?     InputRemapXmlFiles  { get; set; }

		[Required]
		public  ITaskItem       OutputFile          { get; set; } = null!;

		public override bool RunTask ()
		{
			Directory.CreateDirectory (Path.GetDirectoryName (OutputFile.ItemSpec));

			var settings = new XmlWriterSettings () {
				Encoding            = new UTF8Encoding (false),
				Indent              = true,
				OmitXmlDeclaration  = true,
			};
			using var output    = new StreamWriter (OutputFile.ItemSpec, append: false, encoding: settings.Encoding);
			using (var writer   = XmlWriter.Create (output, settings)) {
				writer.WriteStartElement ("replacements");
				var seen    = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
				if (InputRemapXmlFiles != null) {
					foreach (var file in InputRemapXmlFiles) {
						if (!seen.Add (file.ItemSpec)) {
							continue;
						}
						MergeInputFile (writer, file.ItemSpec);
					}
				}
				writer.WriteEndElement ();
			}
			output.WriteLine ();
			return !Log.HasLoggedErrors;
		}

		void MergeInputFile (XmlWriter writer, string file)
		{
			if (!File.Exists (file)) {
				Log.LogCodedWarning ("XA4316", Properties.Resources.XA4316, file);
				return;
			}
			var settings    = new XmlReaderSettings {
				XmlResolver     = null,
			};
			try {
				using var reader    = XmlReader.Create (File.OpenRead (file), settings);
				if (reader.MoveToContent () != XmlNodeType.Element) {
					return;
				}
				if (reader.LocalName != "replacements") {
					Log.LogCodedWarning ("XA4317", Properties.Resources.XA4317, file);
					return;
				}
				while (reader.Read ()) {
					if (reader.NodeType != XmlNodeType.Element) {
						continue;
					}
					writer.WriteNode (reader, defattr: true);
				}
			}
			catch (Exception e) {
				Log.LogCodedWarning ("XA4318", Properties.Resources.XA4318, file, e.Message);
				Log.LogDebugMessage ($"Input file `{file}` could not be read: {e.ToString ()}");
			}
		}
	}
}

