using System;
using System.Linq;
using System.Xml.Linq;

using NUnit.Framework;

using Java.Interop.Tools.JavaSource;

using Irony;
using Irony.Parsing;

namespace Java.Interop.Tools.JavaSource.Tests
{
	[TestFixture]
	public class SourceJavadocToXmldocGrammarInlineTagsBnfTermsTests : SourceJavadocToXmldocGrammarFixture {

		[Test]
		public void CodeDeclaration ()
		{
			var p = CreateParser (g => g.InlineTagsTerms.CodeDeclaration);

			var r = p.Parse ("{@code Object}");
			Assert.IsFalse (r.HasErrors (), DumpMessages (r, p));
			Assert.AreEqual ("<c>Object</c>", r.Root.AstNode.ToString ());
		}

		[Test]
		public void DocRootDeclaration ()
		{
			var p = CreateParser (g => g.InlineTagsTerms.DocRootDeclaration);

			var r = p.Parse ("{@docRoot}");
			Assert.IsFalse (r.HasErrors (), DumpMessages (r, p));
			Assert.AreEqual (DocRootPrefixExpected, r.Root.AstNode.ToString ());
		}

		[Test]
		public void InheritDocDeclaration ()
		{
			var p = CreateParser (g => g.InlineTagsTerms.InheritDocDeclaration);

			var r = p.Parse ("{@inheritDoc}");
			Assert.IsFalse (r.HasErrors (), DumpMessages (r, p));
			Assert.AreEqual ("To be added", r.Root.AstNode.ToString ());
		}

		[Test]
		public void LinkDeclaration ()
		{
			var p = CreateParser (g => g.InlineTagsTerms.LinkDeclaration);

			var r = p.Parse ("{@link #ctor}");
			Assert.IsFalse (r.HasErrors (), DumpMessages (r, p));
			var c = (XElement) r.Root.AstNode;
			Assert.AreEqual ("<c>#ctor</c>", c.ToString (SaveOptions.DisableFormatting));
		}

		[Test]
		public void LinkplainDeclaration ()
		{
			var p = CreateParser (g => g.InlineTagsTerms.LinkplainDeclaration);

			var r = p.Parse ("{@linkplain #ctor}");
			Assert.IsFalse (r.HasErrors (), DumpMessages (r, p));
			Assert.AreEqual ("#ctor", r.Root.AstNode.ToString ());
		}

		[Test]
		public void LiteralDeclaration ()
		{
			var p = CreateParser (g => g.InlineTagsTerms.LiteralDeclaration);

			var r = p.Parse ("{@literal A<B>C}");
			Assert.IsFalse (r.HasErrors (), DumpMessages (r, p));
			Assert.AreEqual ("A&lt;B&gt;C", r.Root.AstNode.ToString ());
		}

		[Test]
		public void SeeDeclaration ()
		{
			var p = CreateParser (g => g.InlineTagsTerms.SeeDeclaration);

			var r = p.Parse ("{@see #cancelNotification(String, String, int)}");
			Assert.IsFalse (r.HasErrors (), DumpMessages (r, p));
			Assert.AreEqual ("<c>#cancelNotification(String, String, int)</c>", r.Root.AstNode.ToString ());
		}

		[Test]
		public void ValueDeclaration ()
		{
			var p = CreateParser (g => g.InlineTagsTerms.ValueDeclaration);

			var r = p.Parse ("{@value}");
			Assert.IsFalse (r.HasErrors (), DumpMessages (r, p));
			Assert.AreEqual ("To be added", r.Root.AstNode.ToString ());

			r = p.Parse ("{@value #field}");
			Assert.IsFalse (r.HasErrors (), DumpMessages (r, p));
			Assert.AreEqual ("<c>#field</c>", r.Root.AstNode.ToString ());
		}
	}
}
