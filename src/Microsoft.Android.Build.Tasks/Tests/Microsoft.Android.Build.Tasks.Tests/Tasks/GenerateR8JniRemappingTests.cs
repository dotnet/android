#nullable enable


using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Tasks;
using NUnit.Framework;

using Microsoft.Android.Runtime;
using MergeRemapXml = Xamarin.Android.Tasks.MergeRemapXml;

namespace Xamarin.Android.Build.Tests.Tasks
{
	[TestFixture]
	public class GenerateR8JniRemappingTests : BaseTest
	{
		List<BuildErrorEventArgs>? errors;
		List<BuildWarningEventArgs>? warnings;
		MockBuildEngine? engine;
		string? directory;

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

		string TestDirectory => directory ?? throw new AssertionException ("Test directory was not initialized.");
		List<BuildErrorEventArgs> Errors => errors ?? throw new AssertionException ("Error list was not initialized.");
		List<BuildWarningEventArgs> Warnings => warnings ?? throw new AssertionException ("Warning list was not initialized.");

		string Run (string mapping, string? nativeObject = null)
		{
			string mappingFile = Path.Combine (TestDirectory, "mapping.txt");
			string outputFile = Path.Combine (TestDirectory, "r8-remap.xml");
			File.WriteAllText (mappingFile, mapping);
			var task = new GenerateR8JniRemapping {
				BuildEngine = engine,
				MappingFile = mappingFile,
				OutputFile = outputFile,
				NativeAot = nativeObject != null,
				NativeAotObjectFile = nativeObject,
			};

			Assert.IsTrue (task.Execute (), string.Join ("; ", Errors.Select (error => error.Message)));
			return File.ReadAllText (outputFile);
		}

		[Test]
		public void GeneratesRuntimeLookupEntries ()
		{
			string xml = Run ("""
				com.contoso.Peer -> a.b:
				    com.contoso.Peer run(com.contoso.Peer[]) -> c
				    com.contoso.Peer[] peers -> d

				""");

			StringAssert.Contains ("""<replace-type from="com/contoso/Peer" to="a/b" />""", xml);
			StringAssert.Contains ("""<reverse-type from="a/b" to="com/contoso/Peer" />""", xml);
			StringAssert.Contains ("source-method-signature=\"([Lcom/contoso/Peer;)Lcom/contoso/Peer;\"", xml);
			StringAssert.Contains ("target-method-signature=\"([La/b;)La/b;\"", xml);
			StringAssert.Contains ("source-field-signature=\"[Lcom/contoso/Peer;\"", xml);
			StringAssert.Contains ("target-field-signature=\"[La/b;\"", xml);
		}

		[Test]
		public void SkipsAmbiguousReverseTypeForMergedClasses ()
		{
			string xml = Run ("""
				com.contoso.First -> a.b:
				com.contoso.Second -> a.b:

				""");

			StringAssert.DoesNotContain ("reverse-type", xml);
		}

		[TestCase ("method")]
		[TestCase ("field")]
		public void ConflictingMergedMembersFailInsteadOfChoosingOne (string memberKind)
		{
			string memberMappings = memberKind == "method"
				? "    void run(int) -> c\ncom.contoso.Second -> a.b:\n    void run(int) -> d\n"
				: "    int value -> c\ncom.contoso.Second -> a.b:\n    int value -> d\n";
			string mappingFile = Path.Combine (TestDirectory, "mapping.txt");
			string outputFile = Path.Combine (TestDirectory, "output.xml");
			File.WriteAllText (mappingFile, "com.contoso.First -> a.b:\n" + memberMappings);
			var task = new GenerateR8JniRemapping {
				BuildEngine = engine,
				MappingFile = mappingFile,
				OutputFile = outputFile,
			};

			Assert.IsFalse (task.Execute ());
			Assert.That (Errors, Has.Some.Property ("Code").EqualTo ("XA4325"));
			FileAssert.DoesNotExist (outputFile);
		}

		[Test]
		public void NativeAotRetentionFiltersUnusedEntries ()
		{
			string objectFile = WriteNativeObject (["com/contoso/Peer", "run.(I)V"]);
			string xml = Run ("""
				com.contoso.Peer -> a.b:
				    void run(int) -> c
				    void removed() -> d
				com.contoso.Unused -> a.e:

				""", objectFile);

			StringAssert.Contains ("source-method-name=\"run\"", xml);
			StringAssert.DoesNotContain ("removed", xml);
			StringAssert.DoesNotContain ("Unused", xml);
		}

		[TestCase (false, false)]
		[TestCase (true, false)]
		[TestCase (false, true)]
		public void NativeAotNestedNameDoesNotRetainMergedParent (bool utf8, bool commandStream)
		{
			string objectFile = WriteNativeObject (["com/contoso/Peer$Inner"], utf8, commandStream: commandStream);
			string xml = Run ("""
				com.contoso.Peer -> a.b:
				com.contoso.Peer$Inner -> a.b:

				""", objectFile);
			var types = XDocument.Parse (xml).Root?.Elements ("replace-type").ToArray ()
				?? throw new AssertionException ("Generated XML has no root.");
			Assert.AreEqual (1, types.Length, "A substring is not evidence that the parent class survived.");
			Assert.AreEqual ("com/contoso/Peer$Inner", (string?) types [0].Attribute ("from"));
			StringAssert.Contains ("""<reverse-type from="a/b" to="com/contoso/Peer$Inner" />""", xml);
		}

