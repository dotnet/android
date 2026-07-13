#nullable enable

using System;
using System.Buffers.Binary;
using System.IO.Hashing;
using System.Text;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Binary layout and reader for a single precompiled trimmable typemap universe blob.
///
/// This file is the single source of truth for the format: it is linked into both
/// <c>Mono.Android</c> (consumed by <see cref="PrecompiledTypeMap"/> at runtime) and the
/// <c>Microsoft.Android.Sdk.TrimmableTypeMap</c> generator (whose <c>PrecompiledTypeMapBlobWriter</c>
/// produces bytes this reader parses). Keeping the reader here — rather than duplicating it — means
/// the build-time writer round-trip tests exercise the exact code path used on device.
///
/// Design goals:
/// <list type="bullet">
/// <item>Zero-cost initialization: the runtime only stores a pointer + length; nothing is parsed
/// or resolved until a lookup hits.</item>
/// <item>No eager assembly loading: proxy <see cref="Type"/> values are stored as metadata tokens
/// (into the root typemap module) and resolved lazily via <c>Module.ResolveType</c> on a hit.</item>
/// <item>Endianness independent: keys are hashed as UTF-8 with <see cref="XxHash3"/> (a byte-sequence
/// hash), and all stored integers are read explicitly as little-endian.</item>
/// </list>
///
/// Layout (all integers little-endian, offsets relative to the universe blob start):
/// <code>
/// Header (32 bytes):
///   +0   u32  Magic         = 0x314D5450 ("PTM1")
///   +4   u32  Version       = 1
///   +8   u32  ExternalCount           (number of JNI-name entries)
///   +12  u32  ProxyCount              (number of managed-type entries)
///   +16  u32  ExternalHashesOffset    -> u64[ExternalCount] (sorted ascending)
///   +20  u32  ExternalEntriesOffset   -> ExternalEntry[ExternalCount] (parallel to hashes)
///   +24  u32  ProxyHashesOffset       -> u64[ProxyCount]    (sorted ascending)
///   +28  u32  ProxyEntriesOffset      -> ProxyEntry[ProxyCount]      (parallel to hashes)
///
/// ExternalEntry (8 bytes): { u32 KeyOffset; u32 TokensOffset }
/// ProxyEntry    (8 bytes): { u32 KeyOffset; i32 Token }
///
/// KeyOffset    -> String region:  { u32 ByteLength; u8 Utf8[ByteLength] }
/// TokensOffset -> Tokens region:  { u32 Count; i32 Token[Count] }
/// </code>
/// Entries are sorted by (hash, key-bytes) so equal-hash collisions form a contiguous run that the
/// reader scans and verifies by key.
/// </summary>
static class PrecompiledTypeMapBlobFormat
{
	public const uint Magic = 0x314D5450; // "PTM1" little-endian
	public const uint Version = 1;

	public const int OffMagic = 0;
	public const int OffVersion = 4;
	public const int OffExternalCount = 8;
	public const int OffProxyCount = 12;
	public const int OffExternalHashes = 16;
	public const int OffExternalEntries = 20;
	public const int OffProxyHashes = 24;
	public const int OffProxyEntries = 28;
	public const int HeaderSize = 32;

	public const int HashSize = 8;
	public const int ExternalEntrySize = 8; // u32 KeyOffset + u32 TokensOffset
	public const int ProxyEntrySize = 8;    // u32 KeyOffset + i32 Token

	// Keys (JNI names, simplified AQNs) are short and bounded in practice, so their UTF-8 form is
	// encoded into a fixed stack buffer to avoid a per-lookup byte[] allocation. Longer keys fall back
	// to a heap array. 512 bytes comfortably covers real type names while keeping stack use predictable.
	const int MaxStackAllocBytes = 512;

	public static ulong HashKey (ReadOnlySpan<byte> utf8Key) => XxHash3.HashToUInt64 (utf8Key);

	static uint ReadU32 (ReadOnlySpan<byte> blob, int offset) =>
		BinaryPrimitives.ReadUInt32LittleEndian (blob.Slice (offset, 4));

	static int ReadI32 (ReadOnlySpan<byte> blob, int offset) =>
		BinaryPrimitives.ReadInt32LittleEndian (blob.Slice (offset, 4));

	static ulong ReadU64 (ReadOnlySpan<byte> blob, int offset) =>
		BinaryPrimitives.ReadUInt64LittleEndian (blob.Slice (offset, 8));

	/// <summary>
	/// Returns true when <paramref name="blob"/> begins with a valid header of the expected version.
	/// </summary>
	public static bool IsValid (ReadOnlySpan<byte> blob) =>
		blob.Length >= HeaderSize &&
		ReadU32 (blob, OffMagic) == Magic &&
		ReadU32 (blob, OffVersion) == Version;

