using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class R8RuntimeRemappingBuildTests : BaseTest
	{
		[TestCase (AndroidRuntime.CoreCLR, false)]
		[TestCase (AndroidRuntime.CoreCLR, true)]
		[TestCase (AndroidRuntime.NativeAOT, true)]
		public void BuildWithoutRemappingOmitsBinaryBlobs (AndroidRuntime runtime, bool release)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release)) {
				return;
			}
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = release,
			};
			proj.SetRuntime (runtime);
			proj.SetRuntimeIdentifiers (new [] { "arm64-v8a" });

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj));
			var intermediate = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			var blobs = Directory.GetFiles (intermediate, "libbinary_blobs.so", SearchOption.AllDirectories)
				.ToArray ();
			bool hasBootstrap = runtime == AndroidRuntime.CoreCLR;
			Assert.AreEqual (hasBootstrap ? 1 : 0, blobs.Length,
				"CoreCLR requires the bootstrap library even without remapping; NativeAOT does not.");
			using (var apk = ZipFile.OpenRead (Path.Combine (Root, builder.ProjectDirectory,
				proj.OutputPath, $"{proj.PackageName}-Signed.apk"))) {
				Assert.AreEqual (hasBootstrap, apk.GetEntry ("lib/arm64-v8a/libbinary_blobs.so") != null);
			}
			Assert.IsTrue (builder.Build (proj), "The no-op build should succeed.");
			Assert.IsTrue (builder.Output.IsTargetSkipped ("_AndroidGenerateBinaryBlobs"),
				"The library presence must be cached, not regenerated on every build.");
			var stamp = Directory.GetFiles (intermediate, "binary-blobs.stamp", SearchOption.AllDirectories).Single ();
			Assert.AreEqual (hasBootstrap ? "true" : "false", File.ReadAllText (stamp));
			File.Delete (stamp);
			Assert.IsTrue (builder.Build (proj), "A deleted presence stamp must be recreated.");
			Assert.AreEqual (hasBootstrap ? "true" : "false", File.ReadAllText (stamp));
			Assert.IsTrue (builder.Clean (proj));
			FileAssert.DoesNotExist (stamp);
			foreach (var blob in blobs)
				FileAssert.DoesNotExist (blob);
		}

		[TestCase (AndroidRuntime.CoreCLR, false)]
		[TestCase (AndroidRuntime.NativeAOT, true)]
		public void MamRemappingTransitionsUpdatePackages (AndroidRuntime runtime, bool aab)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: true))
				return;
			string mapping = """{"ClassRewrites":[]}""";
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				OtherBuildItems = {
					new BuildItem ("_AndroidMamMappingFile", "mam.json") { TextContent = () => mapping },
					new AndroidItem.AndroidAsset ("Assets/ordinary.txt") { TextContent = () => "ordinary asset" },
				},
			};
			proj.SetRuntime (runtime);
			proj.SetRuntimeIdentifiers (new [] { "arm64-v8a" });
			proj.SetProperty ("AndroidPackageFormats", aab ? "aab" : "apk");
			using var builder = CreateApkBuilder ();
			string intermediate = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			void AssertPackage (bool hasRemapping)
			{
				string archivePath = aab
					? Path.Combine (intermediate, "android", "bin", $"{proj.PackageName}.aab")
					: Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.apk");
				using var archive = ZipFile.OpenRead (archivePath);
				string prefix = aab ? "base/" : "";
				bool hasLibrary = runtime == AndroidRuntime.CoreCLR || hasRemapping;
				Assert.AreEqual (hasLibrary, archive.GetEntry ($"{prefix}lib/arm64-v8a/libbinary_blobs.so") != null);
				Assert.IsNotNull (archive.GetEntry ($"{prefix}assets/ordinary.txt"));
				Assert.IsNull (archive.GetEntry ($"{prefix}assets/xa-internal/xa-mam-mapping.xml"));
				Assert.IsNull (archive.GetEntry ($"{prefix}assets/xa-internal/xa-remap-members.xml"));
				Assert.AreEqual (hasLibrary ? "true" : "false",
					File.ReadAllText (Directory.GetFiles (intermediate, "binary-blobs.stamp", SearchOption.AllDirectories).Single ()));
			}
			Assert.IsTrue (builder.Build (proj));
			AssertPackage (false);
			mapping = """{"ClassRewrites":[{"Class":{"From":"android.app.Activity","To":"com.microsoft.intune.mam.client.app.MAMActivity"}}]}""";
			proj.Touch ("mam.json");
			Assert.IsTrue (builder.Build (proj));
			AssertPackage (true);
			Assert.IsTrue (builder.Build (proj), "MAM input must survive ordinary asset staging and support a no-op.");
			Assert.IsTrue (builder.Output.IsTargetSkipped ("_AndroidGenerateBinaryBlobs"));
			AssertPackage (true);
			foreach (var blob in Directory.GetFiles (intermediate, "libbinary_blobs.so", SearchOption.AllDirectories))
				File.Delete (blob);
			Assert.IsTrue (builder.Build (proj), "A deleted data library must be regenerated.");
			AssertPackage (true);
			var binaryBlob = Directory.GetFiles (intermediate, "libbinary_blobs.so", SearchOption.AllDirectories).Single ();
			var stamp = Directory.GetFiles (intermediate, "binary-blobs.stamp", SearchOption.AllDirectories).Single ();
			File.Delete (binaryBlob);
			Directory.CreateDirectory (binaryBlob);
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), "An unwritable library output must fail generation.");
			StringAssertEx.Contains ("error XA4325", builder.LastBuildOutput);
			FileAssert.DoesNotExist (stamp, "Failure must invalidate the previous completion stamp.");
			Directory.Delete (binaryBlob);
			File.WriteAllBytes (binaryBlob, [0, 1, 2]);
			builder.ThrowOnBuildFailure = true;
			Assert.IsTrue (builder.Build (proj), "A failed write must not make a partial library up to date.");
			Assert.IsFalse (builder.Output.IsTargetSkipped ("_AndroidGenerateBinaryBlobs"));
			Assert.Greater (new FileInfo (binaryBlob).Length, 3);
			AssertPackage (true);
			mapping = """{"ClassRewrites":[]}""";
			proj.Touch ("mam.json");
			Assert.IsTrue (builder.Build (proj));
			Assert.AreEqual (runtime == AndroidRuntime.CoreCLR ? 1 : 0,
				Directory.GetFiles (intermediate, "libbinary_blobs.so", SearchOption.AllDirectories).Length);
			AssertPackage (false);
			Assert.IsTrue (builder.Build (proj));
			Assert.IsTrue (builder.Output.IsTargetSkipped ("_AndroidGenerateBinaryBlobs"));
			Assert.IsTrue (builder.Clean (proj));
			Assert.IsEmpty (Directory.GetFiles (intermediate, "binary-blobs.stamp", SearchOption.AllDirectories));
		}

		[TestCase (AndroidRuntime.CoreCLR, false, false, false, false, false)]
		[TestCase (AndroidRuntime.CoreCLR, true, false, false, false, false)]
		[TestCase (AndroidRuntime.NativeAOT, true, false, false, false, false)]
		[TestCase (AndroidRuntime.CoreCLR, true, true, false, false, false)]
		[TestCase (AndroidRuntime.CoreCLR, true, false, true, false, false)]
		[TestCase (AndroidRuntime.CoreCLR, true, false, true, true, false)]
		[TestCase (AndroidRuntime.CoreCLR, true, false, true, false, true)]
		public void ReservedBinaryBlobLibraryNameIsRejected (AndroidRuntime runtime, bool release, bool aab, bool useSonameAlias, bool useShadowDynstr, bool useTrailingDynamicEntry)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release))
				return;
			var remapping = new BuildItem ("_AndroidRemapMembers", "explicit-remap.xml") {
				TextContent = () => """<replacements><replace-type from="example/Original" to="example/Replacement" /></replacements>""",
			};
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = release,
				OtherBuildItems = {
					remapping,
				},
			};
			proj.SetRuntime (runtime);
			proj.SetRuntimeIdentifiers (new [] { "arm64-v8a" });
			proj.SetProperty ("AndroidPackageFormats", aab ? "aab" : "apk");
			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj));
			string intermediate = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			byte [] library = File.ReadAllBytes (Directory.GetFiles (intermediate, "libbinary_blobs.so", SearchOption.AllDirectories).Single ());
			if (useShadowDynstr)
				library = CreateShadowDynstrLibrary (library);
			if (useTrailingDynamicEntry)
				library = AddTrailingDynamicStringTableSize (library);
			proj.OtherBuildItems.Add (new AndroidItem.AndroidNativeLibrary ("Libraries/arm64-v8a/libcustom.so") {
				BinaryContent = () => library,
				Metadata = {
					{ "Abi", "arm64-v8a" },
					{ "ArchiveFileName", useSonameAlias ? "libcustom.so" : "libbinary_blobs.so" },
				},
			});
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), "A native input must not overwrite or be hidden by the generated data library.");
			StringAssertEx.Contains ("error XA4330", builder.LastBuildOutput);
			proj.OtherBuildItems.Remove (remapping);
			Assert.IsFalse (builder.Build (proj), "The library name remains reserved when no remapping data is generated.");
			StringAssertEx.Contains ("error XA4330", builder.LastBuildOutput);
		}

		[TestCase (false, false, false, false, false)]
		[TestCase (false, false, true, false, false)]
		[TestCase (true, false, false, false, false)]
		[TestCase (true, true, true, false, false)]
		[TestCase (false, false, true, false, true)]
		[TestCase (false, false, false, true, false)]
		public void ReservedBinaryBlobLibraryInJarIsRejected (bool release, bool aab, bool useSonameAlias, bool useBackslashPath, bool useShadowDynstr)
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release))
				return;
			var remapping = new BuildItem ("_AndroidRemapMembers", "explicit-remap.xml") {
				TextContent = () => """<replacements><replace-type from="example/Original" to="example/Replacement" /></replacements>""",
			};
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = release,
				OtherBuildItems = {
					remapping,
					new BuildItem ("AndroidPackagingOptionsInclude", "**/*.so"),
				},
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetRuntimeIdentifiers (new [] { "arm64-v8a" });
			proj.SetProperty ("AndroidPackageFormats", aab ? "aab" : "apk");
			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "A clean application with remapping should build.");
			string intermediate = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			byte [] library = File.ReadAllBytes (Directory.GetFiles (intermediate, "libbinary_blobs.so", SearchOption.AllDirectories).Single ());
			if (useShadowDynstr)
				library = CreateShadowDynstrLibrary (library);
			else if (useBackslashPath)
				library = RenameSoname (library, "libcustom_blobs.so");
			string entryName = useBackslashPath
				? "lib/arm64-v8a\\libbinary_blobs.so"
				: useSonameAlias ? "lib/arm64-v8a/libcustom.so" : "lib/arm64-v8a/libbinary_blobs.so";
			proj.OtherBuildItems.Add (new BuildItem ("AndroidJavaLibrary", "NativePayload.jar") {
				BinaryContent = () => CreateJar (entryName, library),
			});
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), "JAR native entries must not bypass the reserved name or SONAME check.");
			StringAssertEx.Contains ("error XA4330", builder.LastBuildOutput);
			proj.OtherBuildItems.Remove (remapping);
			Assert.IsFalse (builder.Build (proj), "The reserved JAR entry must also be rejected without remapping data.");
			StringAssertEx.Contains ("error XA4330", builder.LastBuildOutput);
		}

		static byte [] CreateJar (string entryName, byte [] contents)
		{
			using var jar = new MemoryStream ();
			using (var archive = new ZipArchive (jar, ZipArchiveMode.Create, leaveOpen: true)) {
				using var stream = archive.CreateEntry (entryName).Open ();
				stream.Write (contents);
			}
			return jar.ToArray ();
		}

		static byte [] RenameSoname (byte [] elf, string soname)
		{
			byte [] original = Encoding.ASCII.GetBytes ("libbinary_blobs.so");
			byte [] replacement = Encoding.ASCII.GetBytes (soname);
			Assert.AreEqual (original.Length, replacement.Length, "The replacement SONAME must preserve the ELF string-table size.");
			byte [] result = (byte [])elf.Clone ();
			int offset = result.AsSpan ().IndexOf (original);
			Assert.GreaterOrEqual (offset, 0, "The ELF fixture must contain the original SONAME.");
			replacement.CopyTo (result, offset);
			return result;
		}

		static byte [] AddTrailingDynamicStringTableSize (byte [] elf)
		{
			Assert.AreEqual (2, elf [4], "The generated fixture must be ELF64.");
			Assert.AreEqual (1, elf [5], "The generated fixture must use little-endian encoding.");
			ulong sectionHeadersOffset = BinaryPrimitives.ReadUInt64LittleEndian (elf.AsSpan (40, 8));
			ushort sectionHeaderSize = BinaryPrimitives.ReadUInt16LittleEndian (elf.AsSpan (58, 2));
			int dynamicHeaderOffset = checked ((int)sectionHeadersOffset) + sectionHeaderSize * 4;
			ulong dynamicOffset = BinaryPrimitives.ReadUInt64LittleEndian (elf.AsSpan (dynamicHeaderOffset + 24, 8));
			ulong dynamicSize = BinaryPrimitives.ReadUInt64LittleEndian (elf.AsSpan (dynamicHeaderOffset + 32, 8));
			Assert.AreEqual (112, dynamicSize, "Expected six dynamic tags and DT_NULL in the generated fixture.");

			byte [] result = (byte [])elf.Clone ();
			ulong dynamicTerminatorOffset = dynamicOffset + dynamicSize - 16;
			Assert.AreEqual (0, BinaryPrimitives.ReadUInt64LittleEndian (result.AsSpan (checked ((int)dynamicTerminatorOffset), 8)));
			ulong trailingEntryOffset = dynamicOffset + dynamicSize;
			BinaryPrimitives.WriteUInt64LittleEndian (result.AsSpan (checked ((int)trailingEntryOffset), 8), 10); // DT_STRSZ
			BinaryPrimitives.WriteUInt64LittleEndian (result.AsSpan (checked ((int)trailingEntryOffset + 8), 8), 0);
			BinaryPrimitives.WriteUInt64LittleEndian (result.AsSpan (dynamicHeaderOffset + 32, 8), dynamicSize + 16);

			const int ElfHeaderSize = 64;
			const int ProgramHeaderSize = 56;
			const int DynamicProgramHeaderIndex = 2;
			int dynamicProgramHeaderOffset = ElfHeaderSize + ProgramHeaderSize * DynamicProgramHeaderIndex;
			ulong programDynamicSize = BinaryPrimitives.ReadUInt64LittleEndian (result.AsSpan (dynamicProgramHeaderOffset + 32, 8));
			BinaryPrimitives.WriteUInt64LittleEndian (result.AsSpan (dynamicProgramHeaderOffset + 32, 8), programDynamicSize + 16);
			ulong programDynamicMemorySize = BinaryPrimitives.ReadUInt64LittleEndian (result.AsSpan (dynamicProgramHeaderOffset + 40, 8));
			BinaryPrimitives.WriteUInt64LittleEndian (result.AsSpan (dynamicProgramHeaderOffset + 40, 8), programDynamicMemorySize + 16);
			return result;
		}

		static byte [] CreateShadowDynstrLibrary (byte [] elf)
		{
			Assert.AreEqual (2, elf [4], "The generated fixture must be ELF64.");
			Assert.AreEqual (1, elf [5], "The generated fixture must use little-endian encoding.");
			ulong sectionHeadersOffset = BinaryPrimitives.ReadUInt64LittleEndian (elf.AsSpan (40, 8));
			ushort sectionHeaderSize = BinaryPrimitives.ReadUInt16LittleEndian (elf.AsSpan (58, 2));
			ushort sectionCount = BinaryPrimitives.ReadUInt16LittleEndian (elf.AsSpan (60, 2));
			ushort sectionNamesIndex = BinaryPrimitives.ReadUInt16LittleEndian (elf.AsSpan (62, 2));
			Assert.AreEqual (64, sectionHeaderSize);
			Assert.AreEqual (7, sectionCount);
			Assert.AreEqual (6, sectionNamesIndex);

			int sectionHeadersOffsetInt = checked ((int)sectionHeadersOffset);
			byte [] sectionHeaders = elf.AsSpan (sectionHeadersOffsetInt, sectionHeaderSize * sectionCount).ToArray ();
			int dynamicStringsHeaderOffset = sectionHeaderSize * 2;
			int sectionNamesHeaderOffset = sectionHeaderSize * sectionNamesIndex;
			ulong dynamicStringsOffset = BinaryPrimitives.ReadUInt64LittleEndian (sectionHeaders.AsSpan (dynamicStringsHeaderOffset + 24, 8));
			ulong dynamicStringsSize = BinaryPrimitives.ReadUInt64LittleEndian (sectionHeaders.AsSpan (dynamicStringsHeaderOffset + 32, 8));
			ulong sectionNamesOffset = BinaryPrimitives.ReadUInt64LittleEndian (sectionHeaders.AsSpan (sectionNamesHeaderOffset + 24, 8));
			ulong sectionNamesSize = BinaryPrimitives.ReadUInt64LittleEndian (sectionHeaders.AsSpan (sectionNamesHeaderOffset + 32, 8));
			byte [] dynamicStrings = elf.AsSpan (checked ((int)dynamicStringsOffset), checked ((int)dynamicStringsSize)).ToArray ();
			byte [] fakeDynamicStrings = (byte [])dynamicStrings.Clone ();
			Assert.GreaterOrEqual (dynamicStrings.AsSpan ().IndexOf (Encoding.ASCII.GetBytes ("libbinary_blobs.so")), 0,
				"The ELF fixture's linked string table must contain the reserved SONAME.");
			RenameSonameInStringTable (fakeDynamicStrings, "libbinary_blobs.so", "libcustom_blobs.so");

			using var output = new MemoryStream ();
			output.Write (elf);
			ulong fakeDynamicStringsOffset = (ulong)output.Position;
			output.Write (fakeDynamicStrings);

			byte [] originalSectionNames = elf.AsSpan (checked ((int)sectionNamesOffset), checked ((int)sectionNamesSize)).ToArray ();
			using var names = new MemoryStream ();
			names.Write (originalSectionNames);
			uint alternateNameIndex = checked ((uint)names.Position);
			byte [] alternateName = Encoding.ASCII.GetBytes (".dynstr.primary\0");
			names.Write (alternateName);
			uint duplicateNameIndex = checked ((uint)names.Position);
			names.Write (Encoding.ASCII.GetBytes (".dynstr\0"));
			byte [] updatedSectionNames = names.ToArray ();
			ulong updatedSectionNamesOffset = (ulong)output.Position;
			output.Write (updatedSectionNames);

			BinaryPrimitives.WriteUInt32LittleEndian (sectionHeaders.AsSpan (dynamicStringsHeaderOffset, 4), alternateNameIndex);
			BinaryPrimitives.WriteUInt64LittleEndian (sectionHeaders.AsSpan (sectionNamesHeaderOffset + 24, 8), updatedSectionNamesOffset);
			BinaryPrimitives.WriteUInt64LittleEndian (sectionHeaders.AsSpan (sectionNamesHeaderOffset + 32, 8), (ulong)updatedSectionNames.Length);
			ulong newSectionHeadersOffset = (ulong)output.Position;
			output.Write (sectionHeaders);
			byte [] fakeSectionHeader = new byte [sectionHeaderSize];
			BinaryPrimitives.WriteUInt32LittleEndian (fakeSectionHeader.AsSpan (0, 4), duplicateNameIndex);
			BinaryPrimitives.WriteUInt32LittleEndian (fakeSectionHeader.AsSpan (4, 4), 3); // SHT_STRTAB
			BinaryPrimitives.WriteUInt64LittleEndian (fakeSectionHeader.AsSpan (24, 8), fakeDynamicStringsOffset);
			BinaryPrimitives.WriteUInt64LittleEndian (fakeSectionHeader.AsSpan (32, 8), (ulong)fakeDynamicStrings.Length);
			BinaryPrimitives.WriteUInt64LittleEndian (fakeSectionHeader.AsSpan (48, 8), 1);
			output.Write (fakeSectionHeader);

			byte [] result = output.ToArray ();
			BinaryPrimitives.WriteUInt64LittleEndian (result.AsSpan (40, 8), newSectionHeadersOffset);
			BinaryPrimitives.WriteUInt16LittleEndian (result.AsSpan (60, 2), checked ((ushort)(sectionCount + 1)));
			return result;
		}

		static void RenameSonameInStringTable (byte [] strings, string originalSoname, string replacementSoname)
		{
			byte [] original = Encoding.ASCII.GetBytes (originalSoname);
			byte [] replacement = Encoding.ASCII.GetBytes (replacementSoname);
			Assert.AreEqual (original.Length, replacement.Length, "The replacement SONAME must preserve the ELF string-table size.");
			int offset = strings.AsSpan ().IndexOf (original);
			Assert.GreaterOrEqual (offset, 0, "The ELF string-table fixture must contain its SONAME.");
			replacement.CopyTo (strings, offset);
		}

		[TestCase (true)]
		[TestCase (false)]
		public void UnchangedProguardRulesDoNotRerunR8 (bool obfuscation)
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				EnableDefaultItems = true,
				OtherBuildItems = {
					new AndroidItem.AndroidJavaSource ("Peer.java") {
						Encoding = new UTF8Encoding (encoderShouldEmitUTF8Identifier: false),
						Metadata = { { "Bind", "True" } },
						TextContent = () => """
							package example;
							public class Peer {
								public int first () { return 1; }
								public int second () { return 2; }
							}
							""",
					},
				},
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetRuntimeIdentifiers (new [] { "arm64-v8a" });
			proj.SetProperty ("AndroidLinkTool", "r8");
			proj.SetProperty ("AndroidR8ObfuscationMode", obfuscation ? "runtime-remapping" : "disabled");
			proj.SetProperty ("AndroidPackageFormats", "apk");
			proj.SetProperty ("TrimMode", "full");
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", """
				using var peer = new Example.Peer ();
				System.Console.WriteLine (peer.First ());
				""");

			using var builder = CreateApkBuilder ();
			void AssertTaskCount (string task, int expected)
			{
				var build = BinaryLog.ReadBuild (Path.Combine (Root, builder.ProjectDirectory,
					$"{Path.GetFileNameWithoutExtension (builder.BuildLogFile)}.binlog"));
				Assert.AreEqual (expected, build.FindChildrenRecursive<Microsoft.Build.Logging.StructuredLogger.Task> ()
					.Count (t => t.Name == task), $"Unexpected {task} invocation count.");
			}

			Assert.IsTrue (builder.Build (proj));
			AssertTaskCount ("R8", 1);
			var intermediate = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			var rules = Directory.GetFiles (intermediate, "proguard_project_references.cfg", SearchOption.AllDirectories).Single ();
			var originalRules = File.ReadAllText (rules);
			var originalTime = File.GetLastWriteTimeUtc (rules);
			string? memberRules = null;
			string? originalMemberRules = null;
			DateTime originalMemberTime = default;
			if (obfuscation) {
				StringAssert.Contains ("first(...)", originalRules);
				FileAssert.Exists (rules + ".stamp");
			} else {
				StringAssert.Contains ("-keep class example.Peer", originalRules);
				StringAssert.DoesNotContain ("first(...)", originalRules);
				memberRules = Directory.GetFiles (intermediate, "proguard_typemap_members.cfg", SearchOption.AllDirectories).Single ();
				originalMemberRules = File.ReadAllText (memberRules);
				originalMemberTime = File.GetLastWriteTimeUtc (memberRules);
				StringAssert.Contains ("-keepclassmembers class example.Peer { *; }", originalMemberRules);
			}

			proj.MainActivity = proj.MainActivity.Replace ("peer.First ()", "peer.First () + 1");
			proj.Touch ("MainActivity.cs");
			Assert.IsTrue (builder.Build (proj), "A managed-only change should follow the active ProGuard pipeline's incrementality.");
			AssertTaskCount ("Csc", 1);
			AssertTaskCount ("GenerateProguardConfiguration", obfuscation ? 1 : 0);
			AssertTaskCount ("GenerateTypeMapProguardConfiguration", obfuscation ? 0 : 1);
			AssertTaskCount ("GenerateTypeMapMemberProguardConfiguration", obfuscation ? 0 : 1);
			AssertTaskCount ("R8", obfuscation ? 0 : 1);
			Assert.AreEqual (originalRules, File.ReadAllText (rules));
			if (obfuscation) {
				Assert.AreEqual (originalTime, File.GetLastWriteTimeUtc (rules));
			} else {
				Assert.Greater (File.GetLastWriteTimeUtc (rules), originalTime);
			}
			if (memberRules != null) {
				Assert.AreEqual (originalMemberRules, File.ReadAllText (memberRules));
				Assert.Greater (File.GetLastWriteTimeUtc (memberRules), originalMemberTime);
				originalMemberTime = File.GetLastWriteTimeUtc (memberRules);
			}

			Assert.IsTrue (builder.Build (proj));
			AssertTaskCount ("GenerateProguardConfiguration", 0);
			AssertTaskCount ("GenerateTypeMapProguardConfiguration", 0);
			AssertTaskCount ("GenerateTypeMapMemberProguardConfiguration", 0);
			AssertTaskCount ("R8", 0);

			File.Delete (rules);
			Assert.IsTrue (builder.Build (proj), "A missing rule file must be restored even when the stamp exists.");
			AssertTaskCount ("GenerateProguardConfiguration", obfuscation ? 1 : 0);
			AssertTaskCount ("GenerateTypeMapProguardConfiguration", obfuscation ? 0 : 1);
			AssertTaskCount ("GenerateTypeMapMemberProguardConfiguration", 0);
			AssertTaskCount ("R8", 1);
			Assert.AreEqual (originalRules, File.ReadAllText (rules));

			proj.MainActivity = proj.MainActivity.Replace ("peer.First () + 1", "peer.Second ()");
			proj.Touch ("MainActivity.cs");
			Assert.IsTrue (builder.Build (proj), "Newly retained bindings must update the keep rules.");
			if (obfuscation) {
				AssertTaskCount ("GenerateProguardConfiguration", 1);
				StringAssert.Contains ("second(...)", File.ReadAllText (rules));
				Assert.AreNotEqual (originalRules, File.ReadAllText (rules));
				AssertTaskCount ("R8", 1);
			} else {
				AssertTaskCount ("GenerateProguardConfiguration", 0);
				AssertTaskCount ("GenerateTypeMapProguardConfiguration", 1);
				AssertTaskCount ("GenerateTypeMapMemberProguardConfiguration", 1);
				AssertTaskCount ("R8", 1);
				Assert.AreEqual (originalRules, File.ReadAllText (rules));
				Assert.IsNotNull (memberRules);
				if (memberRules == null) {
					throw new AssertionException ("Scoped member rules were not generated.");
				}
				Assert.AreEqual (originalMemberRules, File.ReadAllText (memberRules));
				Assert.Greater (File.GetLastWriteTimeUtc (memberRules), originalMemberTime);
			}

			Assert.IsTrue (builder.Clean (proj));
			Assert.IsFalse (File.Exists (rules), "Clean should remove the rules.");
			Assert.IsFalse (File.Exists (rules + ".stamp"), "Clean should remove the legacy generation stamp.");
			if (memberRules != null) {
				Assert.IsFalse (File.Exists (memberRules), "Clean should remove the scoped member rules.");
			}
		}

		[TestCase (AndroidRuntime.CoreCLR, false, false)]
		[TestCase (AndroidRuntime.CoreCLR, true, false)]
		[TestCase (AndroidRuntime.NativeAOT, false, false)]
		[TestCase (AndroidRuntime.NativeAOT, true, false)]
		[TestCase (AndroidRuntime.CoreCLR, false, true)]
		[TestCase (AndroidRuntime.NativeAOT, false, true)]
		public void MultiRidUsesOneR8Mapping (AndroidRuntime runtime, bool explicitPrimaryRid, bool aab)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: true)) {
				return;
			}
			string remapping = """<replacements><replace-type from="example/Original" to="example/Replacement" /></replacements>""";
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				OtherBuildItems = {
					new BuildItem ("_AndroidRemapMembers", "explicit-remap.xml") { TextContent = () => remapping },
				},
			};
			proj.SetRuntime (runtime);
			proj.SetRuntimeIdentifiers (new [] { "arm64-v8a", "x86_64" });
			if (explicitPrimaryRid) {
				proj.SetProperty ("RuntimeIdentifier", "android-arm64");
			}
			proj.SetProperty ("AndroidLinkTool", "r8");
			proj.SetProperty ("AndroidR8ObfuscationMode", "runtime-remapping");
			proj.SetProperty ("AndroidCreateProguardMappingFile", "false");
			proj.SetProperty ("AndroidPackageFormats", aab ? "aab" : "apk");

			using var builder = CreateApkBuilder ();
			(int R8, int NativeLinks) ReadInvocationCounts ()
			{
				var build = BinaryLog.ReadBuild (Path.Combine (Root, builder.ProjectDirectory,
					$"{Path.GetFileNameWithoutExtension (builder.BuildLogFile)}.binlog"));
				var tasks = build.FindChildrenRecursive<Microsoft.Build.Logging.StructuredLogger.Task> ().ToList ();
				return (tasks.Count (t => t.Name == "R8"), tasks.Count (t => t.Name == "LinkNativeAotSharedLibrary"));
			}

			Assert.IsTrue (builder.Build (proj), "Both RIDs should build from the same final R8 mapping.");
			var first = ReadInvocationCounts ();
			Assert.AreEqual (1, first.R8);
			var intermediate = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			var maps = Directory.GetFiles (intermediate, "r8-jni-final-mapping.txt", SearchOption.AllDirectories);
			Assert.AreEqual (1, maps.Length, "R8 output must be shared, not regenerated for each RID.");
			if (runtime == AndroidRuntime.NativeAOT) {
				Assert.AreEqual (2, first.NativeLinks, "Each RID should link once, after R8.");
				Assert.AreEqual (2, Directory.GetFiles (intermediate, "r8-jni-remap.xml", SearchOption.AllDirectories).Length);
			}
			var binaryBlobs = Directory.GetFiles (intermediate, "libbinary_blobs.so", SearchOption.AllDirectories)
				.ToDictionary (path => path, File.GetLastWriteTimeUtc);
			Assert.AreEqual (2, binaryBlobs.Count, "Each ABI needs its own independent binary-blob library.");
			if (runtime == AndroidRuntime.NativeAOT) {
				Assert.IsEmpty (Directory.GetFiles (intermediate, "coreclr-bootstrap.bin", SearchOption.AllDirectories));
				foreach (var blob in binaryBlobs.Keys)
					Assert.Less (File.ReadAllBytes (blob).AsSpan ().IndexOf (Encoding.ASCII.GetBytes ("coreclr_bootstrap")), 0);
			}
			var archivePath = aab
				? Path.Combine (intermediate, "android", "bin", $"{proj.PackageName}.aab")
				: Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.apk");
			using (var archive = ZipFile.OpenRead (archivePath)) {
				foreach (var abi in new [] { "arm64-v8a", "x86_64" }) {
					string prefix = aab ? "base/lib/" : "lib/";
					Assert.IsNotNull (archive.GetEntry ($"{prefix}{abi}/libbinary_blobs.so"), $"Missing {abi} remapping data.");
					if (runtime == AndroidRuntime.NativeAOT)
						Assert.IsNotNull (archive.GetEntry ($"{prefix}{abi}/lib{proj.ProjectName}.so"), $"Missing final {abi} native library.");
				}
			}
			if (aab)
				return;

			var objects = Directory.GetFiles (intermediate, $"{proj.ProjectName}.o", SearchOption.AllDirectories)
				.ToDictionary (path => path, File.GetLastWriteTimeUtc);
			Assert.IsTrue (builder.Build (proj), "A multi-RID no-op build should succeed.");
			Assert.AreEqual ((0, 0), ReadInvocationCounts (), "No-op builds must not run R8 or native linking.");
			foreach (var entry in objects) {
				Assert.AreEqual (entry.Value, File.GetLastWriteTimeUtc (entry.Key), "No-op builds must not recompile ILC.");
			}
			if (explicitPrimaryRid)
				return;

			var missingBlob = binaryBlobs.Keys.Single (path => path.Contains ("arm64-v8a", StringComparison.Ordinal));
			File.Delete (missingBlob);
			Assert.IsTrue (builder.Build (proj), "A missing ABI blob should be regenerated.");
			Assert.AreEqual ((0, 0), ReadInvocationCounts (), "Restoring data must not rerun R8 or relink NativeAOT.");
			FileAssert.Exists (missingBlob);
			foreach (var entry in objects)
				Assert.AreEqual (entry.Value, File.GetLastWriteTimeUtc (entry.Key), "Restoring data must not recompile ILC.");
			remapping = "<replacements />";
			proj.Touch ("explicit-remap.xml");
			Assert.IsTrue (builder.Build (proj), "Empty R8 remapping must remove unneeded ABI libraries.");
			Assert.AreEqual (runtime == AndroidRuntime.CoreCLR ? 2 : 0,
				Directory.GetFiles (intermediate, "libbinary_blobs.so", SearchOption.AllDirectories).Length);
			using (var archive = ZipFile.OpenRead (archivePath)) {
				foreach (var abi in new [] { "arm64-v8a", "x86_64" })
					Assert.AreEqual (runtime == AndroidRuntime.CoreCLR, archive.GetEntry ($"lib/{abi}/libbinary_blobs.so") != null);
			}
			Assert.IsTrue (builder.Build (proj), "Empty per-RID remapping must be a no-op.");
			Assert.AreEqual ((0, 0), ReadInvocationCounts ());
			Assert.IsTrue (builder.Clean (proj));
			foreach (var blob in binaryBlobs.Keys)
				FileAssert.DoesNotExist (blob, "Clean must remove generated binary-blob libraries.");
		}
	}
}