		[TestCase (false, false)]
		[TestCase (true, false)]
		[TestCase (false, true)]
		public void NativeAotRetainsCompleteClassAndDescriptorTokens (bool utf8, bool commandStream)
		{
			string objectFile = WriteNativeObject (
				["com.contoso.Peer$Inner", "run.([Lcom/contoso/Argument;)Lcom/contoso/Result;",
					"(ILcom/contoso/Argument;[[I)Lcom/contoso/Result;",
					"com/contoso/LongerPeer", "othercom/contoso/Suffix", "OtherLcom/contoso/Suffix;",
					"com/contoso/PrefixedExtra"], utf8, commandStream: commandStream);
			string xml = Run ("""
				com.contoso.Peer$Inner -> a.b:
				    com.contoso.Result run(com.contoso.Argument[]) -> c
				com.contoso.Argument -> a.d:
				com.contoso.Result -> a.e:
				com.contoso.Peer -> a.f:
				com.contoso.Suffix -> a.g:
				com.contoso.Prefixed -> a.h:

				""", objectFile);
			var types = XDocument.Parse (xml).Root?.Elements ("replace-type")
				.Select (element => (string?) element.Attribute ("from")).ToArray ()
				?? throw new AssertionException ("Generated XML has no root.");
			CollectionAssert.AreEquivalent (new [] {
				"com/contoso/Peer$Inner", "com/contoso/Argument", "com/contoso/Result",
			}, types);
			StringAssert.Contains ("target-method-signature=\"([La/d;)La/e;\"", xml);
		}

		[TestCase (false, false)]
		[TestCase (true, false)]
		[TestCase (false, true)]
		public void NativeAotTrulyRetainedMergedTypesRemainAmbiguous (bool utf8, bool commandStream)
		{
			string objectFile = WriteNativeObject (["com/contoso/Peer", "com/contoso/Peer$Inner"], utf8, commandStream: commandStream);
			string xml = Run ("""
				com.contoso.Peer -> a.b:
				com.contoso.Peer$Inner -> a.b:

				""", objectFile);
			var root = XDocument.Parse (xml).Root ?? throw new AssertionException ("Generated XML has no root.");
			Assert.AreEqual (2, root.Elements ("replace-type").Count ());
			Assert.IsEmpty (root.Elements ("reverse-type"));
		}

		[TestCase (false, false)]
		[TestCase (true, false)]
		[TestCase (false, true)]
		public void NativeAotRetainsUnicodeClasses (bool utf8, bool commandStream)
		{
			string objectFile = WriteNativeObject (["com/contoso/\u0100Peer", "run.()V"], utf8, commandStream: commandStream);
			string xml = Run ("com.contoso.\u0100Peer -> a.b:\n    void run() -> c\n", objectFile);
			StringAssert.Contains ("from=\"com/contoso/\u0100Peer\"", xml);
			StringAssert.Contains ("source-method-name=\"run\"", xml);
		}

		[TestCase (false, false)]
		[TestCase (true, false)]
		[TestCase (false, true)]
		public void NativeAotUnicodeBoundariesDoNotCreateSuffixOrParentMatches (bool utf8, bool commandStream)
		{
			string objectFile = WriteNativeObject (["\u0100Peer$Inner", "Other\u0100Suffix"], utf8, commandStream: commandStream);
			string xml = Run (
				"\u0100Peer -> a.b:\n\u0100Peer$Inner -> a.b:\n\u0100Suffix -> a.c:\n", objectFile);
			var types = XDocument.Parse (xml).Root?.Elements ("replace-type").ToArray ()
				?? throw new AssertionException ("Generated XML has no root.");
			Assert.AreEqual (1, types.Length);
			Assert.AreEqual ("\u0100Peer$Inner", (string?) types [0].Attribute ("from"));
		}

		[TestCase (false, false)]
		[TestCase (false, true)]
		[TestCase (true, false)]
		[TestCase (true, true)]
		public void NativeAotUtf16ClassEdgesPreserveIdentity (bool commandStream, bool elf32)
		{
			string trailingName = "Pee\u0172";
			string leadingName = "\u0120Peer";
			string objectFile = WriteNativeObject (
				["com/contoso/" + trailingName, leadingName, "run.()V", "value", "I"],
				commandStream: commandStream, elf32: elf32);
			string mapping = $"""
				com.contoso.Peer -> a.b:
				com.contoso.{trailingName} -> a.b:
				{'\u0100'}Peer -> c.d:
				{leadingName} -> c.d:

				""";
			var root = XDocument.Parse (Run (mapping, objectFile)).Root
				?? throw new AssertionException ("Generated XML has no root.");
			CollectionAssert.AreEquivalent (new [] { "com/contoso/Pee\u0172", "\u0120Peer" },
				root.Elements ("replace-type").Select (type => (string?) type.Attribute ("from")).ToArray ());
			CollectionAssert.AreEquivalent (new [] { ("a/b", "com/contoso/Pee\u0172"), ("c/d", "\u0120Peer") },
				root.Elements ("reverse-type").Select (type => ((string?) type.Attribute ("from"), (string?) type.Attribute ("to"))).ToArray ());

			string members = Run ($"""
				com.contoso.Peer -> a.b:
				    void run() -> absentMethod
				    int value -> absentField
				com.contoso.{trailingName} -> a.b:
				    void run() -> retainedMethod
				    int value -> retainedField

				""", objectFile);
			StringAssert.Contains ("target-method-name=\"retainedMethod\"", members);
			StringAssert.Contains ("target-field-name=\"retainedField\"", members);
			Assert.IsEmpty (Errors, "A phantom class must not cause a conflicting-member XA4325 error.");
		}

