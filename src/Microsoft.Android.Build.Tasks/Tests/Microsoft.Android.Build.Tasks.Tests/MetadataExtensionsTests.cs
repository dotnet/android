using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class MetadataExtensionsTests
{
	[Test]
	public void BorrowedStreamRequiresUnsafeCaller ()
	{
		var method = typeof (MetadataExtensions).GetMethod (nameof (MetadataExtensions.GetEmbeddedResourceStream))
			?? throw new InvalidOperationException ("Missing resource stream helper.");
		Assert.IsTrue (method.CustomAttributes.Any (a =>
			a.AttributeType.FullName == "System.Diagnostics.CodeAnalysis.RequiresUnsafeAttribute"));
		Assert.IsTrue (typeof (MetadataExtensions).Module.CustomAttributes.Any (a =>
			a.AttributeType.FullName == "System.Runtime.CompilerServices.MemorySafetyRulesAttribute"));
	}

	[TestCase (0)]
	[TestCase (1)]
	[TestCase (4096)]
	public void EmbeddedResourceStreamBorrowsReaderImage (int length)
	{
		byte [] contents = Enumerable.Range (0, length).Select (i => (byte) i).ToArray ();
		using var assembly = Mono.Cecil.AssemblyDefinition.CreateAssembly (
			new AssemblyNameDefinition ("ResourceFixture", new Version (1, 0)),
			"ResourceFixture", ModuleKind.Dll);
		assembly.MainModule.Resources.Add (new EmbeddedResource ("fixture", ManifestResourceAttributes.Public, contents));
		using var image = new MemoryStream ();
		assembly.Write (image);
		image.Position = 0;

		using var pe = new PEReader (image);
		var reader = pe.GetMetadataReader ();
		var resource = reader.GetManifestResource (reader.ManifestResources.Single ());
		Stream stream;
		// SAFETY: resource belongs to pe, which outlives the borrowed stream.
		unsafe {
			stream = pe.GetEmbeddedResourceStream (resource);
		}
		using (stream) {
			Assert.IsInstanceOf<UnmanagedMemoryStream> (stream);
			Assert.AreEqual (contents.Length, stream.Length);
			using var copy = new MemoryStream ();
			stream.CopyTo (copy);
			CollectionAssert.AreEqual (contents, copy.ToArray ());
		}
	}
}
