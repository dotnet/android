using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using NUnit.Framework;
using Xamarin.Android.Tasks.JniRemapping;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class NativeResourceSectionCopierTests : BaseTest
	{
		const int ResourceDirectoryHeaderSize = 16;
		const int ResourceDirectoryEntrySize = 8;
		const int ResourceDataEntrySize = 16;

		/// <summary>
		/// A minimal hand-built Win32 resource directory: one root directory with a single ID
		/// entry that points straight at one data entry (no named entries, no subdirectories).
		/// </summary>
		sealed class OneEntryResourceSectionBuilder : ResourceSectionBuilder
		{
			readonly byte [] data;
			readonly int dataEntryRvaDelta;
			readonly uint dataEntrySize;

			/// <param name="dataEntryRvaDelta">
			/// Added to the section's own final RVA to produce the data entry's absolute
			/// <c>OffsetToData</c>. Use 0 for a well-formed, in-range entry; a large positive
			/// value to simulate a corrupt entry that points past the copied section.
			/// </param>
			public OneEntryResourceSectionBuilder (byte [] data, int dataEntryRvaDelta, uint dataEntrySize)
			{
				this.data = data;
				this.dataEntryRvaDelta = dataEntryRvaDelta;
				this.dataEntrySize = dataEntrySize;
			}

			protected override void Serialize (BlobBuilder builder, SectionLocation location)
			{
				int dataEntryOffset = ResourceDirectoryHeaderSize + ResourceDirectoryEntrySize;
				int dataOffset = dataEntryOffset + ResourceDataEntrySize;

				// IMAGE_RESOURCE_DIRECTORY: no named entries, one ID entry.
				builder.WriteUInt32 (0); // Characteristics
				builder.WriteUInt32 (0); // TimeDateStamp
				builder.WriteUInt16 (0); // MajorVersion
				builder.WriteUInt16 (0); // MinorVersion
				builder.WriteUInt16 (0); // NumberOfNamedEntries
				builder.WriteUInt16 (1); // NumberOfIdEntries

				// IMAGE_RESOURCE_DIRECTORY_ENTRY: straight to the data entry (no high bit).
				builder.WriteUInt32 (1); // Id
				builder.WriteUInt32 ((uint) dataEntryOffset); // OffsetToData

				// IMAGE_RESOURCE_DATA_ENTRY.
				uint dataRva = unchecked ((uint) (location.RelativeVirtualAddress + dataOffset + dataEntryRvaDelta));
				builder.WriteUInt32 (dataRva); // OffsetToData (an absolute RVA, not a section offset)
				builder.WriteUInt32 (dataEntrySize); // Size
				builder.WriteUInt32 (0); // CodePage
				builder.WriteUInt32 (0); // Reserved

				builder.WriteBytes (data);
			}
		}

		sealed class SharedEntryResourceSectionBuilder : ResourceSectionBuilder
		{
			readonly byte [] data;

			public SharedEntryResourceSectionBuilder (byte [] data)
			{
				this.data = data;
			}

			protected override void Serialize (BlobBuilder builder, SectionLocation location)
			{
				int dataEntryOffset = ResourceDirectoryHeaderSize + 2 * ResourceDirectoryEntrySize;
				int dataOffset = dataEntryOffset + ResourceDataEntrySize;

				builder.WriteUInt32 (0);
				builder.WriteUInt32 (0);
				builder.WriteUInt16 (0);
				builder.WriteUInt16 (0);
				builder.WriteUInt16 (0);
				builder.WriteUInt16 (2);
				for (uint id = 1; id <= 2; id++) {
					builder.WriteUInt32 (id);
					builder.WriteUInt32 ((uint) dataEntryOffset);
				}
				builder.WriteUInt32 ((uint) (location.RelativeVirtualAddress + dataOffset));
				builder.WriteUInt32 ((uint) data.Length);
				builder.WriteUInt32 (0);
				builder.WriteUInt32 (0);
				builder.WriteBytes (data);
			}
		}

		static byte [] BuildAndReadBack (ResourceSectionBuilder nativeResources)
		{
			var fixture = new JniFixtureBuilder {
				NativeResources = nativeResources,
			};
			return fixture.Serialize ();
		}

		[Test]
		public void ThrowsWhenADataEntryRvaPlusSizeExtendsPastTheCopiedSection ()
		{
			byte [] payload = { 1, 2, 3, 4 };
			// The RVA delta pushes OffsetToData well past the end of the section this class
			// copies, simulating a corrupt (or malicious) resource directory.
			var builder = new OneEntryResourceSectionBuilder (payload, dataEntryRvaDelta: 4096, dataEntrySize: (uint) payload.Length);

			byte [] image = BuildAndReadBack (builder);
			using var peReader = new PEReader (ImmutableArray.Create (image));

			var ex = Assert.Throws<JniRewriteException> (() => NativeResourceSectionCopier.TryCreate (peReader));
			StringAssert.Contains ("outside of the copied resource section", ex.Message);
		}

		[Test]
		public void ThrowsWhenADataEntrySizeAloneExtendsPastTheCopiedSection ()
		{
			byte [] payload = { 1, 2, 3, 4 };
			// The RVA itself is in range, but the declared size overruns the section.
			var builder = new OneEntryResourceSectionBuilder (payload, dataEntryRvaDelta: 0, dataEntrySize: 0x7FFFFFFF);

			byte [] image = BuildAndReadBack (builder);
			using var peReader = new PEReader (ImmutableArray.Create (image));

			var ex = Assert.Throws<JniRewriteException> (() => NativeResourceSectionCopier.TryCreate (peReader));
			StringAssert.Contains ("outside of the copied resource section", ex.Message);
		}

		[Test]
		public void AcceptsAndRelocatesAWellFormedDataEntry ()
		{
			byte [] payload = { 0xAA, 0xBB, 0xCC, 0xDD };
			var builder = new OneEntryResourceSectionBuilder (payload, dataEntryRvaDelta: 0, dataEntrySize: (uint) payload.Length);

			byte [] image = BuildAndReadBack (builder);
			using var peReader = new PEReader (ImmutableArray.Create (image));

			NativeResourceSectionCopier copier = NativeResourceSectionCopier.TryCreate (peReader);
			Assert.IsNotNull (copier, "A resource directory was built but TryCreate did not find it.");

			DirectoryEntry directory = peReader.PEHeaders.PEHeader.ResourceTableDirectory;
			CollectionAssert.AreEqual (payload,
				peReader.GetSectionData (directory.RelativeVirtualAddress)
					.GetReader (directory.Size - payload.Length, payload.Length)
					.ReadBytes (payload.Length),
				"The payload bytes following the data entry should be exactly what was written.");
		}

		[Test]
		public void RelocatesASharedDataEntryOnlyOnce ()
		{
			byte [] payload = { 0x11, 0x22, 0x33, 0x44 };
			var sourceFixture = new JniFixtureBuilder {
				NativeResources = new SharedEntryResourceSectionBuilder (payload),
			};
			sourceFixture.AddEmbeddedResource ("padding", new byte [8192]);
			byte [] source = sourceFixture.Serialize ();
			using var sourceReader = new PEReader (ImmutableArray.Create (source));
			NativeResourceSectionCopier copier = NativeResourceSectionCopier.TryCreate (sourceReader);
			Assert.IsNotNull (copier);

			byte [] rewritten = BuildAndReadBack (copier);
			using var rewrittenReader = new PEReader (ImmutableArray.Create (rewritten));
			DirectoryEntry directory = rewrittenReader.PEHeaders.PEHeader.ResourceTableDirectory;
			int dataEntryOffset = ResourceDirectoryHeaderSize + 2 * ResourceDirectoryEntrySize;
			int dataRva = rewrittenReader.GetSectionData (directory.RelativeVirtualAddress).GetReader (dataEntryOffset, sizeof (int)).ReadInt32 ();

			CollectionAssert.AreEqual (payload,
				rewrittenReader.GetSectionData (dataRva).GetReader (0, payload.Length).ReadBytes (payload.Length));
		}
	}
}
