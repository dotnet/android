#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

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
				return directory ?? throw new AssertionException ("The test directory must be initialized.");
			}
		}

		List<BuildErrorEventArgs> Errors {
			get {
				return errors ?? throw new AssertionException ("The build error collection must be initialized.");
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
				return LastNativeCodeInfo ?? throw new AssertionException ("The task must provide native code information.");
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
					"jni_remapping_data",
				}) {
				StringAssert.Contains ($"@{symbol}", ll, $"`{symbol}` must always be emitted.");
			}

			var info = task.NativeCodeInfo ?? throw new AssertionException ("The task must provide native code information.");
			Assert.AreEqual (0, info.ReplacementTypeCount);
			Assert.AreEqual (0, info.ReverseTypeCount);
			Assert.AreEqual (0, info.ReplacementMethodIndexEntryCount);
			Assert.AreEqual (0, info.ReplacementFieldIndexEntryCount);
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
				</replacements>
				""");

			AssertOrdered (ll, "aa/First", "mm/Middle", "zz/Last");
			Assert.AreEqual (3, Info.ReplacementTypeCount);
			Assert.AreEqual (2, Info.ReverseTypeCount);
		}

		[Test]
		public void MethodsAndFieldsUseStableLookupOrder ()
		{
			string ll = RunTask (
				"""
				<replacements>
				  <replace-method source-type="a/B" source-method-name="alpha"
				      target-type="x/Y" target-method-name="wildcard"
				      target-method-instance-to-static="false" />
				  <replace-method source-type="a/B" source-method-name="alpha" source-method-signature="(I)"
				      target-type="x/Y" target-method-name="parameters" target-method-signature="(I)V"
				      target-method-instance-to-static="false" />
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

			// Exact descriptors precede parameter-only descriptors and wildcards so MonoVM's
			// single scan cannot let a general remap shadow a specific one.
			int methodsStart = ll.IndexOf ("@mm_0 =", System.StringComparison.Ordinal);
			int methodsEnd = ll.IndexOf ("@jni_remapping_method_replacement_index", methodsStart, System.StringComparison.Ordinal);
			Assert.Greater (methodsStart, -1);
			Assert.Greater (methodsEnd, methodsStart);
			string methodArray = ll.Substring (methodsStart, methodsEnd - methodsStart);
			AssertOrdered (
				methodArray,
				"ptr @.JniRemappingString.1_str",
				"ptr @.JniRemappingString.2_str",
				"ptr @.JniRemappingString.3_str",
				"ptr null",
				"ptr @.JniRemappingString.4_str");
			AssertOrdered (ll, "c\"af", "c\"zf");
			Assert.AreEqual (1, Info.ReplacementMethodIndexEntryCount);
			Assert.AreEqual (1, Info.ReplacementFieldIndexEntryCount);
		}

		[Test]
		public void MemberArraySymbolsAreCollisionProofAndValidLlvm ()
		{
			string ll = RunTask (
				"""
				<replacements>
				  <replace-method source-type="a/b_c" source-method-name="m" source-method-signature="()V"
				      target-type="x/Y" target-method-name="a" target-method-signature="()V"
				      target-method-instance-to-static="false" />
				  <replace-method source-type="a_b/c" source-method-name="m" source-method-signature="()V"
				      target-type="x/Y" target-method-name="b" target-method-signature="()V"
				      target-method-instance-to-static="false" />
				  <replace-field source-type="型/名前" source-field-name="f" source-field-signature="I"
				      target-type="x/Y" target-field-name="g" target-field-signature="I" />
				</replacements>
				""");

			StringAssert.Contains ("@mm_0", ll);
			StringAssert.Contains ("@mm_1", ll);
			StringAssert.Contains ("@mf_0", ll);

			string binUtils = Path.Combine (TestEnvironment.OSBinDirectory, "binutils", "bin");
			var compile = new CompileNativeAssembly {
				BuildEngine = engine,
				Sources = [new Microsoft.Build.Utilities.TaskItem (Path.Combine (TestDirectory, $"jni_remap.{Abi}.ll"))],
				DebugBuild = false,
				WorkingDirectory = TestDirectory,
				AndroidBinUtilsDirectory = binUtils,
			};
			Assert.IsTrue (compile.Execute (), $"Generated LLVM IR should compile. Errors: {string.Join ("; ", Errors.Select (e => e.Message))}");
			FileAssert.Exists (Path.Combine (TestDirectory, $"jni_remap.{Abi}.o"));
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
