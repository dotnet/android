using System;
using System.Collections.Generic;
using System.Linq;
using MonoDroid.Generation;
using NUnit.Framework;
using Xamarin.Android.Binder;

namespace generatortests
{
	[TestFixture]
	public class CodeGenerationOptionsTests
	{
		[TestCase ("JavaInterop1")]
		[TestCase ("javainterop1")]
		[TestCase ("JAVAINTEROP1")]
		public void RetiredCodeGenerationTargetIsRejected (string target)
		{
			var error = Assert.Throws<NotSupportedException> (() =>
				CodeGeneratorOptions.Parse (new [] { $"--codegen-target={target}", "api.xml" }));
			Assert.That (error?.Message, Does.Contain ("no longer supported").And.Contain ("XAJavaInterop1"));
		}

		[Test]
		public void DefaultCodeGenerationTarget ()
		{
			var options = CodeGeneratorOptions.Parse (new [] { "--public", "api.xml" });
			Assert.That (options?.CodeGenerationTarget, Is.EqualTo (CodeGenerationTarget.XAJavaInterop1));
			Assert.That (new CodeGenerationOptions ().CodeGenerationTarget, Is.EqualTo (CodeGenerationTarget.XAJavaInterop1));
		}

		[TestCase ("XAJavaInterop1")]
		[TestCase ("xajavainterop1")]
		public void SupportedCodeGenerationTargetIsParsed (string target)
		{
			var options = CodeGeneratorOptions.Parse (new [] { $"--codegen-target={target}", "api.xml" });
			Assert.That (options?.CodeGenerationTarget, Is.EqualTo (CodeGenerationTarget.XAJavaInterop1));
		}

		[Test]
		public void UndefinedCodeGenerationTargetIsRejected ()
		{
			Assert.Throws<NotSupportedException> (() =>
				new CodeGenerationOptions { CodeGenerationTarget = (CodeGenerationTarget) 1 });
		}

		[Test]
		public void GetOutputNameUseGlobal ()
		{
			var opt = new CodeGenerationOptions { UseGlobal = true };

			Assert.AreEqual (string.Empty, opt.GetOutputName (string.Empty));
			Assert.AreEqual ("int", opt.GetOutputName ("int"));
			Assert.AreEqual ("void", opt.GetOutputName ("void"));
			Assert.AreEqual ("void", opt.GetOutputName ("System.Void"));
			Assert.AreEqual ("params int[]", opt.GetOutputName ("params int[]"));
			Assert.AreEqual ("params global::System.Object[]", opt.GetOutputName ("params System.Object[]"));
			Assert.AreEqual ("int[][][]", opt.GetOutputName ("int[][][]"));
			Assert.AreEqual ("global::System.Object[][][]", opt.GetOutputName ("System.Object[][][]"));

			Assert.AreEqual ("global::System.Collections.Generic.List<string[]>",
				opt.GetOutputName ("System.Collections.Generic.List<string[]>"));

			Assert.AreEqual ("global::System.Collections.Generic.Dictionary<string, string>",
				opt.GetOutputName ("System.Collections.Generic.Dictionary<string, string>"));

			Assert.AreEqual ("global::System.Collections.Generic.List<global::System.Collections.Generic.List<string>>",
				opt.GetOutputName ("System.Collections.Generic.List<System.Collections.Generic.List<string>>"));

			Assert.AreEqual ("global::System.Collections.Generic.List<global::System.Collections.Generic.Dictionary<string, global::System.Collections.Generic.Dictionary<global::System.Object, global::System.Object>>>",
				opt.GetOutputName ("System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<System.Object, System.Object>>>"));

			Assert.AreEqual ("global::System.Collections.Generic.IList<global::Kotlin.Pair>",
				opt.GetOutputName ("System.Collections.Generic.IList<Kotlin.Pair>"));

			Assert.AreEqual ("global::System.Collections.Generic.IDictionary<string, global::System.Collections.Generic.IList<global::Kotlin.Pair>>",
				opt.GetOutputName ("System.Collections.Generic.IDictionary<string, System.Collections.Generic.IList<Kotlin.Pair>>"));

			Assert.AreEqual ("global::System.Collections.Generic.IDictionary<global::System.Collections.Generic.IList<string>, global::Kotlin.Pair>",
				opt.GetOutputName ("System.Collections.Generic.IDictionary<System.Collections.Generic.IList<string>, Kotlin.Pair>"));

			Assert.AreEqual ("global::System.Collections.Generic.IDictionary<global::System.Collections.Generic.IList<string>, global::System.Collections.Generic.IList<global::Kotlin.Pair>>",
				opt.GetOutputName ("System.Collections.Generic.IDictionary<System.Collections.Generic.IList<string>, System.Collections.Generic.IList<Kotlin.Pair>>"));

			Assert.AreEqual ("global::System.Collections.Generic.List<global::System.Collections.Generic.List<string>[]>[]",
				opt.GetOutputName ("System.Collections.Generic.List<System.Collections.Generic.List<string>[]>[]"));

			Assert.AreEqual ("global::System.Collections.Generic.List<global::System.Collections.Generic.List<string>.Enumerator[]>",
				opt.GetOutputName ("System.Collections.Generic.List<System.Collections.Generic.List<string>.Enumerator[]>"));
		}

		[Test]
		public void GetTypeReferenceName_Nullable ()
		{
			var opt = new CodeGenerationOptions { SupportNullableReferenceTypes = true };

			// System.Void isn't a valid return type, ensure it gets changed to void
			var system_void = new ReturnValue (null, "void", "System.Void", false, false);
			system_void.Validate (opt, null, null);

			Assert.AreEqual ("void", opt.GetTypeReferenceName (system_void));

			// Primitive types should not be nullable
			var primitive_void = new ReturnValue (null, "void", "void", false, false);
			primitive_void.Validate (opt, null, null);

			Assert.AreEqual ("void", opt.GetTypeReferenceName (primitive_void));

			var system_uint = new ReturnValue (null, "uint", "System.UInt32", false, false);
			system_uint.Validate (opt, null, null);

			Assert.AreEqual ("System.UInt32", opt.GetTypeReferenceName (system_uint));

			var primitive_uint = new ReturnValue (null, "uint", "uint", false, false);
			primitive_uint.Validate (opt, null, null);

			Assert.AreEqual ("uint", opt.GetTypeReferenceName (primitive_uint));
		}

		[Test, TestCaseSource (nameof (ReservedKeywords))]
		[SetCulture ("cs-CZ")]
		public void GetSafeIdentifierCultureInvariant (string keyword)
		{
			var opt = new CodeGenerationOptions ();

			Assert.AreEqual ($"{keyword}_", opt.GetSafeIdentifier (keyword));
		}

		private static IEnumerable<TestCaseData> ReservedKeywords
			=> TypeNameUtilities.reserved_keywords.Select (keyword => new TestCaseData (keyword));
	}
}
