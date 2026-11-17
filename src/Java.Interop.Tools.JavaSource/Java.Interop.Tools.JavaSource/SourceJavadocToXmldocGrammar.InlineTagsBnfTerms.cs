using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Irony.Ast;
using Irony.Parsing;

namespace Java.Interop.Tools.JavaSource {

	public partial class SourceJavadocToXmldocGrammar {

		// https://docs.oracle.com/javase/7/docs/technotes/tools/windows/javadoc.html#javadoctags
		public class InlineTagsBnfTerms {

			public InlineTagsBnfTerms ()
			{
			}

			internal void CreateRules (SourceJavadocToXmldocGrammar grammar)
			{
				AllInlineTerms.Rule = CodeDeclaration
					| DocRootDeclaration
					| InheritDocDeclaration
					| LinkDeclaration
					| LinkplainDeclaration
					| LiteralDeclaration
					| SeeDeclaration
					| ValueDeclaration
					| IgnorableDeclaration
					| InlineParamDeclaration
					;

				CodeDeclaration.Rule = grammar.ToTerm ("{@code") + InlineValue + "}";
				CodeDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					parseNode.AstNode = new XElement ("c", parseNode.ChildNodes [1].AstNode?.ToString ()?.Trim ());
				};

				DocRootDeclaration.Rule = grammar.ToTerm ("{@docRoot}");
				DocRootDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					var docRoot = grammar.XmldocSettings.DocRootValue;
					if (!string.IsNullOrEmpty (docRoot)) {
						if (!docRoot.EndsWith ("/", StringComparison.OrdinalIgnoreCase)) {
							docRoot += "/";
						}
					} else {
						docRoot = "{@docRoot}";
					}
					parseNode.AstNode = new XText (docRoot);
				};

				InheritDocDeclaration.Rule = grammar.ToTerm ("{@inheritDoc}");
				InheritDocDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					// TODO: Iterate through parents for corresponding javadoc element.
					parseNode.AstNode = new XText ("To be added");
				};

