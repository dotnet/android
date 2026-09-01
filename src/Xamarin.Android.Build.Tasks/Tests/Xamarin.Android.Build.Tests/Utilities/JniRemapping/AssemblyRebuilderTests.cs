using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using NUnit.Framework;
using Xamarin.Android.Tasks.JniRemapping;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class AssemblyRebuilderTests : BaseTest
	{
		[Test]
		public void EmptyPlanPreservesMappedFieldDataAndResources ()
		{
			var fixture = new JniFixtureBuilder ();
			byte [] fieldData = { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04 };
			byte [] resourceData = { 1, 2, 3, 4, 5, 6, 7 };
			fixture.AddEmbeddedResource ("Fixture.resources", resourceData);

			TypeDefinitionHandle enclosing = fixture.EnsurePrivateImplementationDetails ();
			TypeDefinitionHandle dataType = fixture.AddType (null, "__StaticArrayInitTypeSize=8", fixture.NextFieldRid, fixture.NextMethodRid,
				TypeAttributes.NestedPrivate | TypeAttributes.ExplicitLayout | TypeAttributes.Sealed | TypeAttributes.AnsiClass,
				fixture.ValueTypeReference);
			fixture.Metadata.AddTypeLayout (dataType, packingSize: 1, size: (uint) fieldData.Length);
			fixture.Metadata.AddNestedType (dataType, enclosing);

			var signature = new BlobBuilder ();
			new BlobEncoder (signature).FieldSignature ().Type (dataType, isValueType: true);
			int rva = fixture.MappedFieldData.Count;
			fixture.MappedFieldData.WriteBytes (fieldData);
			FieldDefinitionHandle dataField = fixture.Metadata.AddFieldDefinition (
				FieldAttributes.Static | FieldAttributes.Assembly | FieldAttributes.HasFieldRVA,
				fixture.Metadata.GetOrAddString ("ArrayData"), fixture.Metadata.GetOrAddBlob (signature));
			fixture.Metadata.AddFieldRelativeVirtualAddress (dataField, rva);

			byte [] source = fixture.Serialize ();
			using var sourcePe = new PEReader (ImmutableArray.Create (source));
			MetadataReader before = sourcePe.GetMetadataReader ();
			FieldRvaTable sourceFieldRvas = FieldRvaTable.Read (sourcePe, before);

			var result = new AssemblyRebuilder (sourcePe, before, new JniRewritePlan (), sourceFieldRvas).Build ();

			using var rebuiltPe = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader after = rebuiltPe.GetMetadataReader ();
			FieldRvaTable rebuiltFieldRvas = FieldRvaTable.Read (rebuiltPe, after);
			FieldRvaEntry rebuiltField = rebuiltFieldRvas.Get (dataField);

			Assert.IsFalse (result.StrongNameSignatureCleared);
			Assert.IsNotNull (rebuiltField);
			CollectionAssert.AreEqual (fieldData, rebuiltField.Data);
			CollectionAssert.AreEqual (resourceData, ReadResource (rebuiltPe, after, "Fixture.resources"));
			Assert.AreEqual (before.GetGuid (before.GetModuleDefinition ().Mvid), after.GetGuid (after.GetModuleDefinition ().Mvid));

			for (int i = 0; i < MetadataTokens.TableCount; i++) {
				var table = (TableIndex) i;
				Assert.AreEqual (before.GetTableRowCount (table), after.GetTableRowCount (table), $"Row count of table '{table}' changed.");
			}
		}

		static byte [] ReadResource (PEReader peReader, MetadataReader reader, string name)
		{
			foreach (ManifestResourceHandle handle in reader.ManifestResources) {
				ManifestResource resource = reader.GetManifestResource (handle);
				if (reader.GetString (resource.Name) != name) {
					continue;
				}

				DirectoryEntry directory = peReader.PEHeaders.CorHeader.ResourcesDirectory;
				PEMemoryBlock block = peReader.GetSectionData (directory.RelativeVirtualAddress);
				int offset = checked ((int) resource.Offset);
				int size = block.GetReader (offset, sizeof (int)).ReadInt32 ();
				return block.GetReader (offset + sizeof (int), size).ReadBytes (size);
			}

			Assert.Fail ($"Resource '{name}' is missing.");
			return [];
		}
	}
}
