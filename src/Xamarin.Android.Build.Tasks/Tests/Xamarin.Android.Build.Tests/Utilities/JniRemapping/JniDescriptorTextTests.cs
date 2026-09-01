using System;
using NUnit.Framework;
using Xamarin.Android.Tasks.JniRemapping;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class JniDescriptorTextTests : BaseTest
	{
		static string? Rename (string cls) => cls == "acme/orig/MyView" ? "a/b/C" : null;

		[Test]
		public void RewritesObjectParameterAndReturnTypes ()
		{
			bool changed = JniDescriptorText.TryRewriteDescriptor ("(Lacme/orig/MyView;I)Lacme/orig/MyView;", Rename, out string rewritten);

			Assert.IsTrue (changed);
			Assert.AreEqual ("(La/b/C;I)La/b/C;", rewritten);
		}

		[Test]
		public void RewritesArrayOfObjectType ()
		{
			bool changed = JniDescriptorText.TryRewriteDescriptor ("[Lacme/orig/MyView;", Rename, out string rewritten);

			Assert.IsTrue (changed);
			Assert.AreEqual ("[La/b/C;", rewritten);
		}

		[Test]
		public void LeavesUnrelatedTypesAndPrimitivesAlone ()
		{
			bool changed = JniDescriptorText.TryRewriteDescriptor ("(Landroid/view/View;[I)V", Rename, out string rewritten);

			Assert.IsFalse (changed);
			Assert.AreEqual ("(Landroid/view/View;[I)V", rewritten);
		}

		[TestCase ("()V", true)]
		[TestCase ("(Ljava/lang/Object;)Z", true)]
		[TestCase ("(I)I", true)]
		[TestCase ("(V)V", false)]
		[TestCase ("()[V", false)]
		[TestCase ("I", false)]
		[TestCase ("Ljava/lang/Object;", false)]
		[TestCase ("not a descriptor", false)]
		public void ValidatesMethodDescriptors (string descriptor, bool expected)
		{
			Assert.AreEqual (expected, JniDescriptorText.IsValidMethodDescriptor (descriptor));
		}

		[TestCase ("I", true)]
		[TestCase ("[I", true)]
		[TestCase ("Ljava/lang/Object;", true)]
		[TestCase ("V", false)]
		[TestCase ("[V", false)]
		[TestCase ("()V", false)]
		[TestCase ("", false)]
		public void ValidatesFieldDescriptors (string descriptor, bool expected)
		{
			Assert.AreEqual (expected, JniDescriptorText.IsValidFieldDescriptor (descriptor));
		}

		[Test]
		public void ConvertsMethodDescriptorToJavaParameterTypes ()
		{
			var parameters = JniDescriptorText.MethodDescriptorToJavaParameterTypes ("(Landroid/os/Bundle;I[Ljava/lang/String;)V");

			CollectionAssert.AreEqual (new [] { "android.os.Bundle", "int", "java.lang.String[]" }, parameters);
		}

		[Test]
		public void ConvertsSingleTypeTokenToJavaSource ()
		{
			Assert.AreEqual ("boolean", JniDescriptorText.JniTypeTokenToJavaSource ("Z"));
			Assert.AreEqual ("int[]", JniDescriptorText.JniTypeTokenToJavaSource ("[I"));
			Assert.AreEqual ("java.lang.Object", JniDescriptorText.JniTypeTokenToJavaSource ("Ljava/lang/Object;"));
		}
	}
}
