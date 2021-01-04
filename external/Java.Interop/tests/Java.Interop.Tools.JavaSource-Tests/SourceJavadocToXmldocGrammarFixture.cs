using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Text;

using NUnit.Framework;

using Java.Interop.Tools.JavaSource;

using Irony;
using Irony.Parsing;

namespace Java.Interop.Tools.JavaSource.Tests
{
	[TestFixture]
	public class SourceJavadocToXmldocGrammarFixture {

		public static Parser CreateParser (Func<SourceJavadocToXmldocGrammar, NonTerminal> root)
		{
			var g = new SourceJavadocToXmldocGrammar (XmldocStyle.Full) {
				LanguageFlags = LanguageFlags.Default | LanguageFlags.CreateAst,
			};
			g.Root = root (g);
			return new Parser (g) {
				Context = {
					TracingEnabled = true,
				}
			};
		}

		public static string DumpMessages (ParseTree tree, Parser parser)
		{
			var lines   = GetLines (tree.SourceText);
			var message = new StringBuilder ();
			message.AppendLine ("ParserMessages:");
			foreach (var m in tree.ParserMessages) {
				message.AppendLine ($"  {m.Level} {m.Location}: {m.Message}");
				message.AppendLine (lines [m.Location.Line]);
				message.Append ("  ");
				message.Append (new string (' ', m.Location.Column));
				message.Append ("^");
				message.AppendLine ();
			}
			message.AppendLine ("ParserTrace:");
			foreach (var t in parser.Context.ParserTrace) {
				message.AppendLine ($"  input=`{t.Input}`; error? {t.IsError}; message={t.Message}");
			}
			return message.ToString ();
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
