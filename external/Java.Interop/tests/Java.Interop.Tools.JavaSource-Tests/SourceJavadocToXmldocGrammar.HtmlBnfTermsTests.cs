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
	public class SourceJavadocToXmldocGrammarHtmlBnfTermsTests : SourceJavadocToXmldocGrammarFixture {

		[Test]
		public void PBlockDeclaration ()
		{
			var p = CreateParser (g => g.HtmlTerms.PBlockDeclaration);

			var r = p.Parse ("<p>paragraph text\nand more!");
			Assert.IsFalse (r.HasErrors (), DumpMessages (r, p));
			Assert.AreEqual ("<para>paragraph text\nand more!</para>".Replace ("\n", Environment.NewLine),
					r.Root.AstNode.ToString ());

			r = p.Parse ("<p>r= {@code Object} and following {@literal A<B>C}   text</p>");
			Assert.IsFalse (r.HasErrors (), DumpMessages (r, p));
			Assert.AreEqual ("<para>r= <c>Object</c> and following A&lt;B&gt;C   text</para>", r.Root.AstNode.ToString ());

			r = p.Parse("<p>r= <em>unknown</em> text");
			Assert.IsFalse (r.HasErrors (), DumpMessages (r, p));
			Assert.AreEqual ("<para>r= &lt;em&gt;unknown&lt;/em&gt; text</para>", r.Root.AstNode.ToString ());
		}

		[Test]
		public void PreBlockDeclaration ()
		{
			var p = CreateParser (g => g.HtmlTerms.PreBlockDeclaration);

			var r = p.Parse ("<pre>this @contains <arbitrary/> text.</pre>");
			Assert.IsFalse (r.HasErrors (), DumpMessages (r, p));
			Assert.AreEqual ("<code lang=\"text/java\">this @contains &lt;arbitrary/&gt; text.</code>",
					r.Root.AstNode.ToString ());

			r = p.Parse ("<pre class=\"prettyprint\">ColorSpace cs = ColorSpace.get(ColorSpace.Named.DCI_P3);");
			Assert.IsFalse (r.HasErrors (), DumpMessages (r, p));
			Assert.AreEqual ($"<code lang=\"text/java\">ColorSpace cs = ColorSpace.get(ColorSpace.Named.DCI_P3);</code>",
					r.Root.AstNode.ToString ());
		}

		[Test]
		public void HyperLinkDeclaration ()
		{
			var p = CreateParser (g => g.HtmlTerms.InlineHyperLinkDeclaration);

			var r = p.Parse ("<a href=\"https://developer.android.com/guide/topics/manifest/application-element.html\">application</a>");
			Assert.IsFalse (r.HasErrors (), DumpMessages (r, p));
			Assert.AreEqual ("<see href=\"https://developer.android.com/guide/topics/manifest/application-element.html\">application</see>",
					r.Root.AstNode.ToString ());

			r = p.Parse ("<a href=\"AutofillService.html#FieldClassification\">field classification</a>");
			Assert.IsFalse (r.HasErrors (), DumpMessages (r, p));
			Assert.AreEqual ("\"AutofillService.html#FieldClassification\"&gt;field classification",
					r.Root.AstNode.ToString ());
		}

		[Test]
		public void CodeElementDeclaration ()
		{
			var p = CreateParser (g => g.HtmlTerms.CodeElementDeclaration);

			var r = p.Parse ("<code>input.position()</code>");
			Assert.IsFalse (r.HasErrors (), DumpMessages (r, p));
			Assert.AreEqual ("<c>input.position()</c>", r.Root.AstNode.ToString ());
		}

	}
}
