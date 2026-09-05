#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests.Tasks {

	[TestFixture]
	public class GenerateJniRemappingNativeCodeTests : BaseTest {

		List<BuildErrorEventArgs>? errors;
		List<BuildWarningEventArgs>? warnings;
		MockBuildEngine? engine;
		string? directory;

		const string Abi = "arm64-v8a";

		[SetUp]
		public void Setup ()
		{
			errors = new List<BuildErrorEventArgs> ();
			warnings = new List<BuildWarningEventArgs> ();
			engine = new MockBuildEngine (TestContext.Out, errors, warnings);
			directory = Path.Combine (Root, "temp", TestName);
			if (Directory.Exists (directory)) {
				Directory.Delete (directory, recursive: true);
			}
			Directory.CreateDirectory (directory);
		}

		string TestDirectory {
			get {
				Assert.IsNotNull (directory);
				return directory!;
			}
		}

		List<BuildErrorEventArgs> Errors {
			get {
				Assert.IsNotNull (errors);
				return errors!;
			}
		}

		string RunTask (string remappingXml)
		{
			string xmlPath = Path.Combine (TestDirectory, "remap.xml");
			File.WriteAllText (xmlPath, remappingXml);

			var task = new GenerateJniRemappingNativeCode {
				BuildEngine = engine,
				OutputDirectory = TestDirectory,
				SupportedAbis = [Abi],
				RemappingXmlFilePath = new Microsoft.Build.Utilities.TaskItem (xmlPath),
			};

			Assert.IsTrue (task.Execute (), $"Task should have succeeded. Errors: {string.Join ("; ", Errors.Select (e => e.Message))}");
			LastNativeCodeInfo = task.NativeCodeInfo;

			return File.ReadAllText (Path.Combine (TestDirectory, $"jni_remap.{Abi}.ll"));
		}

		GenerateJniRemappingNativeCode.JniRemappingNativeCodeInfo? LastNativeCodeInfo { get; set; }

		GenerateJniRemappingNativeCode.JniRemappingNativeCodeInfo Info {
			get {
				Assert.IsNotNull (LastNativeCodeInfo);
				return LastNativeCodeInfo!;
			}
		}

		[Test]
		public void EmptyCodeEmitsAllTablesAndZeroCounts ()
		{
			var task = new GenerateJniRemappingNativeCode {
				BuildEngine = engine,
				OutputDirectory = TestDirectory,
				SupportedAbis = [Abi],
				GenerateEmptyCode = true,
			};

			Assert.IsTrue (task.Execute (), "Task should have succeeded.");

			string ll = File.ReadAllText (Path.Combine (TestDirectory, $"jni_remap.{Abi}.ll"));
			foreach (string symbol in new [] {
					"jni_remapping_type_replacements",
					"jni_remapping_reverse_type_replacements",
					"jni_remapping_method_replacement_index",
					"jni_remapping_field_replacement_index",
				}) {
				StringAssert.Contains ($"@{symbol}", ll, $"`{symbol}` must always be emitted.");
			}

			foreach (string counter in new [] {
					"jni_remapping_type_replacement_count",
					"jni_remapping_reverse_type_replacement_count",
					"jni_remapping_method_replacement_index_count",
					"jni_remapping_field_replacement_index_count",
				}) {
				StringAssert.Contains ($"@{counter} = dso_local local_unnamed_addr constant i32 0", ll, $"`{counter}` must be zero.");
			}

			var info = task.NativeCodeInfo;
			Assert.IsNotNull (info);
			Assert.AreEqual (0, info!.ReplacementTypeCount);
			Assert.AreEqual (0, info.ReverseTypeCount);
			Assert.AreEqual (0, info.ReplacementMethodIndexEntryCount);
			Assert.AreEqual (0, info.ReplacementFieldIndexEntryCount);
		}

		[Test]
		public void CountsMatchGeneratedTables ()
		{
			RunTask (
				"""
				<replacements>
				  <replace-type from="a/B" to="x/Y" />
				  <replace-type from="c/D" to="x/Z" />
				  <reverse-type from="x/Y" to="a/B" />
				  <replace-method source-type="a/B" source-method-name="m" source-method-signature="()V"
				      target-type="x/Y" target-method-name="a" target-method-signature="()V"
				      target-method-instance-to-static="false" />
				  <replace-method source-type="c/D" source-method-name="m" source-method-signature="()V"
				      target-type="x/Z" target-method-name="a" target-method-signature="()V"
				      target-method-instance-to-static="false" />
				  <replace-field source-type="a/B" source-field-name="f" source-field-signature="I"
				      target-type="x/Y" target-field-name="a" target-field-signature="I" />
				</replacements>
				""");

			Assert.AreEqual (2, Info.ReplacementTypeCount, "replace-type count");
			Assert.AreEqual (1, Info.ReverseTypeCount, "reverse-type count");
			Assert.AreEqual (2, Info.ReplacementMethodIndexEntryCount, "replace-method type count");
			Assert.AreEqual (1, Info.ReplacementFieldIndexEntryCount, "replace-field type count");
		}

		[Test]
		public void ReverseTypesAreEmittedSeparatelyFromForwardTypes ()
		{
			string ll = RunTask (
				"""
				<replacements>
				  <replace-type from="a/B" to="x/Y" />
				  <reverse-type from="x/Y" to="a/B" />
				</replacements>
				""");

			int forward = ll.IndexOf ("@jni_remapping_type_replacements");
			int reverse = ll.IndexOf ("@jni_remapping_reverse_type_replacements");
			Assert.Greater (forward, -1, "Forward table must be emitted.");
			Assert.Greater (reverse, -1, "Reverse table must be emitted.");
			Assert.AreEqual (1, Info.ReplacementTypeCount);
			Assert.AreEqual (1, Info.ReverseTypeCount);
		}

		[Test]
		public void MissingTargetMethodSignatureIsBackwardCompatible ()
		{
			// The Intune/MAM mapping shape: no `target-method-signature`, wildcard source signature.
			string ll = RunTask (
				"""
				<replacements>
				  <replace-type from="android/app/Activity" to="com/microsoft/intune/MAMActivity" />
				  <replace-method source-type="a/B" source-method-name="m"
				      target-type="x/Y" target-method-name="a"
				      target-method-instance-to-static="true" />
				</replacements>
				""");

			Assert.AreEqual (1, Info.ReplacementTypeCount);
			Assert.AreEqual (0, Info.ReverseTypeCount, "No reverse entries in a legacy document.");
			Assert.AreEqual (1, Info.ReplacementMethodIndexEntryCount);
			Assert.AreEqual (0, Info.ReplacementFieldIndexEntryCount);
			StringAssert.Contains ("com/microsoft/intune/MAMActivity", ll);
			// The wildcard signature is emitted as a zero-length string, and the absent target
			// signature as a null pointer.
			StringAssert.Contains ("ptr null", ll, "An absent target-method-signature must be a null pointer.");
		}

		[Test]
		public void TypeTablesAreSortedForBinarySearch ()
		{
			string ll = RunTask (
				"""
				<replacements>
				  <replace-type from="zz/Last" to="a" />
				  <replace-type from="aa/First" to="b" />
				  <replace-type from="mm/Middle" to="c" />
				  <reverse-type from="c" to="mm/Middle" />
				  <reverse-type from="a" to="zz/Last" />
				  <reverse-type from="b" to="aa/First" />
				</replacements>
				""");

			AssertOrdered (ll, "aa/First", "mm/Middle", "zz/Last");
			Assert.AreEqual (3, Info.ReplacementTypeCount);
			Assert.AreEqual (3, Info.ReverseTypeCount);
		}

		[Test]
		public void MethodsAndFieldsAreSortedByNameThenSignature ()
		{
			string ll = RunTask (
				"""
				<replacements>
				  <replace-method source-type="a/B" source-method-name="zeta" source-method-signature="()V"
				      target-type="x/Y" target-method-name="c" target-method-signature="()V"
				      target-method-instance-to-static="false" />
				  <replace-method source-type="a/B" source-method-name="alpha" source-method-signature="(J)V"
				      target-type="x/Y" target-method-name="b" target-method-signature="(J)V"
				      target-method-instance-to-static="false" />
				  <replace-method source-type="a/B" source-method-name="alpha" source-method-signature="(I)V"
				      target-type="x/Y" target-method-name="a" target-method-signature="(I)V"
				      target-method-instance-to-static="false" />
				  <replace-field source-type="a/B" source-field-name="zf" source-field-signature="I"
				      target-type="x/Y" target-field-name="zt" target-field-signature="I" />
				  <replace-field source-type="a/B" source-field-name="af" source-field-signature="I"
				      target-type="x/Y" target-field-name="at" target-field-signature="I" />
				</replacements>
				""");

			// Overloads keep a stable (name, signature) order so the runtime can binary-search the
			// name and scan the equal-name run.
			AssertOrdered (ll, "c\"alpha", "c\"(I)V", "c\"(J)V", "c\"zeta");
			AssertOrdered (ll, "c\"af", "c\"zf");
			Assert.AreEqual (1, Info.ReplacementMethodIndexEntryCount);
			Assert.AreEqual (1, Info.ReplacementFieldIndexEntryCount);
		}

		[Test]
		public void Utf8OrderingMatchesNativeMemcmp ()
		{
			// '_' (0x5F) sorts after 'Z' (0x5A) but before 'a' (0x61); a culture-sensitive
			// comparison would order these differently, and the native binary search would break.
			Assert.Less (JniRemappingAssemblyGenerator.CompareUtf8 (Utf8 ("Z"), Utf8 ("_")), 0);
			Assert.Less (JniRemappingAssemblyGenerator.CompareUtf8 (Utf8 ("_"), Utf8 ("a")), 0);
			Assert.Less (JniRemappingAssemblyGenerator.CompareUtf8 (Utf8 ("a"), Utf8 ("ab")), 0);
			Assert.AreEqual (0, JniRemappingAssemblyGenerator.CompareUtf8 (Utf8 ("a/B"), Utf8 ("a/B")));

			static byte [] Utf8 (string s) => System.Text.Encoding.UTF8.GetBytes (s);
		}

		static void AssertOrdered (string haystack, params string [] needles)
		{
			int previous = -1;
			string previousNeedle = "";
			foreach (string needle in needles) {
				int index = haystack.IndexOf (needle, previous + 1, System.StringComparison.Ordinal);
				Assert.Greater (index, previous, $"`{needle}` must appear after `{previousNeedle}`.");
				previous = index;
				previousNeedle = needle;
			}
		}
	}
}
