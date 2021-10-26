using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml;
using HtmlAgilityPack;

namespace Xamarin.AndroidTools.AnnotationSupport
{
	public class AndroidAnnotationsSupport
	{
		#region static members

		public static IList<AnnotatedItem> ParseArchive (string file)
		{
			using (var zipFile = File.OpenRead (file))
				return new ZipArchive (zipFile).Entries
				    .Where (e => e.Name.EndsWith ("annotations.xml", StringComparison.Ordinal))
				    .SelectMany (e => ParseArchiveEntry (e))
				    .OrderBy (k => k.Name)
				    .ToArray ();
		}

		static IEnumerable<AnnotatedItem> ParseArchiveEntry (ZipArchiveEntry entry)
		{
			using (var s = entry.Open ())
				return SafeXmlLoad (s, entry.FullName).Root.Elements ("item").Select (e => new AnnotatedItem (e));
		}

		static XDocument SafeXmlLoad (Stream s, string fileName)
		{
			// We must save to a temporary stream because the stream doesn't support seeking and should the
			// parsing fail we won't be able to go back to its beginnig to reparse it.
			using (var ms = new MemoryStream ()) {
				s.CopyTo (ms);
				ms.Seek (0, SeekOrigin.Begin);

				try {
					return XDocument.Load (ms);
				} catch (XmlException ex) {
					Console.Error.WriteLine ($"Warning: failed to load annotation document '{fileName}' directly from the annotations archive. {ex.Message}");
					Console.Error.WriteLine ("Attempting to fix up and reload");
				}

				try {
					using (var ns = FixAnnotationXML (ms)) {
						return XDocument.Load (ns);
					}
				} catch (Exception ex) {
					throw new InvalidOperationException ($"Failed to fix up invalid XML in annotation document '{fileName}'. {ex.Message}", ex);
				}
			}
		}

		static Stream FixAnnotationXML (Stream s)
		{
			s.Seek (0, SeekOrigin.Begin);

			//
			// Context: https://issuetracker.google.com/issues/116182838
			//
			// Google ships not well-formed XML files in the platform-tools 28.0.1 package (in the
			// annotations.zip file), so we need to load the files with a forgiving parser in order to fix
			// them up before loading with a validating XML parser.
			var doc = new HtmlDocument ();
			doc.Load (s);
			if (doc.DocumentNode.FirstChild.InnerHtml.StartsWith ("<?xml", StringComparison.Ordinal))
				doc.DocumentNode.FirstChild.Remove ();

			FixEscapedQuotes (doc.DocumentNode);

			var ms = new MemoryStream ();
			var xs = new XmlWriterSettings {
				Encoding = new UTF8Encoding (false),
				CloseOutput = false,
				ConformanceLevel = ConformanceLevel.Fragment,
			};

			using (var xw = XmlWriter.Create (ms, xs)) {
				doc.Save (xw);
				xw.Flush ();
			}

			ms.Seek (0, SeekOrigin.Begin);
			return ms;
		}

		static void FixEscapedQuotes (HtmlNode node)
		{
			// Quotation marks in attribute values are already escaped as '&quot;', however the Save ()
			// is interpreting them as the string '&quot;' and thinks it needs to escape the ampersand,
			// resulting in writing '&amp;quot;'.  Here we "un-escape" the quotation mark,
			// so that Save () will escape it correctly as '&quot;'.
			foreach (var attr in node.Attributes)
				attr.Value = attr.Value.Replace ("&quot;", "\"");

			foreach (var child in node.ChildNodes)
				FixEscapedQuotes (child);
		}
		#endregion

		#region data loader

		public void Load (string annotationsZipFile)
		{
			var annots = ParseArchive (annotationsZipFile);
			foreach (var ext in Extensions)
				ext.OnAnnotationsParsed (annots);

			foreach (var a in annots) {
				string key = a.ManagedInfo.Type.FullName ?? string.Empty;
				IList<AnnotatedItem> l;
				if (!data.TryGetValue (key, out l))
					data [key] = l = new List<AnnotatedItem> ();
				l.Add (a);
			}
		}

		List<AnnotationParserExtension> extensions = new List<AnnotationParserExtension> ();

		public IList<AnnotationParserExtension> Extensions {
			get { return extensions; }
		}

		Dictionary<string, IList<AnnotatedItem>> data = new Dictionary<string, IList<AnnotatedItem>> ();

		public IDictionary<string, IList<AnnotatedItem>> Data {
			get { return data; }
		}

		#endregion
	}
}