	/// <summary>
	/// Looks up the proxy-token list for a JNI name (the <c>GetProxyTypes</c> map). On success,
	/// <paramref name="tokenCount"/> and <paramref name="tokensDataOffset"/> describe a run of
	/// <see cref="ReadTokenAt"/>-readable metadata tokens.
	/// </summary>
	/// <remarks>Convenience string overload; UTF-8-encodes the key into a stack buffer, no heap allocation.</remarks>
	public static bool TryGetExternalTokens (ReadOnlySpan<byte> blob, string jniName, out int tokenCount, out int tokensDataOffset) =>
		TryGetExternalTokens (blob, jniName.AsSpan (), out tokenCount, out tokensDataOffset);

	/// <summary>
	/// <see cref="ReadOnlySpan{Char}"/> overload: UTF-8-encodes the key into a stack buffer (heap fallback
	/// only for very long keys) then delegates to the UTF-8 overload.
	/// </summary>
	public static unsafe bool TryGetExternalTokens (ReadOnlySpan<byte> blob, ReadOnlySpan<char> jniName, out int tokenCount, out int tokensDataOffset)
	{
		int maxBytes = Encoding.UTF8.GetMaxByteCount (jniName.Length);
		if (maxBytes <= MaxStackAllocBytes) {
			byte* buffer = stackalloc byte [MaxStackAllocBytes];
			int written = EncodeUtf8 (jniName, buffer, MaxStackAllocBytes);
			return TryGetExternalTokens (blob, new ReadOnlySpan<byte> (buffer, written), out tokenCount, out tokensDataOffset);
		}
		return TryGetExternalTokens (blob, (ReadOnlySpan<byte>) EncodeUtf8Heap (jniName), out tokenCount, out tokensDataOffset);
	}

	/// <summary>
	/// Looks up the proxy-token list for a JNI name already encoded as UTF-8 (the <c>GetProxyTypes</c>
	/// map). This is the allocation-free path: the blob stores keys as UTF-8 and hashes UTF-8 bytes, so
	/// a UTF-8 JNI name (e.g. handed straight from JNI) is compared and hashed with no re-encoding.
	/// </summary>
	public static bool TryGetExternalTokens (ReadOnlySpan<byte> blob, ReadOnlySpan<byte> jniNameUtf8, out int tokenCount, out int tokensDataOffset)
	{
		tokenCount = 0;
		tokensDataOffset = 0;

		int count = (int) ReadU32 (blob, OffExternalCount);
		if (count == 0) {
			return false;
		}

		int hashesOffset = (int) ReadU32 (blob, OffExternalHashes);
		int entriesOffset = (int) ReadU32 (blob, OffExternalEntries);
		int index = FindVerifiedIndex (blob, jniNameUtf8, count, hashesOffset, entriesOffset, ExternalEntrySize);
		if (index < 0) {
			return false;
		}

		int entryOffset = entriesOffset + index * ExternalEntrySize;
		int tokensOffset = (int) ReadU32 (blob, entryOffset + 4);
		tokenCount = (int) ReadU32 (blob, tokensOffset);
		tokensDataOffset = tokensOffset + 4;
		return tokenCount > 0;
	}

	/// <summary>Reads the metadata token at <paramref name="index"/> from a token run.</summary>
	public static int ReadTokenAt (ReadOnlySpan<byte> blob, int tokensDataOffset, int index) =>
		ReadI32 (blob, tokensDataOffset + index * 4);

	/// <summary>
	/// Looks up the single proxy token for a managed-type key (the <c>TryGetProxyType</c> map).
	/// The key must be the simplified assembly-qualified name: <c>"Namespace.Type, AssemblyName"</c>.
	/// </summary>
	/// <remarks>Convenience string overload; UTF-8-encodes the key into a stack buffer, no heap allocation.</remarks>
	public static bool TryGetProxyToken (ReadOnlySpan<byte> blob, string managedTypeKey, out int token) =>
		TryGetProxyToken (blob, managedTypeKey.AsSpan (), out token);

