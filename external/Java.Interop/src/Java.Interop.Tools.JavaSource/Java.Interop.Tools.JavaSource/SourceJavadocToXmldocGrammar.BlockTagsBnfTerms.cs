using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Irony.Ast;
using Irony.Parsing;

namespace Java.Interop.Tools.JavaSource {

	public partial class SourceJavadocToXmldocGrammar {

		// https://docs.oracle.com/javase/7/docs/technotes/tools/windows/javadoc.html#javadoctags
		public class BlockTagsBnfTerms {

			internal BlockTagsBnfTerms ()
			{
			}

			internal void CreateRules (SourceJavadocToXmldocGrammar grammar)
			{
				AllBlockTerms.Rule = AuthorDeclaration
					| ApiSinceDeclaration
					| DeprecatedDeclaration
					| DeprecatedSinceDeclaration
					| ExceptionDeclaration
					| ParamDeclaration
					| ReturnDeclaration
					| SeeDeclaration
					| SerialDataDeclaration
					| SerialFieldDeclaration
					| SinceDeclaration
					| ThrowsDeclaration
					| UnknownTagDeclaration
					| VersionDeclaration
					;
				BlockValue.Rule = grammar.HtmlTerms.ParsedCharacterData
					| grammar.HtmlTerms.InlineDeclaration
					;
				BlockValues.MakePlusRule (grammar, BlockValue);

				AuthorDeclaration.Rule = "@author" + BlockValues;
				AuthorDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					if (!grammar.ShouldImport (ImportJavadoc.AuthorTag))
						return;
					// Ignore; not sure how best to convert to Xmldoc
					FinishParse (context, parseNode);
				};

				ApiSinceDeclaration.Rule = "@apiSince" + BlockValues;
				ApiSinceDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					if (!grammar.ShouldImport (ImportJavadoc.SinceTag)) {
						return;
					}
					var p = new XElement ("para", "Added in API level ", AstNodeToXmlContent (parseNode.ChildNodes [1]), ".");
					FinishParse (context, parseNode).Remarks.Add (p);
					parseNode.AstNode   = p;
				};

				DeprecatedDeclaration.Rule = "@deprecated" + BlockValues;
				DeprecatedDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					if (!grammar.ShouldImport (ImportJavadoc.DeprecatedTag)) {
						return;
					}
					var p = new XElement ("para", "This member is deprecated. ", AstNodeToXmlContent (parseNode.ChildNodes [1]));
					FinishParse (context, parseNode).Remarks.Add (p);
					parseNode.AstNode   = p;
				};

