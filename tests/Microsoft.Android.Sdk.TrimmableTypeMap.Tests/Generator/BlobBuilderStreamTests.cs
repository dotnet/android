using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

/// <summary>
/// Generated typemap assemblies are surfaced as a read-only stream over the chunks the PE
/// serialiser already produced instead of being copied into a <see cref="MemoryStream"/>.
/// These tests pin the stream semantics the build task relies on: seek to the start, hash the
/// whole image, then copy it to disk.
/// </summary>
public class BlobBuilderStreamTests : FixtureTestBase
{
	static BlobBuilder MultiChunkBuilder (byte [] content, int chunkSize)
	{
		var builder = new BlobBuilder (chunkSize);
		for (int offset = 0; offset < content.Length; offset += chunkSize) {
			int count = Math.Min (chunkSize, content.Length - offset);
			builder.WriteBytes (content, offset, count);
			if (offset + count < content.Length) {
				// Force a new chunk so the stream has to walk several segments.
				builder.LinkSuffix (MultiChunkBuilderTail (content, offset + count, chunkSize));
				return builder;
			}
		}
		return builder;
	}

	static BlobBuilder MultiChunkBuilderTail (byte [] content, int start, int chunkSize)
	{
		var tail = new BlobBuilder (chunkSize);
		tail.WriteBytes (content, start, content.Length - start);
		return tail;
	}

	static byte [] Sequence (int length)
	{
		var bytes = new byte [length];
		for (int i = 0; i < length; i++) {
			bytes [i] = (byte) (i % 251);
		}
		return bytes;
	}

	[Theory]
	[InlineData (0)]
	[InlineData (1)]
	[InlineData (63)]
	[InlineData (64)]
	[InlineData (1000)]
	public void ReadsBackTheExactContent (int length)
	{
		var content = Sequence (length);
		using var stream = new BlobBuilderStream (MultiChunkBuilder (content, 16));

		Assert.Equal (length, stream.Length);
		Assert.Equal (0, stream.Position);
		Assert.True (stream.CanRead);
		Assert.True (stream.CanSeek);
		Assert.False (stream.CanWrite);

		using var copy = new MemoryStream ();
		stream.CopyTo (copy);
		Assert.Equal (content, copy.ToArray ());
		Assert.Equal (length, stream.Position);
		Assert.Equal (0, stream.Read (new byte [1], 0, 1));
	}

	[Fact]
	public void SupportsRepeatedSequentialAndRandomAccess ()
	{
		var content = Sequence (5000);
		using var stream = new BlobBuilderStream (MultiChunkBuilder (content, 37));

		// The build task hashes the stream and then rewinds it to copy it to disk.
		using var first = new MemoryStream ();
		stream.CopyTo (first);
		stream.Position = 0;
		using var second = new MemoryStream ();
		stream.CopyTo (second);
		Assert.Equal (first.ToArray (), second.ToArray ());

		Assert.Equal (4000, stream.Seek (4000, SeekOrigin.Begin));
		var buffer = new byte [10];
		Assert.Equal (10, stream.Read (buffer, 0, 10));
		Assert.Equal (content.Skip (4000).Take (10), buffer);

		Assert.Equal (10, stream.Seek (-4000, SeekOrigin.Current));
		Assert.Equal (10, stream.Read (buffer, 0, 10));
		Assert.Equal (content.Skip (10).Take (10), buffer);

		Assert.Equal (4990, stream.Seek (-10, SeekOrigin.End));
		Assert.Equal (10, stream.Read (buffer, 0, 10));
		Assert.Equal (content.Skip (4990).Take (10), buffer);

		// Backwards seeks must invalidate the sequential-read cursor.
		stream.Position = 0;
		Assert.Equal (10, stream.Read (buffer, 0, 10));
		Assert.Equal (content.Take (10), buffer);
	}

	[Fact]
	public void PartialReadsSpanChunkBoundaries ()
	{
		var content = Sequence (300);
		using var stream = new BlobBuilderStream (MultiChunkBuilder (content, 7));
		using var copy = new MemoryStream ();

		var buffer = new byte [11];
		int read;
		while ((read = stream.Read (buffer, 0, buffer.Length)) > 0) {
			copy.Write (buffer, 0, read);
		}
		Assert.Equal (content, copy.ToArray ());
	}

	[Fact]
	public void RejectsWrites ()
	{
		using var stream = new BlobBuilderStream (MultiChunkBuilder (Sequence (16), 8));
		Assert.Throws<NotSupportedException> (() => stream.Write (new byte [1], 0, 1));
		Assert.Throws<NotSupportedException> (() => stream.SetLength (0));
	}

	[Fact]
	public void ValidatesReadArguments ()
	{
		using var stream = new BlobBuilderStream (MultiChunkBuilder (Sequence (32), 8));
		var buffer = new byte [16];

		Assert.Throws<ArgumentOutOfRangeException> (() => stream.Read (buffer, -1, 1));
		Assert.Throws<ArgumentOutOfRangeException> (() => stream.Read (buffer, 0, -1));
		// Range overflows the buffer: ArgumentException, matching Stream.Read conventions.
		Assert.Throws<ArgumentException> (() => stream.Read (buffer, 8, 16));
	}

	[Fact]
	public void GeneratedAssemblyStreamIsAReadableImage ()
	{
		var generator = new TrimmableTypeMapGenerator (new NoOpTrimmableTypeMapLogger ());
		using var peReader = new PEReader (File.OpenRead (TestFixtureAssemblyPath));
		var reader = peReader.GetMetadataReader ();
		var result = generator.Execute (
			[new AssemblyInput (reader.GetString (reader.GetAssemblyDefinition ().Name), TestFixtureAssemblyPath, peReader)],
			new Version (11, 0),
			new HashSet<string> ());

		Assert.NotEmpty (result.GeneratedAssemblies);
		foreach (var assembly in result.GeneratedAssemblies) {
			Assert.IsType<BlobBuilderStream> (assembly.Content);
			Assert.Equal (0, assembly.Content.Position);
			Assert.True (assembly.Content.Length > 0);
			using var generatedReader = new PEReader (assembly.Content);
			var metadata = generatedReader.GetMetadataReader ();
			Assert.Equal (assembly.Name, metadata.GetString (metadata.GetAssemblyDefinition ().Name));
		}
	}

}
