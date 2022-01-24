using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Text;

using Irony;
using Irony.Ast;
using Irony.Parsing;

namespace Java.Interop.Tools.JavaSource {

	[Flags]
	internal enum ImportJavadoc {
		None,
		Summary             = 1 << 0,
		Remarks             = 1 << 1,
		AuthorTag           = 1 << 2,
		DeprecatedTag       = 1 << 3,
		ExceptionTag        = 1 << 4,
		ParamTag            = 1 << 5,
		ReturnTag           = 1 << 6,
		SeeTag              = 1 << 7,
		SerialTag           = 1 << 8,
		SinceTag            = 1 << 9,
		VersionTag          = 1 << 10,
		ExtraRemarks        = 1 << 11,
	}

	[Flags]
	public enum XmldocStyle {
		None,
		Full = ImportJavadoc.Summary
			| ImportJavadoc.Remarks
			| ImportJavadoc.AuthorTag
			| ImportJavadoc.DeprecatedTag
			| ImportJavadoc.ExceptionTag
			| ImportJavadoc.ParamTag
			| ImportJavadoc.ReturnTag
			| ImportJavadoc.SeeTag
			| ImportJavadoc.SerialTag
			| ImportJavadoc.SinceTag
			| ImportJavadoc.VersionTag
			| ImportJavadoc.ExtraRemarks
			,
		IntelliSense = ImportJavadoc.Summary
			| ImportJavadoc.ExceptionTag
			| ImportJavadoc.ParamTag
			| ImportJavadoc.ReturnTag
			,
		IntelliSenseAndExtraRemarks = IntelliSense
			| ImportJavadoc.ExtraRemarks
			,
	}

	public class SourceJavadocToXmldocParser : Irony.Parsing.Parser {

		public SourceJavadocToXmldocParser (XmldocSettings settings)
			: base (CreateGrammar (settings))
		{
			XmldocSettings = settings;
		}

		public  XmldocSettings XmldocSettings { get; }



		static Grammar CreateGrammar (XmldocSettings settings)
		{
			return new SourceJavadocToXmldocGrammar (settings) {
				LanguageFlags = LanguageFlags.Default | LanguageFlags.CreateAst,
			};
		}

		public IEnumerable<XNode> TryParse (string javadoc, string? fileName = null, Action<ParseTree>? onError = null)
		{
			onError   = onError ?? DumpMessages;

			ParseTree parseTree;
			var r = TryParse (javadoc, fileName, out parseTree);
			if (parseTree.HasErrors ()) {
				onError (parseTree);
			}
			return r;
		}

		public IEnumerable<XNode> TryParse (string javadoc, string? fileName, out ParseTree parseTree)
		{
			parseTree   = base.Parse (javadoc, fileName);
			if (parseTree.HasErrors ()) {
				return Array.Empty<XNode>();
			}
			return CreateParseIterator (parseTree);
		}

		IEnumerable<XNode> CreateParseIterator (ParseTree parseTree)
		{
			if (parseTree.Root.Tag is JavadocInfo info) {
				foreach (var n in info.Parameters)
					yield return n;
				var summary = CreateSummaryNode (info);
				if (summary != null)
					yield return summary;
				var style   = (ImportJavadoc) XmldocSettings.Style;
				if (style.HasFlag (ImportJavadoc.Remarks) &&
						(info.Remarks.Count > 0 || XmldocSettings.ExtraRemarks?.Length > 0)) {
					yield return new XElement ("remarks", info.Remarks, XmldocSettings.ExtraRemarks);
				}
				else if (style.HasFlag (ImportJavadoc.ExtraRemarks) && XmldocSettings.ExtraRemarks?.Length > 0) {
					yield return new XElement ("remarks", XmldocSettings.ExtraRemarks);
				}
				foreach (var n in info.Returns) {
					yield return n;
				}
				foreach (var n in info.Exceptions) {
					yield return n;
				}
				foreach (var n in info.Extra) {
					yield return n;
				}
				yield break;
			}
			var ast = parseTree.Root.AstNode;
			if (ast is XNode node) {
				yield return node;
			}
			else {
				yield return new XCData (ast?.ToString ());
			}
		}

		static XElement? CreateSummaryNode (JavadocInfo info)
		{
			var summaryNode = info.Remarks.FirstOrDefault ();
			if (summaryNode == null)
				return null;

			if (summaryNode is XElement p) {
				var summaryItems    = new List<object> ();
				for (var n = p.FirstNode; n != null; n = n.NextNode) {
					if (n is XText text) {
						var tdot = text.Value.IndexOf ('.');
						if (tdot < 0) {
							summaryItems.Add (n);
							continue;
						}
						summaryItems.Add (text.Value.Substring (0, tdot+1));
						break;
					}
					summaryItems.Add (n);
				}
				return new XElement ("summary", summaryItems);
			}
			var content = summaryNode.ToString ();
			if (string.IsNullOrWhiteSpace (content))
				return null;

			var dot = content.IndexOf ('.');
			if (dot <= 0)
				return new XElement ("summary", content);
			return new XElement ("summary", content.Substring (0, dot+1));
		}

		static void DumpMessages (ParseTree parseTree)
		{
			foreach (var m in parseTree.ParserMessages) {
				Console.Error.WriteLine ($"{m.Level} {m.Location}: {m.Message}");
			}
		}
	}
}
