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
	public class SourceJavadocToXmldocParserTests : SourceJavadocToXmldocGrammarFixture {

		[Test, TestCaseSource (nameof (TryParse_Success))]
		public void TryParse (ParseResult parseResult)
		{
			ParseTree parseTree;
			var p = new SourceJavadocToXmldocParser (new XmldocSettings {
				Style = XmldocStyle.Full,
				DocRootValue = DocRootPrefixActual,
			});
			var n = p.TryParse (parseResult.Javadoc, null, out parseTree);
			Assert.IsFalse (parseTree.HasErrors (), DumpMessages (parseTree, p));
			Assert.AreEqual (parseResult.FullXml, GetMemberXml (n), $"while parsing input: ```{parseResult.Javadoc}```");

			p = new SourceJavadocToXmldocParser (new XmldocSettings {
				Style = XmldocStyle.IntelliSense,
				DocRootValue = DocRootPrefixActual,
			});
			n = p.TryParse (parseResult.Javadoc, null, out parseTree);
			Assert.IsFalse (parseTree.HasErrors (), DumpMessages (parseTree, p));
			Assert.AreEqual (parseResult.IntelliSenseXml, GetMemberXml (n), $"while parsing input: ```{parseResult.Javadoc}```");
		}

		static string GetMemberXml (IEnumerable<XNode> members)
		{
			var e = new XElement ("member", members);
			return e.ToString ();
		}

		public static readonly ParseResult[] TryParse_Success = new ParseResult[]{
			new ParseResult {
				Javadoc = "Summary.\n\nP2.\n\n<p>Hello!</p>",
				FullXml = @"<member>
  <summary>Summary.</summary>
  <remarks>
    <para>Summary.</para>
    <para>P2.</para>
    <para>Hello!</para>
  </remarks>
</member>",
				IntelliSenseXml = @"<member>
  <summary>Summary.</summary>
</member>",
			},
			new ParseResult {
				Javadoc = "The inline {@code code} tag should work for summary info.",
				FullXml = @"<member>
  <summary>The inline <c>code</c> tag should work for summary info.</summary>
  <remarks>
    <para>The inline <c>code</c> tag should work for summary info.</para>
  </remarks>
</member>",
				IntelliSenseXml = @"<member>
  <summary>The inline <c>code</c> tag should work for summary info.</summary>
</member>",
			},
			new ParseResult {
				Javadoc = "@return {@code true} if something\n or other; otherwise {@code false}.",
				FullXml = @"<member>
  <returns>
    <c>true</c> if something
 or other; otherwise <c>false</c>.</returns>
</member>",
				IntelliSenseXml = @"<member>
  <returns>
    <c>true</c> if something
 or other; otherwise <c>false</c>.</returns>
</member>",
			},
			new ParseResult {
				Javadoc = "@return {@code true} if something else @return {@code false}.",
				FullXml = @"<member>
  <returns>
    <c>true</c> if something else <c>false</c>.</returns>
</member>",
				IntelliSenseXml = @"<member>
  <returns>
    <c>true</c> if something else <c>false</c>.</returns>
</member>",
			},
			new ParseResult {
				Javadoc = @"This is the summary sentence.  Insert
more description here.

What about soft paragraphs?

<p>What about <i>hard</i> paragraphs?

@param a something
@see #method()
@apiSince 1
",
				FullXml = @"<member>
  <param name=""a"">something</param>
  <summary>This is the summary sentence.</summary>
  <remarks>
    <para>This is the summary sentence.  Insert
more description here.</para>
    <para>What about soft paragraphs?</para>
    <para>What about <i>hard</i> paragraphs?</para>
    <para>Added in API level 1.</para>
  </remarks>
</member>",
				IntelliSenseXml = @"<member>
  <param name=""a"">something</param>
  <summary>This is the summary sentence.</summary>
</member>",
			},
			new ParseResult {
				Javadoc = "Summary.\n\n<p>Paragraph.</p><pre>foo @bar baz</pre>",
				FullXml = @"<member>
  <summary>Summary.</summary>
  <remarks>
    <para>Summary.</para>
    <para>Paragraph.</para>
    <code lang=""text/java"">foo @bar baz</code>
  </remarks>
</member>",
				IntelliSenseXml = @"<member>
  <summary>Summary.</summary>
</member>",
			},
			new ParseResult {
				Javadoc = "Something {@link #method}: description, \"<code>declaration</code>\" or \"<code>another declaration</code>\".\n\n@apiSince 1\n",
				FullXml = @"<member>
  <summary>Something <c>#method</c>: description, ""&lt;code&gt;declaration&lt;/code&gt;"" or ""&lt;code&gt;another declaration&lt;/code&gt;"".</summary>
  <remarks>
    <para>Something <c>#method</c>: description, ""&lt;code&gt;declaration&lt;/code&gt;"" or ""&lt;code&gt;another declaration&lt;/code&gt;"".</para>
    <para>Added in API level 1.</para>
  </remarks>
</member>",
				IntelliSenseXml = @"<member>
  <summary>Something <c>#method</c>: description, ""&lt;code&gt;declaration&lt;/code&gt;"" or ""&lt;code&gt;another declaration&lt;/code&gt;"".</summary>
</member>",
			},
			new ParseResult {
				// @jls is currently not supported; should be handled by @unknown-tag & ignored.
				Javadoc = "Summary.\n\n@jls 1.2\n",
				FullXml = @"<member>
  <summary>Summary.</summary>
  <remarks>
    <para>Summary.</para>
  </remarks>
</member>",
				IntelliSenseXml = @"<member>
  <summary>Summary.</summary>
</member>",
			},
			new ParseResult {
				// @jls is currently not supported; should be handled by @unknown-tag & ignored.
				Javadoc = "Summary.\n\n@throws Throwable insert <i>description</i> here.\n",
				FullXml = @"<member>
  <summary>Summary.</summary>
  <remarks>
    <para>Summary.</para>
  </remarks>
</member>",
				IntelliSenseXml = @"<member>
  <summary>Summary.</summary>
</member>",
			},
			new ParseResult {
				Javadoc = @"See <a href=""http://man7.org/linux/man-pages/man2/accept.2.html"">accept(2)</a>.  Insert
more description here.
How about another link <a href=""http://man7.org/linux/man-pages/man2/accept.2.html"">accept(2)</a>
@param manifest The value of the <a
href=""{@docRoot}guide/topics/manifest/manifest-element.html#vcode"">{@code
android:versionCode}</a> manifest attribute.
",
				FullXml = $@"<member>
  <param name=""manifest"">The value of the <see href=""{DocRootPrefixExpected}guide/topics/manifest/manifest-element.html#vcode""><c>android:versionCode</c></see> manifest attribute.</param>
  <summary>See <see href=""http://man7.org/linux/man-pages/man2/accept.2.html"">accept(2)</see>.</summary>
  <remarks>
    <para>See <see href=""http://man7.org/linux/man-pages/man2/accept.2.html"">accept(2)</see>.  Insert
more description here.
How about another link <see href=""http://man7.org/linux/man-pages/man2/accept.2.html"">accept(2)</see></para>
  </remarks>
</member>",
				IntelliSenseXml = $@"<member>
  <param name=""manifest"">The value of the <see href=""{DocRootPrefixExpected}guide/topics/manifest/manifest-element.html#vcode""><c>android:versionCode</c></see> manifest attribute.</param>
  <summary>See <see href=""http://man7.org/linux/man-pages/man2/accept.2.html"">accept(2)</see>.</summary>
</member>",
			},
		};

		public class ParseResult {
			public  string  Javadoc;
			public  string  FullXml;
			public  string  IntelliSenseXml;
		}
	}
}