	/// <summary>
	/// <see cref="ReadOnlySpan{Char}"/> overload: UTF-8-encodes the key into a stack buffer (heap fallback
	/// only for very long keys) then delegates to the UTF-8 overload. Lets callers pass a sliced
	/// assembly-qualified name (e.g. from <c>Type.AssemblyQualifiedName</c>) without a substring allocation.
	/// </summary>
	public static unsafe bool TryGetProxyToken (ReadOnlySpan<byte> blob, ReadOnlySpan<char> managedTypeKey, out int token)
	{
		int maxBytes = Encoding.UTF8.GetMaxByteCount (managedTypeKey.Length);
		if (maxBytes <= MaxStackAllocBytes) {
			byte* buffer = stackalloc byte [MaxStackAllocBytes];
			int written = EncodeUtf8 (managedTypeKey, buffer, MaxStackAllocBytes);
			return TryGetProxyToken (blob, new ReadOnlySpan<byte> (buffer, written), out token);
		}
		return TryGetProxyToken (blob, (ReadOnlySpan<byte>) EncodeUtf8Heap (managedTypeKey), out token);
	}

	/// <summary>
	/// Looks up the single proxy token for a managed-type key already encoded as UTF-8 (the
	/// <c>TryGetProxyType</c> map). The key must be the simplified assembly-qualified name
	/// <c>"Namespace.Type, AssemblyName"</c> encoded as UTF-8.
	/// </summary>
	public static bool TryGetProxyToken (ReadOnlySpan<byte> blob, ReadOnlySpan<byte> managedTypeKeyUtf8, out int token)
	{
		token = 0;

		int count = (int) ReadU32 (blob, OffProxyCount);
		if (count == 0) {
			return false;
		}

		int hashesOffset = (int) ReadU32 (blob, OffProxyHashes);
		int entriesOffset = (int) ReadU32 (blob, OffProxyEntries);
		int index = FindVerifiedIndex (blob, managedTypeKeyUtf8, count, hashesOffset, entriesOffset, ProxyEntrySize);
		if (index < 0) {
			return false;
		}

		int entryOffset = entriesOffset + index * ProxyEntrySize;
		token = ReadI32 (blob, entryOffset + 4);
		return true;
	}

	// UTF-8-encodes chars into the pointer buffer, returning the byte count. Uses the pointer-based
	// Encoding.GetBytes overload so it compiles on netstandard2.0 (the generator) as well as net11.0.
	static unsafe int EncodeUtf8 (ReadOnlySpan<char> chars, byte* buffer, int bufferLength)
	{
		if (chars.IsEmpty) {
			return 0;
		}
		fixed (char* charsPtr = chars) {
			return Encoding.UTF8.GetBytes (charsPtr, chars.Length, buffer, bufferLength);
		}
	}

	// Heap fallback for keys whose UTF-8 form exceeds the stack buffer (rare in practice).
	static unsafe byte[] EncodeUtf8Heap (ReadOnlySpan<char> chars)
	{
		var bytes = new byte [Encoding.UTF8.GetMaxByteCount (chars.Length)];
		fixed (char* charsPtr = chars)
		fixed (byte* bytesPtr = bytes) {
			int written = Encoding.UTF8.GetBytes (charsPtr, chars.Length, bytesPtr, bytes.Length);
			return written == bytes.Length ? bytes : bytes.AsSpan (0, written).ToArray ();
		}
	}

	// Binary search over the sorted hash array, then verify the key against the string heap.
	// Equal hashes (collisions) form a contiguous run which is scanned linearly.
	static int FindVerifiedIndex (ReadOnlySpan<byte> blob, ReadOnlySpan<byte> keyUtf8, int count, int hashesOffset, int entriesOffset, int entrySize)
	{
		ulong target = HashKey (keyUtf8);

		int lo = 0;
		int hi = count - 1;
		int found = -1;
		while (lo <= hi) {
			int mid = (int) (((uint) lo + (uint) hi) >> 1);
			ulong hash = ReadU64 (blob, hashesOffset + mid * HashSize);
			if (hash < target) {
				lo = mid + 1;
			} else if (hash > target) {
				hi = mid - 1;
			} else {
				found = mid;
				break;
			}
		}

		if (found < 0) {
			return -1;
		}

		int start = found;
		while (start > 0 && ReadU64 (blob, hashesOffset + (start - 1) * HashSize) == target) {
			start--;
		}
		int end = found;
		while (end + 1 < count && ReadU64 (blob, hashesOffset + (end + 1) * HashSize) == target) {
			end++;
		}

		for (int i = start; i <= end; i++) {
			int keyOffset = (int) ReadU32 (blob, entriesOffset + i * entrySize);
			if (KeyMatches (blob, keyOffset, keyUtf8)) {
				return i;
			}
		}

		return -1;
	}

	static bool KeyMatches (ReadOnlySpan<byte> blob, int keyOffset, ReadOnlySpan<byte> keyUtf8)
	{
		int length = (int) ReadU32 (blob, keyOffset);
		if (length != keyUtf8.Length) {
			return false;
		}
		return blob.Slice (keyOffset + 4, length).SequenceEqual (keyUtf8);
	}
}
