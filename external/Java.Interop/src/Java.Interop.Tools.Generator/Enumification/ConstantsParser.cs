using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Java.Interop.Tools.Generator.Enumification
{
	public static class ConstantsParser
	{
		public static List<ConstantEntry> FromEnumMapCsv (string filename)
		{
			using (var sr = new StreamReader (filename))
				return FromEnumMapCsv (sr);
		}

		public static List<ConstantEntry> FromEnumMapCsv (TextReader reader)
		{
			var constants = new List<ConstantEntry> ();
			var transient = false;

			string? s;

			// Read the enum csv file
			while ((s = reader.ReadLine ()) != null) {
				// Skip empty lines and comments
				if (string.IsNullOrEmpty (s) || s.StartsWith ("//", StringComparison.Ordinal))
					continue;

				// Transient mode means remove the original field
				if (s == "- ENTER TRANSIENT MODE -") {
					transient = true;
					continue;
				}

				constants.Add (ConstantEntry.FromString (s, transient));
			}

			return constants;
		}

		public static void SaveEnumMapCsv (List<ConstantEntry> constants, string filename)
		{
			using (var sw = new StreamWriter (filename))
				SaveEnumMapCsv (constants, sw);
		}

		public static void SaveEnumMapCsv (List<ConstantEntry> constants, TextWriter writer)
		{
			var column_names = new [] {
				"Action",
				"API Level",
				"JNI Signature",
				"Enum Value",
				"C# Enum Type",
				"C# Member Name",
				"Field Action",
				"Is Flags",
				"Deprecated Since",
			};

			writer.WriteLine ("// " + string.Join (",", column_names));

			foreach (var c in Sort (constants))
				writer.WriteLine (c.ToVersion2String ());
		}

		public static List<ConstantEntry> FromApiXml (string filename) => FromApiXml (XDocument.Load (filename));

		public static List<ConstantEntry> FromApiXml (XDocument doc)
		{
			var int_fields = doc.XPathSelectElements ("//field[@type='int']");

			return int_fields.Select (f => ConstantEntry.FromElement (f)).Where (c => c.Value.HasValue ()).ToList ();
		}

		public static List<ConstantEntry> Sort (List<ConstantEntry> constants)
		{
			// We want a well-defined sort to reduce diffs, but it's not as easy as just
			// using the JavaSignature, because there may be added enum members that do not
			// have a JavaSignature, and we would like them to be with their other members.
			// For example:
			// - A,0,,0,Java.MyEnum,None
			// - E,1,java/class/member1,1,Java.MyEnum,Member1
			// - E,1,java/class/member2,2,Java.MyEnum,Member2
			var sorted = constants.Where (c => c.JavaSignature.HasValue ()).OrderBy (c => c.JavaSignature).ToList ();

			// Try to put members without signatures at the beginning of the section with
			// their fellow enum members. If not, put them at the end of the list.
			foreach (var c in constants.Where (c => !c.JavaSignature.HasValue ()).OrderBy (c => $"{c.EnumFullType}.{c.EnumMember}")) {
				var sibling_index = sorted.FindIndex (c2 => c2.EnumFullType == c.EnumFullType);

				if (sibling_index >= 0)
					sorted.Insert (sibling_index, c);
				else
					sorted.Add (c);
			}

			return sorted;
		}
	}

	public class JavaSignatureComparer : IEqualityComparer<ConstantEntry>
	{
		public static JavaSignatureComparer Instance { get; } = new JavaSignatureComparer ();

		public bool Equals (ConstantEntry? x, ConstantEntry? y) => x?.JavaSignature == y?.JavaSignature;
		public int GetHashCode (ConstantEntry obj) => 0;
	}
}
