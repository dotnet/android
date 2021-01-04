using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

using Irony.Parsing;

using Java.Interop.Tools.JavaSource;

namespace MonoDroid.Generation
{
	public static class Javadoc {

		public static void AddJavadocs (ICollection<string> comments, string javadoc)
		{
			if (string.IsNullOrWhiteSpace (javadoc))
				return;

			javadoc         = javadoc.Trim ();

			ParseTree tree  = null;

			try {
				var parser  = new SourceJavadocToXmldocParser ();
				var nodes   = parser.TryParse (javadoc, fileName: null, out tree);
				foreach (var node in (nodes ?? new XNode [0])) {
					AddNode (comments, node);
				}
			}
			catch (Exception e) {
				Console.Error.WriteLine ($"## Exception translating remarks: {e.ToString ()}");
			}

			if (tree != null && tree.HasErrors ()) {
				Console.Error.WriteLine ($"## Unable to translate remarks:");
				Console.Error.WriteLine ("```");
				Console.Error.WriteLine (javadoc);
				Console.Error.WriteLine ("```");
				PrintMessages (tree, Console.Error);
				Console.Error.WriteLine ();
			}
		}

		static void AddNode (ICollection<string> comments, XNode node)
		{
			if (node == null)
				return;
			var contents = node.ToString ();

			var lines = new StringReader (contents);
			string line;
			while ((line = lines.ReadLine ()) != null) {
				comments.Add ($"/// {line}");
			}
		}

		static void PrintMessages (ParseTree tree, TextWriter writer)
		{
			var lines   = GetLines (tree.SourceText);
			foreach (var m in tree.ParserMessages) {
				writer.WriteLine ($"{m.Level} {m.Location}: {m.Message}");
				writer.WriteLine (lines [m.Location.Line]);
				writer.Write (new string (' ', m.Location.Column));
				writer.WriteLine ("^");
			}
		}

		static List<string> GetLines (string text)
		{
			var lines = new List<string>();
			var reader = new StringReader (text);
			string line;
			while ((line = reader.ReadLine()) != null) {
				lines.Add (line);
			}
			return lines;
		}
	}
}
