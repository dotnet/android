using System.IO;
using NUnit.Framework;
using Xamarin.Android.Tasks.JniRemapping;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class LdstrRewriterTests : BaseTest
	{
		static R8Mapping BuildMapping () => R8Mapping.Parse (new StringReader (
			"acme.orig.MyView -> a.b.C:\n" +
			"    void onClick(android.view.View) -> a\n" +
			"    int someField -> x\n" +
			"acme.orig.Marker -> a.b.D:\n"));

		[Test]
		public void RewritesJniPeerMembersEncodedMethodId ()
		{
			bool changed = LdstrRewriter.TryRewrite ("onClick.(Landroid/view/View;)V", "acme/orig/MyView", BuildMapping (), out string rewritten);

			Assert.IsTrue (changed);
			Assert.AreEqual ("a.(Landroid/view/View;)V", rewritten);
		}

		[Test]
		public void RewritesJniPeerMembersEncodedFieldId ()
		{
			bool changed = LdstrRewriter.TryRewrite ("someField.I", "acme/orig/MyView", BuildMapping (), out string rewritten);

			Assert.IsTrue (changed);
			Assert.AreEqual ("x.I", rewritten);
		}

		[Test]
		public void RewrittenFieldIdRoundTripsThroughAccessManifest ()
		{
			R8Mapping mapping = BuildMapping ();
			Assert.IsTrue (LdstrRewriter.TryRewrite ("someField.I", "acme/orig/MyView", mapping, out string rewritten));
			mapping.RestrictReverseLookupsTo (mapping.AccessedEntries);
			IJniNameMapping reverse = mapping.CreateReverseMapping ();

			Assert.AreEqual ("x.I", rewritten);
			Assert.IsTrue (reverse.TryMapField ("a/b/C", "x", out string originalField));
			Assert.AreEqual ("someField", originalField);
		}

		[Test]
		public void RewritesBareConstructorDescriptorEmbeddedTypesOnly ()
		{
			bool changed = LdstrRewriter.TryRewrite ("(Lacme/orig/Marker;)V", "acme/orig/MyView", BuildMapping (), out string rewritten);

			Assert.IsTrue (changed);
			Assert.AreEqual ("(La/b/D;)V", rewritten);
		}

		[Test]
		public void UnchangedBareDescriptorIsReportedAsNotChanged ()
		{
			bool changed = LdstrRewriter.TryRewrite ("()V", "acme/orig/MyView", BuildMapping (), out string rewritten);

			Assert.IsFalse (changed);
			Assert.AreEqual ("()V", rewritten);
		}

		[Test]
		public void RewritesSingleRegisterNativesLine ()
		{
			bool changed = LdstrRewriter.TryRewrite ("onClick:(Landroid/view/View;)V:cb", "acme/orig/MyView", BuildMapping (), out string rewritten);

			Assert.IsTrue (changed);
			Assert.AreEqual ("a:(Landroid/view/View;)V:cb", rewritten);
		}

		[Test]
		public void RewritesMultilineRegisterNativesBlockWithoutTrailingNewline ()
		{
			string original = "onClick:(Landroid/view/View;)V:cb1\nunrelated:()V:cb2";
			bool changed = LdstrRewriter.TryRewrite (original, "acme/orig/MyView", BuildMapping (), out string rewritten);

			Assert.IsTrue (changed);
			Assert.AreEqual ("a:(Landroid/view/View;)V:cb1\nunrelated:()V:cb2", rewritten);
		}

		[Test]
		public void RewritesMultilineRegisterNativesBlockWithTrailingNewline ()
		{
			string original = "onClick:(Landroid/view/View;)V:cb1\n";
			bool changed = LdstrRewriter.TryRewrite (original, "acme/orig/MyView", BuildMapping (), out string rewritten);

			Assert.IsTrue (changed);
			Assert.AreEqual ("a:(Landroid/view/View;)V:cb1\n", rewritten);
		}

		[Test]
		public void RewritesExactClassNameString ()
		{
			bool changed = LdstrRewriter.TryRewrite ("acme/orig/Marker", null, BuildMapping (), out string rewritten);

			Assert.IsTrue (changed);
			Assert.AreEqual ("a/b/D", rewritten);
		}

		[Test]
		public void LeavesUnrelatedDotNetStringsAlone ()
		{
			bool changed = LdstrRewriter.TryRewrite ("System.String", "acme/orig/MyView", BuildMapping (), out string rewritten);

			Assert.IsFalse (changed);
			Assert.AreEqual ("System.String", rewritten);
		}

		[Test]
		public void LeavesUnknownMemberIdAloneWhenOwnerUnknown ()
		{
			bool changed = LdstrRewriter.TryRewrite ("someField.I", null, BuildMapping (), out string rewritten);

			Assert.IsFalse (changed);
			Assert.AreEqual ("someField.I", rewritten);
		}
	}
}
