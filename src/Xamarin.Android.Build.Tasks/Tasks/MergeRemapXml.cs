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
			string? directory = Path.GetDirectoryName (OutputFile.ItemSpec);
			if (!directory.IsNullOrEmpty ()) {
				Directory.CreateDirectory (directory);
			}

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
			var buffer = MemoryStreamPool.Shared.Rent ();
			try {
				using (var input = File.OpenRead (file)) {
					input.CopyTo (buffer);
				}
				buffer.Position = 0;
				using (var reader = XmlReader.Create (buffer, settings)) {
					if (reader.MoveToContent () != XmlNodeType.Element) {
						return;
					}
					if (reader.LocalName != "replacements") {
						Log.LogCodedWarning ("XA4317", Properties.Resources.XA4317, file);
						return;
					}
					// Validate the complete snapshot before committing any of its nodes.
					while (reader.Read ()) {
					}
				}
				buffer.Position = 0;
				using var bufferedReader = XmlReader.Create (buffer, settings);
				bufferedReader.MoveToContent ();
				bufferedReader.Read ();
				while (!bufferedReader.EOF) {
					if (bufferedReader.NodeType == XmlNodeType.Element) {
						writer.WriteNode (bufferedReader, defattr: true);
					} else {
						bufferedReader.Read ();
					}
				}
			}
			catch (Exception e) when (e is XmlException || e is IOException || e is UnauthorizedAccessException) {
				Log.LogCodedWarning ("XA4318", Properties.Resources.XA4318, file, e.Message);
				Log.LogDebugMessage ($"Input file `{file}` could not be read: {e.ToString ()}");
			} finally {
				MemoryStreamPool.Shared.Return (buffer);
			}
		}
	}
}
