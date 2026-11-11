#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Build.Tests.Tasks
{
	[TestClass]
	[DoNotParallelize]
	public class GenerateR8JniRemappingTests : BaseTest
	{
		List<BuildErrorEventArgs>? errors;
		List<BuildWarningEventArgs>? warnings;
		MockBuildEngine? engine;
		string? directory;

		[TestInitialize]
		public void Setup ()
		{
			errors = new List<BuildErrorEventArgs> ();
			warnings = new List<BuildWarningEventArgs> ();
			engine = new MockBuildEngine (Console.Out, errors, warnings);
			directory = Path.Combine (Root, "temp", TestName);
			if (Directory.Exists (directory)) {
				Directory.Delete (directory, recursive: true);
			}
			Directory.CreateDirectory (directory);
		}

		string TestDirectory => directory ?? throw new AssertFailedException ("Test directory was not initialized.");
		List<BuildErrorEventArgs> Errors => errors ?? throw new AssertFailedException ("Error list was not initialized.");
		List<BuildWarningEventArgs> Warnings => warnings ?? throw new AssertFailedException ("Warning list was not initialized.");

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

		[TestMethod]
		public void GeneratesRuntimeLookupEntries ()
		{
			string xml = Run ("""
				com.contoso.Peer -> a.b:
				    com.contoso.Peer run(com.contoso.Peer[]) -> c
				    com.contoso.Peer[] peers -> d

				""");

			Assert.Contains ("""<replace-type from="com/contoso/Peer" to="a/b" />""", xml);
			Assert.Contains ("""<reverse-type from="a/b" to="com/contoso/Peer" />""", xml);
			Assert.Contains ("source-method-signature=\"([Lcom/contoso/Peer;)Lcom/contoso/Peer;\"", xml);
			Assert.Contains ("target-method-signature=\"([La/b;)La/b;\"", xml);
			Assert.Contains ("source-field-signature=\"[Lcom/contoso/Peer;\"", xml);
			Assert.Contains ("target-field-signature=\"[La/b;\"", xml);
		}

		[TestMethod]
		public void SkipsAmbiguousReverseTypeForMergedClasses ()
		{
			string xml = Run ("""
				com.contoso.First -> a.b:
				com.contoso.Second -> a.b:

				""");

			Assert.DoesNotContain ("reverse-type", xml);
		}

		[TestMethod]
		[DataRow ("method")]
		[DataRow ("field")]
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
			Assert.IsTrue (Errors.Any (e => e.Code == "XA4325"));
			Assert.IsFalse (File.Exists (outputFile));
		}

		[TestMethod]
		public void NativeAotRetentionFiltersUnusedEntries ()
		{
			string objectFile = WriteNativeObject (["com/contoso/Peer", "run.(I)V"]);
			string xml = Run ("""
				com.contoso.Peer -> a.b:
				    void run(int) -> c
				    void removed() -> d
				com.contoso.Unused -> a.e:

				""", objectFile);

			Assert.Contains ("source-method-name=\"run\"", xml);
			Assert.DoesNotContain ("removed", xml);
			Assert.DoesNotContain ("Unused", xml);
		}

		[TestMethod]
		[DataRow (false, false)]
		[DataRow (true, false)]
		[DataRow (false, true)]
		public void NativeAotNestedNameDoesNotRetainMergedParent (bool utf8, bool commandStream)
		{
			string objectFile = WriteNativeObject (["com/contoso/Peer$Inner"], utf8, commandStream: commandStream);
			string xml = Run ("""
				com.contoso.Peer -> a.b:
				com.contoso.Peer$Inner -> a.b:

				""", objectFile);
			var types = XDocument.Parse (xml).Root?.Elements ("replace-type").ToArray ()
				?? throw new AssertFailedException ("Generated XML has no root.");
			Assert.AreEqual (1, types.Length, "A substring is not evidence that the parent class survived.");
			Assert.AreEqual ("com/contoso/Peer$Inner", (string?) types [0].Attribute ("from"));
			Assert.Contains ("""<reverse-type from="a/b" to="com/contoso/Peer$Inner" />""", xml);
		}

		[TestMethod]
		[DataRow (false, false)]
		[DataRow (true, false)]
		[DataRow (false, true)]
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
				?? throw new AssertFailedException ("Generated XML has no root.");
			CollectionAssert.AreEquivalent (new [] {
				"com/contoso/Peer$Inner", "com/contoso/Argument", "com/contoso/Result",
			}, types);
			Assert.Contains ("target-method-signature=\"([La/d;)La/e;\"", xml);
		}

		[TestMethod]
		[DataRow (false, false)]
		[DataRow (true, false)]
		[DataRow (false, true)]
		public void NativeAotTrulyRetainedMergedTypesRemainAmbiguous (bool utf8, bool commandStream)
		{
			string objectFile = WriteNativeObject (["com/contoso/Peer", "com/contoso/Peer$Inner"], utf8, commandStream: commandStream);
			string xml = Run ("""
				com.contoso.Peer -> a.b:
				com.contoso.Peer$Inner -> a.b:

				""", objectFile);
			var root = XDocument.Parse (xml).Root ?? throw new AssertFailedException ("Generated XML has no root.");
			Assert.AreEqual (2, root.Elements ("replace-type").Count ());
			Assert.IsEmpty (root.Elements ("reverse-type"));
		}

		[TestMethod]
		[DataRow (false, false)]
		[DataRow (true, false)]
		[DataRow (false, true)]
		public void NativeAotRetainsUnicodeClasses (bool utf8, bool commandStream)
		{
			string objectFile = WriteNativeObject (["com/contoso/\u0100Peer", "run.()V"], utf8, commandStream: commandStream);
			string xml = Run ("com.contoso.\u0100Peer -> a.b:\n    void run() -> c\n", objectFile);
			Assert.Contains ("from=\"com/contoso/\u0100Peer\"", xml);
			Assert.Contains ("source-method-name=\"run\"", xml);
		}

		[TestMethod]
		[DataRow (false, false)]
		[DataRow (true, false)]
		[DataRow (false, true)]
		public void NativeAotUnicodeBoundariesDoNotCreateSuffixOrParentMatches (bool utf8, bool commandStream)
		{
			string objectFile = WriteNativeObject (["\u0100Peer$Inner", "Other\u0100Suffix"], utf8, commandStream: commandStream);
			string xml = Run (
				"\u0100Peer -> a.b:\n\u0100Peer$Inner -> a.b:\n\u0100Suffix -> a.c:\n", objectFile);
			var types = XDocument.Parse (xml).Root?.Elements ("replace-type").ToArray ()
				?? throw new AssertFailedException ("Generated XML has no root.");
			Assert.AreEqual (1, types.Length);
			Assert.AreEqual ("\u0100Peer$Inner", (string?) types [0].Attribute ("from"));
		}

		[TestMethod]
		[DataRow (false, false)]
		[DataRow (false, true)]
		[DataRow (true, false)]
		[DataRow (true, true)]
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
				?? throw new AssertFailedException ("Generated XML has no root.");
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
			Assert.Contains ("target-method-name=\"retainedMethod\"", members);
			Assert.Contains ("target-field-name=\"retainedField\"", members);
			Assert.IsEmpty (Errors, "A phantom class must not cause a conflicting-member XA4325 error.");
		}

		[TestMethod]
		[DataRow (false, false)]
		[DataRow (false, true)]
		[DataRow (true, false)]
		[DataRow (true, true)]
		public void NativeAotStringHeadersAndOtherEncodingsAreNotClasses (bool elf32, bool headerCase)
		{
			string survivingClass = headerCase ? "com/contoso/" + new string ('B', 53) : "\u0141";
			string objectFile = WriteNativeObject (["", survivingClass, "run.()V", "value", "I"],
				commandStream: true, elf32: elf32);
			var root = XDocument.Parse (Run ($"""
				A -> a.b:
				{survivingClass.Replace ('/', '.')} -> a.b:

				""", objectFile)).Root ?? throw new AssertFailedException ("Generated XML has no root.");
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
			Assert.Contains ("target-method-name=\"retainedMethod\"", members);
			Assert.Contains ("target-field-name=\"retainedField\"", members);
		}

		[TestMethod]
		[DataRow (false, false)]
		[DataRow (false, true)]
		[DataRow (true, false)]
		[DataRow (true, true)]
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

				""", objectFile)).Root ?? throw new AssertFailedException ("Generated XML has no root.");
			Assert.AreEqual ("com/contoso/Peer", (string?) root.Elements ("replace-type").Single ().Attribute ("from"));
			Assert.AreEqual ("com/contoso/Peer", (string?) root.Elements ("reverse-type").Single ().Attribute ("to"));
			Assert.AreEqual ("retainedMethod", (string?) root.Elements ("replace-method").Single ().Attribute ("target-method-name"));
			Assert.IsEmpty (Errors, "Primitive descriptor tokens must not claim absent classes or introduce member conflicts.");
		}

		[TestMethod]
		[DataRow ("I", false, false)]
		[DataRow ("LI;", false, true)]
		[DataRow ("reference.(ILI;)V", true, false)]
		[DataRow ("[LI;", true, true)]
		public void NativeAotSingleLetterClassReferencesAreRetained (string reference, bool commandStream, bool elf32)
		{
			string objectFile = WriteNativeObject ([reference], commandStream: commandStream, elf32: elf32);
			var root = XDocument.Parse (Run ("I -> a.b:\n", objectFile)).Root
				?? throw new AssertFailedException ("Generated XML has no root.");
			Assert.AreEqual ("I", (string?) root.Elements ("replace-type").Single ().Attribute ("from"));
			Assert.AreEqual ("I", (string?) root.Elements ("reverse-type").Single ().Attribute ("to"));
		}

		[TestMethod]
		[DataRow (false, false)]
		[DataRow (false, true)]
		[DataRow (true, false)]
		[DataRow (true, true)]
		public void NativeAotRawBytesAreNotClassEvidence (bool utf8, bool elf32)
		{
			string objectFile = WriteNativeObject (["", "\u0141"], utf8, elf32: elf32, unrelatedBytes: true);
			var root = XDocument.Parse (Run ($"""
				A -> a.b:
				{'\u0141'} -> a.b:
				ExecutableOnly -> a.b:
				DataOnly -> a.b:

				""", objectFile)).Root ?? throw new AssertFailedException ("Generated XML has no root.");
			Assert.AreEqual ("\u0141", (string?) root.Elements ("replace-type").Single ().Attribute ("from"));
			Assert.AreEqual ("\u0141", (string?) root.Elements ("reverse-type").Single ().Attribute ("to"));
		}

		[TestMethod]
		[DataRow (false, true, false)]
		[DataRow (true, true, false)]
		[DataRow (false, false, false)]
		[DataRow (true, false, false)]
		[DataRow (false, true, true)]
		[DataRow (true, true, true)]
		public void NativeAotTypeMapKeysSelectOnlyJavaUniverse (bool elf32, bool javaGroup, bool commandStream)
		{
			string objectFile = WriteNativeObject (commandStream ? ["unrelated"] : [],
				elf32: elf32, typeMap: true, javaGroup: javaGroup, commandStream: commandStream);
			var root = XDocument.Parse (Run ("test.Live -> a.b:\n", objectFile)).Root
				?? throw new AssertFailedException ("Generated XML has no root.");
			Assert.AreEqual (javaGroup ? 1 : 0, root.Elements ("replace-type").Count ());
			Assert.AreEqual (javaGroup ? 1 : 0, root.Elements ("reverse-type").Count ());
		}

		[TestMethod]
		[DataRow (false, "Mono_Android_0_Java_Lang_Object", true)]
		[DataRow (true, "Mono_Android_12_Java_Lang_Object", true)]
		[DataRow (false, "_Owner_TypeMap_0___TypeMapAnchor", true)]
		[DataRow (true, "_Owner_TypeMap_12___TypeMapAnchor", true)]
		[DataRow (false, "Mono_Android_0_Java_Lang_Object_1", true)]
		[DataRow (true, "_Owner_TypeMap_12___TypeMapAnchor_0", true)]
		[DataRow (false, "Mono_Android_0_Android_Runtime_JavaDictionary", false)]
		[DataRow (true, "Mono_Android_x_Java_Lang_Object", false)]
		[DataRow (false, "_Owner_TypeMap_x___TypeMapAnchor", false)]
		[DataRow (true, "_Owner_TypeMap_0___TypeMapAnchorExtra", false)]
		public void NativeAotMangledGroupsRespectJavaUniverse (bool elf32, string typeName, bool javaGroup)
		{
			string objectFile = WriteNativeObject ([], elf32: elf32, typeMap: true, groupSymbol: $"_ZTV{typeName.Length}{typeName}");
			var root = XDocument.Parse (Run ("test.Live -> a.b:\n", objectFile)).Root
				?? throw new AssertFailedException ("Generated XML has no root.");
			if (javaGroup) {
				Assert.AreEqual ("test/Live", (string?) root.Elements ("replace-type").Single ().Attribute ("from"));
				Assert.AreEqual ("test/Live", (string?) root.Elements ("reverse-type").Single ().Attribute ("to"));
			} else {
				Assert.IsEmpty (root.Elements (), "Disambiguation must not admit CLR or lookalike mapping universes.");
			}
		}

		[TestMethod]
		[DataRow (false, false)]
		[DataRow (true, false)]
		[DataRow (false, true)]
		[DataRow (true, true)]
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
			Assert.IsFalse (File.Exists (outputFile));
		}

		[TestMethod]
		[DataRow (false, false)]
		[DataRow (true, false)]
		[DataRow (false, true)]
		[DataRow (true, true)]
		public void NativeAotMethodTableMarkerIsNotFrozenString (bool elf32, bool hydrationFileBacked)
		{
			string objectFile = WriteNativeObject (["com/contoso/Peer", "run.()V"],
				commandStream: true, elf32: elf32, methodTableMarker: true, hydrationFileBacked: hydrationFileBacked);
			var root = XDocument.Parse (Run ("""
				com.contoso.Peer -> a.b:
				    void run() -> c

				""", objectFile)).Root ?? throw new AssertFailedException ("Generated XML has no root.");
			Assert.AreEqual ("com/contoso/Peer", (string?) root.Elements ("replace-type").Single ().Attribute ("from"));
			Assert.AreEqual ("com/contoso/Peer", (string?) root.Elements ("reverse-type").Single ().Attribute ("to"));
			Assert.AreEqual ("c", (string?) root.Elements ("replace-method").Single ().Attribute ("target-method-name"));
			Assert.IsEmpty (Errors, "An unrelated managed type must not be decoded as a frozen string.");
		}

		[TestMethod]
		[DataRow (false)]
		[DataRow (true)]
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
			var root = XDocument.Parse (xml).Root ?? throw new AssertFailedException ("Generated XML has no root.");
			CollectionAssert.AreEquivalent (new [] { "com/contoso/Peer$Inner", "com/contoso/Argument" },
				root.Elements ("replace-type").Select (element => (string?) element.Attribute ("from")).ToArray ());
			Assert.Contains ("""<reverse-type from="a/b" to="com/contoso/Peer$Inner" />""", xml);
			Assert.Contains ("target-method-signature=\"([La/d;)V\"", xml);
		}

		[TestMethod]
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
			Assert.IsFalse (File.Exists (outputFile));
		}

		[TestMethod]
		[DataRow (false)]
		[DataRow (true)]
		public void RejectedExistingXmlCannotClaimMappings (bool malformed)
		{
			string existingXml = malformed
				? "<replacements><replace-type from=\"com/contoso/Peer\" to=\"conflicting/Peer\" /></broken>"
				: "<ignored><replace-type from=\"com/contoso/Peer\" to=\"conflicting/Peer\" /></ignored>";
			string mappingFile = Path.Combine (TestDirectory, "mapping.txt");
			string existingFile = Path.Combine (TestDirectory, "existing.xml");
			string outputFile = Path.Combine (TestDirectory, "remap.xml");
			File.WriteAllText (mappingFile, "com.contoso.Peer -> a.b:\n");
			File.WriteAllText (existingFile, existingXml);
			var task = new GenerateR8JniRemapping {
				BuildEngine = engine,
				MappingFile = mappingFile,
				OutputFile = outputFile,
				ExistingRemapXmlFiles = [new TaskItem (existingFile)],
			};
			Assert.IsTrue (task.Execute ());
			var root = XDocument.Load (outputFile).Root ?? throw new AssertFailedException ("Generated XML has no root.");
			var replacements = root.Elements ("replace-type")
				.Where (element => (string?) element.Attribute ("from") == "com/contoso/Peer").ToArray ();
			Assert.AreEqual (1, replacements.Length, "Rejected input must not contribute a conflicting mapping.");
			Assert.AreEqual ("a/b", (string?) replacements [0].Attribute ("to"));
		}

		[TestMethod]
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
				?? throw new AssertFailedException ("Generated XML has no root.");
			Assert.IsEmpty (root.Elements ());
			Assert.AreEqual ("XA4326", Warnings.Single ().Code);
		}

		[TestMethod]
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
			Assert.IsFalse (File.Exists (outputFile));
		}

		[TestMethod]
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
			Assert.DoesNotContain ("replace-type", xml);
			Assert.Contains ("source-method-name=\"run\"", xml);
		}

		[TestMethod]
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

		[TestMethod]
		public void UnsupportedMethodSignatureWarningIncludesReturnType ()
		{
			Run ("""
				com.contoso.Peer -> a.b:
				    void run( ) -> c

				""");

			Assert.AreEqual ("XA4326", Warnings.Single ().Code);
			Assert.Contains ("run( ):void", Warnings [0].Message);
		}

		[TestMethod]
		public void DescriptorDistinctR8FieldsProduceDistinctMappings ()
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
				?? throw new AssertFailedException ("Generated XML has no root.");
			CollectionAssert.AreEqual (new [] { ("I", "integerTarget"), ("Ljava/lang/String;", "stringTarget") }, fields);
		}

		[TestMethod]
		public void GeneratedDocumentIsWellFormed ()
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

			var root = XDocument.Load (outputFile).Root ?? throw new AssertFailedException ("Generated XML has no root.");
			Assert.AreEqual ("replacements", root.Name.LocalName);
			Assert.AreEqual ("a/b", (string?) root.Elements ("replace-type").Single ().Attribute ("to"));
			Assert.AreEqual ("doWork", (string?) root.Elements ("replace-method").Single ().Attribute ("source-method-name"));
			Assert.AreEqual ("d", (string?) root.Elements ("replace-field").Single ().Attribute ("target-field-name"));
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