				LinkDeclaration.Rule = grammar.ToTerm ("{@link") + InlineValue + new NonTerminal ("Optional }", nodeCreator: (context, parseNode) => parseNode.AstNode = "") {
					Rule = grammar.ToTerm ("}") | grammar.Empty,
				};
				LinkDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					// TODO: *everything*; {@link target label}, but target can contain spaces!
					// Also need to convert to appropriate CREF value, use code text for now.
					// Also some {@link tags are missing a closing bracket, use plain text rather than throwing for now.
					var target = parseNode.ChildNodes [1].AstNode;
					var optionalBracketMatch = parseNode.ChildNodes [2];
					if (optionalBracketMatch.ChildNodes.Count > 0 && optionalBracketMatch.ChildNodes [0].Term.Name == "}") {
						parseNode.AstNode = new XElement ("c", target);
					} else {
						parseNode.AstNode = new XText (target.ToString () ?? string.Empty);
					}
				};

				LinkplainDeclaration.Rule = grammar.ToTerm ("{@linkplain") + InlineValue + "}";
				LinkplainDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					// TODO: *everything*; {@link target label}, but target can contain spaces!
					// Also need to convert to appropriate CREF value, use text for now.
					var target = parseNode.ChildNodes [1].AstNode.ToString ();
					parseNode.AstNode = new XText (target ?? string.Empty);
				};

				LiteralDeclaration.Rule = grammar.ToTerm ("{@literal") + InlineValue + "}";
				LiteralDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					var content = parseNode.ChildNodes [1].AstNode.ToString ();
					parseNode.AstNode = new XText (content ?? string.Empty);
				};

				SeeDeclaration.Rule = grammar.ToTerm ("{@see") + InlineValue + "}";
				SeeDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					// TODO: @see supports multiple forms; see: https://docs.oracle.com/javase/7/docs/technotes/tools/windows/javadoc.html#see
					// Also need to convert to appropriate CREF value, ignore for now
					var target = parseNode.ChildNodes [1].AstNode;
					parseNode.AstNode = new XElement ("c", target);
				};

				ValueDeclaration.Rule = grammar.ToTerm ("{@value}")
					| grammar.ToTerm ("{@value") + InlineValue + "}";
				ValueDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					if (parseNode.ChildNodes.Count > 1) {
						// TODO: Need to convert to appropriate CREF value, use code text for now.
						var field = parseNode.ChildNodes [1].AstNode.ToString ();
						parseNode.AstNode = new XElement ("c", field);
					}
					else {
						// TODO: Display the value of the corresponding static field.
						parseNode.AstNode = new XText ("To be added");
					}
				};

				InlineParamDeclaration.Rule = grammar.ToTerm ("{@param") + InlineValue + "}";
				InlineParamDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					var target = parseNode.ChildNodes [1].AstNode;
					parseNode.AstNode = new XElement ("paramref", target);
				};
			}

			public  readonly    NonTerminal AllInlineTerms              = new NonTerminal (nameof (AllInlineTerms), ConcatChildNodes);

			public  readonly    Terminal    InlineValue                 = new RegexBasedTerminal (nameof (InlineValue), "[^}]*") {
				AstConfig = new AstNodeConfig {
					NodeCreator = (context, parseNode) => parseNode.AstNode = parseNode.Token.Value,
				},
			};

			// https://docs.oracle.com/javase/7/docs/technotes/tools/windows/javadoc.html#code
			public  readonly    NonTerminal CodeDeclaration             = new NonTerminal (nameof (CodeDeclaration));

			// https://docs.oracle.com/javase/7/docs/technotes/tools/windows/javadoc.html#docRoot
			public  readonly    NonTerminal DocRootDeclaration          = new NonTerminal (nameof (DocRootDeclaration));

			// https://docs.oracle.com/javase/7/docs/technotes/tools/windows/javadoc.html#inheritDoc
			public  readonly    NonTerminal InheritDocDeclaration       = new NonTerminal (nameof (InheritDocDeclaration));

			// https://docs.oracle.com/javase/7/docs/technotes/tools/windows/javadoc.html#link
			public  readonly    NonTerminal LinkDeclaration             = new NonTerminal (nameof (LinkDeclaration));

			// https://docs.oracle.com/javase/7/docs/technotes/tools/windows/javadoc.html#linkplain
			public  readonly    NonTerminal LinkplainDeclaration        = new NonTerminal (nameof (LinkplainDeclaration));

			// https://docs.oracle.com/javase/7/docs/technotes/tools/windows/javadoc.html#literal
			public  readonly    NonTerminal LiteralDeclaration          = new NonTerminal (nameof (LiteralDeclaration));

			public  readonly    NonTerminal SeeDeclaration              = new NonTerminal (nameof (SeeDeclaration));

			// https://docs.oracle.com/javase/7/docs/technotes/tools/windows/javadoc.html#value
			public  readonly    NonTerminal ValueDeclaration            = new NonTerminal (nameof (ValueDeclaration));

			public  readonly    NonTerminal InlineParamDeclaration      = new NonTerminal (nameof (InlineParamDeclaration));

			public  readonly    Terminal    IgnorableDeclaration        = new IgnorableCharTerminal (nameof (IgnorableDeclaration)) {
				AstConfig = new AstNodeConfig {
					NodeCreator = (context, parseNode) => parseNode.AstNode = parseNode.Token.Value.ToString (),
				},
			};

		}
	}

	class IgnorableCharTerminal : Terminal
	{
		public IgnorableCharTerminal (string name)
			: base (name)
		{
			Priority = TerminalPriority.Low - 1;
		}

		public override Token? TryMatch (ParsingContext context, ISourceStream source)
		{
			var startChar = source.Text [source.Location.Position];
			if (startChar != '@'
				&& startChar != '{'
				&& startChar != '}'
				) {
				return null;
			}
			source.PreviewPosition += 1;
			return source.CreateToken (OutputTerminal, startChar);
		}

	}

}
