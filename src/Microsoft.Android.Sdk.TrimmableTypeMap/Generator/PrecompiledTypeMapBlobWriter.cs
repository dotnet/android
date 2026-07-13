using System;
using System.Collections.Generic;
using System.IO;
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

		// Build the string + token regions, deduplicating identical payloads.
		var stringHeap = new Region (stringsOffset);
		var stringOffsets = new Dictionary<string, int> (StringComparer.Ordinal);

		int InternString (string value, byte[] utf8)
		{
			if (stringOffsets.TryGetValue (value, out int existing)) {
				return existing;
			}
			int offset = stringHeap.Position;
			stringHeap.WriteUInt32 ((uint) utf8.Length);
			stringHeap.WriteBytes (utf8);
			stringOffsets [value] = offset;
			return offset;
		}

		// String heap is written first; tokens region follows it (its base is only known afterwards).
		foreach (var row in externalRows) {
			row.KeyOffset = InternString (row.Key, row.KeyUtf8);
		}
		foreach (var row in proxyRows) {
			row.KeyOffset = InternString (row.Key, row.KeyUtf8);
		}

		int tokensOffset = stringsOffset + stringHeap.Length;
		var tokenHeap = new Region (tokensOffset);
		var tokenListOffsets = new Dictionary<string, int> (StringComparer.Ordinal);

		int InternTokens (IReadOnlyList<int> tokens)
		{
			string key = string.Join (",", tokens);
			if (tokenListOffsets.TryGetValue (key, out int existing)) {
				return existing;
			}
			int offset = tokenHeap.Position;
			tokenHeap.WriteUInt32 ((uint) tokens.Count);
			foreach (int token in tokens) {
				tokenHeap.WriteInt32 (token);
			}
			tokenListOffsets [key] = offset;
			return offset;
		}

		foreach (var row in externalRows) {
			row.TokensOffset = InternTokens (row.Tokens);
		}

		int totalSize = tokensOffset + tokenHeap.Length;
		var blob = new byte [totalSize];
		var writer = new Region (0, blob);

		// Header
		writer.WriteUInt32 (PrecompiledTypeMapBlobFormat.Magic);
		writer.WriteUInt32 (PrecompiledTypeMapBlobFormat.Version);
		writer.WriteUInt32 ((uint) externalCount);
		writer.WriteUInt32 ((uint) proxyCount);
		writer.WriteUInt32 ((uint) externalHashesOffset);
		writer.WriteUInt32 ((uint) externalEntriesOffset);
		writer.WriteUInt32 ((uint) proxyHashesOffset);
		writer.WriteUInt32 ((uint) proxyEntriesOffset);

		// External hashes + entries
		writer.Seek (externalHashesOffset);
		foreach (var row in externalRows) {
			writer.WriteUInt64 (row.Hash);
		}
		writer.Seek (externalEntriesOffset);
		foreach (var row in externalRows) {
			writer.WriteUInt32 ((uint) row.KeyOffset);
			writer.WriteUInt32 ((uint) row.TokensOffset);
		}

		// Proxy hashes + entries
		writer.Seek (proxyHashesOffset);
		foreach (var row in proxyRows) {
			writer.WriteUInt64 (row.Hash);
		}
		writer.Seek (proxyEntriesOffset);
		foreach (var row in proxyRows) {
			writer.WriteUInt32 ((uint) row.KeyOffset);
			writer.WriteInt32 (row.Tokens [0]);
		}

		// String + token regions
		Buffer.BlockCopy (stringHeap.ToArray (), 0, blob, stringsOffset, stringHeap.Length);
		Buffer.BlockCopy (tokenHeap.ToArray (), 0, blob, tokensOffset, tokenHeap.Length);

		return blob;
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

	// Little-endian byte sink. Used both for the growable string/token heaps (backing == null) and
	// for patching fixed-offset regions of the final blob (backing != null).
	sealed class Region
	{
		readonly int _base;
		readonly List<byte>? _buffer;
		readonly byte[]? _backing;
		int _position;

		public Region (int baseOffset)
		{
			_base = baseOffset;
			_buffer = new List<byte> ();
			_position = 0;
		}

		public Region (int baseOffset, byte[] backing)
		{
			_base = baseOffset;
			_backing = backing;
			_position = 0;
		}

		public int Position => _base + _position;
		public int Length => _buffer?.Count ?? _position;

		public void Seek (int absoluteOffset) => _position = absoluteOffset - _base;

		public void WriteUInt32 (uint value)
		{
			WriteByte ((byte) value);
			WriteByte ((byte) (value >> 8));
			WriteByte ((byte) (value >> 16));
			WriteByte ((byte) (value >> 24));
		}

		public void WriteInt32 (int value) => WriteUInt32 (unchecked ((uint) value));

		public void WriteUInt64 (ulong value)
		{
			WriteUInt32 ((uint) value);
			WriteUInt32 ((uint) (value >> 32));
		}

		public void WriteBytes (byte[] value)
		{
			foreach (byte b in value) {
				WriteByte (b);
			}
		}

		void WriteByte (byte value)
		{
			if (_backing != null) {
				_backing [_position] = value;
			} else {
				_buffer!.Add (value);
			}
			_position++;
		}

		public byte[] ToArray () => _buffer!.ToArray ();
	}
}
