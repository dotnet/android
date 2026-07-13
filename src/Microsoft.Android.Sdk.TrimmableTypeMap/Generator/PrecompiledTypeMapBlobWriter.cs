using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Android.Runtime;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Builds the little-endian byte blob for one precompiled typemap universe, in the layout defined by
/// <see cref="PrecompiledTypeMapBlobFormat"/>. The metadata tokens embedded here are assigned by the
/// root PE emitter (each distinct proxy gets a <c>TypeRef</c> in the root typemap module); the writer
/// is otherwise pure data (strings + ints) so it can be round-tripped in host unit tests.
/// </summary>
static class PrecompiledTypeMapBlobWriter
{
	/// <summary>One JNI-name entry: a JNI name mapped to one or more proxy metadata tokens.</summary>
	public readonly struct ExternalEntry
	{
		public ExternalEntry (string jniName, IReadOnlyList<int> proxyTokens)
		{
			JniName = jniName ?? throw new ArgumentNullException (nameof (jniName));
			ProxyTokens = proxyTokens ?? throw new ArgumentNullException (nameof (proxyTokens));
		}

		public string JniName { get; }
		public IReadOnlyList<int> ProxyTokens { get; }
	}

	/// <summary>One managed-type entry: a simplified assembly-qualified name mapped to one proxy token.</summary>
	public readonly struct ProxyEntry
	{
		public ProxyEntry (string managedTypeKey, int proxyToken)
		{
			ManagedTypeKey = managedTypeKey ?? throw new ArgumentNullException (nameof (managedTypeKey));
			ProxyToken = proxyToken;
		}

		public string ManagedTypeKey { get; }
		public int ProxyToken { get; }
	}

	/// <summary>
	/// Serializes one universe. <paramref name="external"/> keys (JNI names) and
	/// <paramref name="proxy"/> keys (managed-type names) must each be unique within their map.
	/// </summary>
	public static byte[] Write (IReadOnlyList<ExternalEntry> external, IReadOnlyList<ProxyEntry> proxy)
	{
		_ = external ?? throw new ArgumentNullException (nameof (external));
		_ = proxy ?? throw new ArgumentNullException (nameof (proxy));

		var externalRows = external
			.Select (e => new Row (e.JniName, e.ProxyTokens))
			.OrderBy (r => r, RowComparer.Instance)
			.ToList ();
		var proxyRows = proxy
			.Select (p => new Row (p.ManagedTypeKey, new[] { p.ProxyToken }))
			.OrderBy (r => r, RowComparer.Instance)
			.ToList ();

		int externalCount = externalRows.Count;
		int proxyCount = proxyRows.Count;

		int externalHashesOffset = PrecompiledTypeMapBlobFormat.HeaderSize;
		int externalEntriesOffset = externalHashesOffset + externalCount * PrecompiledTypeMapBlobFormat.HashSize;
		int proxyHashesOffset = externalEntriesOffset + externalCount * PrecompiledTypeMapBlobFormat.ExternalEntrySize;
		int proxyEntriesOffset = proxyHashesOffset + proxyCount * PrecompiledTypeMapBlobFormat.HashSize;
		int stringsOffset = proxyEntriesOffset + proxyCount * PrecompiledTypeMapBlobFormat.ProxyEntrySize;

		// Build the string + token heaps, deduplicating identical payloads. Each offset is recorded
		// relative to the final blob start (section base + position in the heap), so the heaps can be
		// copied verbatim into their precomputed sections at the end.
		var stringHeap = new List<byte> ();
		var stringOffsets = new Dictionary<string, int> (StringComparer.Ordinal);

		int InternString (string value, byte[] utf8)
		{
			if (stringOffsets.TryGetValue (value, out int existing)) {
				return existing;
			}
			int offset = stringsOffset + stringHeap.Count;
			AppendUInt32 (stringHeap, (uint) utf8.Length);
			stringHeap.AddRange (utf8);
			stringOffsets [value] = offset;
			return offset;
		}

		// String heap is built first; the tokens region follows it (its base is only known afterwards).
		foreach (var row in externalRows) {
			row.KeyOffset = InternString (row.Key, row.KeyUtf8);
		}
		foreach (var row in proxyRows) {
			row.KeyOffset = InternString (row.Key, row.KeyUtf8);
		}

		int tokensOffset = stringsOffset + stringHeap.Count;
		var tokenHeap = new List<byte> ();
		var tokenListOffsets = new Dictionary<string, int> (StringComparer.Ordinal);

		int InternTokens (IReadOnlyList<int> tokens)
		{
			string key = string.Join (",", tokens);
			if (tokenListOffsets.TryGetValue (key, out int existing)) {
				return existing;
			}
			int offset = tokensOffset + tokenHeap.Count;
			AppendUInt32 (tokenHeap, (uint) tokens.Count);
			foreach (int token in tokens) {
				AppendInt32 (tokenHeap, token);
			}
			tokenListOffsets [key] = offset;
			return offset;
		}

		foreach (var row in externalRows) {
			row.TokensOffset = InternTokens (row.Tokens);
		}

		int totalSize = tokensOffset + tokenHeap.Count;
		var blob = new byte [totalSize];

		// Every section lives at a precomputed offset, so each is written straight into the blob.
		int pos = 0;
		pos = WriteUInt32 (blob, pos, PrecompiledTypeMapBlobFormat.Magic);
		pos = WriteUInt32 (blob, pos, PrecompiledTypeMapBlobFormat.Version);
		pos = WriteUInt32 (blob, pos, (uint) externalCount);
		pos = WriteUInt32 (blob, pos, (uint) proxyCount);
		pos = WriteUInt32 (blob, pos, (uint) externalHashesOffset);
		pos = WriteUInt32 (blob, pos, (uint) externalEntriesOffset);
		pos = WriteUInt32 (blob, pos, (uint) proxyHashesOffset);
		WriteUInt32 (blob, pos, (uint) proxyEntriesOffset);

		// External hashes + entries (parallel arrays, sorted by hash).
		int p = externalHashesOffset;
		foreach (var row in externalRows) {
			p = WriteUInt64 (blob, p, row.Hash);
		}
		p = externalEntriesOffset;
		foreach (var row in externalRows) {
			p = WriteUInt32 (blob, p, (uint) row.KeyOffset);
			p = WriteUInt32 (blob, p, (uint) row.TokensOffset);
		}

		// Proxy hashes + entries.
		p = proxyHashesOffset;
		foreach (var row in proxyRows) {
			p = WriteUInt64 (blob, p, row.Hash);
		}
		p = proxyEntriesOffset;
		foreach (var row in proxyRows) {
			p = WriteUInt32 (blob, p, (uint) row.KeyOffset);
			p = WriteInt32 (blob, p, row.Tokens [0]);
		}

		// String + token heaps copied verbatim into their precomputed sections.
		stringHeap.CopyTo (blob, stringsOffset);
		tokenHeap.CopyTo (blob, tokensOffset);

		return blob;
	}

