#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
			Assert.That (Errors, Has.Some.Property ("Code").EqualTo ("XA4327"));
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
		public void MalformedMappingReportsXA4327 ()
		{
			string mappingFile = Path.Combine (TestDirectory, "mapping.txt");
			File.WriteAllText (mappingFile, "    void run() -> a\n");
			var task = new GenerateR8JniRemapping {
				BuildEngine = engine,
				MappingFile = mappingFile,
				OutputFile = Path.Combine (TestDirectory, "output.xml"),
			};

			Assert.IsFalse (task.Execute ());
			Assert.AreEqual ("XA4327", Errors.Single ().Code);
		}

		string WriteNativeObject (string [] literals)
		{
			byte [] Encode ()
			{
				using var data = new MemoryStream ();
				foreach (string value in literals) {
					data.Write (Encoding.Unicode.GetBytes (value));
					data.WriteByte (0xFF);
					data.WriteByte (0xFF);
				}
				return data.ToArray ();
			}

			var sections = new [] {
				(Name: "", Flags: 0UL, Type: 0U, Bytes: new byte [0]),
				(Name: ".shstrtab", Flags: 0UL, Type: 3U, Bytes: new byte [0]),
				(Name: "__managedcode", Flags: 6UL, Type: 1U, Bytes: new byte [] { 0xC0, 0x03, 0x5F, 0xD6 }),
				(Name: ".rodata", Flags: 2UL, Type: 1U, Bytes: Encode ()),
			};
			sections [1].Bytes = Encoding.UTF8.GetBytes (string.Join ("\0", sections.Select (section => section.Name)) + "\0");
			var offsets = new long [sections.Length];
			using var image = new MemoryStream ();
			using var writer = new BinaryWriter (image);
			writer.Write (new byte [] { 0x7F, (byte) 'E', (byte) 'L', (byte) 'F', 2, 1, 1, 0 });
			writer.Write (0UL);
			writer.Write ((ushort) 1);
			writer.Write ((ushort) 183);
			writer.Write (1U);
			writer.Write (0UL);
			writer.Write (0UL);
			writer.Write (0UL);
			writer.Write (0U);
			writer.Write ((ushort) 64);
			writer.Write ((ushort) 0);
			writer.Write ((ushort) 0);
			writer.Write ((ushort) 64);
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
				writer.Write (sections [i].Flags);
				writer.Write (0UL);
				writer.Write ((ulong) offsets [i]);
				writer.Write ((ulong) sections [i].Bytes.Length);
				writer.Write (0U);
				writer.Write (0U);
				writer.Write (i == 0 ? 0UL : 1UL);
				writer.Write (0UL);
				nameIndex += Encoding.UTF8.GetByteCount (sections [i].Name) + 1;
			}
			image.Position = 40;
			writer.Write ((ulong) sectionHeaders);
			string path = Path.Combine (TestDirectory, "app.o");
			File.WriteAllBytes (path, image.ToArray ());
			return path;
		}
	}
}