		[TestCase (false, false)]
		[TestCase (false, true)]
		[TestCase (true, false)]
		[TestCase (true, true)]
		public void NativeAotStringHeadersAndOtherEncodingsAreNotClasses (bool elf32, bool headerCase)
		{
			string survivingClass = headerCase ? "com/contoso/" + new string ('B', 53) : "\u0141";
			string objectFile = WriteNativeObject (["", survivingClass, "run.()V", "value", "I"],
				commandStream: true, elf32: elf32);
			var root = XDocument.Parse (Run ($"""
				A -> a.b:
				{survivingClass.Replace ('/', '.')} -> a.b:

				""", objectFile)).Root ?? throw new AssertionException ("Generated XML has no root.");
			Assert.AreEqual (survivingClass, (string?) root.Elements ("replace-type").Single ().Attribute ("from"));
			Assert.AreEqual (survivingClass, (string?) root.Elements ("reverse-type").Single ().Attribute ("to"));
			string members = Run ($"""
				A -> a.b:
				    void run() -> absentMethod
				    int value -> absentField
				{survivingClass.Replace ('/', '.')} -> a.b:
				    void run() -> retainedMethod
				    int value -> retainedField

				""", objectFile);
			StringAssert.Contains ("target-method-name=\"retainedMethod\"", members);
			StringAssert.Contains ("target-field-name=\"retainedField\"", members);
		}

		[TestCase (false, false)]
		[TestCase (false, true)]
		[TestCase (true, false)]
		[TestCase (true, true)]
		public void NativeAotPrimitiveDescriptorTokensAreNotClasses (bool commandStream, bool elf32)
		{
			string objectFile = WriteNativeObject (
				["com/contoso/Peer", "run.(I)V", "consume.([BZ)J", "field.[I"],
				commandStream: commandStream, elf32: elf32);
			var root = XDocument.Parse (Run ("""
				com.contoso.Peer -> a.b:
				    void run(int) -> retainedMethod
				I -> a.b:
				    void run(int) -> absentMethod
				V -> a.b:
				B -> a.b:
				Z -> a.b:
				J -> a.b:

				""", objectFile)).Root ?? throw new AssertionException ("Generated XML has no root.");
			Assert.AreEqual ("com/contoso/Peer", (string?) root.Elements ("replace-type").Single ().Attribute ("from"));
			Assert.AreEqual ("com/contoso/Peer", (string?) root.Elements ("reverse-type").Single ().Attribute ("to"));
			Assert.AreEqual ("retainedMethod", (string?) root.Elements ("replace-method").Single ().Attribute ("target-method-name"));
			Assert.IsEmpty (Errors, "Primitive descriptor tokens must not claim absent classes or introduce member conflicts.");
		}

		[TestCase ("I", false, false)]
		[TestCase ("LI;", false, true)]
		[TestCase ("reference.(ILI;)V", true, false)]
		[TestCase ("[LI;", true, true)]
		public void NativeAotSingleLetterClassReferencesAreRetained (string reference, bool commandStream, bool elf32)
		{
			string objectFile = WriteNativeObject ([reference], commandStream: commandStream, elf32: elf32);
			var root = XDocument.Parse (Run ("I -> a.b:\n", objectFile)).Root
				?? throw new AssertionException ("Generated XML has no root.");
			Assert.AreEqual ("I", (string?) root.Elements ("replace-type").Single ().Attribute ("from"));
			Assert.AreEqual ("I", (string?) root.Elements ("reverse-type").Single ().Attribute ("to"));
		}

		[TestCase (false, false)]
		[TestCase (false, true)]
		[TestCase (true, false)]
		[TestCase (true, true)]
		public void NativeAotRawBytesAreNotClassEvidence (bool utf8, bool elf32)
		{
			string objectFile = WriteNativeObject (["", "\u0141"], utf8, elf32: elf32, unrelatedBytes: true);
			var root = XDocument.Parse (Run ($"""
				A -> a.b:
				{'\u0141'} -> a.b:
				ExecutableOnly -> a.b:
				DataOnly -> a.b:

				""", objectFile)).Root ?? throw new AssertionException ("Generated XML has no root.");
			Assert.AreEqual ("\u0141", (string?) root.Elements ("replace-type").Single ().Attribute ("from"));
			Assert.AreEqual ("\u0141", (string?) root.Elements ("reverse-type").Single ().Attribute ("to"));
		}

		[TestCase (false, true, false)]
		[TestCase (true, true, false)]
		[TestCase (false, false, false)]
		[TestCase (true, false, false)]
		[TestCase (false, true, true)]
		[TestCase (true, true, true)]
		public void NativeAotTypeMapKeysSelectOnlyJavaUniverse (bool elf32, bool javaGroup, bool commandStream)
		{
			string objectFile = WriteNativeObject (commandStream ? ["unrelated"] : [],
				elf32: elf32, typeMap: true, javaGroup: javaGroup, commandStream: commandStream);
			var root = XDocument.Parse (Run ("test.Live -> a.b:\n", objectFile)).Root
				?? throw new AssertionException ("Generated XML has no root.");
			Assert.AreEqual (javaGroup ? 1 : 0, root.Elements ("replace-type").Count ());
			Assert.AreEqual (javaGroup ? 1 : 0, root.Elements ("reverse-type").Count ());
		}

