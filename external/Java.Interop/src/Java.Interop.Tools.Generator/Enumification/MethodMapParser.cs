using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Java.Interop.Tools.Generator.Enumification
{
	public class MethodMapParser
	{
		public static List<MethodMapEntry> FromMethodMapCsv (string filename)
		{
			using (var sr = new StreamReader (filename))
				return FromMethodMapCsv (sr);
		}

		public static List<MethodMapEntry> FromMethodMapCsv (TextReader reader)
		{
			var entries = new List<MethodMapEntry> ();

			string s;

			// Read the enum csv file
			while ((s = reader.ReadLine ()) != null) {
				// Skip empty lines and comments
				if (string.IsNullOrEmpty (s) || s.StartsWith ("//", StringComparison.Ordinal))
					continue;

				entries.Add (MethodMapEntry.FromString (s));
			}

			return entries;
		}

		public static void SaveMethodMapCsv (IEnumerable<MethodMapEntry> entries, string filename, bool version2)
		{
			using (var sw = new StreamWriter (filename))
				SaveMethodMapCsv (entries, sw, version2);
		}

		public static void SaveMethodMapCsv (IEnumerable<MethodMapEntry> entries, TextWriter writer, bool version2)
		{
			foreach (var entry in entries.OrderBy (e => e.JavaSignature))
				writer.WriteLine (version2 ? entry.ToVersion2String () : entry.ToVersion1String ());
		}

		public static List<MethodMapEntry> FromApiXml (string filename) => FromApiXml (XDocument.Load (filename));

		public static List<MethodMapEntry> FromApiXml (XDocument doc)
		{
			var results = new List<MethodMapEntry> ();

			// Methods that return int or have an int parameter
			results.AddRange (doc.XPathSelectElements ("//method[@return='int'] | //method[parameter/@type='int']").SelectMany (x => MethodMapEntry.FromXml (x)));

			// Constructors with an int parameter
			results.AddRange (doc.XPathSelectElements ("//constructor[parameter/@type='int']").SelectMany (x => MethodMapEntry.FromXml (x)));

			// Fields that are a non-constant int
			results.AddRange (doc.XPathSelectElements ("//field[@type='int' and @final='false']").SelectMany (x => MethodMapEntry.FromXml (x)));

			return results;
		}
	}
}
