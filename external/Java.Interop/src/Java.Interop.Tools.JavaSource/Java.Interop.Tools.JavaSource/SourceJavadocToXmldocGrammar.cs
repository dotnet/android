using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Irony.Ast;
using Irony.Parsing;

namespace Java.Interop.Tools.JavaSource {

	[Language ("SourceJavadocToXmldoc", "0.1", "Convert Javadoc within Java source code, sans comment delimiter, to CSC /doc XML.")]
	public partial class SourceJavadocToXmldocGrammar : Grammar {

		public  readonly    BlockTagsBnfTerms   BlockTagsTerms;
		public  readonly    InlineTagsBnfTerms  InlineTagsTerms;
		public  readonly    HtmlBnfTerms        HtmlTerms;

		public  readonly    XmldocStyle         XmldocStyle;

		public SourceJavadocToXmldocGrammar (XmldocStyle style)
		{
			BlockTagsTerms  = new BlockTagsBnfTerms ();
			InlineTagsTerms = new InlineTagsBnfTerms ();
			HtmlTerms       = new HtmlBnfTerms ();

			XmldocStyle     = style;

			BlockTagsTerms.CreateRules (this);
			InlineTagsTerms.CreateRules (this);
			HtmlTerms.CreateRules (this);

			var remark  = new NonTerminal ("<html>", ConcatChildNodes) {
				Rule    = HtmlTerms.AllHtmlTerms,
			};
			var remarks = new NonTerminal ("<html>*", ConcatChildNodes);
			remarks.MakeStarRule (this, remark);

			var block   = new NonTerminal ("@block", ConcatChildNodes) {
				Rule    = BlockTagsTerms.AllBlockTerms,
			};
			var blocks  = new NonTerminal ("@blocks", ConcatChildNodes);
			blocks.MakeStarRule (this, block);

			var root    = new NonTerminal ("<javadocs>", ConcatChildNodes) {
				Rule    = remarks + blocks,
			};

			root.AstConfig.NodeCreator = (context, parseNode) => {
				FinishParse (context, parseNode);
			};

			this.Root	= root;
		}

		internal bool ShouldImport (ImportJavadoc value)
		{
			var v = (ImportJavadoc) XmldocStyle;
			return v.HasFlag (value);
		}

		internal static void ConcatChildNodes (AstContext context, ParseTreeNode parseNode)
		{
			switch (parseNode.ChildNodes.Count) {
				case 0:
					parseNode.AstNode = "";
					break;
				case 1:
					parseNode.AstNode = parseNode.ChildNodes [0].AstNode ?? "";
					break;
				default: {
					parseNode.AstNode = parseNode.ChildNodes
						.Select (c => c.AstNode ?? "")
						.ToArray ();
					break;
				}
			}
		}

		internal static IEnumerable<object> AstNodeToXmlContent (ParseTreeNode node)
		{
			return ToXmlContent (node.AstNode);
		}

		// Trim leading & trailing whitespace from `value`, which could be:
		//   * a string
		//   * an object[]
		//   * Anything else (XElement, etc.)
		internal static IEnumerable<object> ToXmlContent (object? value)
		{
			if (value == null)
				yield break;
			if (value is string s) {
				yield return s.Trim ();
			}
			else if (value is IEnumerable<object> nested) {
				object? first    = null;
				object? last     = null;
				foreach (var n in nested) {
					if (first != null) {
						if (last != null)
							yield return last;
						last = n;
						continue;
					}
					first = n;
					if (first is string s1) {
						yield return s1.TrimStart ();
					}
					else
						yield return ToXmlContent (first);
				}
				if (last != null) {
					if (last is string l)
						yield return l.TrimEnd ();
					else
						yield return ToXmlContent (last);
				}
			}
			else
				yield return value;
		}

		internal static JavadocInfo FinishParse (AstContext context, ParseTreeNode parseNode)
		{
			const string key = ".__JavadocInfo";
			if (!context.Values.TryGetValue (key, out var r)) {
				context.Values.Add (key, r = new JavadocInfo ());
			}
			parseNode.Tag       = r;
			return (JavadocInfo) r;
		}
	}
}
