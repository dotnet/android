using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests.Tasks {

	[TestFixture]
	public class GenerateR8JniRemappingTests : BaseTest {

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

		string TestDirectory {
			get {
				Assert.IsNotNull (directory);
				return directory;
			}
		}

		List<BuildErrorEventArgs> Errors {
			get {
				Assert.IsNotNull (errors);
				return errors;
			}
		}

		List<BuildWarningEventArgs> Warnings {
			get {
				Assert.IsNotNull (warnings);
				return warnings;
			}
		}

		string WriteMapping (string content, string fileName = "mapping.txt")
		{
			var path = Path.Combine (TestDirectory, fileName);
			File.WriteAllText (path, content);
			return path;
		}

		string WriteRemapXml (string content, string fileName = "existing.xml")
		{
			var path = Path.Combine (TestDirectory, fileName);
			File.WriteAllText (path, content);
			return path;
		}

		string Run (string mappingContent, params string [] existingRemapXmlFiles)
			=> Run (mappingContent, null, existingRemapXmlFiles);

		string Run (string mappingContent, string []? linkedAssemblies, string [] existingRemapXmlFiles, string? nativeAotObjectFile = null)
		{
			var outputFile = Path.Combine (TestDirectory, "r8-jni-remap.xml");
			var task = new GenerateR8JniRemapping {
				BuildEngine = engine,
				MappingFile = WriteMapping (mappingContent),
				OutputFile = outputFile,
				ExistingRemapXmlFiles = existingRemapXmlFiles
					.Select (f => (ITaskItem) new TaskItem (f))
					.ToArray (),
				LinkedAssemblies = linkedAssemblies?
					.Select (f => (ITaskItem) new TaskItem (f))
					.ToArray (),
				NativeAot = nativeAotObjectFile != null,
				NativeAotObjectFile = nativeAotObjectFile,
			};
			Assert.IsTrue (task.Execute (), "Task should have succeeded.");
			Assert.AreEqual (0, Errors.Count, "Task should have no errors.");
			FileAssert.Exists (outputFile);
			return File.ReadAllText (outputFile);
		}

		string WriteNativeObject (string [] literals, bool utf8 = false, bool dehydrated = false,
			string []? debugLiterals = null, bool managedCode = true, bool elf32 = false)
		{
			byte [] Encode (string [] values)
			{
				using var data = new MemoryStream ();
				foreach (string value in values) {
					byte [] bytes = (utf8 ? Encoding.UTF8 : Encoding.Unicode).GetBytes (value);
					int start = dehydrated && bytes [0] == 0 ? 1 : 0;
					int end = bytes.Length - (dehydrated && bytes [bytes.Length - 1] == 0 ? 1 : 0);
					data.Write (bytes, start, end - start);
					data.WriteByte (0xFF);
					data.WriteByte (0xFF);
				}
				return data.ToArray ();
			}

			var sections = new [] {
				(Name: "", Flags: 0UL, Type: 0U, Bytes: new byte [0]),
				(Name: ".shstrtab", Flags: 0UL, Type: 3U, Bytes: new byte [0]),
				(Name: managedCode ? "__managedcode" : ".text", Flags: 6UL, Type: 1U,
					Bytes: elf32 ? new byte [] { 0x1E, 0xFF, 0x2F, 0xE1 } : new byte [] { 0xC0, 0x03, 0x5F, 0xD6 }),
				(Name: ".rodata", Flags: 2UL, Type: 1U, Bytes: Encode (literals)),
				(Name: ".debug_info", Flags: 0UL, Type: 1U, Bytes: Encode (debugLiterals ?? [])),
			};
			sections [1].Bytes = Encoding.UTF8.GetBytes (string.Join ("\0", sections.Select (s => s.Name)) + "\0");
			var offsets = new long [sections.Length];
			using var image = new MemoryStream ();
			using var writer = new BinaryWriter (image);
			void WriteWord (ulong value)
			{
				if (elf32) {
					writer.Write (checked ((uint) value));
				} else {
					writer.Write (value);
				}
			}
			writer.Write (new byte [] { 0x7F, (byte) 'E', (byte) 'L', (byte) 'F', elf32 ? (byte) 1 : (byte) 2, 1, 1, 0 });
			writer.Write (0UL);
			writer.Write ((ushort) 1); // ET_REL
			writer.Write (elf32 ? (ushort) 40 : (ushort) 183); // ARM or AArch64
			writer.Write (1U);
			WriteWord (0); // entry point
			WriteWord (0); // program headers
			WriteWord (0); // section headers, filled below
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
				WriteWord (sections [i].Flags);
				WriteWord (0);
				WriteWord ((ulong) offsets [i]);
				WriteWord ((ulong) sections [i].Bytes.Length);
				writer.Write (0U); // link
				writer.Write (0U); // info
				WriteWord (i == 0 ? 0UL : 1UL);
				WriteWord (0);
				nameIndex += Encoding.UTF8.GetByteCount (sections [i].Name) + 1;
			}
			image.Position = elf32 ? 32 : 40;
			WriteWord ((ulong) sectionHeaders);
			string path = Path.Combine (TestDirectory, "app.o");
			File.WriteAllBytes (path, image.ToArray ());
			return path;
		}

		[TestCase (false, false, false)]
		[TestCase (false, true, false)]
		[TestCase (true, false, false)]
		[TestCase (false, false, true)]
		[TestCase (false, true, true)]
		[TestCase (true, false, true)]
		public void NativeAotFiltersMembersAndOverloadsOfRetainedType (bool utf8, bool dehydrated, bool elf32)
		{
			var nativeObject = WriteNativeObject (
				["com/contoso/Peer", "run.(I)V", "value.I", "callback:()V:n_Callback"],
				utf8, dehydrated,
				debugLiterals: ["removed.()V", "run.(Ljava/lang/String;)V", "unused.I", "com/contoso/Unused"],
				elf32: elf32);
			var xml = Run (
				"""
				com.contoso.Peer -> a.b:
				    void run(int) -> c
				    void run(java.lang.String) -> d
				    void removed() -> e
				    void callback() -> f
				    int value -> g
				    int unused -> h
				com.contoso.Unused -> a.i:
				    void run(int) -> j
				""", null, [], nativeObject);

			StringAssert.Contains (Method ("a/b", "run", "(I)V", "a/b", "c", "(I)V"), xml);
			StringAssert.Contains (Method ("a/b", "callback", "()V", "a/b", "f", "()V"), xml);
			StringAssert.Contains (Field ("a/b", "value", "I", "a/b", "g", "I"), xml);
			StringAssert.DoesNotContain ("removed", xml);
			StringAssert.DoesNotContain ("unused", xml);
			StringAssert.DoesNotContain ("Unused", xml);
			StringAssert.DoesNotContain ("Ljava/lang/String;", xml);
		}

		[Test]
		public void NativeAotRetainsConstructorsAndDescriptorOnlyTypes ()
		{
			var nativeObject = WriteNativeObject (["com/contoso/Peer", "([Lcom/contoso/Argument;)V",
				"run.([Lcom/contoso/Argument;)Lcom/contoso/Result;"]);
			var xml = Run (
				"""
				com.contoso.Peer -> a.b:
				    void <init>(com.contoso.Argument[]) -> <init>
				    void <init>(int) -> <init>
				    com.contoso.Result run(com.contoso.Argument[]) -> c
				com.contoso.Argument -> a.d:
				com.contoso.Result -> a.e:
				""", null, [], nativeObject);

			StringAssert.Contains (Method ("a/b", "&lt;init&gt;", "([Lcom/contoso/Argument;)V",
				"a/b", "&lt;init&gt;", "([La/d;)V"), xml);
			StringAssert.Contains (Method ("a/b", "run", "([Lcom/contoso/Argument;)Lcom/contoso/Result;",
				"a/b", "c", "([La/d;)La/e;"), xml);
			StringAssert.Contains ("""<replace-type from="com/contoso/Argument" to="a/d" />""", xml);
			StringAssert.Contains ("""<reverse-type from="a/e" to="com/contoso/Result" />""", xml);
			StringAssert.DoesNotContain ("(I)V", xml);
		}

		[Test]
		public void NativeAotSharedGenericAndInlinedLiteralsConservativelyRetainEveryOwner ()
		{
			// Generic instantiations and inlined methods do not need distinct compiled method
			// symbols. A shared Java-erased member ID is sufficient for both reachable owners.
			var nativeObject = WriteNativeObject (["com/contoso/Generic", "com/contoso/Generic$Nested",
				"get.(Ljava/lang/Object;)Ljava/lang/Object;"]);
			var xml = Run (
				"""
				com.contoso.Generic -> a.b:
				    java.lang.Object get(java.lang.Object) -> c
				    int get(int) -> d
				com.contoso.Generic$Nested -> a.e:
				    java.lang.Object get(java.lang.Object) -> f
				""", null, [], nativeObject);

			StringAssert.Contains (Method ("a/b", "get", "(Ljava/lang/Object;)Ljava/lang/Object;",
				"a/b", "c", "(Ljava/lang/Object;)Ljava/lang/Object;"), xml);
			StringAssert.Contains (Method ("a/e", "get", "(Ljava/lang/Object;)Ljava/lang/Object;",
				"a/e", "f", "(Ljava/lang/Object;)Ljava/lang/Object;"), xml);
			StringAssert.DoesNotContain ("(I)I", xml);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void NativeAotRetainsUnicodeIdentifiers (bool dehydrated)
		{
			var nativeObject = WriteNativeObject (["com/contoso/例", "Āction.()V", "café.()V"], dehydrated: dehydrated);
			var xml = Run (
				"""
				com.contoso.例 -> a.b:
				    void Āction() -> c
				    void café() -> d
				""", null, [], nativeObject);
			StringAssert.Contains ("Āction", xml);
			StringAssert.Contains ("café", xml);
		}

		[Test]
		public void NativeAotEncodingCollisionsConservativelyRetainBothMembers ()
		{
			// UTF-8 U+0100 and UTF-16 U+80C4 have the same bytes. Neither interpretation
			// may overwrite the other in the retention index.
			var nativeObject = WriteNativeObject (["com/contoso/Peer", "\u0100.()V"], utf8: true);
			var xml = Run ("com.contoso.Peer -> a.b:\n    void \u0100() -> c\n    void \u80C4() -> d\n",
				null, [], nativeObject);
			StringAssert.Contains ("\u0100", xml);
			StringAssert.Contains ("\u80C4", xml);
		}

		[Test]
		public void NativeAotEmptySelectionDoesNotFallBackToFullMapping ()
		{
			var nativeObject = WriteNativeObject (["unrelated literal"]);
			var xml = Run ("com.contoso.Unused -> a.b:\n    void unused() -> c\n",
				[Path.Combine (TestDirectory, "PreIlc.dll")], [], nativeObject);
			StringAssert.DoesNotContain ("com/contoso/Unused", xml);
			StringAssert.DoesNotContain ("replace-method", xml);
		}

		[TestCase ("missing")]
		[TestCase ("truncated")]
		[TestCase ("unrelated-object")]
		[TestCase ("invalid-section")]
		public void InvalidNativeAotRetentionIsReportedAsXA4327 (string kind)
		{
			string path = Path.Combine (TestDirectory, "missing.o");
			switch (kind) {
			case "truncated":
				File.WriteAllBytes (path, [0x7F, (byte) 'E', (byte) 'L', (byte) 'F', 2, 1, 1]);
				break;
			case "unrelated-object":
				path = WriteNativeObject (["com/contoso/Peer"], managedCode: false);
				break;
			case "invalid-section":
				path = WriteNativeObject (["com/contoso/Peer"]);
				using (var file = File.Open (path, FileMode.Open, FileAccess.ReadWrite)) {
					using var reader = new BinaryReader (file, Encoding.UTF8, leaveOpen: true);
					using var writer = new BinaryWriter (file, Encoding.UTF8, leaveOpen: true);
					file.Position = 40;
					long sectionHeaders = reader.ReadInt64 ();
					file.Position = sectionHeaders + 3 * 64 + 24;
					writer.Write ((ulong) file.Length + 1);
				}
				break;
			}
			var task = new GenerateR8JniRemapping {
				BuildEngine = engine,
				MappingFile = WriteMapping ("com.contoso.Peer -> a.b:\n"),
				OutputFile = Path.Combine (TestDirectory, "r8-jni-remap.xml"),
				NativeAot = true,
				NativeAotObjectFile = path,
			};
			Assert.IsFalse (task.Execute ());
			Assert.AreEqual (1, Errors.Count);
			Assert.AreEqual ("XA4327", Errors [0].Code);
			FileAssert.DoesNotExist (task.OutputFile);
		}

		[Test]
		public void LinkedAssembliesFilterUnusedMappings ()
		{
			var fixture = new JniFixtureBuilder ();
			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;
			MethodDefinitionHandle onClick = fixture.AddVoidMethod ("OnClick", fixture.EmitReturnOnlyBody ());
			fixture.Metadata.AddCustomAttribute (onClick, fixture.RegisterCtor3,
				fixture.AttributeBlob ("onClick", "()V", "n_OnClick"));
			TypeDefinitionHandle peer = fixture.AddType ("Com.Contoso", "Peer", fieldStart, methodStart,
				TypeAttributes.Public | TypeAttributes.Class);
			fixture.Metadata.AddCustomAttribute (peer, fixture.RegisterCtor1, fixture.AttributeBlob ("com/contoso/Peer"));

			string assembly = Path.Combine (TestDirectory, "Linked.dll");
			File.WriteAllBytes (assembly, fixture.Serialize ());
			var xml = Run (
				"""
				com.contoso.Peer -> a.b:
				    void onClick() -> c
				com.contoso.Unused -> a.d:
				    void unused() -> e
				""",
				[ assembly ],
				[]);

			StringAssert.Contains ("""<replace-type from="com/contoso/Peer" to="a/b" />""", xml);
			StringAssert.Contains (Method ("a/b", "onClick", "()V", "a/b", "c", "()V"), xml);
			StringAssert.DoesNotContain ("com/contoso/Unused", xml);
			StringAssert.DoesNotContain ("unused", xml);
		}

		static string Method (string sourceType, string name, string signature, string targetType, string targetName, string targetSignature) =>
			$"""<replace-method source-type="{sourceType}" source-method-name="{name}" source-method-signature="{signature}" target-type="{targetType}" target-method-name="{targetName}" target-method-signature="{targetSignature}" target-method-instance-to-static="false" />""";

		static string Field (string sourceType, string name, string signature, string targetType, string targetName, string targetSignature) =>
			$"""<replace-field source-type="{sourceType}" source-field-name="{name}" source-field-signature="{signature}" target-type="{targetType}" target-field-name="{targetName}" target-field-signature="{targetSignature}" />""";

		[Test]
		public void RemovedClassesAreSkipped ()
		{
			var xml = Run (
				"""
				com.contoso.Gone -> R8$$REMOVED$$CLASS$$1:
				""");

			StringAssert.DoesNotContain ("com/contoso/Gone", xml);
		}

		[Test]
		public void RenamedMembersUseResidualOwnersAndOriginalSignatures ()
		{
			var xml = Run (
				"""
				com.contoso.Peer -> a.b:
				    com.contoso.Peer run(com.contoso.Peer[]) -> c
				    com.contoso.Peer[] peers -> d
				""");

			StringAssert.Contains ("""<replace-type from="com/contoso/Peer" to="a/b" />""", xml);
			StringAssert.Contains ("""<reverse-type from="a/b" to="com/contoso/Peer" />""", xml);
			StringAssert.Contains (Method ("a/b", "run", "([Lcom/contoso/Peer;)Lcom/contoso/Peer;",
				"a/b", "c", "([La/b;)La/b;"), xml);
			StringAssert.Contains (Field ("a/b", "peers", "[Lcom/contoso/Peer;", "a/b", "d", "[La/b;"), xml);
			StringAssert.DoesNotContain ("source-type=\"com/contoso/Peer\"", xml);
			Assert.AreEqual (0, Warnings.Count);
		}

		[Test]
		public void ConstructorsAreEmittedWhenOnlyTheirDescriptorChanges ()
		{
			var xml = Run (
				"""
				com.contoso.Peer -> com.contoso.Peer:
				    void <init>(com.contoso.Argument) -> <init>
				com.contoso.Argument -> a.d:
				""");

			StringAssert.Contains (
				Method ("com/contoso/Peer", "&lt;init&gt;", "(Lcom/contoso/Argument;)V", "com/contoso/Peer", "&lt;init&gt;", "(La/d;)V"),
				xml);
		}

		[Test]
		public void UnchangedMembersAreNotEmitted ()
		{
			var xml = Run (
				"""
				com.contoso.Peer -> com.contoso.Peer:
				    void doWork(int) -> doWork
				    int counter -> counter
				""");

			StringAssert.DoesNotContain ("replace-method", xml);
			StringAssert.DoesNotContain ("replace-field", xml);
			StringAssert.DoesNotContain ("replace-type", xml);
			StringAssert.DoesNotContain ("reverse-type", xml);
		}

		[TestCase ("int", "I", "java.lang.String", "Ljava/lang/String;")]
		[TestCase ("com.contoso.One", "Lcom/contoso/One;", "com.contoso.Two", "Lcom/contoso/Two;")]
		public void MergedFieldsKeepDistinctSourceSignatures (string firstType, string firstSignature, string secondType, string secondSignature)
		{
			var xml = Run (
				$"""
				com.contoso.One -> a.b:
				    {firstType} value -> c
				com.contoso.Two -> a.b:
				    {secondType} value -> d
				""");

			string firstTargetSignature = firstType == "com.contoso.One" ? "La/b;" : firstSignature;
			string secondTargetSignature = secondType == "com.contoso.Two" ? "La/b;" : secondSignature;
			StringAssert.Contains (Field ("a/b", "value", firstSignature, "a/b", "c", firstTargetSignature), xml);
			StringAssert.Contains (Field ("a/b", "value", secondSignature, "a/b", "d", secondTargetSignature), xml);
			StringAssert.Contains ("""<replace-type from="com/contoso/One" to="a/b" />""", xml);
			StringAssert.Contains ("""<replace-type from="com/contoso/Two" to="a/b" />""", xml);
			StringAssert.DoesNotContain ("reverse-type", xml, "Merged classes have no unambiguous reverse mapping.");
			Assert.AreEqual (0, Warnings.Count, "Different source descriptors must not conflict, even if the target descriptors match.");
		}

		[TestCase (false)]
		[TestCase (true)]
		public void ExistingFieldEntriesOnlyConflictForTheSameSignature (bool identicalTarget)
		{
			var existing = WriteRemapXml (
				$"""
				<replacements>
				  {Field ("a/b", "value", "I", identicalTarget ? "a/b" : "com/contoso/Mam", "c", "I")}
				</replacements>
				""");
			var xml = Run (
				"""
				com.contoso.One -> a.b:
				    int value -> c
				com.contoso.Two -> a.b:
				    java.lang.String value -> d
				""",
				existing);

			StringAssert.DoesNotContain ("""source-field-signature="I" """, xml, "The existing mapping must win for the same signature.");
			StringAssert.Contains (Field ("a/b", "value", "Ljava/lang/String;", "a/b", "d", "Ljava/lang/String;"), xml);
			Assert.AreEqual (identicalTarget ? 0 : 1, Warnings.Count);
			if (!identicalTarget) {
				Assert.AreEqual ("XA4328", Warnings [0].Code);
			}
		}

		[Test]
		public void AmbiguousMethodNamesAreSkipped ()
		{
			// The same method mapped to two different residual names has no single runtime name.
			var xml = Run (
				"""
				com.contoso.Peer -> a.b:
				    void doWork(int) -> c
				    void doWork(int) -> d
				""");

			StringAssert.DoesNotContain ("doWork", xml);
		}

		[Test]
		public void OutputIsDeterministic ()
		{
			// The mapping is written in a different order the second time around.
			const string first =
				"""
				com.contoso.Zebra -> a.b:
				    void run(int) -> c
				    int counter -> d
				com.contoso.Apple -> a.e:
				    void run() -> f
				""";
			const string second =
				"""
				com.contoso.Apple -> a.e:
				    void run() -> f
				com.contoso.Zebra -> a.b:
				    int counter -> d
				    void run(int) -> c
				""";

			Assert.AreEqual (Run (first), Run (second), "The output must not depend on the mapping file's order.");
		}

		[Test]
		public void MalformedMappingIsReportedAsXA4327 ()
		{
			var task = new GenerateR8JniRemapping {
				BuildEngine = engine,
				MappingFile = WriteMapping ("    void doWork(int) -> c\n"),
				OutputFile = Path.Combine (TestDirectory, "r8-jni-remap.xml"),
			};

			Assert.IsFalse (task.Execute (), "Task should have failed.");
			Assert.AreEqual (1, Errors.Count, "Task should have reported one error.");
			Assert.AreEqual ("XA4327", Errors [0].Code);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void ExistingRemapEntriesAreNotOverridden (bool identicalTarget)
		{
			string targetType = identicalTarget ? "a/b" : "com/microsoft/intune/MainActivity";
			var existing = WriteRemapXml (
				$"""
				<replacements>
				  <replace-type from="com/contoso/MainActivity" to="{targetType}" />
				</replacements>
				""");

			var xml = Run (
				"""
				com.contoso.MainActivity -> a.b:
				    void onCreate() -> c
				    int counter -> d
				com.contoso.Other -> a.c:
				""",
				existing);

			StringAssert.DoesNotContain ("com/contoso/MainActivity", xml,
				"The pre-existing remapping input must win.");
			StringAssert.DoesNotContain ("source-type=\"a/b\"", xml,
				"Members of an externally owned type must not be emitted using the residual owner.");
			StringAssert.Contains ("""<replace-type from="com/contoso/Other" to="a/c" />""", xml);
			Assert.AreEqual (identicalTarget ? 0 : 1, Warnings.Count);
			if (!identicalTarget) {
				Assert.AreEqual ("XA4328", Warnings [0].Code);
			}
		}

		[Test]
		public void ExistingMethodEntriesOnlyConflictForTheSameOverload ()
		{
			var existing = WriteRemapXml (
				"""
				<replacements>
				  <replace-method source-type="a/b" source-method-name="doWork" source-method-signature="(I)V" target-type="com/contoso/Mam" target-method-name="doWorkMam" target-method-instance-to-static="true" />
				</replacements>
				""");

			var xml = Run (
				"""
				com.contoso.Peer -> a.b:
				    void doWork(int) -> c
				    void doWork(java.lang.String) -> d
				""",
				existing);

			StringAssert.DoesNotContain ("(I)V", xml,
				"The overload owned by another input must not be emitted.");
			StringAssert.Contains (Method ("a/b", "doWork", "(Ljava/lang/String;)V", "a/b", "d", "(Ljava/lang/String;)V"), xml,
				"A different overload is not a conflict.");
			Assert.AreEqual (1, Warnings.Count);
			Assert.AreEqual ("XA4328", Warnings [0].Code);
		}

		[TestCase ("a/b", false)]
		[TestCase ("a/b", true)]
		[TestCase ("com/contoso/Peer", false)]
		[TestCase ("com/contoso/Peer", true)]
		public void ExistingMemberEntriesAreMatchedOnResidualOwner (string sourceType, bool identicalTarget)
		{
			string targetType = identicalTarget ? "a/b" : "com/contoso/Mam";
			var existing = WriteRemapXml (
				$"""
				<replacements>
				  {Method (sourceType, "run", "([Lcom/contoso/Peer;)Lcom/contoso/Peer;", targetType, "c", "([La/b;)La/b;")}
				  {Field (sourceType, "peers", "[Lcom/contoso/Peer;", targetType, "d", "[La/b;")}
				</replacements>
				""");
			var xml = Run (
				"""
				com.contoso.Peer -> a.b:
				    com.contoso.Peer run(com.contoso.Peer[]) -> c
				    com.contoso.Peer[] peers -> d
				""", existing);

			StringAssert.Contains ("""<replace-type from="com/contoso/Peer" to="a/b" />""", xml);
			StringAssert.Contains ("""<reverse-type from="a/b" to="com/contoso/Peer" />""", xml);
			if (sourceType == "a/b") {
				StringAssert.DoesNotContain ("replace-method", xml);
				StringAssert.DoesNotContain ("replace-field", xml);
				Assert.AreEqual (identicalTarget ? 0 : 2, Warnings.Count);
			} else {
				StringAssert.Contains (Method ("a/b", "run", "([Lcom/contoso/Peer;)Lcom/contoso/Peer;",
					"a/b", "c", "([La/b;)La/b;"), xml);
				StringAssert.Contains (Field ("a/b", "peers", "[Lcom/contoso/Peer;", "a/b", "d", "[La/b;"), xml);
				Assert.AreEqual (0, Warnings.Count, "An original owner is a different member lookup key.");
			}
			foreach (var warning in Warnings) {
				Assert.AreEqual ("XA4328", warning.Code);
			}
		}

		[Test]
		public void GeneratedDocumentParsesWithTheExistingRemapSchema ()
		{
			var mappingFile = WriteMapping (
				"""
				com.contoso.Peer -> a.b:
				    void doWork(int) -> c
				    int counter -> d
				""");
			var outputFile = Path.Combine (TestDirectory, "r8-jni-remap.xml");
			var task = new GenerateR8JniRemapping {
				BuildEngine = engine,
				MappingFile = mappingFile,
				OutputFile = outputFile,
			};
			Assert.IsTrue (task.Execute (), "Task should have succeeded.");

			var mergedFile = Path.Combine (TestDirectory, "xa-remap-members.xml");
			var mamFile = WriteRemapXml (
				"""
				<replacements>
				  <replace-type from="com/contoso/Mam" to="com/microsoft/intune/Mam" />
				</replacements>
				""",
				"mam.xml");
			var merge = new MergeRemapXml {
				BuildEngine = engine,
				InputRemapXmlFiles = new ITaskItem [] {
					new TaskItem (mamFile),
					new TaskItem (outputFile),
				},
				OutputFile = new TaskItem (mergedFile),
			};
			Assert.IsTrue (merge.Execute (), "MergeRemapXml should have succeeded.");
			Assert.AreEqual (0, Errors.Count, "The merge should have no errors.");

			var merged = File.ReadAllText (mergedFile);
			StringAssert.Contains ("""<replace-type from="com/contoso/Mam" to="com/microsoft/intune/Mam" />""", merged,
				"Existing inputs must survive the merge.");
			StringAssert.Contains ("""<replace-type from="com/contoso/Peer" to="a/b" />""", merged);
			StringAssert.Contains ("replace-field", merged, "New elements must survive the merge.");

			// The pre-existing consumer must still be able to read the merged document.
			var generate = new GenerateJniRemappingNativeCode {
				BuildEngine = engine,
				RemappingXmlFilePath = new TaskItem (mergedFile),
				OutputDirectory = TestDirectory,
				SupportedAbis = new [] { "arm64-v8a" },
			};
			Assert.IsTrue (generate.Execute (), "GenerateJniRemappingNativeCode should have succeeded.");
			Assert.AreEqual (0, Errors.Count, "The generated document must parse with the existing schema.");
		}
	}
}
