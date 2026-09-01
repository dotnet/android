using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

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
		const string DgmlNamespace = "http://schemas.microsoft.com/vs/2009/dgml";
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
			var resultToken = Guid.NewGuid ().ToString ("N");
			proj.MainActivity = proj.ProcessSourceTemplate (
				ReadFixture ("MainActivity.cs").Replace ("${RESULT_TOKEN}", resultToken, StringComparison.Ordinal));
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
					resultLine = FindResultLine (logcatOutput, resultToken);
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

		static string FindResultLine (string logcatOutput, string resultToken)
		{
			foreach (var line in logcatOutput.Split (['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)) {
				if (line.Contains (ResultPrefix, StringComparison.Ordinal) &&
						line.Contains (resultToken, StringComparison.Ordinal)) {
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
					"SafeJavaCollectionFactory__CreateReferenceListFromJniHandle, " +
						"Type metadata: [Java.Interop]Java.Interop.IJavaPeerable)",
					"Mono_Android_Android_Runtime_JavaList_1<Java_Interop_Java_Interop_IJavaPeerable> constructed",
					"__GenericDict_Mono_Android_Android_Runtime_JavaList_1<Java_Interop_Java_Interop_IJavaPeerable>",
					"(__GenericDict_Mono_Android_Android_Runtime_JavaList_1<Java_Interop_Java_Interop_IJavaPeerable>, " +
						"Mono_Android_Android_Runtime_JavaList_1<System___Canon>___ctor_0)",
					"Mono_Android_Android_Runtime_JavaList_1<System___Canon>___ctor_0",
					"JavaList`1<Java.Interop.IJavaPeerable>..ctor(native int,JniHandleOwnership)"),
				new RootingChain (
					"JavaCollection",
					"SafeJavaCollectionFactory__CreateReferenceCollectionFromJniHandle, " +
						"Type metadata: [Java.Interop]Java.Interop.IJavaPeerable)",
					"Mono_Android_Android_Runtime_JavaCollection_1<Java_Interop_Java_Interop_IJavaPeerable> constructed",
					"__GenericDict_Mono_Android_Android_Runtime_JavaCollection_1<Java_Interop_Java_Interop_IJavaPeerable>",
					"(__GenericDict_Mono_Android_Android_Runtime_JavaCollection_1<Java_Interop_Java_Interop_IJavaPeerable>, " +
						"Mono_Android_Android_Runtime_JavaCollection_1<System___Canon>___ctor)",
					"Mono_Android_Android_Runtime_JavaCollection_1<System___Canon>___ctor",
					"JavaCollection`1<Java.Interop.IJavaPeerable>..ctor(native int,JniHandleOwnership)"),
				new RootingChain (
					"JavaDictionary",
					"SafeJavaCollectionFactory__CreateReferenceDictionaryFromJniHandle, " +
						"Type metadata: [Java.Interop]Java.Interop.IJavaPeerable)",
					"Mono_Android_Android_Runtime_JavaDictionary_2<Java_Interop_Java_Interop_IJavaPeerable__" +
						"Java_Interop_Java_Interop_IJavaPeerable> constructed",
					"__GenericDict_Mono_Android_Android_Runtime_JavaDictionary_2<Java_Interop_Java_Interop_IJavaPeerable__" +
						"Java_Interop_Java_Interop_IJavaPeerable>",
					"(__GenericDict_Mono_Android_Android_Runtime_JavaDictionary_2<Java_Interop_Java_Interop_IJavaPeerable__" +
						"Java_Interop_Java_Interop_IJavaPeerable>, " +
						"Mono_Android_Android_Runtime_JavaDictionary_2<System___Canon__System___Canon>___ctor_0)",
					"Mono_Android_Android_Runtime_JavaDictionary_2<System___Canon__System___Canon>___ctor_0",
					"JavaDictionary`2<Java.Interop.IJavaPeerable,Java.Interop.IJavaPeerable>..ctor(native int,JniHandleOwnership)"),
			};
			var duplicateNodeIds = new List<string> ();
			var missingNodeIds = new List<string> ();
			var nodeIds = new HashSet<string> (StringComparer.Ordinal);
			var unexpectedCanonicalRoots = new List<string> ();

			using (var reader = CreateDgmlReader (dgmlFile)) {
				while (reader.Read ()) {
					if (reader.NodeType != XmlNodeType.Element ||
							reader.LocalName != "Node" ||
							reader.NamespaceURI != DgmlNamespace) {
						continue;
					}
					var id = reader.GetAttribute ("Id") ?? "";
					var label = reader.GetAttribute ("Label") ?? "";
					if (id.Length == 0) {
						missingNodeIds.Add (label);
					} else if (!nodeIds.Add (id)) {
						duplicateNodeIds.Add ($"Id=\"{id}\" Label=\"{label}\"");
					}
					foreach (var chain in chains) {
						chain.ObserveNode (id, label);
					}
					if (IsUnexpectedCanonicalReferenceConstructor (label)) {
						unexpectedCanonicalRoots.Add (label);
					}
				}
			}

			using (var reader = CreateDgmlReader (dgmlFile)) {
				while (reader.Read ()) {
					if (reader.NodeType != XmlNodeType.Element ||
							reader.LocalName != "Link" ||
							reader.NamespaceURI != DgmlNamespace) {
						continue;
					}
					var source = reader.GetAttribute ("Source") ?? "";
					var target = reader.GetAttribute ("Target") ?? "";
					var reason = reader.GetAttribute ("Reason") ?? "";
					foreach (var chain in chains) {
						chain.ObserveLink (source, target, reason);
					}
				}
			}

			Assert.IsEmpty (missingNodeIds, "The NativeAOT dependency graph contained nodes without IDs.");
			Assert.IsEmpty (duplicateNodeIds, "The NativeAOT dependency graph contained duplicate node IDs.");
			Assert.IsEmpty (
				unexpectedCanonicalRoots,
				"Only SafeJavaCollectionFactory's IJavaPeerable instantiations should root the reference-wrapper canonical constructors.");
			foreach (var chain in chains) {
				chain.AssertComplete ();
				TestContext.Out.WriteLine ($"{chain.Name} canonical constructor rooted through SafeJavaCollectionFactory.");
			}
		}

		static XmlReader CreateDgmlReader (string dgmlFile)
		{
			return XmlReader.Create (dgmlFile, new XmlReaderSettings {
				DtdProcessing = DtdProcessing.Prohibit,
				XmlResolver = null,
			});
		}

		static bool IsUnexpectedCanonicalReferenceConstructor (string label)
		{
			if (!label.Contains ("..ctor(native int,JniHandleOwnership) backed by ", StringComparison.Ordinal)) {
				return false;
			}
			bool usesReferenceCanonicalCode =
				label.Contains ("JavaList_1<System___Canon>___ctor_0", StringComparison.Ordinal) ||
				label.Contains ("JavaCollection_1<System___Canon>___ctor", StringComparison.Ordinal) ||
				label.Contains ("JavaDictionary_2<System___Canon__System___Canon>___ctor_0", StringComparison.Ordinal);
			if (!usesReferenceCanonicalCode) {
				return false;
			}
			bool isExpectedRoot =
				label == "[Mono.Android]Android.Runtime.JavaList`1<Java.Interop.IJavaPeerable>..ctor(native int,JniHandleOwnership) " +
					"backed by Mono_Android_Android_Runtime_JavaList_1<System___Canon>___ctor_0" ||
				label == "[Mono.Android]Android.Runtime.JavaList`1<System.__Canon>..ctor(native int,JniHandleOwnership) " +
					"backed by Mono_Android_Android_Runtime_JavaList_1<System___Canon>___ctor_0" ||
				label == "[Mono.Android]Android.Runtime.JavaCollection`1<Java.Interop.IJavaPeerable>..ctor(native int,JniHandleOwnership) " +
					"backed by Mono_Android_Android_Runtime_JavaCollection_1<System___Canon>___ctor" ||
				label == "[Mono.Android]Android.Runtime.JavaCollection`1<System.__Canon>..ctor(native int,JniHandleOwnership) " +
					"backed by Mono_Android_Android_Runtime_JavaCollection_1<System___Canon>___ctor" ||
				label == "[Mono.Android]Android.Runtime.JavaDictionary`2<Java.Interop.IJavaPeerable,Java.Interop.IJavaPeerable>" +
					"..ctor(native int,JniHandleOwnership) backed by " +
					"Mono_Android_Android_Runtime_JavaDictionary_2<System___Canon__System___Canon>___ctor_0" ||
				label == "[Mono.Android]Android.Runtime.JavaDictionary`2<System.__Canon,System.__Canon>" +
					"..ctor(native int,JniHandleOwnership) backed by " +
					"Mono_Android_Android_Runtime_JavaDictionary_2<System___Canon__System___Canon>___ctor_0";
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
			readonly List<string> ambiguousNodeMatches = new ();
			readonly HashSet<string> observedNodeRoles = new (StringComparer.Ordinal);
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

			public void ObserveNode (string id, string label)
			{
				int matchedRoles = 0;
				matchedRoles += ObserveNode (
					label == $"(Mono_Android_Java_Interop_{sourcePattern}",
					id,
					label,
					"SafeJavaCollectionFactory source",
					ref sourceId) ? 1 : 0;
				matchedRoles += ObserveNode (
					IsConstructedTypeLabel (label, constructedTypePattern),
					id,
					label,
					"IJavaPeerable constructed type",
					ref constructedTypeId) ? 1 : 0;
				matchedRoles += ObserveNode (
					label == genericDictionaryPattern,
					id,
					label,
					"IJavaPeerable generic dictionary",
					ref genericDictionaryId) ? 1 : 0;
				matchedRoles += ObserveNode (
					label == genericDictionaryDependencyPattern,
					id,
					label,
					"IJavaPeerable constructor dictionary dependency",
					ref genericDictionaryDependencyId) ? 1 : 0;
				matchedRoles += ObserveNode (
					label == canonicalConstructorPattern,
					id,
					label,
					"canonical compiled constructor",
					ref canonicalConstructorId) ? 1 : 0;
				matchedRoles += ObserveNode (
					label == $"[Mono.Android]Android.Runtime.{constructorPattern} backed by {canonicalConstructorPattern}",
					id,
					label,
					"IJavaPeerable activation constructor",
					ref constructorId) ? 1 : 0;
				if (matchedRoles > 1) {
					ambiguousNodeMatches.Add ($"multiple roles: Id=\"{id}\" Label=\"{label}\"");
				}
			}

			public void ObserveLink (string source, string target, string reason)
			{
				sourceToConstructedType |= IsLink (source, target, reason, sourceId, constructedTypeId, "newobj");
				constructedTypeToGenericDictionary |= IsLink (source, target, reason, constructedTypeId, genericDictionaryId, "reloc");
				genericDictionaryToDependency |= IsLink (
					source,
					target,
					reason,
					genericDictionaryId,
					genericDictionaryDependencyId,
					"Primary");
				canonicalConstructorToDependency |= IsLink (
					source,
					target,
					reason,
					canonicalConstructorId,
					genericDictionaryDependencyId,
					"Secondary");
				genericDictionaryToConstructor |= IsLink (
					source,
					target,
					reason,
					genericDictionaryDependencyId,
					constructorId,
					"Generic dictionary dependency");

				RejectUnexpectedIncoming (source, target, reason, constructedTypeId, sourceId, "newobj");
				RejectUnexpectedIncoming (source, target, reason, genericDictionaryId, constructedTypeId, "reloc");
				if (IsIncomingLink (target, genericDictionaryDependencyId) &&
						!IsLink (source, target, reason, genericDictionaryId, genericDictionaryDependencyId, "Primary") &&
						!IsLink (source, target, reason, canonicalConstructorId, genericDictionaryDependencyId, "Secondary")) {
					unexpectedIncomingLinks.Add (FormatLink (source, target, reason));
				}
				RejectUnexpectedIncoming (
					source,
					target,
					reason,
					constructorId,
					genericDictionaryDependencyId,
					"Generic dictionary dependency");
			}

			public void AssertComplete ()
			{
				Assert.IsEmpty (ambiguousNodeMatches, $"{Name} canonical constructor path had ambiguous node matches.");
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

			bool ObserveNode (bool matches, string id, string label, string role, ref string observedId)
			{
				if (!matches) {
					return false;
				}
				if (!observedNodeRoles.Add (role)) {
					ambiguousNodeMatches.Add ($"{role}: Id=\"{id}\" Label=\"{label}\"");
					return true;
				}
				observedId = id;
				return true;
			}

			void RejectUnexpectedIncoming (
				string source,
				string target,
				string reason,
				string expectedTarget,
				string expectedSource,
				string expectedReason)
			{
				if (IsIncomingLink (target, expectedTarget) &&
						!IsLink (source, target, reason, expectedSource, expectedTarget, expectedReason)) {
					unexpectedIncomingLinks.Add (FormatLink (source, target, reason));
				}
			}

			static bool IsIncomingLink (string actualTarget, string expectedTarget)
			{
				return expectedTarget.Length > 0 && actualTarget == expectedTarget;
			}

			static bool IsLink (
				string actualSource,
				string actualTarget,
				string actualReason,
				string expectedSource,
				string expectedTarget,
				string expectedReason)
			{
				return expectedSource.Length > 0 &&
					expectedTarget.Length > 0 &&
					actualSource == expectedSource &&
					actualTarget == expectedTarget &&
					actualReason == expectedReason;
			}

			static string FormatLink (string source, string target, string reason)
			{
				return $"Source=\"{source}\" Target=\"{target}\" Reason=\"{reason}\"";
			}

			static bool IsConstructedTypeLabel (string label, string constructedTypePattern)
			{
				if (!label.EndsWith (constructedTypePattern, StringComparison.Ordinal)) {
					return false;
				}

				int prefixLength = label.Length - constructedTypePattern.Length;
				if (prefixLength <= "_ZTV".Length ||
						!label.StartsWith ("_ZTV", StringComparison.Ordinal)) {
					return false;
				}
				for (int i = "_ZTV".Length; i < prefixLength; i++) {
					if (label [i] < '0' || label [i] > '9') {
						return false;
					}
				}
				return true;
			}
		}
	}
}