		[TestCase (false, "Mono_Android_0_Java_Lang_Object", true)]
		[TestCase (true, "Mono_Android_12_Java_Lang_Object", true)]
		[TestCase (false, "_Owner_TypeMap_0___TypeMapAnchor", true)]
		[TestCase (true, "_Owner_TypeMap_12___TypeMapAnchor", true)]
		[TestCase (false, "Mono_Android_0_Java_Lang_Object_1", true)]
		[TestCase (true, "_Owner_TypeMap_12___TypeMapAnchor_0", true)]
		[TestCase (false, "Mono_Android_0_Android_Runtime_JavaDictionary", false)]
		[TestCase (true, "Mono_Android_x_Java_Lang_Object", false)]
		[TestCase (false, "_Owner_TypeMap_x___TypeMapAnchor", false)]
		[TestCase (true, "_Owner_TypeMap_0___TypeMapAnchorExtra", false)]
		public void NativeAotMangledGroupsRespectJavaUniverse (bool elf32, string typeName, bool javaGroup)
		{
			string objectFile = WriteNativeObject ([], elf32: elf32, typeMap: true, groupSymbol: $"_ZTV{typeName.Length}{typeName}");
			var root = XDocument.Parse (Run ("test.Live -> a.b:\n", objectFile)).Root
				?? throw new AssertionException ("Generated XML has no root.");
			if (javaGroup) {
				Assert.AreEqual ("test/Live", (string?) root.Elements ("replace-type").Single ().Attribute ("from"));
				Assert.AreEqual ("test/Live", (string?) root.Elements ("reverse-type").Single ().Attribute ("to"));
			} else {
				Assert.IsEmpty (root.Elements (), "Disambiguation must not admit CLR or lookalike mapping universes.");
			}
		}

		[TestCase (false, false)]
		[TestCase (true, false)]
		[TestCase (false, true)]
		[TestCase (true, true)]
		public void NativeAotRelocationsDoNotCompleteUtf16CodeUnits (bool elf32, bool hydrationFileBacked)
		{
			string objectFile = WriteNativeObject (["com/contoso/Peer"],
				commandStream: true, elf32: elf32, zeroFillAfterLiteral: false, hydrationFileBacked: hydrationFileBacked);
			string mappingFile = Path.Combine (TestDirectory, "mapping.txt");
			string outputFile = Path.Combine (TestDirectory, "remap.xml");
			File.WriteAllText (mappingFile, "com.contoso.Peer -> a.b:\n");
			var task = new GenerateR8JniRemapping {
				BuildEngine = engine, MappingFile = mappingFile, OutputFile = outputFile,
				NativeAot = true, NativeAotObjectFile = objectFile,
			};
			Assert.IsFalse (task.Execute ());
			Assert.AreEqual ("XA4325", Errors.Single ().Code,
				"A named frozen string truncated by an unknown relocation must fail closed.");
			FileAssert.DoesNotExist (outputFile);
		}

		[TestCase (false, false)]
		[TestCase (true, false)]
		[TestCase (false, true)]
		[TestCase (true, true)]
		public void NativeAotMethodTableMarkerIsNotFrozenString (bool elf32, bool hydrationFileBacked)
		{
			string objectFile = WriteNativeObject (["com/contoso/Peer", "run.()V"],
				commandStream: true, elf32: elf32, methodTableMarker: true, hydrationFileBacked: hydrationFileBacked);
			var root = XDocument.Parse (Run ("""
				com.contoso.Peer -> a.b:
				    void run() -> c

				""", objectFile)).Root ?? throw new AssertionException ("Generated XML has no root.");
			Assert.AreEqual ("com/contoso/Peer", (string?) root.Elements ("replace-type").Single ().Attribute ("from"));
			Assert.AreEqual ("com/contoso/Peer", (string?) root.Elements ("reverse-type").Single ().Attribute ("to"));
			Assert.AreEqual ("c", (string?) root.Elements ("replace-method").Single ().Attribute ("target-method-name"));
			Assert.IsEmpty (Errors, "An unrelated managed type must not be decoded as a frozen string.");
		}

		[TestCase (false)]
		[TestCase (true)]
		public void NativeAotRealDehydrationCommandsAreNotIdentifierBoundaries (bool elf32)
		{
			string objectFile = WriteNativeObject (
				["com/contoso/Peer$Inner", "run.([Lcom/contoso/Argument;)V"], commandStream: true, elf32: elf32);
			string xml = Run ("""
				com.contoso.Peer -> a.b:
				com.contoso.Peer$Inner -> a.b:
				    void run(com.contoso.Argument[]) -> c
				com.contoso.Argument -> a.d:

				""", objectFile);
			var root = XDocument.Parse (xml).Root ?? throw new AssertionException ("Generated XML has no root.");
			CollectionAssert.AreEquivalent (new [] { "com/contoso/Peer$Inner", "com/contoso/Argument" },
				root.Elements ("replace-type").Select (element => (string?) element.Attribute ("from")).ToArray ());
			StringAssert.Contains ("""<reverse-type from="a/b" to="com/contoso/Peer$Inner" />""", xml);
			StringAssert.Contains ("target-method-signature=\"([La/d;)V\"", xml);
		}

