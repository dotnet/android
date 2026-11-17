using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MonoDroid.Generation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xamarin.Android.Binder;

namespace generatortests
{
	[TestClassAttribute]
	public class CodeGenerationOptionsTests
	{
		[TestMethodAttribute]
		[DataRow ("JavaInterop1")]
		[DataRow ("javainterop1")]
		[DataRow ("JAVAINTEROP1")]
		public void RetiredCodeGenerationTargetIsRejected (string target)
		{
			var error = Assert.ThrowsExactly<NotSupportedException> (() =>
				CodeGeneratorOptions.Parse (new [] { $"--codegen-target={target}", "api.xml" }));
			StringAssert.Contains (error?.Message, "no longer supported");
			StringAssert.Contains (error?.Message, "XAJavaInterop1");
		}

		[TestMethodAttribute]
		public void DefaultCodeGenerationTarget ()
		{
			var options = CodeGeneratorOptions.Parse (new [] { "--public", "api.xml" });
			Assert.AreEqual (CodeGenerationTarget.XAJavaInterop1, options?.CodeGenerationTarget);
			Assert.AreEqual (CodeGenerationTarget.XAJavaInterop1, new CodeGenerationOptions ().CodeGenerationTarget);
		}

		[TestMethodAttribute]
		[DataRow ("XAJavaInterop1")]
		[DataRow ("xajavainterop1")]
		public void SupportedCodeGenerationTargetIsParsed (string target)
		{
			var options = CodeGeneratorOptions.Parse (new [] { $"--codegen-target={target}", "api.xml" });
			Assert.AreEqual (CodeGenerationTarget.XAJavaInterop1, options?.CodeGenerationTarget);
		}

		[TestMethodAttribute]
		public void UndefinedCodeGenerationTargetIsRejected ()
		{
			Assert.ThrowsExactly<NotSupportedException> (() =>
				new CodeGenerationOptions { CodeGenerationTarget = (CodeGenerationTarget) 1 });
		}

		[TestMethodAttribute]
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

		[TestMethodAttribute]
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

		[TestMethodAttribute, DynamicData (nameof (ReservedKeywords))]
		public void GetSafeIdentifierCultureInvariant (string keyword)
		{
			var culture = CultureInfo.CurrentCulture;
			try {
				CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo ("cs-CZ");
				var opt = new CodeGenerationOptions ();
				Assert.AreEqual ($"{keyword}_", opt.GetSafeIdentifier (keyword));
			} finally {
				CultureInfo.CurrentCulture = culture;
			}
		}

		public static IEnumerable<object []> ReservedKeywords
			=> TypeNameUtilities.reserved_keywords.Select (keyword => new object [] { keyword });
	}
}
