using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Irony.Ast;
using Irony.Parsing;

namespace Java.Interop.Tools.JavaSource {

	using static IronyExtensions;

	public partial class SourceJavadocToXmldocGrammar {

		public class HtmlBnfTerms {
			internal HtmlBnfTerms ()
			{
			}

			internal void CreateRules (SourceJavadocToXmldocGrammar grammar)
			{
				AllHtmlTerms.Rule = TopLevelInlineDeclaration
					| PBlockDeclaration
					| PreBlockDeclaration
					;

				var inlineDeclaration = new NonTerminal ("<html inline decl>", ConcatChildNodes) {
					Rule = ParsedCharacterData
						| FontStyleDeclaration
						/*
						| PhraseDeclaration
						| SpecialDeclaration
						| FormCtrlDeclaration
						*/
						| grammar.InlineTagsTerms.AllInlineTerms
						| UnknownHtmlElementStart
						,
				};
				var inlineDeclarations = new NonTerminal ("<html inline decl>*", ConcatChildNodes);
				inlineDeclarations.MakePlusRule (grammar, inlineDeclaration);

				InlineDeclaration.Rule = inlineDeclaration;
				InlineDeclarations.MakeStarRule (grammar, InlineDeclaration);

				TopLevelInlineDeclaration.Rule = inlineDeclarations;
				TopLevelInlineDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					var remarks     = FinishParse (context, parseNode).Remarks;
					var addRemarks  = grammar.ShouldImport (ImportJavadoc.Remarks) ||
						(grammar.ShouldImport (ImportJavadoc.Summary) && remarks.Count == 0);
					if (!addRemarks) {
						parseNode.AstNode   = "";
						return;
					}
					foreach (var p in GetParagraphs (parseNode.ChildNodes)) {
						remarks.Add (p);
					}
					parseNode.AstNode       = "";
				};

				var fontstyle_tt    = CreateHtmlToCrefElement (grammar, "tt",   "c", InlineDeclarations);
				var fontstyle_i     = CreateHtmlToCrefElement (grammar, "i",    "i", InlineDeclarations);

				var preText = new PreBlockDeclarationBodyTerminal ();
				PreBlockDeclaration.Rule = CreateStartElement ("pre", grammar) + preText + CreateEndElement ("pre", grammar);
				PreBlockDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					if (!grammar.ShouldImport (ImportJavadoc.Remarks)) {
						parseNode.AstNode   = "";
						return;
					}
					var c = new XElement ("code",
							new XAttribute ("lang", "text/java"),
							parseNode.ChildNodes [1].Token.Value);
					FinishParse (context, parseNode).Remarks.Add (c);
					parseNode.AstNode   = c;
				};

				FontStyleDeclaration.Rule = fontstyle_tt | fontstyle_i;

				PBlockDeclaration.Rule =
					CreateStartElement ("p", grammar) + InlineDeclarations + CreateEndElement ("p", grammar, optional:true)
					;
				PBlockDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					var remarks     = FinishParse (context, parseNode).Remarks;
					var addRemarks  = grammar.ShouldImport (ImportJavadoc.Remarks) ||
						(grammar.ShouldImport (ImportJavadoc.Summary) && remarks.Count == 0);
					if (!addRemarks) {
						parseNode.AstNode   = "";
						return;
					}
					var p = new XElement ("para",
							parseNode.ChildNodes
							.Select (c => AstNodeToXmlContent (c)));
					FinishParse (context, parseNode).Remarks.Add (p);
					parseNode.AstNode   = p;
				};
			}

			static IEnumerable<XElement> GetParagraphs (ParseTreeNodeList children)
			{
				var items = new List<object> ();
				foreach (var child in children) {
					var s = child.AstNode as string;
					if (s == null || (!s.Contains ("\n\n") && !s.Contains ("\r\n\r\n"))) {
						items.Add (child.AstNode);
						continue;
					}

					const string UnixParagraph  = "\n\n";
					const string DosParagraph   = "\r\n\r\n";
					for (int i = 0; i < s.Length; ) {
						int len = 0;
						int n   = -1;

						if ((n = s.IndexOf (UnixParagraph, i, StringComparison.Ordinal)) >= 0) {
							len = UnixParagraph.Length;
						}
						else if ((n = s.IndexOf (DosParagraph, i, StringComparison.Ordinal)) >= 0) {
							len = DosParagraph.Length;
						}

						if (n <= 0) {
							items.Add (s.Substring (i));
							break;
						}

						var c = s.Substring (i, n-i);
						items.Add (c);
						i = n + len;
						yield return new XElement ("para", items.Select (v => ToXmlContent (v)));
						items.Clear ();
					}
				}
				if (items.Count > 0) {
					yield return new XElement ("para", items.Select (v => ToXmlContent (v)));
				}
			}

			public  readonly    NonTerminal AllHtmlTerms               = new NonTerminal (nameof (AllHtmlTerms), ConcatChildNodes);

			public  readonly    NonTerminal TopLevelInlineDeclaration  = new NonTerminal (nameof (TopLevelInlineDeclaration), ConcatChildNodes);


			// https://www.w3.org/TR/html401/struct/global.html#h-7.5.3
