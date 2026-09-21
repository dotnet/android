using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Xamarin.SourceWriter.Tests
{
	[TestFixture]
	public class WarningSuppressionTests
	{
		static string Write (Action<CodeWriter> body)
		{
			var sw = new StringWriter ();

			body (new CodeWriter (sw));

			return sw.ToString ();
		}

		static ClassWriter CreateClass (params string [] typeSuppressionCodes)
		{
			var klass = new ClassWriter { IsPublic = true, Name = "MyClass" };

			foreach (var code in typeSuppressionCodes)
				klass.SuppressWarnings.Add (new WarningSuppression (code, $"CS{code}: type"));

			return klass;
		}

		static MethodWriter CreateMethod (string name, params string [] suppressionCodes)
		{
			var method = new MethodWriter { IsPublic = true, Name = name, ReturnType = TypeReferenceWriter.Void };

			foreach (var code in suppressionCodes)
				method.SuppressWarnings.Add (new WarningSuppression (code, $"CS{code}: member"));

			return method;
		}

		[Test]
		public void MemberSuppressionIsScopedToTheMember ()
		{
			var klass = CreateClass ();

			klass.Methods.Add (CreateMethod ("Clean"));
			klass.Methods.Add (CreateMethod ("Dirty", "0618"));

			var output = Write (w => klass.Write (w));

			Assert.AreEqual (1, CountOf (output, "#pragma warning disable 0618"));
			Assert.AreEqual (1, CountOf (output, "#pragma warning restore 0618"));
			Assert.Less (output.IndexOf ("public void Clean", StringComparison.Ordinal), output.IndexOf ("#pragma warning disable 0618", StringComparison.Ordinal),
					"The suppression must not cover the preceding clean member.");
		}

		[Test]
		public void NestedSameCodeScopesEmitOnlyTheOutermostPair ()
		{
			// A `#pragma warning restore` ends the suppression however many `disable`
			// directives preceded it, so emitting a nested pair for the same code would leave
			// the rest of the enclosing scope unguarded.
			var klass = CreateClass ("0618");

			klass.Methods.Add (CreateMethod ("First", "0618"));
			klass.Methods.Add (CreateMethod ("Second", "0618"));

			var output = Write (w => klass.Write (w));

			Assert.AreEqual (1, CountOf (output, "#pragma warning disable 0618"));
			Assert.AreEqual (1, CountOf (output, "#pragma warning restore 0618"));
			Assert.Less (output.IndexOf ("public class MyClass", StringComparison.Ordinal), output.IndexOf ("#pragma warning restore 0618", StringComparison.Ordinal),
					"The one restore that is emitted must be the type's, after the whole body.");
		}

		[Test]
		public void NestedDifferentCodeScopesAreBothEmitted ()
		{
			var klass = CreateClass ("0618");

			klass.Methods.Add (CreateMethod ("First", "8764"));

			var output = Write (w => klass.Write (w));

			Assert.AreEqual (1, CountOf (output, "#pragma warning disable 0618"));
			Assert.AreEqual (1, CountOf (output, "#pragma warning disable 8764"));
			Assert.Less (output.IndexOf ("#pragma warning disable 8764", StringComparison.Ordinal), output.IndexOf ("#pragma warning restore 8764", StringComparison.Ordinal));
			Assert.Less (output.IndexOf ("#pragma warning restore 8764", StringComparison.Ordinal), output.IndexOf ("#pragma warning restore 0618", StringComparison.Ordinal),
					"The inner scope has to close before the outer one.");
		}

		[Test]
		public void SiblingScopesForTheSameCodeAreEachEmitted ()
		{
			var klass = CreateClass ();

			klass.Methods.Add (CreateMethod ("First", "0618"));
			klass.Methods.Add (CreateMethod ("Second", "0618"));

			var output = Write (w => klass.Write (w));

			Assert.AreEqual (2, CountOf (output, "#pragma warning disable 0618"));
			Assert.AreEqual (2, CountOf (output, "#pragma warning restore 0618"));
		}

		[Test]
		public void SignatureSuppressionIsRestoredInsideTheTypeBody ()
		{
			var klass = CreateClass ();

			klass.SignatureSuppressions.SuppressWarnings.Add (new WarningSuppression ("0618", "CS0618: base type"));
			klass.Methods.Add (CreateMethod ("Member", "0618"));

			var output = Write (w => klass.Write (w));

			// The signature scope covers only the declaration, so the member inside the body
			// is no longer nested within it and needs its own pair.
			Assert.AreEqual (2, CountOf (output, "#pragma warning disable 0618"));
			Assert.Less (output.IndexOf ("#pragma warning restore 0618", StringComparison.Ordinal), output.IndexOf ("public void Member", StringComparison.Ordinal));
		}

		[Test]
		public void TheSameCodeIsEmittedOnceWithBothReasons ()
		{
			var method = CreateMethod ("Member");

			method.SuppressWarnings.Add (new WarningSuppression ("0618", "first reason"));
			method.SuppressWarnings.Add (new WarningSuppression ("0618", "second reason"));

			var output = Write (w => method.Write (w));

			Assert.AreEqual (1, CountOf (output, "#pragma warning disable 0618"));
			StringAssert.Contains ("// first reason second reason", output);
		}

		[Test]
		public void RestoringAnUnopenedSuppressionThrows ()
		{
			var writer = new CodeWriter (new StringWriter ());

			Assert.Throws<InvalidOperationException> (() => writer.EndWarningSuppression ("0618"));
		}

		[Test]
		public void SuppressionDepthIsTracked ()
		{
			var writer = new CodeWriter (new StringWriter ());

			Assert.IsFalse (writer.IsWarningSuppressed ("0618"));
			Assert.IsTrue (writer.BeginWarningSuppression ("0618"), "The outermost scope is the one that writes the directive.");
			Assert.IsTrue (writer.IsWarningSuppressed ("0618"));
			Assert.IsFalse (writer.BeginWarningSuppression ("0618"), "A nested scope must not write a second directive.");
			Assert.IsFalse (writer.EndWarningSuppression ("0618"), "A nested scope must not restore the enclosing one.");
			Assert.IsTrue (writer.IsWarningSuppressed ("0618"));
			Assert.IsTrue (writer.EndWarningSuppression ("0618"));
			Assert.IsFalse (writer.IsWarningSuppressed ("0618"));
		}

		static int CountOf (string haystack, string needle)
		{
			var count = 0;

			for (var i = haystack.IndexOf (needle, StringComparison.Ordinal); i >= 0; i = haystack.IndexOf (needle, i + needle.Length, StringComparison.Ordinal))
				count++;

			return count;
		}
	}
}
