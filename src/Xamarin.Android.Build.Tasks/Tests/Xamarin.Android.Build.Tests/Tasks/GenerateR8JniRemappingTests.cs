#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;

using Xamarin.Android.Tasks;

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

		[Test]
		public void RetentionMakesMergedReverseTypeUnambiguous ()
		{
			string objectFile = WriteNativeObject (["com/contoso/First"]);
			string xml = Run ("""
				com.contoso.First -> a.b:
				com.contoso.Second -> a.b:

				""", objectFile);

			StringAssert.Contains ("""<reverse-type from="a/b" to="com/contoso/First" />""", xml);
			StringAssert.DoesNotContain ("com/contoso/Second", xml);
		}

		[Test]
		public void SameNamedFieldsKeepBothDescriptorsInXml ()
		{
			string xml = Run ("""
				com.contoso.Peer -> a.b:
				    int value -> c
				    java.lang.String value -> d

				""");
			var fields = XDocument.Parse (xml).Root?.Elements ("replace-field").ToArray ()
				?? throw new AssertionException ("Generated XML has no root.");
			Assert.AreEqual (2, fields.Length);
			Assert.AreEqual ("I", (string?) fields [0].Attribute ("source-field-signature"));
			Assert.AreEqual ("c", (string?) fields [0].Attribute ("target-field-name"));
			Assert.AreEqual ("Ljava/lang/String;", (string?) fields [1].Attribute ("source-field-signature"));
			Assert.AreEqual ("d", (string?) fields [1].Attribute ("target-field-name"));
		}

		[TestCase (false, false)]
		[TestCase (true, false)]
		[TestCase (false, true)]
		public void NativeAotNestedNameDoesNotRetainMergedParent (bool utf8, bool dehydrated)
		{
			string objectFile = WriteNativeObject (["com/contoso/Peer$Inner"], utf8, dehydrated);
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
		public void NativeAotRetainsCompleteClassAndDescriptorTokens (bool utf8, bool dehydrated)
		{
			string objectFile = WriteNativeObject (
				["com.contoso.Peer$Inner", "run.([Lcom/contoso/Argument;)Lcom/contoso/Result;",
					"(ILcom/contoso/Argument;[[I)Lcom/contoso/Result;",
					"com/contoso/LongerPeer", "othercom/contoso/Suffix", "OtherLcom/contoso/Suffix;",
					"com/contoso/PrefixedExtra"], utf8, dehydrated);
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
		public void NativeAotTrulyRetainedMergedTypesRemainAmbiguous (bool utf8, bool dehydrated)
		{
			string objectFile = WriteNativeObject (["com/contoso/Peer", "com/contoso/Peer$Inner"], utf8, dehydrated);
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
		public void NativeAotRetainsUnicodeClasses (bool utf8, bool dehydrated)
		{
			string objectFile = WriteNativeObject (["com/contoso/\u0100Peer", "run.()V"], utf8, dehydrated);
			string xml = Run ("com.contoso.\u0100Peer -> a.b:\n    void run() -> c\n", objectFile);
			StringAssert.Contains ("from=\"com/contoso/\u0100Peer\"", xml);
			StringAssert.Contains ("source-method-name=\"run\"", xml);
		}

		[TestCase (false, false)]
		[TestCase (true, false)]
		[TestCase (false, true)]
		public void NativeAotUnicodeBoundariesDoNotCreateSuffixOrParentMatches (bool utf8, bool dehydrated)
		{
			string objectFile = WriteNativeObject (["\u0100Peer$Inner", "Other\u0100Suffix"], utf8, dehydrated);
			string xml = Run (
				"\u0100Peer -> a.b:\n\u0100Peer$Inner -> a.b:\n\u0100Suffix -> a.c:\n", objectFile);
			var types = XDocument.Parse (xml).Root?.Elements ("replace-type").ToArray ()
				?? throw new AssertionException ("Generated XML has no root.");
			Assert.AreEqual (1, types.Length);
			Assert.AreEqual ("\u0100Peer$Inner", (string?) types [0].Attribute ("from"));
		}

		[TestCase (false, false)]
		[TestCase (true, false)]
		[TestCase (false, true)]
		[TestCase (true, true)]
		public void NativeAotRealDehydrationCommandsAreNotIdentifierBoundaries (bool utf8, bool elf32)
		{
			string objectFile = WriteNativeObject (
				["com/contoso/Peer$Inner", "run.([Lcom/contoso/Argument;)V"], utf8, commandStream: true, elf32: elf32);
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

		string WriteNativeObject (string [] literals, bool utf8 = false, bool dehydrated = false,
			bool commandStream = false, bool invalidCommand = false, bool elf32 = false)
		{
			byte [] Encode ()
			{
				using var data = new MemoryStream ();
				foreach (string value in literals) {
					byte [] bytes = (utf8 ? Encoding.UTF8 : Encoding.Unicode).GetBytes (value);
					int start = dehydrated && bytes [0] == 0 ? 1 : 0;
					int end = bytes.Length - (dehydrated && bytes [bytes.Length - 1] == 0 ? 1 : 0);
					data.Write (bytes, start, end - start);
					data.WriteByte (0xFF);
					data.WriteByte (0xFF);
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
					Command (1, 8); // Frozen-object sync block.
					Command (3, 0); // PtrReloc(0).
					byte [] bytes = (utf8 ? Encoding.UTF8 : Encoding.Unicode).GetBytes (value);
					int count = bytes.Length - (bytes [bytes.Length - 1] == 0 ? 1 : 0);
					Command (invalidCommand ? 6 : 0, count + 4);
					commands.Write (value.Length);
					commands.Write (bytes, 0, count);
					Command (1, 13); // 69 03 follows: ZeroFill(13), PtrReloc(0), not U+0369.
					Command (3, 0);
				}
				uint length = (uint) image.Length;
				commands.Write (0U); // Fixup-table relocation placeholder.
				image.Position = 4;
				commands.Write (length);
				return image.ToArray ();
			}

			byte [] objectData = commandStream ? EncodeCommands () : Encode ();
			var sections = new [] {
				(Name: "", Flags: 0UL, Type: 0U, Bytes: new byte [0]),
				(Name: ".shstrtab", Flags: 0UL, Type: 3U, Bytes: new byte [0]),
				(Name: "__managedcode", Flags: 6UL, Type: 1U, Bytes: new byte [] { 0xC0, 0x03, 0x5F, 0xD6 }),
				(Name: ".rodata", Flags: 2UL, Type: 1U, Bytes: objectData),
			};
			if (commandStream) {
				using var symbolData = new MemoryStream ();
				using var symbols = new BinaryWriter (symbolData);
				symbols.Write (new byte [elf32 ? 16 : 24]);
				symbols.Write (1U);
				if (elf32) {
					symbols.Write (0U);
					symbols.Write ((uint) objectData.Length);
				}
				symbols.Write ((byte) 0x11); // STB_GLOBAL, STT_OBJECT.
				symbols.Write ((byte) 0);
				symbols.Write ((ushort) 3);
				if (!elf32) {
					symbols.Write (0UL);
					symbols.Write ((ulong) objectData.Length);
				}
				sections = sections.Concat (new [] {
					(Name: ".strtab", Flags: 0UL, Type: 3U,
						Bytes: Encoding.UTF8.GetBytes ("\0fixture__dehydrated_data\0")),
					(Name: ".symtab", Flags: 0UL, Type: 2U, Bytes: symbolData.ToArray ()),
				}).ToArray ();
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
				Word ((ulong) sections [i].Bytes.Length);
				writer.Write (sections [i].Type == 2 ? 4U : 0U); // Symbol string table.
				writer.Write (sections [i].Type == 2 ? 1U : 0U); // First non-local symbol.
				Word (i == 0 ? 0UL : 1UL);
				Word (sections [i].Type == 2 ? (elf32 ? 16UL : 24UL) : 0UL);
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
