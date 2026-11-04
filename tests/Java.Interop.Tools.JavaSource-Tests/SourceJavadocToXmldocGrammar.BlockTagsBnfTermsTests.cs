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
	public class SourceJavadocToXmldocGrammarBlockTagsBnfTermsTests : SourceJavadocToXmldocGrammarFixture {

		[Test]
		public void ApiSinceDeclaration ()
		{
			var p = CreateParser (g => g.BlockTagsTerms.ApiSinceDeclaration);

			var r = p.Parse ("@apiSince 3\n");
			Assert.IsFalse (r.HasErrors (), "@apiSince: " + DumpMessages (r, p));
			Assert.AreEqual ("<para>Added in API level 3.</para>", r.Root.AstNode.ToString ());
		}

		[Test]
		public void DeprecatedDeclaration ()
		{
			var p = CreateParser (g => g.BlockTagsTerms.DeprecatedDeclaration);

			var r = p.Parse ("@deprecated Insert reason here.\n");
			Assert.IsFalse (r.HasErrors (), "@deprecated: " + DumpMessages (r, p));
			Assert.AreEqual ("<para>This member is deprecated. Insert reason here.</para>", r.Root.AstNode.ToString ());
		}

		[Test]
		public void DeprecatedSinceDeclaration ()
		{
			var p = CreateParser (g => g.BlockTagsTerms.DeprecatedSinceDeclaration);

			var r = p.Parse ("@deprecatedSince 3\n");
			Assert.IsFalse (r.HasErrors (), "@deprecatedSince: " + DumpMessages (r, p));
			Assert.AreEqual ("<para>This member was deprecated in API level 3.</para>", r.Root.AstNode.ToString ());
		}

		[Test]
		public void ExceptionDeclaration ()
		{
			var p = CreateParser (g => g.BlockTagsTerms.ExceptionDeclaration);

			var r = p.Parse ("@exception Throwable Just Because.\n");
			Assert.IsFalse (r.HasErrors (), "@exception: " + DumpMessages (r, p));
			Assert.IsNull (r.Root.AstNode, "@exception should be ignored, but node was not null.");
			// TODO: Re-enable when we can generate valid crefs
			//Assert.AreEqual ("<exception cref=\"Throwable\">Just Because.</exception>", r.Root.AstNode.ToString ());
		}

		[Test]
		public void InheritDocDeclaration ()
		{
			var p = CreateParser (g => g.BlockTagsTerms.InheritDocDeclaration);

			var r = p.Parse ("@inheritDoc");
			Assert.IsFalse (r.HasErrors (), "@inheritDoc: " + DumpMessages (r, p));
			// TODO: Enable after adding support for @inheritDoc
			Assert.IsNull (r.Root.AstNode, "@inheritDoc should be ignored, but node was not null.");

			r = p.Parse ("@inheritDoc With extra");
			Assert.IsFalse (r.HasErrors (), "@inheritDoc with trailing content " + DumpMessages (r, p));
			// TODO: Enable after adding support for @inheritDoc
			Assert.IsNull (r.Root.AstNode, "@inheritDoc with trailing content should be ignored, but node was not null.");
		}

		[Test]
		public void HideDeclaration ()
		{
			var p = CreateParser (g => g.BlockTagsTerms.HideDeclaration);

			var r = p.Parse ("@hide");
			Assert.IsFalse (r.HasErrors (), "@hide: " + DumpMessages (r, p));
			Assert.IsNull (r.Root.AstNode, "@hide should be ignored, but node was not null.");
			r = p.Parse ("@hide Method is broken");
			Assert.IsFalse (r.HasErrors (), "@hide with trailing content: " + DumpMessages (r, p));
			Assert.IsNull (r.Root.AstNode, "@hide with trailing content should be ignored, but node was not null.");
		}

		[Test]
		public void ParamDeclaration ()
		{
			var p = CreateParser (g => g.BlockTagsTerms.ParamDeclaration);

			var r = p.Parse ("@param a Insert description here\nand here.");
			Assert.IsFalse (r.HasErrors (), "@param: " + DumpMessages (r, p));
			Assert.AreEqual ($"<param name=\"a\">Insert description here{Environment.NewLine}and here.</param>", r.Root.AstNode.ToString ());

			r = p.Parse ("@param b");
			Assert.IsFalse (r.HasErrors (), "name only @param: " + DumpMessages (r, p));
			Assert.AreEqual ("<param name=\"b\">b</param>", r.Root.AstNode.ToString ());
		}

		[Test]
		public void ReturnDeclaration ()
		{
			var p = CreateParser (g => g.BlockTagsTerms.ReturnDeclaration);

			var r = p.Parse ("@return insert description here");
			Assert.IsFalse (r.HasErrors (), "single-line @return: " + DumpMessages (r, p));
			Assert.AreEqual ("<returns>insert description here</returns>", r.Root.AstNode.ToString ());

			r = p.Parse ("@return line 1\n\tline two");
			Assert.IsFalse (r.HasErrors (), "multi-line @return: " + DumpMessages (r, p));
			Assert.AreEqual ("<returns>line 1\n\tline two</returns>".Replace ("\n", Environment.NewLine),
					r.Root.AstNode.ToString ());
		}

		[Test]
		public void ReturnDeclaration_WithInlineTags ()
		{
			var p = CreateParser (g => g.BlockTagsTerms.ReturnDeclaration);

			var r = p.Parse ("@return {@code text} here.");
			Assert.IsFalse (r.HasErrors (), DumpMessages (r, p));
			Assert.AreEqual ("<returns>\n  <c>text</c> here.</returns>".Replace ("\n", Environment.NewLine),
					r.Root.AstNode.ToString ());
		}

		[Test]
		public void SeeDeclaration ()
		{
			var p = CreateParser (g => g.BlockTagsTerms.SeeDeclaration);

			var r = p.Parse ("@see \"Insert Book Name Here\"");
			Assert.IsFalse (r.HasErrors (), "@see: " + DumpMessages (r, p));
			Assert.IsNull (r.Root.AstNode, "@see should be ignored, but node was not null.");
			// TODO: Re-enable when we can generate valid crefs
			//Assert.AreEqual ("<seealso cref=\"&quot;Insert Book Name Here&quot;\" />", r.Root.AstNode.ToString ());
		}

		[Test]
		public void SinceDeclaration ()
		{
			var p = CreateParser (g => g.BlockTagsTerms.SinceDeclaration);

			var r = p.Parse ("@since Insert Version Here");
			Assert.IsFalse (r.HasErrors (), "@since: " + DumpMessages (r, p));
			Assert.AreEqual ("<para>Added in Insert Version Here.</para>", r.Root.AstNode.ToString ());
		}

		[Test]
		public void ThrowsDeclaration ()
		{
			var p = CreateParser (g => g.BlockTagsTerms.ThrowsDeclaration);

			var r = p.Parse ("@throws Throwable the {@code Exception} raised by this method");
			Assert.IsFalse (r.HasErrors (), "{@code} @throws: " + DumpMessages (r, p));
			Assert.IsNull (r.Root.AstNode, "@throws should be ignored, but node with code block was not null.");
			// TODO: Re-enable when we can generate valid crefs
			//Assert.AreEqual ("<exception cref=\"Throwable\">the <c>Exception</c> raised by this method</exception>", r.Root.AstNode.ToString ());

			r = p.Parse ("@throws Throwable something <i>or other</i>!");
			Assert.IsFalse (r.HasErrors (), "<i> @throws: " + DumpMessages (r, p));
			Assert.IsNull (r.Root.AstNode, "@throws should be ignored, but node with <i> was not null.");
			// TODO: Re-enable when we can generate valid crefs
			//Assert.AreEqual ("<exception cref=\"Throwable\">something <i>or other</i>!</exception>", r.Root.AstNode.ToString ());

			r = p.Parse ("@throws android.content.ActivityNotFoundException");
			Assert.IsFalse (r.HasErrors (), "name only @throws: " + DumpMessages (r, p));
			// TODO: Re-enable when we can generate valid crefs
			Assert.IsNull (r.Root.AstNode, "@throws should be ignored, but name only node was not null.");
		}

		[Test]
		public void UnknownTagDeclaration ()
		{
			var p = CreateParser (g => g.BlockTagsTerms.UnknownTagDeclaration);

			var r = p.Parse ("@this-is-not-supported something {@code foo} else.");
			Assert.IsFalse (r.HasErrors (), "@this-is-not-supported: " + DumpMessages (r, p));
			Assert.AreEqual (null, r.Root.AstNode);

			r = p.Parse ("@standalonetag");
			Assert.IsFalse (r.HasErrors (), "@standalonetag: " + DumpMessages (r, p));
			Assert.AreEqual (null, r.Root.AstNode);
		}
	}
}