		[Test]
		public void InvalidNativeAotDehydrationFailsClosed ()
		{
			string objectFile = WriteNativeObject (["com/contoso/Peer"], commandStream: true, invalidCommand: true);
			string mappingFile = Path.Combine (TestDirectory, "mapping.txt");
			string outputFile = Path.Combine (TestDirectory, "remap.xml");
			File.WriteAllText (mappingFile, "com.contoso.Peer -> a.b:\n");
			var task = new GenerateR8JniRemapping {
				BuildEngine = engine, MappingFile = mappingFile, OutputFile = outputFile,
				NativeAot = true, NativeAotObjectFile = objectFile,
			};
			Assert.IsFalse (task.Execute ());
			Assert.AreEqual ("XA4325", Errors.Single ().Code);
			FileAssert.DoesNotExist (outputFile);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void RejectedExistingXmlCannotClaimMappings (bool malformed)
		{
			string existingXml = malformed
				? "<replacements><replace-type from=\"com/contoso/Peer\" to=\"conflicting/Peer\" /></broken>"
				: "<ignored><replace-type from=\"com/contoso/Peer\" to=\"conflicting/Peer\" /></ignored>";
			string mappingFile = Path.Combine (TestDirectory, "mapping.txt");
			string existingFile = Path.Combine (TestDirectory, "existing.xml");
			string outputFile = Path.Combine (TestDirectory, "remap.xml");
			string mergedFile = Path.Combine (TestDirectory, "merged.xml");
			File.WriteAllText (mappingFile, "com.contoso.Peer -> a.b:\n");
			File.WriteAllText (existingFile, existingXml);
			var task = new GenerateR8JniRemapping {
				BuildEngine = engine,
				MappingFile = mappingFile,
				OutputFile = outputFile,
				ExistingRemapXmlFiles = [new TaskItem (existingFile)],
			};
			Assert.IsTrue (task.Execute ());
			StringAssert.Contains ("""<replace-type from="com/contoso/Peer" to="a/b" />""", File.ReadAllText (outputFile));
			var merge = new MergeRemapXml {
				BuildEngine = engine,
				InputRemapXmlFiles = [new TaskItem (existingFile), new TaskItem (outputFile)],
				OutputFile = new TaskItem (mergedFile),
			};
			Assert.IsTrue (merge.Execute ());
			var mergedRoot = XDocument.Load (mergedFile).Root ?? throw new AssertionException ("Merged XML has no root.");
			var replacements = mergedRoot.Elements ("replace-type")
				.Where (element => (string?) element.Attribute ("from") == "com/contoso/Peer").ToArray ();
			Assert.AreEqual (1, replacements.Length, "Rejected input must not leave a partial conflicting mapping.");
			Assert.AreEqual ("a/b", (string?) replacements [0].Attribute ("to"));
			Assert.AreEqual (malformed ? "XA4318" : "XA4317", Warnings.Single ().Code);
		}

		[Test]
		public void ExistingConflictingTypeMappingStillOwnsItsMembers ()
		{
			string mappingFile = Path.Combine (TestDirectory, "mapping.txt");
			string existingFile = Path.Combine (TestDirectory, "existing.xml");
			string outputFile = Path.Combine (TestDirectory, "remap.xml");
			File.WriteAllText (mappingFile, "com.contoso.Peer -> a.b:\n    void run() -> c\n");
			File.WriteAllText (existingFile, """
				<replacements>
				  <replace-type from="com/contoso/Peer" to="external/Peer" />
				</replacements>
				""");
			var task = new GenerateR8JniRemapping {
				BuildEngine = engine, MappingFile = mappingFile, OutputFile = outputFile,
				ExistingRemapXmlFiles = [new TaskItem (existingFile)],
			};
			Assert.IsTrue (task.Execute ());
			var root = XDocument.Parse (File.ReadAllText (outputFile)).Root
				?? throw new AssertionException ("Generated XML has no root.");
			Assert.IsEmpty (root.Elements ());
			Assert.AreEqual ("XA4326", Warnings.Single ().Code);
		}

		[Test]
		public void MissingLinkedAssemblyReportsXA4325 ()
		{
			string mappingFile = Path.Combine (TestDirectory, "mapping.txt");
			string outputFile = Path.Combine (TestDirectory, "output.xml");
			File.WriteAllText (mappingFile, "com.contoso.Peer -> a.b:\n");
			var task = new GenerateR8JniRemapping {
				BuildEngine = engine,
				MappingFile = mappingFile,
				OutputFile = outputFile,
				LinkedAssemblies = [new TaskItem (Path.Combine (TestDirectory, "missing.dll"))],
			};

			Assert.IsFalse (task.Execute ());
			Assert.AreEqual ("XA4325", Errors.Single ().Code);
			FileAssert.DoesNotExist (outputFile);
		}

		[Test]
		public void IdenticalExistingTypeMappingDoesNotSuppressMembers ()
		{
			string mappingFile = Path.Combine (TestDirectory, "mapping.txt");
			string existingFile = Path.Combine (TestDirectory, "existing.xml");
			string outputFile = Path.Combine (TestDirectory, "output.xml");
			File.WriteAllText (mappingFile, """
				com.contoso.Peer -> a.b:
				    void run() -> c

				""");
			File.WriteAllText (existingFile, """
				<replacements>
				  <replace-type from="com/contoso/Peer" to="a/b" />
				</replacements>
				""");
			var task = new GenerateR8JniRemapping {
				BuildEngine = engine,
				MappingFile = mappingFile,
				OutputFile = outputFile,
				ExistingRemapXmlFiles = [new TaskItem (existingFile)],
			};

			Assert.IsTrue (task.Execute (), string.Join ("; ", Errors.Select (error => error.Message)));
			string xml = File.ReadAllText (outputFile);
			StringAssert.DoesNotContain ("replace-type", xml);
			StringAssert.Contains ("source-method-name=\"run\"", xml);
		}

		[Test]
		public void MalformedMappingReportsXA4325 ()
		{
			string mappingFile = Path.Combine (TestDirectory, "mapping.txt");
			File.WriteAllText (mappingFile, "    void run() -> a\n");
			var task = new GenerateR8JniRemapping {
				BuildEngine = engine,
				MappingFile = mappingFile,
				OutputFile = Path.Combine (TestDirectory, "output.xml"),
			};

			Assert.IsFalse (task.Execute ());
			Assert.AreEqual ("XA4325", Errors.Single ().Code);
		}

		[Test]
		public void UnsupportedMethodSignatureWarningIncludesReturnType ()
		{
			Run ("""
				com.contoso.Peer -> a.b:
				    void run( ) -> c

				""");

			Assert.AreEqual ("XA4326", Warnings.Single ().Code);
			StringAssert.Contains ("run( ):void", Warnings [0].Message);
		}

		[Test]
		public void DescriptorDistinctR8FieldsReachBinaryAsset ()
		{
			string mappingFile = Path.Combine (TestDirectory, "mapping.txt");
			string xmlFile = Path.Combine (TestDirectory, "r8.xml");
			File.WriteAllText (mappingFile, """
				com.contoso.Peer -> a.b:
				    int value -> integerTarget
				    java.lang.String value -> stringTarget

				""");
			var generate = new GenerateR8JniRemapping {
				BuildEngine = engine, MappingFile = mappingFile, OutputFile = xmlFile,
			};
			Assert.IsTrue (generate.Execute ());
			var fields = XDocument.Load (xmlFile).Root?.Elements ("replace-field")
				.Select (field => ((string?) field.Attribute ("source-field-signature"), (string?) field.Attribute ("target-field-name"))).ToArray ()
				?? throw new AssertionException ("Generated XML has no root.");
			CollectionAssert.AreEqual (new [] { ("I", "integerTarget"), ("Ljava/lang/String;", "stringTarget") }, fields);

			string assetFile = Path.Combine (TestDirectory, "jni-remap.bin");
			var binary = new GenerateJniRemappingAsset {
				BuildEngine = engine,
				OutputFile = assetFile,
				RemappingXmlFilePath = xmlFile,
			};
			Assert.IsTrue (binary.Execute ());
			var asset = new JniRemappingAsset (File.ReadAllBytes (assetFile));
			var integer = asset.FindField ("a/b", "value", "I")
				?? throw new AssertionException ("The integer field remap was lost.");
			var text = asset.FindField ("a/b", "value", "Ljava/lang/String;")
				?? throw new AssertionException ("The string field remap was lost.");
			Assert.AreEqual ("integerTarget", asset.ReadString (integer.TargetName));
			Assert.AreEqual ("stringTarget", asset.ReadString (text.TargetName));
			Assert.AreEqual ("I", asset.ReadString (integer.TargetSignature));
			Assert.AreEqual ("Ljava/lang/String;", asset.ReadString (text.TargetSignature));
		}

		[Test]
		public void GeneratedDocumentParsesWithTheExistingRemapSchema ()
		{
			string mappingFile = Path.Combine (TestDirectory, "mapping.txt");
			string outputFile = Path.Combine (TestDirectory, "r8-jni-remap.xml");
			File.WriteAllText (mappingFile, """
				com.contoso.Peer -> a.b:
				    void doWork(int) -> c
				    int counter -> d
				""");
			var task = new GenerateR8JniRemapping {
				BuildEngine = engine,
				MappingFile = mappingFile,
				OutputFile = outputFile,
			};
			Assert.IsTrue (task.Execute (), "Task should have succeeded.");

			string mamFile = Path.Combine (TestDirectory, "mam.xml");
			File.WriteAllText (mamFile, """
				<replacements>
				  <replace-type from="com/contoso/Mam" to="com/microsoft/intune/Mam" />
				</replacements>
				""");
			string mergedFile = Path.Combine (TestDirectory, "xa-remap-members.xml");
			var merge = new MergeRemapXml {
				BuildEngine = engine,
				InputRemapXmlFiles = [
					new TaskItem (mamFile),
					new TaskItem (outputFile),
				],
				OutputFile = new TaskItem (mergedFile),
			};
			Assert.IsTrue (merge.Execute (), "MergeRemapXml should have succeeded.");
			Assert.AreEqual (0, Errors.Count, "The merge should have no errors.");

			string merged = File.ReadAllText (mergedFile);
			StringAssert.Contains ("""<replace-type from="com/contoso/Mam" to="com/microsoft/intune/Mam" />""", merged);
			StringAssert.Contains ("""<replace-type from="com/contoso/Peer" to="a/b" />""", merged);
			StringAssert.Contains ("replace-field", merged);

			string assetFile = Path.Combine (TestDirectory, "jni-remap.bin");
			var generate = new GenerateJniRemappingAsset {
				BuildEngine = engine,
				RemappingXmlFilePath = mergedFile,
				OutputFile = assetFile,
			};
			Assert.IsTrue (generate.Execute (), "GenerateJniRemappingAsset should have succeeded.");
			Assert.AreEqual (0, Errors.Count, "The generated document must parse with the existing schema.");
			var asset = new JniRemappingAsset (File.ReadAllBytes (assetFile));
			Assert.AreEqual ("com/microsoft/intune/Mam", asset.ReadString (
				asset.FindReplacementType ("com/contoso/Mam") ?? throw new AssertionException ("MAM type remap was lost.")));
			Assert.AreEqual ("com/contoso/Peer", asset.ReadString (
				asset.FindReverseType ("a/b") ?? throw new AssertionException ("R8 reverse type remap was lost.")));
			var method = asset.FindMethod ("a/b", "doWork", "(I)V")
				?? throw new AssertionException ("R8 method remap was lost.");
			Assert.AreEqual ("c", asset.ReadString (method.TargetName));
			var field = asset.FindField ("a/b", "counter", "I")
				?? throw new AssertionException ("R8 field remap was lost.");
			Assert.AreEqual ("d", asset.ReadString (field.TargetName));
		}

		string WriteNativeObject (string [] literals, bool utf8 = false,
			bool commandStream = false, bool invalidCommand = false, bool elf32 = false, bool zeroFillAfterLiteral = true,
			bool unrelatedBytes = false, bool typeMap = false, bool javaGroup = true, bool methodTableMarker = false,
			bool hydrationFileBacked = false, string? groupSymbol = null)
		{
			int pointerSize = elf32 ? 4 : 8;
			var stringOffsets = new List<ulong> ();
			ulong hydratedSize = 0;
			ulong frozenSize = 0;
			ulong methodTableOffset = 0;
			byte [] Encode ()
			{
				using var data = new MemoryStream ();
				using var writer = new BinaryWriter (data);
				foreach (string value in literals) {
					byte [] bytes = (utf8 ? Encoding.UTF8 : Encoding.Unicode).GetBytes (value);
					if (!utf8) {
						stringOffsets.Add ((ulong) data.Position + (ulong) pointerSize);
						writer.Write (new byte [pointerSize * 2]);
						writer.Write (value.Length);
					}
					writer.Write (bytes);
					writer.Write ((byte) 0);
					if (!utf8) {
						writer.Write ((byte) 0);
					}
				}
				return data.ToArray ();
			}

			byte [] EncodeCommands ()
			{
				using var image = new MemoryStream ();
				using var commands = new BinaryWriter (image);
				commands.Write (0U); // Destination relocation placeholder.
				commands.Write (0U); // Stream length, filled below.
				void Command (int command, int payload)
				{
					if (payload <= 28) {
						commands.Write ((byte) (command | payload << 3));
						return;
					}
					int remainder = payload - 28;
					int extra = remainder <= 0xFF ? 1 : remainder <= 0xFFFF ? 2 : 3;
					commands.Write ((byte) (command | (28 + extra) << 3));
					for (int i = 0; i < extra; i++) {
						commands.Write ((byte) (remainder >> (8 * i)));
					}
				}
				foreach (string value in literals) {
					stringOffsets.Add (hydratedSize + (ulong) pointerSize);
					Command (1, pointerSize); // Frozen-object sync block.
					hydratedSize += (ulong) pointerSize;
					Command (3, 0); // PtrReloc(0).
					hydratedSize += (ulong) pointerSize;
					byte [] bytes = Encoding.Unicode.GetBytes (value);
					int count = bytes.Length - (bytes.Length > 0 && bytes [bytes.Length - 1] == 0 ? 1 : 0);
					using var literal = new MemoryStream ();
					using (var writer = new BinaryWriter (literal, Encoding.UTF8, leaveOpen: true)) {
						writer.Write (value.Length);
						writer.Write (bytes, 0, count);
					}
					byte [] contents = literal.ToArray ();
					int position = 0;
					while (position < contents.Length) {
						int copyLength = 0;
						int zeros = 0;
						for (int i = position; i < contents.Length; i++) {
							if (contents [i] == 0) {
								zeros++;
							} else if (zeros >= 4) {
								break;
							} else {
								copyLength += zeros + 1;
								zeros = 0;
							}
						}
						if (zeros < 4) {
							copyLength += zeros;
							zeros = 0;
						}
						if (copyLength > 0) {
							Command (invalidCommand ? 6 : 0, copyLength);
							commands.Write (contents, position, copyLength);
							position += copyLength;
							hydratedSize += (ulong) copyLength;
						}
						if (zeros > 0) {
							Command (1, zeros);
							position += zeros;
							hydratedSize += (ulong) zeros;
						}
					}
					if (zeroFillAfterLiteral) {
						Command (1, 13); // 69 03 follows: ZeroFill(13), PtrReloc(0), not U+0369.
						hydratedSize += 13;
					}
					Command (3, 0);
					hydratedSize += (ulong) pointerSize;
				}
				frozenSize = hydratedSize;
				if (methodTableMarker) {
					methodTableOffset = hydratedSize;
					Command (0, 8);
					commands.Write (0U); // Component size and flags.
					commands.Write (24U); // BaseSize, not a string length.
					Command (3, 0); // Related-type relocation.
					hydratedSize += 8 + (ulong) pointerSize;
					Command (1, 64);
					hydratedSize += 64;
				}
				uint length = (uint) image.Length;
				commands.Write (0U); // Fixup-table relocation placeholder.
				image.Position = 4;
				commands.Write (length);
				return image.ToArray ();
			}

			byte [] objectData = commandStream ? EncodeCommands () : Encode ();
			ulong mapOffset = (ulong) objectData.Length;
			byte [] map = typeMap ? System.Convert.FromHexString ("000208000f050000000002000208000f0500000012746573742f4c69766500") : [];
			ulong fixupsOffset = mapOffset + (ulong) map.Length;
			if (typeMap) {
				objectData = objectData.Concat (map).Concat (new byte [4]).ToArray ();
			}
			if (unrelatedBytes && !utf8) {
				objectData = objectData.Concat (Encoding.Unicode.GetBytes ("DataOnly\0")).ToArray ();
			}
			byte [] codeBytes = unrelatedBytes
				? Encoding.UTF8.GetBytes ("ExecutableOnly\0").Concat (Encoding.Unicode.GetBytes ("ExecutableOnly\0")).ToArray ()
				: [0xC0, 0x03, 0x5F, 0xD6];
			var sections = new [] {
				(Name: "", Flags: 0UL, Type: 0U, Bytes: new byte [0]),
				(Name: ".shstrtab", Flags: 0UL, Type: 3U, Bytes: new byte [0]),
				(Name: "__managedcode", Flags: 6UL, Type: 1U, Bytes: codeBytes),
				(Name: ".rodata", Flags: utf8 && !commandStream ? 0x32UL : 2UL, Type: 1U, Bytes: objectData),
				(Name: hydrationFileBacked ? ".hydrated" : ".bss", Flags: 3UL,
					Type: hydrationFileBacked ? 1U : 8U, Bytes: hydrationFileBacked ? new byte [checked ((int) hydratedSize)] : []),
			};
			if (!utf8 || commandStream || typeMap) {
				using var symbolData = new MemoryStream ();
				using var symbols = new BinaryWriter (symbolData);
				var names = new StringBuilder ("\0");
				int symbolIndex = 1;
				symbols.Write (new byte [elf32 ? 16 : 24]);
				int Symbol (string name, ulong offset, ulong size, ushort section)
				{
					symbols.Write ((uint) names.Length);
					names.Append (name).Append ('\0');
					if (elf32) {
						symbols.Write ((uint) offset);
						symbols.Write ((uint) size);
					}
					symbols.Write ((byte) 0x11); // STB_GLOBAL, STT_OBJECT.
					symbols.Write ((byte) 0);
					symbols.Write (section);
					if (!elf32) {
						symbols.Write (offset);
						symbols.Write (size);
					}
					return symbolIndex++;
				}
				if (commandStream) {
					Symbol ("fixture__dehydrated_data", 0, mapOffset, 3);
					Symbol ("fixture__hydrated", 0, hydratedSize, 4);
				}
				if (stringOffsets.Count > 0) {
					Symbol ("fixture__FrozenSegmentStart", 0, commandStream ? frozenSize : (ulong) objectData.Length,
						commandStream ? (ushort) 4 : (ushort) 3);
				}
				for (int i = 0; i < stringOffsets.Count; i++) {
					Symbol ($"fixture__Str_{i}", stringOffsets [i], 0, commandStream ? (ushort) 4 : (ushort) 3);
				}
				if (methodTableMarker) {
					Symbol ("_ZTV18App_N___Str_Helper", methodTableOffset, 8 + (ulong) pointerSize + 64, 4);
					Symbol ("fixture__Str_Helper", methodTableOffset, 8 + (ulong) pointerSize + 64, 4);
				}
				int groupSymbolIndex = 0;
				if (typeMap) {
					Symbol ("fixture__external_type_map__", mapOffset, (ulong) map.Length, 3);
					Symbol ("fixture__external_CommonFixupsTable_references", fixupsOffset, 4, 3);
					groupSymbolIndex = Symbol (groupSymbol ??
						(javaGroup ? "_ZTV29Mono_Android_Java_Lang_Object" : "_ZTV43Mono_Android_Android_Runtime_JavaDictionary"), 0, 0, 0);
				}
				sections = sections.Concat (new [] {
					(Name: ".strtab", Flags: 0UL, Type: 3U,
						Bytes: Encoding.UTF8.GetBytes (names.ToString ())),
					(Name: ".symtab", Flags: 0UL, Type: 2U, Bytes: symbolData.ToArray ()),
				}).ToArray ();
				if (commandStream || typeMap) {
					using var relocationData = new MemoryStream ();
					using var relocation = new BinaryWriter (relocationData);
					void Relocation (ulong offset, int index)
					{
						if (elf32) {
							relocation.Write ((uint) offset);
							relocation.Write (((uint) index << 8) | 3U); // R_ARM_REL32.
						} else {
							relocation.Write (offset);
							relocation.Write (((ulong) index << 32) | 261UL); // R_AARCH64_PREL32.
							relocation.Write (0L);
						}
					}
					if (commandStream) {
						Relocation (0, 2);
					}
					if (typeMap) {
						Relocation (fixupsOffset, groupSymbolIndex);
					}
					sections = sections.Concat (new [] {
						(Name: elf32 ? ".rel.rodata" : ".rela.rodata", Flags: 0UL,
							Type: elf32 ? 9U : 4U, Bytes: relocationData.ToArray ()),
					}).ToArray ();
				}
			}
			sections [1].Bytes = Encoding.UTF8.GetBytes (string.Join ("\0", sections.Select (section => section.Name)) + "\0");
			var offsets = new long [sections.Length];
			using var image = new MemoryStream ();
			using var writer = new BinaryWriter (image);
			void Word (ulong value)
			{
				if (elf32) {
					writer.Write (checked ((uint) value));
				} else {
					writer.Write (value);
				}
			}
			writer.Write (new byte [] { 0x7F, (byte) 'E', (byte) 'L', (byte) 'F', elf32 ? (byte) 1 : (byte) 2, 1, 1, 0 });
			writer.Write (0UL);
			writer.Write ((ushort) 1);
			writer.Write (elf32 ? (ushort) 40 : (ushort) 183);
			writer.Write (1U);
			Word (0);
			Word (0);
			Word (0);
			writer.Write (0U);
			writer.Write (elf32 ? (ushort) 52 : (ushort) 64);
			writer.Write ((ushort) 0);
			writer.Write ((ushort) 0);
			writer.Write (elf32 ? (ushort) 40 : (ushort) 64);
			writer.Write ((ushort) sections.Length);
			writer.Write ((ushort) 1);
			for (int i = 1; i < sections.Length; i++) {
				offsets [i] = image.Position;
				writer.Write (sections [i].Bytes);
			}
			long sectionHeaders = image.Position;
			int nameIndex = 0;
			for (int i = 0; i < sections.Length; i++) {
				writer.Write (nameIndex);
				writer.Write (sections [i].Type);
				Word (sections [i].Flags);
				Word (0);
				Word ((ulong) offsets [i]);
				Word (i == 4 ? hydratedSize : (ulong) sections [i].Bytes.Length);
				bool relocations = sections [i].Type == 4 || sections [i].Type == 9;
				writer.Write (sections [i].Type == 2 ? 5U : relocations ? 6U : 0U);
				writer.Write (sections [i].Type == 2 ? 1U : relocations ? 3U : 0U);
				Word (i == 0 ? 0UL : 1UL);
				Word (sections [i].Type == 2 ? (elf32 ? 16UL : 24UL) : relocations ? (elf32 ? 8UL : 24UL) :
					sections [i].Flags == 0x32 ? 1UL : 0UL);
				nameIndex += Encoding.UTF8.GetByteCount (sections [i].Name) + 1;
			}
			image.Position = elf32 ? 32 : 40;
			Word ((ulong) sectionHeaders);
			string path = Path.Combine (TestDirectory, "app.o");
			File.WriteAllBytes (path, image.ToArray ());
			return path;
		}
	}
}