//			public  readonly    Terminal    ParsedCharacterData        = new RegexBasedTerminal (nameof (ParsedCharacterData), "[^<{@}]*") {
//			public  readonly    Terminal    ParsedCharacterData        = new WikiTextTerminal (nameof (ParsedCharacterData)) {*
			public  readonly    Terminal    ParsedCharacterData        = new CharacterDataTerminal ("#PCDATA", preserveLeadingWhitespace:true);

			// https://www.w3.org/TR/html4/sgml/dtd.html#inline
			public  readonly    NonTerminal InlineDeclaration           = new NonTerminal (nameof (InlineDeclaration), ConcatChildNodes);
			public  readonly    NonTerminal InlineDeclarations          = new NonTerminal (nameof (InlineDeclarations), ConcatChildNodes);
			// https://www.w3.org/TR/html4/sgml/dtd.html#fontstyle
			public  readonly    NonTerminal FontStyleDeclaration        = new NonTerminal (nameof (FontStyleDeclaration), ConcatChildNodes);
			// https://www.w3.org/TR/html4/sgml/dtd.html#phrase
			public  readonly    NonTerminal PhraseDeclaration           = new NonTerminal (nameof (PhraseDeclaration), ConcatChildNodes);
			// https://www.w3.org/TR/html4/sgml/dtd.html#special
			public  readonly    NonTerminal SpecialDeclaration          = new NonTerminal (nameof (SpecialDeclaration), ConcatChildNodes);
			// https://www.w3.org/TR/html4/sgml/dtd.html#formctrl
			public  readonly    NonTerminal FormCtrlDeclaration         = new NonTerminal (nameof (FormCtrlDeclaration), ConcatChildNodes);
			// https://www.w3.org/TR/html4/sgml/dtd.html#block
			public  readonly    NonTerminal BlockDeclaration            = new NonTerminal (nameof (BlockDeclaration), ConcatChildNodes);
			public  readonly    NonTerminal PBlockDeclaration           = new NonTerminal (nameof (PBlockDeclaration), ConcatChildNodes);
			public  readonly    NonTerminal PreBlockDeclaration         = new NonTerminal (nameof (PreBlockDeclaration), ConcatChildNodes);

			public  readonly    Terminal    UnknownHtmlElementStart     = new UnknownHtmlElementStartTerminal (nameof (UnknownHtmlElementStart)) {
				AstConfig   = new AstNodeConfig {
					NodeCreator = (context, parseNode) => parseNode.AstNode = parseNode.Token.Value.ToString (),
				},
			};

			static NonTerminal CreateHtmlToCrefElement (Grammar grammar, string htmlElement, string crefElement, BnfTerm body, bool optionalEnd = false)
			{
				var start       = CreateStartElement (htmlElement, grammar);
				var end         = CreateEndElement (htmlElement, grammar, optionalEnd);
				var nonTerminal = new NonTerminal ("<" + htmlElement + ">", ConcatChildNodes) {
					Rule = start + body + end,
					AstConfig = {
						NodeCreator = (context, parseNode) => {
							var n = new XElement (crefElement,
									parseNode.ChildNodes.Select (c => c.AstNode ?? ""));
							parseNode.AstNode = n;
						},
					}
				};
				return nonTerminal;
			}

			static NonTerminal CreateStartElement (string startElement, Grammar grammar)
			{
				var start   = new NonTerminal ("<" + startElement + ">", nodeCreator: (context, parseNode) => parseNode.AstNode = "") {
					Rule    = grammar.ToTerm ("<" + startElement + ">") | "<" + startElement.ToUpperInvariant () + ">",
				};
				return start;
			}

			static NonTerminal CreateEndElement (string endElement, Grammar grammar, bool optional = false)
			{
				var end	    = new NonTerminal (endElement, nodeCreator: (context, parseNode) => parseNode.AstNode = "") {
					Rule    = grammar.ToTerm ("</" + endElement + ">") | "</" + endElement.ToUpperInvariant () + ">",
				};
				if (optional) {
					end.Rule |= grammar.Empty;
				}
				return end;
			}
		}
	}

	// Based in part on WikiTextTerminal
	class CharacterDataTerminal : Terminal {

		char[]? _stopChars;

		bool    preserveLeadingWhitespace;

		public CharacterDataTerminal (string name, bool preserveLeadingWhitespace)
			: base (name)
		{
			base.Priority = TerminalPriority.Low;

			this.preserveLeadingWhitespace  = preserveLeadingWhitespace;

			this.AstConfig.NodeCreator = (context, parseNode) =>
				parseNode.AstNode = parseNode.Token.Value.ToString ();
		}

		public override void Init (GrammarData grammarData)
		{
			base.Init (grammarData);
			var stopCharSet = new Irony.CharHashSet ();
			foreach(var term in grammarData.Terminals) {
				var firsts = term.GetFirsts ();
				if (firsts == null)
					continue;
				foreach (var first in firsts) {
					if (string.IsNullOrEmpty (first))
						continue;
					stopCharSet.Add (first [0]);
				}
			}
			_stopChars = stopCharSet.ToArray();
		}

		public override Token? TryMatch (ParsingContext context, ISourceStream source)
		{
			var stopIndex = source.Text.IndexOfAny (_stopChars, source.Location.Position);
			if (stopIndex == source.Location.Position)
				return null;
			if (stopIndex < 0)
				stopIndex = source.Text.Length;
			source.PreviewPosition = stopIndex;

			// preserve leading whitespace, if present.
			int start = source.Location.Position;
			if (preserveLeadingWhitespace) {
				while (start > 0 && char.IsWhiteSpace (source.Text, start-1)) {
					start--;
				}
			}
			var content = source.Text.Substring (start, stopIndex - start);

			return source.CreateToken (this.OutputTerminal, content);
		}
	}

	class PreBlockDeclarationBodyTerminal : Terminal {

		public PreBlockDeclarationBodyTerminal ()
			: base ("<pre> body")
		{
			this.AstConfig.NodeCreator = (context, parseNode) =>
				parseNode.AstNode = parseNode.Token.Value.ToString ();
		}

		public override void Init (GrammarData grammarData)
		{
			base.Init (grammarData);
		}

		public override Token? TryMatch (ParsingContext context, ISourceStream source)
		{
			int startIndex  = source.Location.Position;
			var stopIndex   = source.Text.IndexOf ("</pre>", source.Location.Position, StringComparison.OrdinalIgnoreCase);
			if (stopIndex < 0)
				stopIndex   = source.Text.Length;
			source.PreviewPosition = stopIndex;

			var content = source.Text.Substring (startIndex, stopIndex - startIndex);

			return source.CreateToken (this.OutputTerminal, content);
		}
	}

	class UnknownHtmlElementStartTerminal : Terminal {

		bool    addingRemarks;

		public UnknownHtmlElementStartTerminal (string name)
			: base (name)
		{
			base.Priority = TerminalPriority.Low-1;
		}

		public override void Init (GrammarData grammarData)
		{
			base.Init (grammarData);
			var g           = grammarData.Grammar as SourceJavadocToXmldocGrammar;
			addingRemarks   = g?.ShouldImport (ImportJavadoc.Remarks) ?? false;
		}

		public override Token? TryMatch (ParsingContext context, ISourceStream source)
		{
			if (source.Text [source.Location.Position] != '<')
				return null;
			source.PreviewPosition += 1;
			int start = source.Location.Position;
			int stop  = start;
			while (source.Text [stop] != '>' && stop < source.Text.Length)
				stop++;
			if (addingRemarks) {
				Console.Error.WriteLine ($"# Unsupported HTML element: {source.Text.Substring (start, stop - start)}");
			}
			return source.CreateToken (this.OutputTerminal, "<");
		}
	}
}
