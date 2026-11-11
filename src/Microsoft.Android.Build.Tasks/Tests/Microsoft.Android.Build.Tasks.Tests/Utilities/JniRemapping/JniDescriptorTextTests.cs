using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Android.Tasks.JniRemapping;

namespace Xamarin.Android.Build.Tests
{
	[TestClass]
	public class JniDescriptorTextTests : BaseTest
	{
		static string? Rename (string cls) => cls == "acme/orig/MyView" ? "a/b/C" : null;

		[TestMethod]
		public void RewritesObjectParameterAndReturnTypes ()
		{
			bool changed = JniDescriptorText.TryRewriteDescriptor ("(Lacme/orig/MyView;I)Lacme/orig/MyView;", Rename, out string rewritten);

			Assert.IsTrue (changed);
			Assert.AreEqual ("(La/b/C;I)La/b/C;", rewritten);
		}

		[TestMethod]
		public void RewritesArrayOfObjectType ()
		{
			bool changed = JniDescriptorText.TryRewriteDescriptor ("[Lacme/orig/MyView;", Rename, out string rewritten);

			Assert.IsTrue (changed);
			Assert.AreEqual ("[La/b/C;", rewritten);
		}

		[TestMethod]
		public void LeavesUnrelatedTypesAndPrimitivesAlone ()
		{
			bool changed = JniDescriptorText.TryRewriteDescriptor ("(Landroid/view/View;[I)V", Rename, out string rewritten);

			Assert.IsFalse (changed);
			Assert.AreEqual ("(Landroid/view/View;[I)V", rewritten);
		}

		[TestMethod]
		[DataRow ("()V", true)]
		[DataRow ("(Ljava/lang/Object;)Z", true)]
		[DataRow ("(I)I", true)]
		[DataRow ("(V)V", false)]
		[DataRow ("()[V", false)]
		[DataRow ("(L;)V", false)]
		[DataRow ("(Ljava.lang.Object;)V", false)]
		[DataRow ("(Lfoo[Bar;)V", false)]
		[DataRow ("(Lfoo//Bar;)V", false)]
		[DataRow ("I", false)]
		[DataRow ("Ljava/lang/Object;", false)]
		[DataRow ("not a descriptor", false)]
		public void ValidatesMethodDescriptors (string descriptor, bool expected)
		{
			Assert.AreEqual (expected, JniDescriptorText.IsValidMethodDescriptor (descriptor));
		}

		[TestMethod]
		public void ConvertsMethodDescriptorToJavaTypes ()
		{
			JniDescriptorText.MethodDescriptorToJavaTypes ("(Landroid/os/Bundle;I[Ljava/lang/String;)Z",
				out var parameters, out string returnType);

			CollectionAssert.AreEqual (new [] { "android.os.Bundle", "int", "java.lang.String[]" }, parameters);
			Assert.AreEqual ("boolean", returnType);
		}
	}
}