	// Little-endian appends to a growable heap (the string/token regions, whose sizes aren't known up front).
	static void AppendUInt32 (List<byte> heap, uint value)
	{
		heap.Add ((byte) value);
		heap.Add ((byte) (value >> 8));
		heap.Add ((byte) (value >> 16));
		heap.Add ((byte) (value >> 24));
	}

	static void AppendInt32 (List<byte> heap, int value) => AppendUInt32 (heap, unchecked ((uint) value));

	// Little-endian writes into a fixed section of the final blob; each returns the next write position.
	static int WriteUInt32 (byte[] blob, int offset, uint value)
	{
		BinaryPrimitives.WriteUInt32LittleEndian (blob.AsSpan (offset), value);
		return offset + sizeof (uint);
	}

	static int WriteInt32 (byte[] blob, int offset, int value)
	{
		BinaryPrimitives.WriteInt32LittleEndian (blob.AsSpan (offset), value);
		return offset + sizeof (int);
	}

	static int WriteUInt64 (byte[] blob, int offset, ulong value)
	{
		BinaryPrimitives.WriteUInt64LittleEndian (blob.AsSpan (offset), value);
		return offset + sizeof (ulong);
	}

	sealed class Row
	{
		public Row (string key, IReadOnlyList<int> tokens)
		{
			Key = key;
			Tokens = tokens;
			KeyUtf8 = Encoding.UTF8.GetBytes (key);
			Hash = PrecompiledTypeMapBlobFormat.HashKey (KeyUtf8);
		}

		public string Key { get; }
		public byte[] KeyUtf8 { get; }
		public ulong Hash { get; }
		public IReadOnlyList<int> Tokens { get; }
		public int KeyOffset { get; set; }
		public int TokensOffset { get; set; }
	}

	// Sort by (hash, key bytes) so equal-hash collisions form a contiguous, deterministically ordered run.
	sealed class RowComparer : IComparer<Row>
	{
		public static readonly RowComparer Instance = new ();

		public int Compare (Row? x, Row? y)
		{
			if (x is null || y is null) {
				return Comparer<object?>.Default.Compare (x, y);
			}
			int byHash = x.Hash.CompareTo (y.Hash);
			if (byHash != 0) {
				return byHash;
			}
			return CompareBytes (x.KeyUtf8, y.KeyUtf8);
		}

		static int CompareBytes (byte[] a, byte[] b)
		{
			int min = Math.Min (a.Length, b.Length);
			for (int i = 0; i < min; i++) {
				int diff = a [i].CompareTo (b [i]);
				if (diff != 0) {
					return diff;
				}
			}
			return a.Length.CompareTo (b.Length);
		}
	}
}
