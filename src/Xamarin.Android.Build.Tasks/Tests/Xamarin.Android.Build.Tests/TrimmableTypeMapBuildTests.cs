using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.Android.AssemblyStore;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests {
	[TestFixture]
	[Category ("Node-2")]
	public class TrimmableTypeMapBuildTests : BaseTest {

		[Test]
		public void Build_WithTrimmableTypeMap_Succeeds ([Values] bool isRelease, [Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var intermediateDir = builder.Output.GetIntermediaryPath ("typemap");
			AssertTrimmableTypeMapOutputs (intermediateDir);
		}

		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "parameters", "XA4205")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "parameters", "XA4205")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "parameters", "XA4205")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "void", "XA4208")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "void", "XA4208")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "void", "XA4208")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "generic", "XA4207")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "generic", "XA4207")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "generic", "XA4207")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "parameters-and-void", "XA4205")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "parameters-and-void", "XA4205")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "parameters-and-void", "XA4205")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "generic-parameters-and-void", "XA4207")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "generic-parameters-and-void", "XA4207")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "generic-parameters-and-void", "XA4207")]
		public void Build_InvalidExportField_ReportsLegacyDiagnostic (
			string typeMapImplementation,
			AndroidRuntime runtime,
			string invalidShape,
			string expectedCode)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var initializer = invalidShape switch {
				"parameters" => "public int InitialValue (int value) => value;",
				"void" => "public void InitialValue () { }",
				"generic" => "public int InitialValue () => 42;",
				"parameters-and-void" => "public void InitialValue (int value) { }",
				"generic-parameters-and-void" => "public void InitialValue (int value) { }",
				_ => throw new InvalidOperationException ($"Unknown invalid [ExportField] shape '{invalidShape}'."),
			};
			var proj = CreateExportFieldValidationProject (runtime, typeMapImplementation, $"""
						[ExportField ("VALUE")]
						{initializer}
				""", genericType: invalidShape.StartsWith ("generic", StringComparison.Ordinal));

			using var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), $"{runtime}/{typeMapImplementation} should reject {invalidShape} [ExportField] initializers.");
			StringAssertEx.Contains ($"error {expectedCode}", builder.LastBuildOutput, $"The build should report {expectedCode}.");
			if (invalidShape == "parameters-and-void") {
				Assert.IsFalse (
					builder.LastBuildOutput.Any (line => line.Contains ("error XA4208", StringComparison.Ordinal)),
					"XA4205 should take precedence over XA4208, matching LLVM-IR."
				);
			} else if (invalidShape == "generic-parameters-and-void") {
				Assert.IsFalse (
					builder.LastBuildOutput.Any (line =>
						line.Contains ("error XA4205", StringComparison.Ordinal) ||
						line.Contains ("error XA4208", StringComparison.Ordinal)),
					"XA4207 should take precedence over initializer signature diagnostics, matching LLVM-IR."
				);
			}
		}

		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "arbitrary-parameter", "XALNS7003")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "arbitrary-parameter", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "arbitrary-parameter", "XA4263")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "arbitrary-return", "XALNS7003")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "arbitrary-return", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "arbitrary-return", "XA4263")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "arbitrary-field", "XALNS7003")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "arbitrary-field", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "arbitrary-field", "XA4263")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "generic-parameter", "XALNS7003")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "generic-parameter", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "generic-parameter", "XA4263")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "generic-instantiation", "XALNS7003")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "generic-instantiation", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "generic-instantiation", "XA4263")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "function-pointer", "XALNS7003")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "function-pointer", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "function-pointer", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "by-ref-parameter", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "by-ref-parameter", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "pointer-parameter", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "pointer-parameter", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "rectangular-array-parameter", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "rectangular-array-parameter", "XA4263")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "generic-declaring-type", "XA4206")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "generic-declaring-type", "XA4206")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "generic-declaring-type", "XA4206")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "mismatched-export-parameter", "XALNS7004")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "mismatched-export-parameter", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "mismatched-export-parameter", "XA4263")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "generic-export-parameter", "XALNS7003")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "generic-export-parameter", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "generic-export-parameter", "XA4263")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "function-pointer-export-parameter", "XALNS7003")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "function-pointer-export-parameter", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "function-pointer-export-parameter", "XA4263")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "mismatched-field-export-parameter", "XALNS7004")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "mismatched-field-export-parameter", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "mismatched-field-export-parameter", "XA4263")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "special-array-parameter", "success")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "special-array-parameter", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "special-array-parameter", "XA4263")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "special-array-return", "success")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "special-array-return", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "special-array-return", "XA4263")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "special-array-field", "success")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "special-array-field", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "special-array-field", "XA4263")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "special-xml-array-return", "success")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "special-xml-array-return", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "special-xml-array-return", "XA4263")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "export-static-constructor", "success")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "export-static-constructor", "success")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "export-static-constructor", "success")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "export-constructor-arbitrary", "XALNS7003")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "export-constructor-arbitrary", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "export-constructor-arbitrary", "XA4263")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "export-constructor-invalid-kind", "XALNS7004")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "export-constructor-invalid-kind", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "export-constructor-invalid-kind", "XA4263")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "export-constructor-valid-kind", "success")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "export-constructor-valid-kind", "success")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "export-constructor-valid-kind", "success")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "export-named-constructor-arbitrary", "XALNS7003")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "export-named-constructor-arbitrary", "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "export-named-constructor-arbitrary", "XA4263")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "export-named-constructor-valid-kind", "success")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "export-named-constructor-valid-kind", "success")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "export-named-constructor-valid-kind", "success")]
		public void Build_ExportSignature_MatchesRuntimeClassification (
			string typeMapImplementation,
			AndroidRuntime runtime,
			string invalidShape,
			string expectedCode)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var (additionalTypes, member, marker, typeParameters) = invalidShape switch {
				"arbitrary-parameter" => (
					"public sealed class ManagedOnly { }",
					"""[Export ("unsupported")] public void UnsupportedMember (ManagedOnly value) { }""",
					"unsupported",
					""),
				"arbitrary-return" => (
					"public sealed class ManagedOnly { }",
					"""[Export ("unsupported")] public ManagedOnly UnsupportedMember () => new ();""",
					"unsupported",
					""),
				"arbitrary-field" => (
					"public sealed class ManagedOnly { }",
					"""[ExportField ("UNSUPPORTED_FIELD")] public ManagedOnly UnsupportedMember () => new ();""",
					"UNSUPPORTED_FIELD",
					""),
				"generic-parameter" => (
					"",
					"""[Export ("unsupported")] public T UnsupportedMember<T> (T value) => value;""",
					"unsupported",
					""),
				"generic-instantiation" => (
					"",
					"""[Export ("unsupported")] public List<string> UnsupportedMember (List<string> value) => value;""",
					"unsupported",
					""),
				"function-pointer" => (
					"",
					"""[Export ("unsupported")] public unsafe delegate* unmanaged<void> UnsupportedMember (delegate* unmanaged<void> value) => value;""",
					"unsupported",
					""),
				"by-ref-parameter" => (
					"",
					"""[Export ("unsupported")] public void UnsupportedMember (ref int value) { }""",
					"unsupported",
					""),
				"pointer-parameter" => (
					"",
					"""[Export ("unsupported")] public unsafe void UnsupportedMember (int* value) { }""",
					"unsupported",
					""),
				"rectangular-array-parameter" => (
					"",
					"""[Export ("unsupported")] public void UnsupportedMember (string [,] value) { }""",
					"unsupported",
					""),
				"generic-declaring-type" => (
					"",
					"""[Export ("unsupported")] public int UnsupportedMember () => 0;""",
					"unsupported",
					"<T>"),
				"mismatched-export-parameter" => (
					"public sealed class ManagedOnly { }",
					"""
					[Export ("unsupported")]
					public ManagedOnly UnsupportedMember (
						[ExportParameter (ExportParameterKind.InputStream)] ManagedOnly value)
						=> value;
					""",
					"unsupported",
					""),
				"generic-export-parameter" => (
					"",
					"""
					[Export ("unsupported")]
					public T UnsupportedMember<T> (
						[ExportParameter (ExportParameterKind.InputStream)] T value)
						=> value;
					""",
					"unsupported",
					""),
				"function-pointer-export-parameter" => (
					"",
					"""
					[Export ("unsupported")]
					public unsafe delegate* unmanaged<void> UnsupportedMember (
						[ExportParameter (ExportParameterKind.InputStream)] delegate* unmanaged<void> value)
						=> value;
					""",
					"unsupported",
					""),
				"mismatched-field-export-parameter" => (
					"public sealed class ManagedOnly { }",
					"""
					[return: ExportParameter (ExportParameterKind.OutputStream)]
					[ExportField ("UNSUPPORTED_FIELD")]
					public ManagedOnly UnsupportedMember () => new ();
					""",
					"UNSUPPORTED_FIELD",
					""),
				"special-array-parameter" => (
					"",
					"""
					[Export ("unsupported")]
					public void UnsupportedMember (
						[ExportParameter (ExportParameterKind.InputStream)] Stream [] value)
					{
					}
					""",
					"unsupported",
					""),
				"special-array-return" => (
					"",
					"""
					[return: ExportParameter (ExportParameterKind.OutputStream)]
					[Export ("unsupported")]
					public Stream [] UnsupportedMember () => [];
					""",
					"unsupported",
					""),
				"special-array-field" => (
					"",
					"""
					[return: ExportParameter (ExportParameterKind.OutputStream)]
					[ExportField ("UNSUPPORTED_FIELD")]
					public Stream [] UnsupportedMember () => [];
					""",
					"UNSUPPORTED_FIELD",
					""),
				"special-xml-array-return" => (
					"",
					"""
					[return: ExportParameter (ExportParameterKind.XmlPullParser)]
					[Export ("unsupported")]
					public XmlReader [] UnsupportedMember () => [];
					""",
					"unsupported",
					""),
				"export-static-constructor" => (
					"",
					"""
					[Export]
					static SignaturePeer ()
					{
					}
					""",
					".cctor",
					""),
				"export-constructor-arbitrary" => (
					"public sealed class ManagedOnly { }",
					"""
					[Export (".ctor", SuperArgumentsString = "")]
					public SignaturePeer (ManagedOnly value)
					{
					}
					""",
					"SignaturePeer",
					""),
				"export-constructor-invalid-kind" => (
					"public sealed class ManagedOnly { }",
					"""
					[Export (".ctor", SuperArgumentsString = "")]
					public SignaturePeer (
						[ExportParameter (ExportParameterKind.InputStream)] ManagedOnly value)
					{
					}
					""",
					"SignaturePeer",
					""),
				"export-constructor-valid-kind" => (
					"",
					"""
					[Export (".ctor", SuperArgumentsString = "")]
					public SignaturePeer (
						[ExportParameter (ExportParameterKind.InputStream)] Stream value)
					{
					}
					""",
					"java.io.InputStream",
					""),
				"export-named-constructor-arbitrary" => (
					"public sealed class ManagedOnly { }",
					"""
					[Export ("notAConstructor", SuperArgumentsString = "")]
					public SignaturePeer (ManagedOnly value)
					{
					}
					""",
					"SignaturePeer",
					""),
				"export-named-constructor-valid-kind" => (
					"",
					"""
					[Export ("notAConstructor", SuperArgumentsString = "")]
					public SignaturePeer (
						[ExportParameter (ExportParameterKind.InputStream)] Stream value)
					{
					}
					""",
					"java.io.InputStream",
					""),
				_ => throw new InvalidOperationException ($"Unknown unsupported [Export] shape '{invalidShape}'."),
			};
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
				},
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidTypeMapImplementation", typeMapImplementation);
			proj.SetProperty ("AllowUnsafeBlocks", "true");
			proj.Sources.Add (new BuildItem.Source ("ExportSignatureValidation.cs") {
				TextContent = () => $$"""
					using System.Collections.Generic;
					using System.IO;
					using System.Xml;
					using Android.Runtime;
					using Java.Interop;

					namespace ExportSignatureValidation {
						{{additionalTypes}}

						[Register ("com/example/exports/SignaturePeer")]
						public class SignaturePeer{{typeParameters}} : Java.Lang.Object {
							public SignaturePeer () {
							}

							{{member}}
						}
					}
					""",
			});

			using var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			var succeeded = builder.Build (proj);
			if (expectedCode == "success") {
				Assert.IsTrue (succeeded, $"{runtime}/{typeMapImplementation} should retain legacy build support for {invalidShape}.");
				return;
			}

			Assert.IsFalse (succeeded, $"{runtime}/{typeMapImplementation} should reject {invalidShape}.");
			StringAssertEx.Contains ($"error {expectedCode}", builder.LastBuildOutput, $"The build should report {expectedCode}.");
			if (expectedCode == "XA4263") {
				var expectedMemberName = invalidShape.Contains ("constructor", StringComparison.Ordinal)
					? "ExportSignatureValidation.SignaturePeer.ctor"
					: "ExportSignatureValidation.SignaturePeer.UnsupportedMember";
				StringAssertEx.Contains (
					expectedMemberName,
					builder.LastBuildOutput,
					"The diagnostic should identify the unsupported managed member."
				);
			}
			AssertNoExportOutputs (builder, marker);
		}

		[TestCase (AndroidRuntime.CoreCLR)]
		[TestCase (AndroidRuntime.NativeAOT)]
		public void Build_SpecialMappingLookalikeTypes_ReportXA4263WithoutPartialOutputs (AndroidRuntime runtime)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
				},
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");
			proj.Sources.Add (new BuildItem.Source ("SpecialMappingLookalikes.cs") {
				TextContent = () => """
					using Android.Runtime;
					using Java.Interop;

					namespace System.IO {
						public class Stream {
						}
					}

					namespace System.Xml {
						public class XmlReader {
						}
					}

					namespace Java.Lang {
						public interface ICharSequence {
						}
					}

					namespace System.Collections {
						public interface IList {
						}

						public interface IDictionary {
						}

						public interface ICollection {
						}
					}

					namespace SpecialMappingLookalikes {
						[Register ("com/example/exports/SpecialMappingLookalikePeer")]
						public class SpecialMappingLookalikePeer : Java.Lang.Object {
							[return: ExportParameter (ExportParameterKind.OutputStream)]
							[Export ("invalidStream")]
							public System.IO.Stream InvalidStream (
								[ExportParameter (ExportParameterKind.InputStream)] System.IO.Stream value)
								=> value;

							[return: ExportParameter (ExportParameterKind.XmlPullParser)]
							[ExportField ("INVALID_XML_FIELD")]
							public System.Xml.XmlReader InvalidXmlField () => new ();

							[Export ("invalidCharSequence")]
							public Java.Lang.ICharSequence InvalidCharSequence (Java.Lang.ICharSequence value) => value;

							[Export ("invalidList")]
							public System.Collections.IList InvalidList (System.Collections.IList value) => value;

							[Export ("invalidDictionary")]
							public System.Collections.IDictionary InvalidDictionary (System.Collections.IDictionary value) => value;

							[Export ("invalidCollection")]
							public System.Collections.ICollection InvalidCollection (System.Collections.ICollection value) => value;

							[Export ("notAConstructor", SuperArgumentsString = "")]
							public SpecialMappingLookalikePeer (
								[ExportParameter (ExportParameterKind.InputStream)] System.IO.Stream value)
							{
							}
						}
					}
					""",
			});

			using var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), $"{runtime}/trimmable should reject special-mapping lookalike types.");
			foreach (var memberName in new [] {
				"SpecialMappingLookalikes.SpecialMappingLookalikePeer.InvalidStream",
				"SpecialMappingLookalikes.SpecialMappingLookalikePeer.InvalidXmlField",
				"SpecialMappingLookalikes.SpecialMappingLookalikePeer.InvalidCharSequence",
				"SpecialMappingLookalikes.SpecialMappingLookalikePeer.InvalidList",
				"SpecialMappingLookalikes.SpecialMappingLookalikePeer.InvalidDictionary",
				"SpecialMappingLookalikes.SpecialMappingLookalikePeer.InvalidCollection",
				"SpecialMappingLookalikes.SpecialMappingLookalikePeer.ctor",
			}) {
				Assert.IsTrue (
					builder.LastBuildOutput.Any (line =>
						line.Contains ("error XA4263", StringComparison.Ordinal) &&
						line.Contains (memberName, StringComparison.Ordinal)),
					$"The build should report XA4263 for '{memberName}'."
				);
			}
			AssertNoExportOutputs (builder, "invalidStream");
		}

		static XamarinAndroidApplicationProject CreateExportFieldValidationProject (
			AndroidRuntime runtime,
			string typeMapImplementation,
			string members,
			bool genericType = false)
		{
			var typeParameters = genericType ? "<T>" : "";
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = runtime == AndroidRuntime.NativeAOT,
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
				},
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidTypeMapImplementation", typeMapImplementation);
			proj.Sources.Add (new BuildItem.Source ("ExportFieldValidation.cs") {
				TextContent = () => $$"""
					using Android.Runtime;
					using Java.Interop;

					namespace ExportFieldValidation {
						[Register ("com/example/exportfields/ValidationPeer")]
						class ValidationPeer{{typeParameters}} : Java.Lang.Object {
							public ValidationPeer () {
							}

					{{members}}
						}
					}
					""",
			});
			return proj;
		}

		[Test]
		public void FindExportOutputs_FindsEveryArtifactKind ()
		{
			var root = Path.Combine (Root, "temp", TestName);
			var typemapDirectory = Path.Combine (root, "typemap");
			var acwMapFile = Path.Combine (root, "acw-map.txt");
			var trimmableJavaDirectory = Path.Combine (typemapDirectory, "java", "com", "example");
			var llvmIrJavaDirectory = Path.Combine (root, "android", "src", "com", "example");
			Directory.CreateDirectory (trimmableJavaDirectory);
			Directory.CreateDirectory (llvmIrJavaDirectory);

			var expected = new [] {
				Path.Combine (typemapDirectory, "_Example.TypeMap.dll"),
				Path.Combine (typemapDirectory, "_Microsoft.Android.TypeMaps.dll"),
				acwMapFile,
				Path.Combine (trimmableJavaDirectory, "TrimmablePeer.java"),
				Path.Combine (llvmIrJavaDirectory, "LlvmIrPeer.java"),
			};
			File.WriteAllBytes (expected [0], []);
			File.WriteAllBytes (expected [1], []);
			File.WriteAllText (expected [2], "Managed, Assembly;com/example/Peer");
			File.WriteAllText (expected [3], "public int VALUE = InitialValue ();");
			File.WriteAllText (expected [4], "public int VALUE = InitialValue ();");

			CollectionAssert.AreEquivalent (
				expected,
				FindExportOutputs (typemapDirectory, acwMapFile, Path.Combine (root, "android", "src"), "VALUE")
			);
		}

		static void AssertNoExportOutputs (ProjectBuilder builder, string memberName)
		{
			var typemapDirectory = builder.Output.GetIntermediaryPath ("typemap");
			var acwMapFile = builder.Output.GetIntermediaryPath ("acw-map.txt");
			var androidSourceDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android", "src"));
			var outputs = FindExportOutputs (typemapDirectory, acwMapFile, androidSourceDirectory, memberName);
			Assert.IsEmpty (
				outputs,
				"Invalid exported metadata should not produce typemap assemblies, ACW maps, or partial Java output:" +
				Environment.NewLine + string.Join (Environment.NewLine, outputs)
			);
		}

		static string [] FindExportOutputs (string typemapDirectory, string acwMapFile, string androidSourceDirectory, string memberName)
		{
			var outputs = new List<string> ();
			if (Directory.Exists (typemapDirectory)) {
				outputs.AddRange (Directory.GetFiles (typemapDirectory, "*.TypeMap.dll", SearchOption.AllDirectories));
				outputs.AddRange (Directory.GetFiles (typemapDirectory, "_Microsoft.Android.TypeMaps.dll", SearchOption.AllDirectories));
			}
			if (File.Exists (acwMapFile)) {
				outputs.Add (acwMapFile);
			}

			foreach (var javaDirectory in new [] { Path.Combine (typemapDirectory, "java"), androidSourceDirectory }) {
				if (!Directory.Exists (javaDirectory)) {
					continue;
				}
				foreach (var javaFile in Directory.GetFiles (javaDirectory, "*.java", SearchOption.AllDirectories)) {
					if (File.ReadAllText (javaFile).Contains (memberName, StringComparison.Ordinal)) {
						outputs.Add (javaFile);
					}
				}
			}
			return outputs.ToArray ();
		}

		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "JAVAC0000", "JAVAC0000")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "XA4262", "XA4258")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "XA4262", "XA4258")]
		public void Build_IndependentConstructorAndJavaNameDiagnostics_AreBothReported (
			string typeMapImplementation,
			AndroidRuntime runtime,
			string constructorCode,
			string javaNameCode)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
				},
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidTypeMapImplementation", typeMapImplementation);
			proj.Sources.Add (new BuildItem.Source ("IndependentDiagnostics.cs") {
				TextContent = () => """
					using Android.Runtime;
					using Java.Interop;

					namespace UnnamedProject;

					[Register ("my/app/InvalidConstructor")]
					public class InvalidConstructor : Java.Lang.Object
					{
						[Export (".ctor", SuperArgumentsString = "p1 +")]
						public InvalidConstructor (string value) { }
					}

					[Register ("my/app/for")]
					public class ReservedName : Java.Lang.Object { }
					""",
			});

			using var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), $"{runtime}/{typeMapImplementation} should report both independent errors.");
			StringAssertEx.Contains ($"error {constructorCode}", builder.LastBuildOutput);
			StringAssertEx.Contains ($"error {javaNameCode}", builder.LastBuildOutput);
			if (typeMapImplementation == "trimmable") {
				AssertNoExportOutputs (builder, "InvalidConstructor");
			} else {
				StringAssertEx.Contains ("InvalidConstructor.java", builder.LastBuildOutput);
				StringAssertEx.Contains ("for.java", builder.LastBuildOutput);
			}
		}

		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "XALNS7003")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "XALNS7003")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "success")]
		public void Build_ExplicitExportConstructorAttributeOrders_MatchLegacyPipeline (
			string typeMapImplementation,
			AndroidRuntime runtime,
			string expectedCode)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
				},
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidTypeMapImplementation", typeMapImplementation);
			proj.Sources.Add (new BuildItem.Source ("ExplicitExportConstructors.cs") {
				TextContent = () => """
					using Android.App;
					using Android.Runtime;
					using Java.Interop;

					namespace UnnamedProject;

					[Register ("my/app/RegisterFirst")]
					public class RegisterFirst : Activity {
						[Register (".ctor", "(I)V", "")]
						[Export (".ctor", SuperArgumentsString = "")]
						public RegisterFirst (uint value) { }
					}

					[Register ("my/app/ExportFirst")]
					public class ExportFirst : Activity {
						[Export (".ctor", SuperArgumentsString = "")]
						[JniConstructorSignature ("(I)V")]
						public ExportFirst (uint value) { }
					}
					""",
			});

			using var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			var succeeded = builder.Build (proj);
			if (expectedCode == "success") {
				Assert.IsTrue (succeeded, $"{runtime}/{typeMapImplementation} should preserve explicit constructor metadata.");
			} else {
				Assert.IsFalse (succeeded, $"{runtime}/{typeMapImplementation} should retain measured legacy validation.");
				StringAssertEx.Contains ($"error {expectedCode}", builder.LastBuildOutput);
			}
		}

		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR)]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR)]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT)]
		public void Build_ImplicitConstructorUsesCompatibleBaseJniSignature (string typeMapImplementation, AndroidRuntime runtime)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject { IsRelease = isRelease };
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidTypeMapImplementation", typeMapImplementation);
			proj.Sources.Add (new BuildItem.Source ("CompatibleBaseConstructor.cs") {
				TextContent = () => """
					using Android.Runtime;

					namespace UnnamedProject;

					[Register ("my/app/IntBase", DoNotGenerateAcw = true)]
					public class IntBase : Java.Lang.Object {
						[Register (".ctor", "(I)V", "")]
						public IntBase (int value) { }
					}

					public class UIntDerived : IntBase {
						public UIntDerived (uint value) : base ((int)value) { }
					}
					""",
			});
			proj.AndroidJavaSources.Add (new AndroidItem.AndroidJavaSource ("my\\app\\IntBase.java") {
				Encoding = Encoding.ASCII,
				TextContent = () => """
					package my.app;

					public class IntBase {
						public IntBase (int value) {}
					}
					""",
			});

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), $"{runtime}/{typeMapImplementation} should match legacy JNI base compatibility.");
		}

		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "lambda", "success")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "lambda", "success")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "lambda", "success")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "parenthesized-lambda", "success")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "parenthesized-lambda", "success")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "parenthesized-lambda", "success")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "typed-lambda", "success")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "typed-lambda", "success")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "typed-lambda", "success")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "literal-comma", "success")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "literal-comma", "success")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "literal-comma", "success")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "method-reference", "success")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "method-reference", "success")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "method-reference", "success")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "generic-method-reference", "success")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "generic-method-reference", "success")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "generic-method-reference", "success")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "generic-construction", "success")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "generic-construction", "success")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "generic-construction", "success")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "nested-generic", "success")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "nested-generic", "success")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "nested-generic", "success")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "instanceof-generic", "success")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "instanceof-generic", "success")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "instanceof-generic", "success")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "comparison", "success")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "comparison", "success")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "comparison", "success")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "shift", "success")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "shift", "success")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "shift", "success")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "ordinary-bare", "JAVAC0000")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "ordinary-bare", "XA4262")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "ordinary-bare", "XA4262")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "ordinary-call", "JAVAC0000")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "ordinary-call", "XA4262")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "ordinary-call", "XA4262")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "ordinary-arithmetic", "JAVAC0000")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "ordinary-arithmetic", "XA4262")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "ordinary-arithmetic", "XA4262")]
		public void Build_SuperArgumentsLambdaAndMethodReference_MatchJavac (
			string typeMapImplementation,
			AndroidRuntime runtime,
			string shape,
			string expectedCode)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var superArguments = shape switch {
				"lambda" => "p1 -> p1",
				"parenthesized-lambda" => "(p1) -> p1",
				"typed-lambda" => "(String p1, String p2) -> p1 + p2",
				"literal-comma" => "p1 -> \\\"a,b\\\"",
				"method-reference" => "Helper::p1",
				"generic-method-reference" => "Helper::<String>p1",
				"generic-construction" => "(p1) -> new SimpleEntry<String, String>(p1, p1)",
				"nested-generic" => "(p1) -> new SimpleEntry<String, java.util.List<String>>(p1, java.util.Arrays.asList(\\\"a,b\\\", p1))",
				"instanceof-generic" => "(p1) -> p1 instanceof java.util.Map<?, ?> ? p1 : p1",
				"comparison" => "(int p1) -> p1 < 2 ? p1 : 2",
				"shift" => "(int p1) -> p1 >> 1",
				"ordinary-bare" => "p1",
				"ordinary-call" => "p1.hashCode()",
				"ordinary-arithmetic" => "p1 + 1",
				_ => throw new InvalidOperationException ($"Unknown super argument shape '{shape}'."),
			};
			var functionalType = shape switch {
				"typed-lambda" => "java.util.function.BiFunction<String, String, String>",
				"comparison" or "shift" => "java.util.function.IntUnaryOperator",
				"instanceof-generic" => "java.util.function.Function<Object, ?>",
				"method-reference" or "generic-method-reference" => "java.util.function.Supplier<String>",
				_ => "java.util.function.Function<String, ?>",
			};
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
				},
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidTypeMapImplementation", typeMapImplementation);
			proj.Sources.Add (new BuildItem.Source ("SuperArgumentsPeer.cs") {
				TextContent = () => $$"""
					using Android.Runtime;
					using Java.Interop;

					namespace UnnamedProject;

					[Register ("my/app/SuperArgumentsBase", DoNotGenerateAcw = true)]
					public class SuperArgumentsBase : Java.Lang.Object
					{
						[Register (".ctor", "(Ljava/util/function/Function;Ljava/lang/Object;)V", "")]
						public SuperArgumentsBase () { }
					}

					[Register ("my/app/SuperArgumentsPeer")]
					public class SuperArgumentsPeer : SuperArgumentsBase
					{
						[Export (".ctor", SuperArgumentsString = "{{superArguments}}")]
						public SuperArgumentsPeer () { }
					}
					""",
			});
			proj.AndroidJavaSources.Add (new AndroidItem.AndroidJavaSource ("my\\app\\SuperArgumentsBase.java") {
				Encoding = Encoding.ASCII,
				TextContent = () => $$"""
					package my.app;

					public class SuperArgumentsBase {
						public SuperArgumentsBase (
							{{functionalType}} function) {}
						public SuperArgumentsBase (
							java.util.function.Function function,
							Object value) {}

						public static class Helper {
							public static <T> T p1 () { return null; }
						}

						public static class SimpleEntry<K, V> extends java.util.AbstractMap.SimpleEntry<K, V> {
							public SimpleEntry (K key, V value) { super (key, value); }
						}
					}
					""",
			});

			using var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			var succeeded = builder.Build (proj);
			if (expectedCode == "success") {
				Assert.IsTrue (succeeded, $"{runtime}/{typeMapImplementation} should compile {shape} super arguments.");
			} else {
				Assert.IsFalse (succeeded, $"{runtime}/{typeMapImplementation} should reject the bare p1 reference.");
				StringAssertEx.Contains ($"error {expectedCode}", builder.LastBuildOutput);
				if (typeMapImplementation == "trimmable") {
					AssertNoExportOutputs (builder, "SuperArgumentsPeer");
				}
			}
		}

		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "XALNS7004")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "XA4263")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "XA4263")]
		public void Build_UnsupportedExportConstructorOverloads_ReportOnlyExportDiagnostics (
			string typeMapImplementation,
			AndroidRuntime runtime,
			string expectedCode)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
				},
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidTypeMapImplementation", typeMapImplementation);
			proj.Sources.Add (new BuildItem.Source ("UnsupportedExportConstructors.cs") {
				TextContent = () => """
					using Android.Runtime;
					using Java.Interop;

					namespace UnnamedProject;

					public sealed class UnsupportedOne { }
					public sealed class UnsupportedTwo { }

					[Register ("my/app/NoDefaultBase", DoNotGenerateAcw = true)]
					public class NoDefaultBase : Java.Lang.Object
					{
						[Register (".ctor", "(I)V", "")]
						public NoDefaultBase (int value) { }
					}

					[Register ("my/app/UnsupportedExportConstructorOverloads")]
					public class UnsupportedExportConstructorOverloads : NoDefaultBase
					{
						[Export (".ctor")]
						public UnsupportedExportConstructorOverloads (
							[ExportParameter (ExportParameterKind.InputStream)] UnsupportedOne value) : base (0) { }

						[Export (".ctor")]
						public UnsupportedExportConstructorOverloads (
							[ExportParameter (ExportParameterKind.InputStream)] UnsupportedTwo value) : base (0) { }
					}
					""",
			});

			using var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), $"{runtime}/{typeMapImplementation} should reject unsupported exported constructors.");
			StringAssertEx.Contains ($"error {expectedCode}", builder.LastBuildOutput);
			if (typeMapImplementation == "trimmable") {
				StringAssertEx.Contains ("unsupported signature type 'UnnamedProject.UnsupportedOne'", builder.LastBuildOutput);
				StringAssertEx.Contains ("unsupported signature type 'UnnamedProject.UnsupportedTwo'", builder.LastBuildOutput);
				Assert.IsFalse (builder.LastBuildOutput.Any (line =>
					line.Contains ("Type 'UnnamedProject.UnsupportedExportConstructorOverloads'", StringComparison.Ordinal) &&
					(line.Contains ("error XA4259", StringComparison.Ordinal) ||
					 line.Contains ("error XA4260", StringComparison.Ordinal) ||
					 line.Contains ("error XA4261", StringComparison.Ordinal))));
			}
			AssertNoExportOutputs (builder, "UnsupportedExportConstructorOverloads");
		}

		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, true)]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, false)]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, false)]
		public void Build_WithCollidingConstructorSignatures_MatchesLegacyCount (
			string typeMapImplementation,
			AndroidRuntime runtime,
			bool shouldSucceed)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidTypeMapImplementation", typeMapImplementation);
			proj.Sources.Add (new BuildItem.Source ("ConstructorCollision.cs") {
				TextContent = () => """
					using Android.App;

					namespace UnnamedProject;

					public class ConstructorCollision : Activity
					{
						public enum Kind { None }
						public ConstructorCollision (int value) { }
						public ConstructorCollision (uint value) { }
						public ConstructorCollision (Kind value) { }
					}
					""",
			});

			using var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.AreEqual (shouldSucceed, builder.Build (proj), $"{runtime}/{typeMapImplementation} should match legacy collision behavior.");
			if (shouldSucceed) {
				Assert.IsFalse (builder.LastBuildOutput.Any (line => line.Contains ("error XA4259", StringComparison.Ordinal)));
				return;
			}
			StringAssertEx.Contains ("error XA4259", builder.LastBuildOutput);

			var typemapDirectory = builder.Output.GetIntermediaryPath ("typemap");
			Assert.IsFalse (Directory.Exists (typemapDirectory) && Directory.EnumerateFiles (typemapDirectory, "*.java", SearchOption.AllDirectories).Any (),
				"Constructor diagnostics must be reported before partial Java output is written.");
		}

		[TestCase ("rectangular-in-sz-array", false)]
		[TestCase ("pointer-array", true)]
		[TestCase ("function-pointer-array", true)]
		public void Build_WithUnsupportedNestedConstructorParameter_FailsBeforeWritingTrimmableOutputs (string shape, bool isUnsafe)
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: false)) {
				return;
			}

			var parameterType = shape switch {
				"rectangular-in-sz-array" => "string[][,]",
				"pointer-array" => "int*[]",
				"function-pointer-array" => "delegate* unmanaged<void>[]",
				_ => throw new InvalidOperationException ($"Unknown nested constructor shape '{shape}'."),
			};
			var proj = new XamarinAndroidApplicationProject {
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
				},
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");
			if (isUnsafe) {
				proj.SetProperty ("AllowUnsafeBlocks", "true");
			}
			proj.Sources.Add (new BuildItem.Source ("NestedConstructorShape.cs") {
				TextContent = () => $$"""
					using Android.App;
					using Java.Interop;

					namespace UnnamedProject;

					public {{(isUnsafe ? "unsafe " : "")}}class NestedConstructorShape : Activity
					{
						[Export (".ctor", SuperArgumentsString = "")]
						public NestedConstructorShape ({{parameterType}} value) { }
					}
					""",
			});

			using var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), $"Build should fail for nested constructor parameter '{parameterType}'.");
			StringAssertEx.Contains ("error XA4260", builder.LastBuildOutput);

			var typemapDirectory = builder.Output.GetIntermediaryPath ("typemap");
			Assert.IsFalse (Directory.Exists (typemapDirectory) && Directory.EnumerateFiles (typemapDirectory, "*.java", SearchOption.AllDirectories).Any (),
				"Constructor diagnostics must be reported before partial Java output is written.");
		}

		[Test]
		public void Build_PublishAotProject_UsesTrimmableTypeMapForCoreClrDebug ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: false)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = false,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty (KnownProperties.PublishAot, "true");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var intermediateDir = builder.Output.GetIntermediaryPath ("typemap");
			AssertTrimmableTypeMapOutputs (intermediateDir);
		}

		[Test]
		public void Build_WithTrimmableTypeMap_IncrementalBuild ([Values] bool isRelease, [Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");
			bool trimNativeAotJavaCode = isRelease && runtime == AndroidRuntime.NativeAOT;

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			var intermediateDir = builder.Output.GetIntermediaryPath ("typemap");
			AssertTrimmableTypeMapOutputs (intermediateDir);
			var typemapDlls = Directory.GetFiles (intermediateDir, "*.dll");
			Assert.IsNotEmpty (typemapDlls, "First build should have generated typemap DLL(s).");
			var typemapFingerprints = Path.Combine (intermediateDir, "typemap-fingerprints.txt");
			FileAssert.Exists (typemapFingerprints, "First build should persist typemap fingerprints.");
			var typemapFingerprintContent = File.ReadAllText (typemapFingerprints);
			var typemapWriteTimes = typemapDlls.ToDictionary (path => path, File.GetLastWriteTimeUtc);

			string scanDgml = "";
			DateTime scanDgmlTimestamp = default;
			if (trimNativeAotJavaCode) {
				var ridIntermediateDir = builder.Output.GetIntermediaryPath ("android-arm64");
				scanDgml = Path.Combine (ridIntermediateDir, "native", $"{proj.ProjectName}.scan.dgml.xml");
				var codegenDgml = Path.Combine (ridIntermediateDir, "native", $"{proj.ProjectName}.codegen.dgml.xml");
				FileAssert.Exists (scanDgml);
				FileAssert.DoesNotExist (codegenDgml, "Optimized builds should emit only the scan DGML needed for Java trimming.");
				scanDgmlTimestamp = File.GetLastWriteTimeUtc (scanDgml);
			}

			Assert.IsTrue (builder.Build (proj), "Second build should have succeeded.");

			Assert.IsTrue (
				builder.Output.IsTargetSkipped ("_GenerateJavaStubs"),
				"_GenerateJavaStubs should be skipped on incremental build.");
			if (trimNativeAotJavaCode) {
				builder.Output.AssertTargetIsSkipped ("_GenerateTrimmableTypeMapProguardConfiguration");
				Assert.AreEqual (scanDgmlTimestamp, File.GetLastWriteTimeUtc (scanDgml), "No-op builds should not rewrite the scan DGML.");
			}
			if (isRelease && runtime == AndroidRuntime.CoreCLR) {
				builder.Output.AssertTargetIsSkipped ("_RemoveRegisterAttributeCoreClr");
			}
			if (isRelease && runtime == AndroidRuntime.NativeAOT) {
				builder.Output.AssertTargetIsNotSkipped ("_RemoveRegisterAttributeNativeAot");
			}
			foreach (var typemapDll in typemapDlls) {
				FileAssert.Exists (typemapDll, $"No-op builds should preserve generated typemap assembly {typemapDll} when _GenerateTrimmableTypeMap is skipped.");
			}

			FileAssert.Exists (typemapFingerprints, "IncrementalClean should preserve typemap fingerprints on a no-op build.");
			Assert.AreEqual (typemapFingerprintContent, File.ReadAllText (typemapFingerprints), "A no-op build should not change typemap fingerprints.");

			proj.MainActivity = proj.DefaultMainActivity + Environment.NewLine + "// Force trimmable typemap regeneration.";
			proj.Touch ("MainActivity.cs");
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "Changed-input build should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_GenerateTrimmableTypeMap");
			foreach (var typemapDll in typemapDlls) {
				Assert.AreEqual (typemapWriteTimes [typemapDll], File.GetLastWriteTimeUtc (typemapDll),
					$"A source change that does not affect the typemap model should skip PE emission for {typemapDll}.");
			}
		}

		[Test]
		public void Build_WithTrimmableTypeMap_MissingJavaListPreservesGeneratedJava ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: false)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject ();
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			var typemapDirectory = builder.Output.GetIntermediaryPath ("typemap");
			var javaDirectory = Path.Combine (typemapDirectory, "java");
			var javaFiles = Directory.GetFiles (javaDirectory, "*.java", SearchOption.AllDirectories);
			var javaFilesList = Path.Combine (typemapDirectory, "java-files.txt");
			Assert.IsNotEmpty (javaFiles, "First build should have generated pre-trim Java sources.");
			FileAssert.Exists (javaFilesList, "First build should have persisted the pre-trim Java file list.");
			foreach (var path in File.ReadAllLines (javaFilesList)) {
				Assert.IsTrue (Path.IsPathFullyQualified (path), $"The persisted Java path should be fully qualified: {path}");
			}
			File.Delete (javaFilesList);

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "No-op build should have succeeded.");
			builder.Output.AssertTargetIsSkipped ("_GenerateTrimmableTypeMap");
			foreach (var javaFile in javaFiles) {
				FileAssert.Exists (javaFile, $"IncrementalClean should preserve {javaFile} when upgrading an obj directory without java-files.txt.");
			}
		}

		[Test]
		public void Build_WithTrimmableTypeMap_PublishTrimmed_MissingLinkedJavaListRegenerates ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			var typemapDirectory = builder.Output.GetIntermediaryPath ("typemap");
			var linkedJavaDirectory = Path.Combine (typemapDirectory, "linked-java");
			var linkedJavaFiles = Directory.GetFiles (linkedJavaDirectory, "*.java", SearchOption.AllDirectories);
			var linkedJavaFilesList = Path.Combine (typemapDirectory, "linked-java-files.txt");
			Assert.IsNotEmpty (linkedJavaFiles, "First build should have generated post-trim Java sources.");
			FileAssert.Exists (linkedJavaFilesList, "First build should have persisted the post-trim Java file list.");
			foreach (var path in File.ReadAllLines (linkedJavaFilesList)) {
				Assert.IsTrue (Path.IsPathFullyQualified (path), $"The persisted linked Java path should be fully qualified: {path}");
			}
			File.Delete (linkedJavaFilesList);

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "Migration rebuild should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_GeneratePostTrimTrimmableTypeMapJavaSources");
			builder.Output.AssertTargetIsSkipped ("_CompileJava");
			FileAssert.Exists (linkedJavaFilesList, "Post-trim generation should recreate linked-java-files.txt.");
			foreach (var javaFile in linkedJavaFiles) {
				FileAssert.Exists (javaFile, $"IncrementalClean should preserve {javaFile} until post-trim generation recreates its list.");
			}
		}

		[Test]
		public void Build_WithTrimmableTypeMap_KeepsNativeAotRuntimeHostAcws ()
		{
			const bool isRelease = true;
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.NativeAOT, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				LinkTool = "r8",
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var dexFile = builder.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "classes.dex"));
			FileAssert.Exists (dexFile);

			// Regression test: the NativeAOT runtime host assembly (Microsoft.Android.Runtime.NativeAOT) is
			// resolved only in the per-RID inner build, so the RID-independent outer-build trimmable typemap
			// generator never scanned it. Its only Java Callable Wrapper type, UncaughtExceptionMarshaler,
			// therefore had no JCW and no typemap entry -> the runtime ACW is absent from classes.dex and the
			// app crashes at startup in JavaInteropRuntime.init (setDefaultUncaughtExceptionHandler). A
			// reference assembly for the host is now shipped in the SDK pack and fed to the generator. The JCW
			// name is CRC-hashed (e.g. `scrc64...UncaughtExceptionMarshaler`), so match on the type name suffix.
			Assert.IsTrue (DexUtils.ContainsClass ("UncaughtExceptionMarshaler;", dexFile, AndroidSdkPath),
				$"`{dexFile}` should include the UncaughtExceptionMarshaler runtime ACW.");
		}

		[Test]
		public void Build_WithTrimmableTypeMap_DeletesStaleGeneratedJavaSources ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: false)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject ();
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			var staleRelativePath = Path.Combine ("crc64stale", "Old.java");
			var staleClassPath = Path.Combine ("crc64stale", "Old.class");
			var staleGeneratedJava = builder.Output.GetIntermediaryPath (Path.Combine ("typemap", "java", staleRelativePath));
			var staleCompiledClass = builder.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "classes", staleClassPath));
			var staleGeneratedJavaDirectory = Path.GetDirectoryName (staleGeneratedJava);
			var staleCompiledClassDirectory = Path.GetDirectoryName (staleCompiledClass);
			if (staleGeneratedJavaDirectory is null || staleCompiledClassDirectory is null) {
				throw new InvalidOperationException ("Could not determine stale Java output directories.");
			}
			Directory.CreateDirectory (staleGeneratedJavaDirectory);
			Directory.CreateDirectory (staleCompiledClassDirectory);
			File.WriteAllText (staleGeneratedJava, "package crc64stale; public class Old {}");
			File.WriteAllBytes (staleCompiledClass, []);

			proj.MainActivity += Environment.NewLine + "// Force trimmable typemap regeneration.";
			proj.Touch ("MainActivity.cs");
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "Second build should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_GenerateTrimmableTypeMap");
			builder.Output.AssertTargetIsNotSkipped ("_CompileJava");

			FileAssert.DoesNotExist (staleGeneratedJava, "Regenerated trimmable typemap should delete stale Java sources.");
			FileAssert.DoesNotExist (staleCompiledClass, "Deleting stale generated Java sources should force Java recompilation and remove stale class outputs.");
		}

		[Test]
		public void Build_WithTrimmableTypeMap_RecompilesUpdatedGeneratedJavaSources ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: false)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject ();
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			var generatedJavaDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("typemap", "java"));
			var generatedJavaFiles = Directory.GetFiles (generatedJavaDirectory, "*.java", SearchOption.AllDirectories);
			Assert.IsNotEmpty (generatedJavaFiles, "Test setup should have generated trimmable typemap Java sources.");

			var generatedJava = generatedJavaFiles [0];
			var typeMapStamp = builder.Output.GetIntermediaryPath (Path.Combine ("typemap", "_GenerateTrimmableTypeMap.stamp"));
			var javaStubsStamp = builder.Output.GetIntermediaryPath (Path.Combine ("stamp", "_GenerateJavaStubs.stamp"));
			FileAssert.Exists (typeMapStamp, "First build should have written the trimmable typemap output stamp.");
			FileAssert.Exists (javaStubsStamp, "First build should have written the Java stubs output stamp.");

			var updatedJava = File.ReadAllText (generatedJava) + "\n// Force generated Java recompilation regression.\n";
			File.WriteAllText (generatedJava, updatedJava);
			var stampTime = DateTime.UtcNow;
			File.SetLastWriteTimeUtc (typeMapStamp, stampTime);
			File.SetLastWriteTimeUtc (javaStubsStamp, stampTime.AddSeconds (-5));

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true), "Second build should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_GenerateJavaStubs");
			builder.Output.AssertTargetIsNotSkipped ("_CompileJava");
			Assert.AreEqual (updatedJava, File.ReadAllText (generatedJava), "Updated generated Java sources should be compiled in place from the generator output directory even when typemap assemblies do not change.");

			var relativePath = Path.GetRelativePath (generatedJavaDirectory, generatedJava);
			var compiledClass = builder.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "classes", Path.ChangeExtension (relativePath, ".class")));
			FileAssert.Exists (compiledClass, "Updated generated Java sources should be compiled in place into android/bin/classes.");
		}

		// The JCWs that actually get compiled and packaged are the generated Java sources compiled
		// in place from their output directory (no longer copied into $(IntermediateOutputPath)android/src).
		// For CoreCLR + PublishTrimmed those come from the post-trim `typemap/linked-java` directory,
		// which `_GeneratePostTrimTrimmableTypeMapJavaSources` (re)generates from the linked assemblies
		// and `_CompileJava` compiles via the Javac AdditionalStubSourceDirectories parameter.
		static void AssertLinkedJavaCompiledInPlace (ProjectBuilder builder, string message)
		{
			var linkedJavaDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("typemap", "linked-java"));
			DirectoryAssert.Exists (linkedJavaDirectory, $"{message}: post-trim linked-java directory should exist.");

			var linkedJavaFiles = Directory.GetFiles (linkedJavaDirectory, "*.java", SearchOption.AllDirectories);
			Assert.IsNotEmpty (linkedJavaFiles, $"{message}: post-trim build should have generated linked-java JCWs.");

			var androidSrcDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android", "src"));
			var classesDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "classes"));
			foreach (var linkedJava in linkedJavaFiles) {
				var relativePath = Path.GetRelativePath (linkedJavaDirectory, linkedJava);
				var copiedJava = Path.Combine (androidSrcDirectory, relativePath);
				FileAssert.DoesNotExist (copiedJava, $"{message}: linked-java JCW '{relativePath}' should be compiled in place, not copied to android/src.");

				var compiledClass = Path.Combine (classesDirectory, Path.ChangeExtension (relativePath, ".class"));
				FileAssert.Exists (compiledClass, $"{message}: linked-java JCW '{relativePath}' should be compiled in place into android/bin/classes.");
			}
		}

		[Test]
		public void Build_WithTrimmableTypeMap_PublishTrimmed_CompilesLinkedJavaInPlace ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");
			AssertLinkedJavaCompiledInPlace (builder, "After first build");

			// A no-op rebuild must not start copying linked-java into android/src, even though
			// the post-trim Java generation may run again.
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "No-op rebuild should have succeeded.");
			AssertLinkedJavaCompiledInPlace (builder, "After no-op rebuild");
		}

		[Test]
		public void Build_WithTrimmableTypeMap_PublishTrimmed_DeletesStaleLinkedJavaWhenLinkedJavaShrinks ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			// Simulate a JCW the post-trim pass produced on a previous build but no longer
			// produces (e.g. its type was trimmed away). It lives in the post-trim
			// `linked-java` source directory (compiled in place), with a compiled .class.
			var staleRelativePath = Path.Combine ("crc64stale", "Old.java");
			var staleClassPath = Path.Combine ("crc64stale", "Old.class");
			var staleLinkedJava = builder.Output.GetIntermediaryPath (Path.Combine ("typemap", "linked-java", staleRelativePath));
			var staleCompiledClass = builder.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "classes", staleClassPath));
			var staleLinkedJavaDirectory = Path.GetDirectoryName (staleLinkedJava);
			var staleCompiledClassDirectory = Path.GetDirectoryName (staleCompiledClass);
			if (staleLinkedJavaDirectory is null || staleCompiledClassDirectory is null) {
				throw new InvalidOperationException ("Could not determine stale Java output directories.");
			}
			Directory.CreateDirectory (staleLinkedJavaDirectory);
			Directory.CreateDirectory (staleCompiledClassDirectory);
			File.WriteAllText (staleLinkedJava, "package crc64stale; public class Old {}");
			File.WriteAllBytes (staleCompiledClass, []);

			// Force the post-trim Java generation to re-run by removing its stamp (without a source
			// change, which would trigger an unrelated incremental CrossGen rebuild). It updates
			// linked-java in place (dropping the stale JCW); the JCWs are compiled in place, so
			// busting the compile stamp must drop the stale .class too.
			var postTrimStamp = builder.Output.GetIntermediaryPath (Path.Combine ("stamp", "_GeneratePostTrimTrimmableTypeMapJavaSources.stamp"));
			FileAssert.Exists (postTrimStamp, "First build should have written the post-trim Java stamp.");
			File.Delete (postTrimStamp);

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "Second build should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_GeneratePostTrimTrimmableTypeMapJavaSources");
			builder.Output.AssertTargetIsNotSkipped ("_CompileJava");

			FileAssert.DoesNotExist (staleLinkedJava, "Post-trim regeneration should drop the stale linked-java JCW.");
			FileAssert.DoesNotExist (staleCompiledClass, "Dropping the stale linked-java JCW should force Java recompilation and remove the stale class output.");
		}

		[Test]
		public void Build_WithTrimmableTypeMap_PublishTrimmed_PostTrimJavaGenerationIsIncremental ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			// A no-op rebuild should not regenerate the post-trim JCWs or recompile them. If
			// _GeneratePostTrimTrimmableTypeMapJavaSources runs on every build, the JCWs that feed
			// _GenerateJavaStubs are rewritten each time, which both wastes work and means
			// _GenerateJavaStubs must re-run to stay consistent (otherwise the compiled JCWs go stale).
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "No-op rebuild should have succeeded.");
			builder.Output.AssertTargetIsSkipped ("_GeneratePostTrimTrimmableTypeMapJavaSources");
			builder.Output.AssertTargetIsSkipped ("_GenerateJavaStubs");

			var acwMap = builder.Output.GetIntermediaryPath ("acw-map.txt");
			FileAssert.Exists (acwMap, "First build should have generated the post-trim ACW map.");
			var linkedJava = Directory.GetFiles (
				builder.Output.GetIntermediaryPath (Path.Combine ("typemap", "linked-java")),
				"*.java",
				SearchOption.AllDirectories).First ();
			File.Delete (acwMap);
			File.Delete (linkedJava);

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "Missing-output recovery build should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_GeneratePostTrimTrimmableTypeMapJavaSources");
			FileAssert.Exists (acwMap, "Post-trim generation should restore a missing ACW map even when linked assemblies are unchanged.");
			FileAssert.Exists (linkedJava, "Post-trim generation should restore a missing linked Java source even when linked assemblies are unchanged.");
			Assert.That (new FileInfo (acwMap).Length, Is.GreaterThan (0), "The restored ACW map should contain the linked mappings.");
		}

		[Test]
		public void Build_WithTrimmableTypeMap_PublishTrimmed_IncrementalChangesAvoidUnnecessaryJavaWork ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			var linkedJavaDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("typemap", "linked-java"));
			var linkedJavaBefore = Directory.GetFiles (linkedJavaDirectory, "*.java", SearchOption.AllDirectories)
				.ToDictionary (
					path => Path.GetRelativePath (linkedJavaDirectory, path),
					path => (Hash: ComputeFileHash (path), WriteTime: File.GetLastWriteTimeUtc (path)),
					StringComparer.Ordinal);
			Assert.IsNotEmpty (linkedJavaBefore, "First build should have generated post-trim Java sources.");

			var updatedMainActivity = proj.DefaultMainActivity.Replace (
				"//${AFTER_ONCREATE}",
				"System.Console.WriteLine (\"Managed-only incremental change.\");");
			Assert.AreNotEqual (proj.DefaultMainActivity, updatedMainActivity, "Test setup should update the managed MainActivity body.");
			proj.MainActivity = updatedMainActivity;
			proj.Touch ("MainActivity.cs");

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "Managed-only rebuild should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_GeneratePostTrimTrimmableTypeMapJavaSources");
			builder.Output.AssertTargetIsNotSkipped ("_RemoveRegisterAttributeCoreClr");
			builder.Output.AssertTargetIsSkipped ("_CompileJava");
			builder.Output.AssertTargetIsSkipped ("_CompileToDalvik");

			var linkedJavaAfter = Directory.GetFiles (linkedJavaDirectory, "*.java", SearchOption.AllDirectories)
				.ToDictionary (
					path => Path.GetRelativePath (linkedJavaDirectory, path),
					path => (Hash: ComputeFileHash (path), WriteTime: File.GetLastWriteTimeUtc (path)),
					StringComparer.Ordinal);
			CollectionAssert.AreEquivalent (linkedJavaBefore.Keys, linkedJavaAfter.Keys, "A managed method-body change should not alter the JCW set.");
			foreach (var pair in linkedJavaBefore) {
				var current = linkedJavaAfter [pair.Key];
				Assert.IsTrue (pair.Value.Hash.SequenceEqual (current.Hash), $"{pair.Key} content should be unchanged.");
				Assert.AreEqual (pair.Value.WriteTime, current.WriteTime, $"{pair.Key} timestamp should remain stable.");
			}

			var acwMap = builder.Output.GetIntermediaryPath ("acw-map.txt");
			var applicationRegistration = builder.Output.GetIntermediaryPath (Path.Combine ("android", "src", "net", "dot", "android", "ApplicationRegistration.java"));
			var postTrimAcwMap = ComputeFileHash (acwMap);
			var postTrimApplicationRegistration = ComputeFileHash (applicationRegistration);

			var updatedManifest = proj.AndroidManifest.Replace (
				"</manifest>",
				"<uses-permission android:name=\"android.permission.CAMERA\" /></manifest>");
			Assert.AreNotEqual (proj.AndroidManifest, updatedManifest, "Test setup should update AndroidManifest.xml.");
			proj.AndroidManifest = updatedManifest;
			proj.Touch ("Properties\\AndroidManifest.xml");

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true), "Manifest-only rebuild should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_GenerateTrimmableTypeMap");
			builder.Output.AssertTargetIsSkipped ("_GeneratePostTrimTrimmableTypeMapJavaSources");
			builder.Output.AssertTargetIsNotSkipped ("_GenerateJavaStubs");
			builder.Output.AssertTargetIsSkipped ("_RemoveRegisterAttributeCoreClr");
			builder.Output.AssertTargetIsSkipped ("_CompileJava");
			builder.Output.AssertTargetIsSkipped ("_CompileToDalvik");
			Assert.IsTrue (postTrimAcwMap.SequenceEqual (ComputeFileHash (acwMap)), "The pre-trim pass should not overwrite the linked acw-map.txt.");
			Assert.IsTrue (
				postTrimApplicationRegistration.SequenceEqual (ComputeFileHash (applicationRegistration)),
				"The pre-trim pass should not overwrite the linked ApplicationRegistration.java.");

			var mergedManifest = builder.Output.GetIntermediaryPath (Path.Combine ("android", "AndroidManifest.xml"));
			StringAssert.Contains ("android.permission.CAMERA", File.ReadAllText (mergedManifest), "The manifest-only change should flow into the packaged manifest.");
		}


		[Test]
		public void Build_WithTrimmableTypeMap_DoesNotHitCopyIfChangedMismatch ([Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			Assert.IsFalse (
				StringAssertEx.ContainsText (builder.LastBuildOutput, "source and destination count mismatch"),
				$"{builder.BuildLogFile} should not fail with XACIC7004.");
			Assert.IsFalse (
				StringAssertEx.ContainsText (builder.LastBuildOutput, "Internal error: architecture"),
				$"{builder.BuildLogFile} should keep trimmable typemap assemblies aligned across ABIs.");
		}

		[Test]
		public void Build_WithTrimmableTypeMap_AssemblyStoreMappingsStayInRange ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var environmentFiles = Directory.GetFiles (builder.Output.GetIntermediaryPath ("android"), "environment.*.ll");
			Assert.IsNotEmpty (environmentFiles, "Expected generated environment.<abi>.ll files.");

			foreach (var environmentFile in environmentFiles) {
				var abi = Path.GetFileNameWithoutExtension (environmentFile).Substring ("environment.".Length);
				var manifestFile = builder.Output.GetIntermediaryPath (Path.Combine ("app_shared_libraries", abi, "assembly-store.so.manifest"));

				if (!File.Exists (manifestFile)) {
					continue;
				}

				var environmentText = File.ReadAllText (environmentFile);
				var runtimeDataMatch = Regex.Match (environmentText, @"assembly_store_bundled_assemblies.*\[(\d+)\s+x");
				Assert.IsTrue (runtimeDataMatch.Success, $"{environmentFile} should declare assembly_store_bundled_assemblies.");

				var runtimeDataCount = int.Parse (runtimeDataMatch.Groups [1].Value);
				var maxMappingIndex = File.ReadLines (manifestFile)
					.Select (line => Regex.Match (line, @"\bmi:(\d+)\b"))
					.Where (match => match.Success)
					.Select (match => int.Parse (match.Groups [1].Value))
					.Max ();

				Assert.That (
					runtimeDataCount,
					Is.GreaterThan (maxMappingIndex),
					$"{Path.GetFileName (environmentFile)} should allocate enough runtime slots for {Path.GetFileName (manifestFile)}.");
			}
		}

		[Test]
		public void NativeAotTrimmableTypeMap_DoesNotExportFrameworkTypeMaps ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.NativeAOT, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var ridIntermediateDir = builder.Output.GetIntermediaryPath ("android-arm64");
			var rspFiles = Directory.GetFiles (ridIntermediateDir, "*.ilc.rsp", SearchOption.AllDirectories);
			Assert.IsNotEmpty (rspFiles, $"{ridIntermediateDir} should contain an ILC response file.");

			var rspText = File.ReadAllText (rspFiles [0]);
			StringAssert.Contains ("_Java.Interop.TypeMap.dll", rspText);
			StringAssert.Contains ("_Mono.Android.TypeMap.dll", rspText);
			StringAssert.DoesNotContain ("--generateunmanagedentrypoints:_Java.Interop.TypeMap", rspText);
			StringAssert.DoesNotContain ("--generateunmanagedentrypoints:_Mono.Android.TypeMap", rspText);
			StringAssert.Contains ($"--generateunmanagedentrypoints:_{proj.ProjectName}.TypeMap", rspText);
		}

		[Test]
		public void CoreClrTrimmableTypeMap_PackagesJavaProxyThrowable ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var dexFile = builder.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "classes.dex"));
			FileAssert.Exists (dexFile);
			Assert.IsTrue (
				DexUtils.ContainsClassWithMethod ("Landroid/runtime/JavaProxyThrowable;", "<init>", "(Ljava/lang/String;)V", dexFile, AndroidSdkPath),
				$"`{dexFile}` should include `android.runtime.JavaProxyThrowable`.");
		}

		[Test]
		public void CoreClrTrimmableTypeMap_PackagesReadyToRunTypeMap ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");
			proj.SetProperty ("RuntimeIdentifier", "android-arm64");
			proj.SetProperty ("AndroidEnableAssemblyCompression", "false");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var r2rTypeMap = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "R2R", "_Microsoft.Android.TypeMaps.dll"));
			FileAssert.Exists (r2rTypeMap, "ReadyToRun should compile the generated TypeMap entry assembly.");
			using (var r2rStream = File.OpenRead (r2rTypeMap)) {
				using var r2rReader = new System.Reflection.PortableExecutable.PEReader (r2rStream);
				Assert.IsTrue (
					r2rReader.PEHeaders.CorHeader.ManagedNativeHeaderDirectory.Size > 0,
					"ReadyToRun output for _Microsoft.Android.TypeMaps.dll should have a managed native header.");
			}

			var apk = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, "android-arm64", $"{proj.PackageName}-Signed.apk");
			FileAssert.Exists (apk);

			var helper = new ArchiveAssemblyHelper (apk, useAssemblyStores: true);
			var packagedTypeMapEntries = helper.ListArchiveContents ("lib/", arch: AndroidTargetArch.Arm64)
				.Where (entry => entry.StartsWith ("lib/arm64-v8a/lib__", StringComparison.Ordinal) &&
					entry.EndsWith (".dll.so", StringComparison.Ordinal) &&
					!entry.EndsWith (".ni.dll.so", StringComparison.Ordinal) &&
					entry.Contains ("TypeMap", StringComparison.Ordinal))
				.ToArray ();
			Assert.AreEqual (
				packagedTypeMapEntries.Distinct ().Count (),
				packagedTypeMapEntries.Length,
				"TypeMap assemblies should be packaged only once; do not include both linked IL and ReadyToRun copies.");
			Assert.AreEqual (
				1,
				packagedTypeMapEntries.Count (entry => entry == "lib/arm64-v8a/lib__Microsoft.Android.TypeMaps.dll.so"),
				"_Microsoft.Android.TypeMaps.dll should be packaged only once.");

			Assert.IsTrue (helper.Exists ("assemblies/arm64-v8a/_Microsoft.Android.TypeMaps.dll"), "_Microsoft.Android.TypeMaps.dll should exist in the APK.");
			using (var packagedTypeMap = helper.ReadEntry ("assemblies/arm64-v8a/_Microsoft.Android.TypeMaps.dll", AndroidTargetArch.Arm64)) {
				Assert.IsNotNull (packagedTypeMap, "_Microsoft.Android.TypeMaps.dll should be readable from the APK.");
				using var packagedReader = new System.Reflection.PortableExecutable.PEReader (packagedTypeMap);
				Assert.IsTrue (
					packagedReader.PEHeaders.CorHeader.ManagedNativeHeaderDirectory.Size > 0,
					"Packaged _Microsoft.Android.TypeMaps.dll should be the ReadyToRun image, not the linked IL image.");
			}
		}

		[Test]
		public void ReleaseCoreClrTrimmableTypeMap_SupportsExplicitDynamicCodeSupportOff ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var dynamicCodeDisabledTrimmable = BuildDynamicCodeSupportProfile ("trimmable", dynamicCodeSupport: false);

			using var runtimeConfigJson = JsonDocument.Parse (dynamicCodeDisabledTrimmable.RuntimeConfig);
			Assert.IsTrue (
				runtimeConfigJson.RootElement.TryGetProperty ("runtimeOptions", out var runtimeOptions),
				"runtimeconfig.json should include runtimeOptions.");
			Assert.IsTrue (
				runtimeOptions.TryGetProperty ("configProperties", out var configProperties),
				"runtimeconfig.json should include runtimeOptions.configProperties.");
			Assert.IsTrue (
				configProperties.TryGetProperty ("System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported", out var dynamicCodeSupportProperty),
				"runtimeconfig.json should include RuntimeFeature.IsDynamicCodeSupported.");
			Assert.IsFalse (
				dynamicCodeSupportProperty.GetBoolean (),
				"trimmable typemap builds should honor explicit DynamicCodeSupport=false.");
		}

		[Test]
		public void ReleaseCoreClrTrimmableTypeMap_SingleRuntimeIdentifier_PackagesLinkedOrReadyToRunTypeMapAssemblies ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				PackageName = "com.xamarin.typemapcomparison",
				ProjectName = "TypemapComparison",
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty (KnownProperties.RuntimeIdentifier, "android-arm64");
			proj.SetProperty ("AndroidPackageFormat", "apk");
			proj.SetProperty ("AndroidEnableAssemblyCompression", "false");
			proj.SetProperty (KnownProperties.AndroidLinkTool, "r8");
			proj.SetProperty ("TrimMode", "full");
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder (Path.Combine ("temp", $"TypemapComparison_trimmable_single_rid_{Guid.NewGuid ():N}"));
			Assert.IsTrue (builder.Build (proj), "trimmable single-RID build should have succeeded.");

			var apkDirectory = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath);
			var apkPath = Directory.GetFiles (apkDirectory, "*-Signed.apk", SearchOption.AllDirectories).Single ();
			var typeMapDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "typemap"));
			var linkedAssemblyDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "linked"));
			var readyToRunAssemblyDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "R2R"));
			var javaSourceDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "typemap", "linked-java"));
			var dexFile = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "android", "bin", "classes.dex"));
			var acwMapPath = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "acw-map.txt"));
			var proguardPrimaryPath = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "proguard", "proguard_project_primary.cfg"));

			DirectoryAssert.Exists (typeMapDirectory, "trimmable build should generate typemap assemblies.");
			DirectoryAssert.Exists (linkedAssemblyDirectory, "Release trimmable build should run ILLink.");

			var generatedTypeMapAssemblies = Directory.GetFiles (typeMapDirectory, "*.dll")
				.Where (IsTypeMapAssemblyPath)
				.ToDictionary (Path.GetFileName, StringComparer.Ordinal);
			var linkedTypeMapAssemblies = Directory.GetFiles (linkedAssemblyDirectory, "*.dll")
				.Where (IsTypeMapAssemblyPath)
				.ToDictionary (Path.GetFileName, StringComparer.Ordinal);
			var expectedPackagedTypeMapAssemblies = linkedTypeMapAssemblies.ToDictionary (
				pair => pair.Key,
				pair => File.Exists (Path.Combine (readyToRunAssemblyDirectory, pair.Key))
					? Path.Combine (readyToRunAssemblyDirectory, pair.Key)
					: pair.Value,
				StringComparer.Ordinal);
			var postLinkModifiedTypeMapAssemblies = expectedPackagedTypeMapAssemblies
				.Where (pair => generatedTypeMapAssemblies.TryGetValue (pair.Key, out var generatedPath) && !FileContentsAreEqual (generatedPath, pair.Value))
				.OrderBy (pair => pair.Key, StringComparer.Ordinal)
				.ToArray ();
			Assert.IsNotEmpty (postLinkModifiedTypeMapAssemblies, "Test setup should include typemap assemblies changed by ILLink or ReadyToRun.");

			var packagedAssemblyNames = ReadPackagedManagedAssemblyNames (apkPath, AndroidTargetArch.Arm64);
			var packagedUnexpectedTypeMapAssemblies = packagedAssemblyNames
				.Where (IsTypeMapAssemblyName)
				.Except (expectedPackagedTypeMapAssemblies.Keys, StringComparer.Ordinal)
				.OrderBy (name => name, StringComparer.Ordinal)
				.ToArray ();
			Assert.IsEmpty (
				packagedUnexpectedTypeMapAssemblies,
				$"{apkPath} should package post-link typemap assemblies, not generated typemap assemblies absent from ILLink output.");
			var helper = new ArchiveAssemblyHelper (apkPath, useAssemblyStores: true);
			foreach (var pair in postLinkModifiedTypeMapAssemblies) {
				using var packagedAssembly = helper.ReadEntry ($"assemblies/arm64-v8a/{pair.Key}", AndroidTargetArch.Arm64);
				Assert.IsNotNull (packagedAssembly, $"{pair.Key} should be packaged in the APK.");
				var expectedHash = ComputeFileHash (pair.Value);
				var packagedHash = ComputeHash (packagedAssembly);
				Assert.IsTrue (
					expectedHash.SequenceEqual (packagedHash),
					$"{apkPath} should package post-link typemap assembly {pair.Key} from {pair.Value}, not the generated pre-link copy.");
			}

			AssertPostTrimR8InputsExcludeDeadFrameworkImplementor (dexFile, javaSourceDirectory, acwMapPath, proguardPrimaryPath);
		}

		[Test]
		public void ReleaseCoreClrTrimmableTypeMap_TrimsUnusedBindingListenerImplementors ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var testRoot = Path.Combine ("temp", $"{TestName}_{Guid.NewGuid ():N}");
			var binding = new XamarinAndroidBindingProject {
				IsRelease = true,
				ProjectName = "ListenerBinding",
				AndroidClassParser = "class-parse",
			};
			binding.SetRuntime (AndroidRuntime.CoreCLR);

			var javaRoot = Path.Combine (Root, testRoot, "java");
			var javaSource = Path.Combine ("com", "example", "listener", "Widget.java");
			Directory.CreateDirectory (Path.Combine (javaRoot, Path.GetDirectoryName (javaSource) ?? ""));
			binding.Jars.Add (new AndroidItem.EmbeddedJar (Path.Combine ("java", "listener.jar")) {
				BinaryContent = new JarContentBuilder {
					BaseDirectory = javaRoot,
					JarFileName = "listener.jar",
					JavaSourceFileName = javaSource,
					JavaSourceText = """
						package com.example.listener;

						public class Widget {
							public interface OnChangedListener {
								void onChanged ();
							}

							public void setOnChangedListener (OnChangedListener listener) {
							}
						}
						""",
				}.Build,
			});

			using var bindingBuilder = CreateDllBuilder (Path.Combine (testRoot, binding.ProjectName));
			Assert.IsTrue (bindingBuilder.Build (binding), "Listener binding build should have succeeded.");

			foreach (bool useListener in new [] { false, true }) {
				var app = new XamarinAndroidApplicationProject {
					IsRelease = true,
					PackageName = useListener ? "com.xamarin.listenerused" : "com.xamarin.listenerunused",
					ProjectName = useListener ? "ListenerUsed" : "ListenerUnused",
				};
				app.SetRuntime (AndroidRuntime.CoreCLR);
				app.SetProperty (KnownProperties.RuntimeIdentifier, "android-arm64");
				app.SetProperty ("AndroidPackageFormat", "apk");
				app.SetProperty (KnownProperties.AndroidLinkTool, "r8");
				app.SetProperty ("TrimMode", "full");
				app.SetProperty ("PublishReadyToRun", "false");
				app.SetProperty ("AndroidTypeMapImplementation", "trimmable");
				app.References.Add (new BuildItem.ProjectReference ($"..\\{binding.ProjectName}\\{binding.ProjectName}.csproj", binding.ProjectName, binding.ProjectGuid));
				if (useListener) {
					app.MainActivity = app.DefaultMainActivity.Replace (
						"//${AFTER_ONCREATE}",
						"""
									var widget = new Com.Example.Listener.Widget ();
									widget.Changed += (sender, args) => { };
						""");
				}

				using var builder = CreateApkBuilder (Path.Combine (testRoot, app.ProjectName));
				Assert.IsTrue (builder.Build (app), $"{app.ProjectName} build should have succeeded.");

				var linkedDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "linked"));
				var linkedBinding = Path.Combine (linkedDirectory, $"{binding.ProjectName}.dll");
				var javaDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "typemap", "linked-java"));
				var implementorJava = Path.Combine (javaDirectory, "mono", "com", "example", "listener", "Widget_OnChangedListenerImplementor.java");
				var acwMapPath = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "acw-map.txt"));
				var proguardPath = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "proguard", "proguard_project_primary.cfg"));
				var dexPath = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "android", "bin", "classes.dex"));

				Assert.AreEqual (
					useListener,
					AssemblyContainsType (linkedBinding, "Com.Example.Listener.Widget/IOnChangedListenerImplementor"),
					$"{app.ProjectName} linked managed output should {(useListener ? "retain" : "trim")} the listener implementor.");
				Assert.AreEqual (
					useListener,
					File.Exists (implementorJava),
					$"{app.ProjectName} post-trim Java output should {(useListener ? "retain" : "trim")} the listener implementor.");
				AssertFileContains (
					acwMapPath,
					"IOnChangedListenerImplementor",
					useListener,
					$"{app.ProjectName} ACW map");
				AssertFileContains (
					proguardPath,
					"mono.com.example.listener.Widget_OnChangedListenerImplementor",
					useListener,
					$"{app.ProjectName} ProGuard configuration");
				Assert.AreEqual (
					useListener,
					DexUtils.ContainsClass ("Lmono/com/example/listener/Widget_OnChangedListenerImplementor;", dexPath, AndroidSdkPath),
					$"{app.ProjectName} DEX should {(useListener ? "retain" : "trim")} the listener implementor.");
			}
		}

		[Test]
		public void ReleaseCoreClrTrimmableTypeMap_UsesExternalJavaRoots ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var app = new XamarinAndroidApplicationProject {
				IsRelease = true,
				PackageName = "com.xamarin.externaljavaroots",
				ProjectName = "ExternalJavaRoots",
			};
			app.SetRuntime (AndroidRuntime.CoreCLR);
			app.SetProperty (KnownProperties.RuntimeIdentifier, "android-arm64");
			app.SetProperty ("AndroidPackageFormat", "apk");
			app.SetProperty ("TrimMode", "full");
			app.SetProperty ("PublishReadyToRun", "false");
			app.SetProperty ("AndroidTypeMapImplementation", "trimmable");
			app.Sources.Add (new BuildItem.Source ("Views.cs") {
				TextContent = () => """
					using Android.Content;
					using Android.Runtime;
					using Android.Util;
					using Android.Views;

					namespace ExternalJavaRoots;

					public class LayoutOnlyView : View
					{
						public LayoutOnlyView (Context context, IAttributeSet attributes) : base (context, attributes)
						{
						}
					}

					[Register ("com.example.RegisteredLayoutOnlyView")]
					public class RegisteredLayoutOnlyView : View
					{
						public RegisteredLayoutOnlyView (Context context, IAttributeSet attributes) : base (context, attributes)
						{
						}
					}

					public class UnusedView : View
					{
						public UnusedView (Context context) : base (context)
						{
						}
					}
					""",
			});
			app.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\layout_only.xml") {
				TextContent = () => """
					<?xml version="1.0" encoding="utf-8"?>
					<ExternalJavaRoots.LayoutOnlyView
						xmlns:android="http://schemas.android.com/apk/res/android"
						android:layout_width="match_parent"
						android:layout_height="match_parent" />
					""",
			});
			app.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\registered_layout_only.xml") {
				TextContent = () => """
					<?xml version="1.0" encoding="utf-8"?>
					<com.example.RegisteredLayoutOnlyView
						xmlns:android="http://schemas.android.com/apk/res/android"
						android:layout_width="match_parent"
						android:layout_height="match_parent" />
					""",
			});

			using var builder = CreateApkBuilder (Path.Combine ("temp", $"{TestName}_{Guid.NewGuid ():N}"));
			Assert.IsTrue (builder.Build (app), "External Java roots build should have succeeded.");

			var linkedApp = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "linked", $"{app.ProjectName}.dll"));
			Assert.IsTrue (AssemblyContainsType (linkedApp, "ExternalJavaRoots.LayoutOnlyView"), "The XML-only custom view should survive linking.");
			Assert.IsTrue (AssemblyContainsType (linkedApp, "ExternalJavaRoots.RegisteredLayoutOnlyView"), "The XML-only custom view referenced by its explicit Java name should survive linking.");
			Assert.IsFalse (AssemblyContainsType (linkedApp, "ExternalJavaRoots.UnusedView"), "An unreferenced ACW should be trimmed.");

			var javaDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "typemap", "linked-java"));
			Assert.IsNotEmpty (Directory.GetFiles (javaDirectory, "LayoutOnlyView.java", SearchOption.AllDirectories));
			Assert.IsNotEmpty (Directory.GetFiles (javaDirectory, "RegisteredLayoutOnlyView.java", SearchOption.AllDirectories));
			Assert.IsEmpty (Directory.GetFiles (javaDirectory, "UnusedView.java", SearchOption.AllDirectories));
		}

		[Test]
		public void TrimmableTypeMap_PreserveLists_ArePackagedInSdk ()
		{
			foreach (var file in new [] {
				"Trimmable.CoreCLR.xml",
				"System.Private.CoreLib.xml",
			}) {
				var path = Path.Combine (TestEnvironment.DotNetPreviewAndroidSdkDirectory, "PreserveLists", file);
				FileAssert.Exists (path, $"{path} should exist in the SDK pack.");
			}
		}

		[Test]
		public void TrimmableTypeMap_RuntimeArtifacts_ArePackagedInSdk ()
		{
			var toolsDir = TestEnvironment.AndroidMSBuildDirectory;

			foreach (var file in new [] {
				"java_runtime.jar",
				"java_runtime.dex",
				"java_runtime_fastdev.jar",
				"java_runtime_fastdev.dex",
				"java_runtime_trimmable.jar",
				"java_runtime_trimmable.dex",
				"java_runtime_clr.jar",
				"java_runtime_clr.dex",
				"java_runtime_fastdev_clr.jar",
				"java_runtime_fastdev_clr.dex",
			}) {
				FileAssert.Exists (Path.Combine (toolsDir, file), $"{file} should exist in the SDK pack.");
			}
		}

		// T1: end-to-end build coverage for [Export] and [ExportField] under trimmable.
		// The trimmable typemap path emits a per-assembly typemap DLL and JCW Java
		// sources for user peer types. This test confirms that, for a project that
		// uses both [Export] (instance method) and [ExportField] (static getter),
		// the JCW Java file the build generates contains the expected `native`
		// method declaration AND a static field declaration referencing the field
		// initializer method. If either side regresses, the runtime would silently
		// fail to wire up the user's exports.
		[Test]
		public void Build_WithExportAndExportField_GeneratesJcwAndTypeMap ()
		{
			const AndroidRuntime runtime = AndroidRuntime.CoreCLR;

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = false,
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
				},
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");
			proj.Sources.Add (new BuildItem.Source ("ExportShapes.cs") {
				TextContent = () => @"using System;
using Java.Interop;

namespace UnnamedProject {
	class ExportShapes : Java.Lang.Object {
		[Export]
		public string EchoString (string x) => ""<"" + x + "">"";

		[ExportField (""FOO"")]
		public static int InitialFoo () => 42;
	}
}"
			});

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var javaDir = Path.Combine (builder.Output.GetIntermediaryPath ("typemap"), "java");
			DirectoryAssert.Exists (javaDir, "Trimmable JCW Java output directory should exist.");

			var allJavaFiles = Directory.GetFiles (javaDir, "*.java", SearchOption.AllDirectories);
			Assert.IsNotEmpty (allJavaFiles, "At least one JCW Java source file should be generated.");

			// The JCW Java file for ExportShapes lives under a crc64<hash>/ECDH directory
			// matching the CRC64 hash of the type. Search by content (one of the method
			// names that must appear in the generated source) rather than by filename
			// to avoid coupling to the hash.
			string? exportShapesJava = null;
			string? exportShapesText = null;
			foreach (var f in allJavaFiles) {
				var text = File.ReadAllText (f);
				if (text.Contains ("EchoString") && text.Contains ("InitialFoo")) {
					exportShapesJava = f;
					exportShapesText = text;
					break;
				}
			}
			Assert.IsNotNull (exportShapesJava,
				$"Could not find a generated JCW Java file referencing both EchoString and InitialFoo under {javaDir}.");
			Assert.IsNotNull (exportShapesText,
				$"Could not find a generated JCW Java file referencing both EchoString and InitialFoo under {javaDir}.");
			var javaText = exportShapesText;

			// [Export] EchoString — Java side must declare a `native` method matching
			// the C# signature (String -> String). The trimmable emitter generates
			// `public native` for instance [Export] methods.
			StringAssert.Contains ("native", javaText,
				"Generated JCW should contain a native method declaration for [Export].");
			StringAssert.Contains ("EchoString", javaText,
				"Generated JCW should contain the [Export] method name.");

			// [ExportField] FOO — Java side must declare a static field initialized
			// by calling the C# initializer method (`InitialFoo`). Without this,
			// the [ExportField] is silently dropped and Java callers see no FOO.
			StringAssert.Contains ("FOO", javaText,
				"Generated JCW should contain the [ExportField] declaration.");
			StringAssert.Contains ("InitialFoo", javaText,
				"Generated JCW should reference the [ExportField] initializer method.");

			// A per-assembly typemap DLL should be present (named after the app
			// assembly + .TypeMap suffix). We only check that *some* user typemap
			// assembly was produced — the exact name varies based on app assembly.
			var typemapDir = builder.Output.GetIntermediaryPath ("typemap");
			var typemapDlls = Directory.GetFiles (typemapDir, "*.TypeMap.dll");
			Assert.IsNotEmpty (typemapDlls, "Trimmable typemap should produce at least one *.TypeMap.dll.");
		}

		// T6: trim-warning baseline for [Export] under trimmable.
		// The trimmable [Export] code generator emits IL that reaches into
		// Mono.Android via [IgnoresAccessChecksTo] and dispatches through
		// member references built from System.Reflection.Metadata. If the
		// emitter starts producing reflection-style patterns that the trim
		// analyzer cannot track (e.g. missing [DynamicallyAccessedMembers] on
		// helper signatures), IL2xxx / IL3xxx warnings will appear pointing
		// at the generated `_<App>.TypeMap.dll` or at the user's [Export]
		// source. The baseline is: zero such warnings reference either of
		// those locations. This is a targeted assertion (not a full no-IL-warnings
		// guarantee), so unrelated framework warnings don't fail the test.
		[Test]
		public void Build_WithExport_ProducesNoTrimWarningsTargetingExportCodegen ()
		{
			const AndroidRuntime runtime = AndroidRuntime.CoreCLR;

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
				},
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");
			proj.SetProperty ("TrimMode", "full");
			proj.SetProperty ("TrimmerSingleWarn", "false");
			proj.Sources.Add (new BuildItem.Source ("ExportShapes.cs") {
				TextContent = () => @"using System;
using Java.Interop;

namespace UnnamedProject {
	class ExportShapes : Java.Lang.Object {
		[Export]
		public string EchoString (string x) => ""<"" + x + "">"";

		[ExportField (""FOO"")]
		public static int InitialFoo () => 42;
	}
}"
			});

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			// Match actual IL2xxx and IL3xxx warning lines (trim + AOT analysis), then
			// keep only those whose message text references either the generated
			// trimmable typemap assembly or the [Export] source file.
			// The regex requires ": warning IL" to avoid matching CSC command lines
			// that mention IL codes in /nowarn switches.
			// Exclude IL2026 about ExportAttribute/ExportFieldAttribute constructors
			// themselves — those are expected (the attributes carry [RequiresUnreferencedCode]).
			var ilWarningRegex = new Regex (@":\s*warning\s+(IL[23]\d{3})\b", RegexOptions.Compiled);
			var offending = new List<string> ();
			foreach (var line in builder.LastBuildOutput) {
				if (!ilWarningRegex.IsMatch (line)) {
					continue;
				}
				if (line.Contains ("ExportAttribute", StringComparison.Ordinal)
						&& line.Contains ("RequiresUnreferencedCode", StringComparison.Ordinal)) {
					continue;
				}
				bool mentionsTypeMap = line.Contains (".TypeMap.dll", StringComparison.OrdinalIgnoreCase)
					|| line.Contains ("_Microsoft.Android.TypeMaps", StringComparison.OrdinalIgnoreCase);
				bool mentionsExportSource = line.Contains ("ExportShapes.cs", StringComparison.OrdinalIgnoreCase);
				if (mentionsTypeMap || mentionsExportSource) {
					offending.Add (line.Trim ());
				}
			}

			Assert.IsEmpty (offending,
				"Trimmable [Export] codegen should not introduce IL2xxx / IL3xxx warnings against the generated typemap " +
				"assembly or the user's [Export] source. Offending warning lines:\n  " +
				string.Join ("\n  ", offending));
		}

		[Test]
		public void Build_WithTrimmableTypeMap_AbstractTypeWithProtectedCtor_Succeeds ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.NativeAOT, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");
			proj.Sources.Add (new BuildItem.Source ("AbstractProvider.cs") {
				TextContent = () => @"
namespace UnnamedProject {
	public abstract class AbstractProvider : Java.Lang.Object {
		protected AbstractProvider (Android.Content.Context context) { }
		public abstract string GetData ();
	}

	public class ConcreteProvider : AbstractProvider {
		public ConcreteProvider (Android.Content.Context context) : base (context) { }
		public override string GetData () => ""hello"";
	}
}"
			});

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded — abstract types with protected ctors should not cause XAGTT7009.");
		}

		static void AssertTrimmableTypeMapOutputs (string typemapDir)
		{
			DirectoryAssert.Exists (typemapDir);
			FileAssert.Exists (Path.Combine (typemapDir, "_Microsoft.Android.TypeMaps.dll"));
			FileAssert.Exists (Path.Combine (typemapDir, "_Mono.Android.TypeMap.dll"));

			var javaDir = Path.Combine (typemapDir, "java");
			DirectoryAssert.Exists (javaDir, "Trimmable JCW Java output directory should exist.");

			var javaFiles = Directory.GetFiles (javaDir, "*.java", SearchOption.AllDirectories);
			Assert.IsNotEmpty (javaFiles, "At least one trimmable JCW Java source file should be generated.");
		}

		static bool AssemblyContainsType (string assemblyPath, string typeFullName)
		{
			if (!File.Exists (assemblyPath)) {
				return false;
			}

			using var assembly = AssemblyDefinition.ReadAssembly (assemblyPath);
			return ContainsType (assembly.MainModule.Types, typeFullName);
		}

		static bool ContainsType (IEnumerable<TypeDefinition> types, string typeFullName)
		{
			foreach (var type in types) {
				if (type.FullName == typeFullName || ContainsType (type.NestedTypes, typeFullName)) {
					return true;
				}
			}

			return false;
		}

		static void AssertFileContains (string path, string value, bool expected, string description)
		{
			FileAssert.Exists (path, $"{description} should exist.");
			var contents = File.ReadAllText (path);
			Assert.AreEqual (
				expected,
				contents.Contains (value, StringComparison.Ordinal),
				$"{description} should {(expected ? "contain" : "exclude")} '{value}'.");
		}

		DynamicCodeSupportProfile BuildDynamicCodeSupportProfile (string typemapImplementation, bool? dynamicCodeSupport)
		{
			var dynamicCodeSuffix = dynamicCodeSupport.HasValue ? $"_{dynamicCodeSupport.Value.ToString ().ToLowerInvariant ()}" : "";
			var projectName = $"DynamicCodeSupport_{typemapImplementation.Replace ("-", "_")}{dynamicCodeSuffix}";
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				PackageName = "com.xamarin.dynamiccodesupport",
				ProjectName = projectName,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty (KnownProperties.RuntimeIdentifier, "android-arm64");
			proj.SetProperty ("AndroidPackageFormat", "apk");
			proj.SetProperty (KnownProperties.AndroidLinkTool, "r8");
			proj.SetProperty ("TrimMode", "full");
			proj.SetProperty ("PublishReadyToRun", "false");
			proj.SetProperty ("AndroidTypeMapImplementation", typemapImplementation);
			if (dynamicCodeSupport.HasValue) {
				proj.SetProperty ("DynamicCodeSupport", dynamicCodeSupport.Value.ToString ().ToLowerInvariant ());
			}

			using var builder = CreateApkBuilder (Path.Combine ("temp", $"{projectName}_{Guid.NewGuid ():N}"));
			Assert.IsTrue (builder.Build (proj), $"{typemapImplementation} build should have succeeded.");

			var runtimeConfigPath = FindOutputFile (builder, proj, $"{proj.ProjectName}.runtimeconfig.json");
			return new DynamicCodeSupportProfile (File.ReadAllText (runtimeConfigPath));
		}

		ISet<string> ReadPackagedManagedAssemblyNames (string apkPath, AndroidTargetArch targetArch)
		{
			(var explorers, var errorMessage) = AssemblyStoreExplorer.Open (apkPath);
			Assert.IsNull (errorMessage, $"{apkPath} should contain readable assembly stores.");
			Assert.IsNotNull (explorers, $"{apkPath} should contain assembly stores.");

			var explorer = explorers.FirstOrDefault (e => e.TargetArch == targetArch);
			Assert.IsNotNull (explorer, $"{apkPath} should contain an {targetArch} assembly store.");

			return explorer.Assemblies
				.Where (a => !a.Ignore && a.Name.EndsWith (".dll", StringComparison.OrdinalIgnoreCase) && !a.Name.EndsWith (".ni.dll", StringComparison.OrdinalIgnoreCase))
				.Select (a => a.Name)
				.ToHashSet (StringComparer.Ordinal);
		}

		void AssertPostTrimR8InputsExcludeDeadFrameworkImplementor (string dexFile, string javaSourceDirectory, string acwMapPath, string proguardPrimaryPath)
		{
			const string deadManagedType = "Android.Animation.Animator+IAnimatorListenerImplementor";
			const string deadJavaName = "Lmono/android/animation/Animator_AnimatorListenerImplementor;";
			const string deadJavaDotName = "mono.android.animation.Animator_AnimatorListenerImplementor";

			Assert.IsTrue (
				Directory.EnumerateFiles (javaSourceDirectory, "MainActivity.java", SearchOption.AllDirectories).Any (),
				"Post-trim Java source generation should keep the app activity JCW.");
			FileAssert.DoesNotExist (
				Path.Combine (javaSourceDirectory, "mono", "android", "animation", "Animator_AnimatorListenerImplementor.java"),
				"Post-trim Java source generation should not generate framework listener implementors removed by ILLink.");

			FileAssert.Exists (acwMapPath, "Post-trim scan should rewrite acw-map.txt for R8.");
			var acwMap = File.ReadAllText (acwMapPath);
			Assert.IsFalse (acwMap.Contains (deadManagedType, StringComparison.Ordinal), $"{acwMapPath} should be based on linked assemblies.");
			Assert.IsFalse (acwMap.Contains (deadJavaDotName, StringComparison.Ordinal), $"{acwMapPath} should not keep removed framework listener implementors.");

			FileAssert.Exists (proguardPrimaryPath, "R8 should generate a primary proguard configuration from the post-trim acw-map.");
			Assert.IsFalse (
				File.ReadAllText (proguardPrimaryPath).Contains (deadJavaDotName, StringComparison.Ordinal),
				$"{proguardPrimaryPath} should not keep removed framework listener implementors.");

			FileAssert.Exists (dexFile, "R8 should produce classes.dex.");
			Assert.IsFalse (
				DexUtils.ContainsClass (deadJavaName, dexFile, AndroidSdkPath),
				$"{dexFile} should not contain the removed framework listener implementor.");
		}

		string FindOutputFile (ProjectBuilder builder, XamarinAndroidApplicationProject proj, string fileName)
		{
			var outputDirectory = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath);
			var files = Directory.GetFiles (outputDirectory, fileName, SearchOption.AllDirectories);
			Assert.AreEqual (1, files.Length, $"{outputDirectory} should contain one {fileName}.");
			return files [0];
		}

		bool IsTypeMapAssemblyPath (string file)
		{
			return IsTypeMapAssemblyName (Path.GetFileName (file));
		}

		bool IsTypeMapAssemblyName (string fileName)
		{
			return fileName.EndsWith (".TypeMap.dll", StringComparison.Ordinal) ||
				fileName.StartsWith ("_Microsoft.Android.TypeMap", StringComparison.Ordinal);
		}

		static bool FileContentsAreEqual (string first, string second)
		{
			return ComputeFileHash (first).SequenceEqual (ComputeFileHash (second));
		}

		static byte [] ComputeFileHash (string path)
		{
			using var stream = File.OpenRead (path);
			return ComputeHash (stream);
		}

		static byte [] ComputeHash (Stream stream)
		{
			return SHA256.HashData (stream);
		}

		sealed record DynamicCodeSupportProfile (string RuntimeConfig);
	}
}
