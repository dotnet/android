using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using NUnit.Framework;

using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("UsesDevice")]
	public class InterfaceCollectionTests : DeviceTest
	{
		const string ResultPrefix = "INTERFACE_COLLECTION_RESULT";

		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR)]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR)]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT)]
		public void InterfaceValuedJavaCollections (string typemapImplementation, AndroidRuntime runtime)
		{
			var suffix = $"interfacecollections{typemapImplementation.Replace ("-", "")}{runtime}".ToLowerInvariant ();
			var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime, suffix)) {
				IsRelease = true,
			};
			proj.SetRuntime (runtime);
			proj.SetRuntimeIdentifiers ([DeviceAbi]);
			proj.SetProperty ("AndroidTypeMapImplementation", typemapImplementation);
			proj.SetProperty ("AndroidSdkDirectory", AndroidSdkResolver.GetAndroidSdkPath ());
			var javaSdkDirectory = AndroidSdkResolver.GetJavaSdkPath ();
			proj.SetProperty ("JavaSdkDirectory", javaSdkDirectory);
			proj.SetProperty ("JavaCPath", Path.Combine (javaSdkDirectory, "bin", "javac"));
			proj.SetProperty ("JarPath", Path.Combine (javaSdkDirectory, "bin", "jar"));
			proj.SetDefaultTargetDevice ();
			proj.MainActivity = proj.ProcessSourceTemplate (ReadFixture ("MainActivity.cs"));
			proj.AndroidJavaSources.Add (CreateJavaSource ("ValueProvider.java", bind: true));
			proj.AndroidJavaSources.Add (CreateJavaSource ("ExtendedValueProvider.java", bind: true));
			proj.AndroidJavaSources.Add (CreateJavaSource ("InterfaceCollectionFixture.java", bind: false));
			proj.OtherBuildItems.Add (new AndroidItem.ProguardConfiguration ("proguard.cfg") {
				TextContent = () => ReadFixture ("proguard.cfg"),
			});

			var testDirectory = Path.Combine ("temp", $"{nameof (InterfaceValuedJavaCollections)}-{typemapImplementation}-{runtime}");
			using var builder = CreateApkBuilder (testDirectory);
			try {
				Assert.IsTrue (builder.Install (proj), "The focused interface-collection app should install.");
				AssertGeneratedBindingsAreIsolated (builder, proj);

				ClearAdbLogcat ();
				var logcatPath = Path.Combine (Root, builder.ProjectDirectory, "interface-collections-logcat.log");
				StartActivityAndAssert (proj);
				string logcatOutput = "";
				string resultLine = "";
				WaitFor (TimeSpan.FromSeconds (60), () => {
					logcatOutput = RunAdbCommand ("logcat -d");
					resultLine = FindResultLine (logcatOutput);
					return resultLine.Length > 0;
				}, intervalInMS: 250);
				File.WriteAllText (logcatPath, logcatOutput);
				Assert.IsNotEmpty (resultLine, $"The focused app did not report a result. See '{logcatPath}'.");
				StringAssert.Contains ($"{ResultPrefix} PASS 6/6", resultLine);

				if (runtime == AndroidRuntime.NativeAOT) {
					var projectDirectory = Path.Combine (Root, builder.ProjectDirectory);
					var dgmlFiles = Directory.GetFiles (projectDirectory, $"{proj.ProjectName}.scan.dgml.xml", SearchOption.AllDirectories);
					Assert.AreEqual (1, dgmlFiles.Length, "The focused NativeAOT app should produce one scan dependency graph.");
					AssertCanonicalWrapperRooting (dgmlFiles [0]);
					TestContext.Out.WriteLine ($"Focused NativeAOT dependency graph: {dgmlFiles [0]}");
				}
			} finally {
				RunAdbCommand ($"uninstall {proj.PackageName}");
			}
		}

		static string FindResultLine (string logcatOutput)
		{
			foreach (var line in logcatOutput.Split (['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)) {
				if (line.Contains (ResultPrefix, StringComparison.Ordinal)) {
					return line;
				}
			}
			return "";
		}

		static AndroidItem.AndroidJavaSource CreateJavaSource (string fileName, bool bind)
		{
			return new AndroidItem.AndroidJavaSource (Path.Combine ("java", "net", "dot", "android", "test", fileName)) {
				Encoding = Encoding.ASCII,
				TextContent = () => ReadFixture (fileName),
				Metadata = {
					{ "Bind", bind.ToString () },
				},
			};
		}

		void AssertGeneratedBindingsAreIsolated (ProjectBuilder builder, XamarinAndroidApplicationProject proj)
		{
			var projectDirectory = Path.Combine (Root, builder.ProjectDirectory);
			var generatedSourceDirectory = Path.Combine (projectDirectory, proj.IntermediateOutputPath, "generated", "src");
			FileAssert.Exists (Path.Combine (generatedSourceDirectory, "Net.Dot.Android.Test.IValueProvider.cs"));
			FileAssert.Exists (Path.Combine (generatedSourceDirectory, "Net.Dot.Android.Test.IExtendedValueProvider.cs"));
			Assert.IsEmpty (
				Directory.GetFiles (generatedSourceDirectory, "*InterfaceCollection*.cs", SearchOption.TopDirectoryOnly),
				"The raw JNI holder and concrete peers must not produce managed bindings that can root closed collection wrappers.");
		}

		static void AssertCanonicalWrapperRooting (string dgmlFile)
		{
			var chains = new [] {
				new RootingChain (
					"JavaList",
					"SafeJavaCollectionFactory__CreateReferenceListFromJniHandle, Type metadata: [Java.Interop]Java.Interop.IJavaPeerable)",
					"Android_Runtime_JavaList_1&lt;Java_Interop_Java_Interop_IJavaPeerable&gt; constructed\"",
					"Label=\"__GenericDict_Mono_Android_Android_Runtime_JavaList_1&lt;Java_Interop_Java_Interop_IJavaPeerable&gt;\"",
					"(__GenericDict_Mono_Android_Android_Runtime_JavaList_1&lt;Java_Interop_Java_Interop_IJavaPeerable&gt;, " +
						"Mono_Android_Android_Runtime_JavaList_1&lt;System___Canon&gt;___ctor_0)",
					"Label=\"Mono_Android_Android_Runtime_JavaList_1&lt;System___Canon&gt;___ctor_0\"",
					"JavaList`1&lt;Java.Interop.IJavaPeerable&gt;..ctor(native int,JniHandleOwnership)"),
				new RootingChain (
					"JavaCollection",
					"SafeJavaCollectionFactory__CreateReferenceCollectionFromJniHandle, Type metadata: [Java.Interop]Java.Interop.IJavaPeerable)",
					"Android_Runtime_JavaCollection_1&lt;Java_Interop_Java_Interop_IJavaPeerable&gt; constructed\"",
					"Label=\"__GenericDict_Mono_Android_Android_Runtime_JavaCollection_1&lt;Java_Interop_Java_Interop_IJavaPeerable&gt;\"",
					"(__GenericDict_Mono_Android_Android_Runtime_JavaCollection_1&lt;Java_Interop_Java_Interop_IJavaPeerable&gt;, " +
						"Mono_Android_Android_Runtime_JavaCollection_1&lt;System___Canon&gt;___ctor)",
					"Label=\"Mono_Android_Android_Runtime_JavaCollection_1&lt;System___Canon&gt;___ctor\"",
					"JavaCollection`1&lt;Java.Interop.IJavaPeerable&gt;..ctor(native int,JniHandleOwnership)"),
				new RootingChain (
					"JavaDictionary",
					"Label=\"Mono_Android_Java_Interop_SafeJavaCollectionFactory__CreateReferenceDictionaryFromJniHandle\"",
					"Android_Runtime_JavaDictionary_2&lt;Java_Interop_Java_Interop_IJavaPeerable__Java_Interop_Java_Interop_IJavaPeerable&gt; constructed\"",
					"Label=\"__GenericDict_Mono_Android_Android_Runtime_JavaDictionary_2&lt;Java_Interop_Java_Interop_IJavaPeerable__Java_Interop_Java_Interop_IJavaPeerable&gt;\"",
					"(__GenericDict_Mono_Android_Android_Runtime_JavaDictionary_2&lt;Java_Interop_Java_Interop_IJavaPeerable__" +
						"Java_Interop_Java_Interop_IJavaPeerable&gt;, " +
						"Mono_Android_Android_Runtime_JavaDictionary_2&lt;System___Canon__System___Canon&gt;___ctor_0)",
					"Label=\"Mono_Android_Android_Runtime_JavaDictionary_2&lt;System___Canon__System___Canon&gt;___ctor_0\"",
					"JavaDictionary`2&lt;Java.Interop.IJavaPeerable,Java.Interop.IJavaPeerable&gt;..ctor(native int,JniHandleOwnership)"),
			};
			var unexpectedCanonicalRoots = new List<string> ();

			foreach (var line in File.ReadLines (dgmlFile)) {
				if (line.Contains ("<Node ", StringComparison.Ordinal)) {
					foreach (var chain in chains) {
						chain.ObserveNode (line);
					}
					if (IsUnexpectedCanonicalReferenceConstructor (line)) {
						unexpectedCanonicalRoots.Add (line.Trim ());
					}
				} else if (line.Contains ("<Link ", StringComparison.Ordinal)) {
					foreach (var chain in chains) {
						chain.ObserveLink (line);
					}
				}
			}

			Assert.IsEmpty (
				unexpectedCanonicalRoots,
				"Only SafeJavaCollectionFactory's IJavaPeerable instantiations should root the reference-wrapper canonical constructors.");
			foreach (var chain in chains) {
				chain.AssertComplete ();
				TestContext.Out.WriteLine ($"{chain.Name} canonical constructor rooted through SafeJavaCollectionFactory.");
			}
		}

		static bool IsUnexpectedCanonicalReferenceConstructor (string line)
		{
			if (!line.Contains ("..ctor(native int,JniHandleOwnership) backed by ", StringComparison.Ordinal)) {
				return false;
			}
			bool usesReferenceCanonicalCode =
				line.Contains ("JavaList_1&lt;System___Canon&gt;___ctor_0", StringComparison.Ordinal) ||
				line.Contains ("JavaCollection_1&lt;System___Canon&gt;___ctor", StringComparison.Ordinal) ||
				line.Contains ("JavaDictionary_2&lt;System___Canon__System___Canon&gt;___ctor_0", StringComparison.Ordinal);
			if (!usesReferenceCanonicalCode) {
				return false;
			}
			bool isExpectedRoot =
				line.Contains ("JavaList`1&lt;Java.Interop.IJavaPeerable&gt;..ctor(native int,JniHandleOwnership)", StringComparison.Ordinal) ||
				line.Contains ("JavaList`1&lt;System.__Canon&gt;..ctor(native int,JniHandleOwnership)", StringComparison.Ordinal) ||
				line.Contains ("JavaCollection`1&lt;Java.Interop.IJavaPeerable&gt;..ctor(native int,JniHandleOwnership)", StringComparison.Ordinal) ||
				line.Contains ("JavaCollection`1&lt;System.__Canon&gt;..ctor(native int,JniHandleOwnership)", StringComparison.Ordinal) ||
				line.Contains (
					"JavaDictionary`2&lt;Java.Interop.IJavaPeerable,Java.Interop.IJavaPeerable&gt;..ctor(native int,JniHandleOwnership)",
					StringComparison.Ordinal) ||
				line.Contains (
					"JavaDictionary`2&lt;System.__Canon,System.__Canon&gt;..ctor(native int,JniHandleOwnership)",
					StringComparison.Ordinal);
			return !isExpectedRoot;
		}

		static string ReadFixture (string fileName)
		{
			return File.ReadAllText (
				Path.Combine (
					XABuildPaths.TopDirectory,
					"tests",
					"MSBuildDeviceIntegration",
					"Resources",
					"InterfaceCollectionApp",
					fileName));
		}

		sealed class RootingChain
		{
			readonly string constructorPattern;
			readonly string canonicalConstructorPattern;
			readonly string constructedTypePattern;
			readonly string genericDictionaryPattern;
			readonly string genericDictionaryDependencyPattern;
			readonly string sourcePattern;
			readonly List<string> unexpectedIncomingLinks = new ();

			string canonicalConstructorId = "";
			string constructedTypeId = "";
			string constructorId = "";
			string genericDictionaryId = "";
			string genericDictionaryDependencyId = "";
			string sourceId = "";
			bool canonicalConstructorToDependency;
			bool constructedTypeToGenericDictionary;
			bool genericDictionaryToDependency;
			bool genericDictionaryToConstructor;
			bool sourceToConstructedType;

			public RootingChain (
				string name,
				string sourcePattern,
				string constructedTypePattern,
				string genericDictionaryPattern,
				string genericDictionaryDependencyPattern,
				string canonicalConstructorPattern,
				string constructorPattern)
			{
				Name = name;
				this.sourcePattern = sourcePattern;
				this.constructedTypePattern = constructedTypePattern;
				this.genericDictionaryPattern = genericDictionaryPattern;
				this.genericDictionaryDependencyPattern = genericDictionaryDependencyPattern;
				this.canonicalConstructorPattern = canonicalConstructorPattern;
				this.constructorPattern = constructorPattern;
			}

			public string Name { get; }

			public void ObserveNode (string line)
			{
				if (line.Contains (sourcePattern, StringComparison.Ordinal)) {
					sourceId = GetAttribute (line, "Id");
				} else if (line.Contains (constructedTypePattern, StringComparison.Ordinal)) {
					constructedTypeId = GetAttribute (line, "Id");
				} else if (line.Contains (genericDictionaryPattern, StringComparison.Ordinal)) {
					genericDictionaryId = GetAttribute (line, "Id");
				} else if (line.Contains (genericDictionaryDependencyPattern, StringComparison.Ordinal)) {
					genericDictionaryDependencyId = GetAttribute (line, "Id");
				} else if (line.Contains (canonicalConstructorPattern, StringComparison.Ordinal)) {
					canonicalConstructorId = GetAttribute (line, "Id");
				} else if (line.Contains (constructorPattern, StringComparison.Ordinal)) {
					constructorId = GetAttribute (line, "Id");
				}
			}

			public void ObserveLink (string line)
			{
				sourceToConstructedType |= IsLink (line, sourceId, constructedTypeId, "newobj");
				constructedTypeToGenericDictionary |= IsLink (line, constructedTypeId, genericDictionaryId, "reloc");
				genericDictionaryToDependency |= IsLink (line, genericDictionaryId, genericDictionaryDependencyId, "Primary");
				canonicalConstructorToDependency |= IsLink (
					line,
					canonicalConstructorId,
					genericDictionaryDependencyId,
					"Secondary");
				genericDictionaryToConstructor |= IsLink (
					line,
					genericDictionaryDependencyId,
					constructorId,
					"Generic dictionary dependency");

				RejectUnexpectedIncoming (line, constructedTypeId, sourceId, "newobj");
				RejectUnexpectedIncoming (line, genericDictionaryId, constructedTypeId, "reloc");
				if (IsIncomingLink (line, genericDictionaryDependencyId) &&
						!IsLink (line, genericDictionaryId, genericDictionaryDependencyId, "Primary") &&
						!IsLink (line, canonicalConstructorId, genericDictionaryDependencyId, "Secondary")) {
					unexpectedIncomingLinks.Add (line.Trim ());
				}
				RejectUnexpectedIncoming (
					line,
					constructorId,
					genericDictionaryDependencyId,
					"Generic dictionary dependency");
			}

			public void AssertComplete ()
			{
				Assert.IsNotEmpty (sourceId, $"{Name} SafeJavaCollectionFactory source node was not found.");
				Assert.IsNotEmpty (constructedTypeId, $"{Name} IJavaPeerable constructed-type node was not found.");
				Assert.IsNotEmpty (genericDictionaryId, $"{Name} IJavaPeerable generic dictionary node was not found.");
				Assert.IsNotEmpty (genericDictionaryDependencyId, $"{Name} IJavaPeerable constructor dictionary dependency was not found.");
				Assert.IsNotEmpty (canonicalConstructorId, $"{Name} canonical compiled constructor node was not found.");
				Assert.IsNotEmpty (constructorId, $"{Name} IJavaPeerable activation constructor node was not found.");
				Assert.IsTrue (sourceToConstructedType, $"{Name} SafeJavaCollectionFactory newobj dependency was not found.");
				Assert.IsTrue (constructedTypeToGenericDictionary, $"{Name} constructed-type relocation dependency was not found.");
				Assert.IsTrue (genericDictionaryToDependency, $"{Name} generic dictionary primary dependency was not found.");
				Assert.IsTrue (canonicalConstructorToDependency, $"{Name} canonical constructor secondary dependency was not found.");
				Assert.IsTrue (genericDictionaryToConstructor, $"{Name} generic dictionary constructor dependency was not found.");
				Assert.IsEmpty (unexpectedIncomingLinks, $"{Name} canonical constructor path had an unexpected incoming dependency.");
			}

			void RejectUnexpectedIncoming (string line, string target, string expectedSource, string expectedReason)
			{
				if (IsIncomingLink (line, target) && !IsLink (line, expectedSource, target, expectedReason)) {
					unexpectedIncomingLinks.Add (line.Trim ());
				}
			}

			static bool IsIncomingLink (string line, string target)
			{
				return target.Length > 0 && line.Contains ($"Target=\"{target}\"", StringComparison.Ordinal);
			}

			static bool IsLink (string line, string source, string target, string reason)
			{
				return source.Length > 0 &&
					target.Length > 0 &&
					line.Contains ($"Source=\"{source}\"", StringComparison.Ordinal) &&
					line.Contains ($"Target=\"{target}\"", StringComparison.Ordinal) &&
					line.Contains ($"Reason=\"{reason}\"", StringComparison.Ordinal);
			}

			static string GetAttribute (string line, string name)
			{
				var prefix = $"{name}=\"";
				int start = line.IndexOf (prefix, StringComparison.Ordinal);
				if (start < 0) {
					return "";
				}
				start += prefix.Length;
				int end = line.IndexOf ('"', start);
				return end < 0 ? "" : line.Substring (start, end - start);
			}
		}
	}
}