				DeprecatedSinceDeclaration.Rule = "@deprecatedSince" + BlockValues;
				DeprecatedSinceDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					if (!grammar.ShouldImport (ImportJavadoc.DeprecatedTag)) {
						return;
					}
					var p = new XElement ("para", "This member was deprecated in API level ", AstNodeToXmlContent (parseNode.ChildNodes [1]), ".");
					FinishParse (context, parseNode).Remarks.Add (p);
					parseNode.AstNode   = p;
				};

				var nonSpaceTerm = new RegexBasedTerminal ("[^ ]", "[^ ]+") {
					AstConfig = new AstNodeConfig {
						NodeCreator = (context, parseNode) => parseNode.AstNode = parseNode.Token.Value,
					},
				};

				ExceptionDeclaration.Rule = "@exception" + nonSpaceTerm + BlockValues;
				ExceptionDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					if (!grammar.ShouldImport (ImportJavadoc.ExceptionTag)) {
						return;
					}
					/* TODO: convert `nonSpaceTerm` into a proper CREF
					var e = new XElement ("exception",
							new XAttribute ("cref", string.Join ("", AstNodeToXmlContent (parseNode.ChildNodes [1]))),
							AstNodeToXmlContent (parseNode.ChildNodes [2]));
					FinishParse (context, parseNode).Exceptions.Add (e);
					parseNode.AstNode   = e;
					*/
					FinishParse (context, parseNode);
				};

				ParamDeclaration.Rule = "@param" + nonSpaceTerm + BlockValues;
				ParamDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					if (!grammar.ShouldImport (ImportJavadoc.ParamTag)) {
						return;
					}
					var p = new XElement ("param",
							new XAttribute ("name", string.Join ("", AstNodeToXmlContent (parseNode.ChildNodes [1]))),
							AstNodeToXmlContent (parseNode.ChildNodes [2]));
					FinishParse (context, parseNode).Parameters.Add (p);
					parseNode.AstNode   = p;
				};

				ReturnDeclaration.Rule = "@return" + BlockValues;
				ReturnDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					if (!grammar.ShouldImport (ImportJavadoc.ReturnTag)) {
						return;
					}
					// When encountering multiple @return keys in a line, append subsequent @return key content to the original <returns> element.
					var jdi = FinishParse (context, parseNode);
					if (jdi.Returns.Count == 0) {
						var r = new XElement ("returns",
							AstNodeToXmlContent (parseNode.ChildNodes [1]));
						FinishParse (context, parseNode).Returns.Add (r);
						parseNode.AstNode = r;
					} else {
						var r = jdi.Returns.First () as XElement;
						if (r != null) {
							r.Add (" ", AstNodeToXmlContent (parseNode.ChildNodes [1]));
							parseNode.AstNode = r;
						}
					}
				};

				SeeDeclaration.Rule = "@see" + BlockValues;
				SeeDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					if (!grammar.ShouldImport (ImportJavadoc.SeeTag)) {
						return;
					}
					/* TODO: @see supports multiple forms; see: https://docs.oracle.com/javase/7/docs/technotes/tools/windows/javadoc.html#see
					// Also need to convert to appropriate CREF value, ignore for now
					var e = new XElement ("seealso",
							new XAttribute ("cref", string.Join ("", AstNodeToXmlContent (parseNode.ChildNodes [1]))));
					FinishParse (context, parseNode).Extra.Add (e);
					parseNode.AstNode   = e;
					*/
					FinishParse (context, parseNode);

				};

				SinceDeclaration.Rule = "@since" + BlockValues;
				SinceDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					if (!grammar.ShouldImport (ImportJavadoc.SinceTag)) {
						return;
					}
					var p = new XElement ("para", "Added in ", AstNodeToXmlContent (parseNode.ChildNodes [1]), ".");
					FinishParse (context, parseNode).Remarks.Add (p);
					parseNode.AstNode   = p;
				};

				ThrowsDeclaration.Rule = "@throws" + nonSpaceTerm + BlockValues;
				ThrowsDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					if (!grammar.ShouldImport (ImportJavadoc.ExceptionTag)) {
						return;
					}
					/* TODO: convert `nonSpaceTerm` into a proper CREF
					var e = new XElement ("exception",
							new XAttribute ("cref", string.Join ("", AstNodeToXmlContent (parseNode.ChildNodes [1]))),
							AstNodeToXmlContent (parseNode.ChildNodes [2]));
					FinishParse (context, parseNode).Exceptions.Add (e);
					parseNode.AstNode   = e;
					*/
					FinishParse (context, parseNode);
				};

				// Ignore serialization informatino
				SerialDeclaration.Rule = "@serial" + BlockValues;
				SerialDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					if (!grammar.ShouldImport (ImportJavadoc.SerialTag)) {
						return;
					}
					FinishParse (context, parseNode);
				};

				SerialDataDeclaration.Rule = "@serialData" + BlockValues;
				SerialDataDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					if (!grammar.ShouldImport (ImportJavadoc.SerialTag)) {
						return;
					}
					FinishParse (context, parseNode);
				};

				SerialFieldDeclaration.Rule = "@serialField" + BlockValues;
				SerialFieldDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					if (!grammar.ShouldImport (ImportJavadoc.SerialTag)) {
						return;
					}
					FinishParse (context, parseNode);
				};

				var unknownTagTerminal = new RegexBasedTerminal ("@[unknown]", @"@\S+") {
					Priority    = TerminalPriority.Low,
				};
				unknownTagTerminal.AstConfig.NodeCreator = (context, parseNode) =>
					parseNode.AstNode = parseNode.Token.Value.ToString ();


				UnknownTagDeclaration.Rule = unknownTagTerminal + BlockValues;
				UnknownTagDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					if (!grammar.ShouldImport (ImportJavadoc.Remarks)) {
						return;
					}
					Console.WriteLine ($"# Unsupported @block-tag value: {parseNode.ChildNodes [0].AstNode}");
					FinishParse (context, parseNode);
				};

				// Ignore Version
				VersionDeclaration.Rule = "@version" + BlockValues;
				VersionDeclaration.AstConfig.NodeCreator = (context, parseNode) => {
					if (!grammar.ShouldImport (ImportJavadoc.VersionTag)) {
						return;
					}
					FinishParse (context, parseNode);
				};
			}

			public  readonly    NonTerminal AllBlockTerms              = new NonTerminal (nameof (AllBlockTerms), ConcatChildNodes);

			public  readonly    Terminal    Cdata                      = new CharacterDataTerminal ("#CDATA", preserveLeadingWhitespace: true);
/*
			public  readonly    Terminal    Cdata                      = new RegexBasedTerminal (nameof (BlockValue), "[^<]*") {
				AstConfig   = new AstNodeConfig {
					NodeCreator = (context, parseNode) => parseNode.AstNode = parseNode.Token.Value.ToString (),
				},
			};
			*/

			public  readonly    NonTerminal BlockValue                 = new NonTerminal (nameof (BlockValue), ConcatChildNodes);
			public  readonly    NonTerminal BlockValues                = new NonTerminal (nameof (BlockValues), ConcatChildNodes);
			public  readonly    NonTerminal AuthorDeclaration          = new NonTerminal (nameof (AuthorDeclaration));
			public  readonly    NonTerminal ApiSinceDeclaration        = new NonTerminal (nameof (ApiSinceDeclaration));
			public  readonly    NonTerminal DeprecatedDeclaration      = new NonTerminal (nameof (DeprecatedDeclaration));
			public  readonly    NonTerminal DeprecatedSinceDeclaration = new NonTerminal (nameof (DeprecatedSinceDeclaration));
			public  readonly    NonTerminal ExceptionDeclaration       = new NonTerminal (nameof (ExceptionDeclaration));
			public  readonly    NonTerminal ParamDeclaration           = new NonTerminal (nameof (ParamDeclaration));
			public  readonly    NonTerminal ReturnDeclaration          = new NonTerminal (nameof (ReturnDeclaration));
			public  readonly    NonTerminal SeeDeclaration             = new NonTerminal (nameof (SeeDeclaration));
			public  readonly    NonTerminal SerialDeclaration          = new NonTerminal (nameof (SerialDeclaration));
			public  readonly    NonTerminal SerialDataDeclaration      = new NonTerminal (nameof (SerialDataDeclaration));
			public  readonly    NonTerminal SerialFieldDeclaration     = new NonTerminal (nameof (SerialFieldDeclaration));
			public  readonly    NonTerminal SinceDeclaration           = new NonTerminal (nameof (SinceDeclaration));
			public  readonly    NonTerminal ThrowsDeclaration          = new NonTerminal (nameof (ThrowsDeclaration));
			public  readonly    NonTerminal UnknownTagDeclaration      = new NonTerminal (nameof (UnknownTagDeclaration));
			public  readonly    NonTerminal VersionDeclaration         = new NonTerminal (nameof (VersionDeclaration));
		}
	}
}
