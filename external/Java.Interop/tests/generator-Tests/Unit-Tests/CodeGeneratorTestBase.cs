using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using MonoDroid.Generation;
using NUnit.Framework;

namespace generatortests
{
	abstract class CodeGeneratorTestBase
	{
		protected CodeGenerator generator;
		protected StringBuilder builder;
		protected StringWriter writer;
		protected CodeGenerationOptions options;

		[SetUp]
		public void SetUp ()
		{
			builder = new StringBuilder ();
			writer = new StringWriter (builder);
			options = CreateOptions ();

			generator = options.CreateCodeGenerator (writer);
		}

		[TearDown]
		public void TearDown ()
		{
			writer.Dispose ();
		}

		protected virtual CodeGenerationOptions CreateOptions ()
		{
			return new CodeGenerationOptions {
				CodeGenerationTarget = Target,
			};
		}

		protected abstract Xamarin.Android.Binder.CodeGenerationTarget Target { get; }

		// Optionally override the directory where the expected test results are located.
		// For example, we duplicate the "XAJavaInterop1" tests with NRT as "XAJavaInterop1-NRT".
		protected virtual string TargetedDirectoryOverride => null;
		protected virtual string CommonDirectoryOverride => null;

		// Get the test results from "Common" for tests with the same results regardless of Target
		protected string GetExpected (string testName) => GetOriginalExpected (testName).NormalizeLineEndings ();
		string GetOriginalExpected (string testName) => GetExpectedResults (testName, CommonDirectoryOverride ?? "Common");

		// Get the test results from "JavaInterop1" or "XamarinAndroid" for tests with different results per Target
		protected string GetTargetedExpected (string testName) => GetOriginalTargetExpected (testName).NormalizeLineEndings ();
		string GetOriginalTargetExpected (string testName) => GetExpectedResults (testName, TargetedDirectoryOverride ?? Target.ToString ());

		string GetExpectedResults (string testName, string target)
		{
			var root = Path.GetDirectoryName (Assembly.GetExecutingAssembly ().Location);
			var path = Path.Combine (root, "Unit-Tests", "CodeGeneratorExpectedResults", target, $"{testName}.txt");
			Console.WriteLine ($"# jonp: test {GetType().Name}.{testName}: expected path: {path}");
			return File.ReadAllText (path);
		}

		protected List<GenBase> ParseApiDefinition (string xml)
		{
			var doc = XDocument.Parse (xml);
			var gens = XmlApiImporter.Parse (doc, options);

			foreach (var gen in gens)
				options.SymbolTable.AddType (gen);

			foreach (var gen in gens)
				gen.FixupAccessModifiers (options);

			foreach (var gen in gens)
				gen.Validate (options, new GenericParameterDefinitionList (), generator.Context);

			foreach (var gen in gens)
				gen.FillProperties ();

			foreach (var gen in gens)
				gen.FixupMethodOverrides (options);

			return gens;
		}

		protected void AssertTargetedExpected (string testName, string actual)
		{
			WriteActualContents (testName, actual);

			var expected    = GetTargetedExpected (testName);
			Assert.AreEqual (expected, actual.NormalizeLineEndings ());
		}

		protected void AssertOriginalExpected (string testName, string actual)
		{
			WriteActualContents (testName, actual);

			var expected    = GetOriginalExpected (testName);
			Assert.AreEqual (expected.NormalizeLineEndings (), actual.NormalizeLineEndings (),
					GetAssertionMessage ($"Test `{testName}` failed.", expected, actual));
		}

		protected void WriteActualContents (string testName, string contents)
		{
			var t = this.TargetedDirectoryOverride;
			if (string.IsNullOrEmpty (t))
				t = this.CommonDirectoryOverride;
			if (string.IsNullOrEmpty (t))
				t = GetType ().Name;
			var dir = Path.Combine ("__jonp", t);
			Directory.CreateDirectory (dir);
			File.WriteAllText (Path.Combine (dir, testName + ".txt"), contents);
		}

		protected void AssertOriginalTargetExpected (string testName, string actual)
		{
			WriteActualContents (testName, actual);

			var expected = GetOriginalTargetExpected (testName);
			Assert.AreEqual (expected.NormalizeLineEndings (), actual.NormalizeLineEndings (),
					GetAssertionMessage ($"Test `{testName}` failed.", expected, actual));
		}

		protected static string GetAssertionMessage (string header, string expected, string actual)
		{
			return header + "\n" +
				$"Expected:\n```\n{expected}\n```\n" +
				$"Actual:\n```\n{actual}\n```";
		}
	}
}
