using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

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
				return XDocument.Load (s).Root.Elements ("item").Select (e => new AnnotatedItem (e));
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
