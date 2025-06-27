using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks.LLVMIR;

/// <summary><para>
/// This class is an optimization which allows us to store strings
/// as a single "blob" of data where each string follows another, all
/// of them separated with a NUL character. This allows us to use a single
/// pointer at run time instead of several (one per string). The result is
/// less relocations in the final .so, which is good for performance
/// </para><para>
/// Each string is converted to UTF8 before storing as a byte array. To optimize
/// for size, duplicate strings are not stored, instead the earlier offset+length
/// are returned when calling the <see cref="Add(string)" /> method.
/// </para>
/// </summary>
class LlvmIrStringBlob
{
	// Length is one more than byte size, to account for the terminating nul
	public record struct StringInfo (int Offset, int Length, byte[] Bytes, string Value);

	Dictionary<string, StringInfo> cache = new (StringComparer.Ordinal);
	List<StringInfo> segments = new ();
	long size = 0;

	public long Size => size;

	/// <summary>
	/// Add string <paramref name="s"/> to the blob, returning its offset in the blob
	/// and its size (which is the string length after conversion from UTF8 to a byte array plus
	/// 1 for the terminating NUL character)
	/// </summary>
	public (int offset, int length) Add (string s)
	{
		if (cache.TryGetValue (s, out StringInfo info)) {
			return (info.Offset, info.Length);
		}

		byte[] bytes = MonoAndroidHelper.Utf8StringToBytes (s);
		int offset;
		if (segments.Count > 0) {
			StringInfo lastSegment = segments[segments.Count - 1];
			offset = lastSegment.Offset + lastSegment.Length + 1; // Include trailing NUL here
		} else {
			offset = 0;
		}

		info = new StringInfo (
			Offset: offset,
			Length: bytes.Length,
			Bytes: bytes,
			Value: s
		);
		segments.Add (info);
		cache.Add (s, info);
		size += info.Length + 1; // Account for the trailing NUL

		return (info.Offset, info.Length);
	}

	public int GetIndexOf (string s)
	{
		if (String.IsNullOrEmpty (s)) {
			return -1;
		}

		if (!cache.TryGetValue (s, out StringInfo info)) {
			return -1;
		}

		return info.Offset;
	}

	public IEnumerable<StringInfo> GetSegments ()
	{
		foreach (StringInfo si in segments) {
			yield return si;
		}
	}
}
