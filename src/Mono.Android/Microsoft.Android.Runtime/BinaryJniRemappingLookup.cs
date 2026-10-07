#nullable enable

using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

// All offsets in this format are relative to the uncompressed table, never to an ELF address.
// The ELF segment is checked before even the fixed-size envelope is read.
static unsafe class BinaryJniRemappingLookup
{
	const uint Magic = 0x42424c42;
	const uint MaximumTableSize = 256 * 1024 * 1024;
	const int HeaderSize = 56;
	const uint TypeStride = 12;
	const uint IndexStride = 16;
	const uint MethodStride = 32;
	const uint FieldStride = 28;

	[StructLayout (LayoutKind.Sequential)]
	struct PhdrInfo
	{
		public nuint Address;
		public byte* Name;
		public byte* Headers;
		public ushort Count;
	}

	struct ExtentQuery
	{
		public byte* Symbol;
		public nuint Length;
	}

	static IntPtr library;
	static byte* table;
	static uint size;
	static uint [] counts = [];
	static uint [] offsets = [];

	[DllImport ("libdl.so", EntryPoint = "dlopen")]
	static extern IntPtr Dlopen (string name, int flags);

	[DllImport ("libdl.so", EntryPoint = "dlsym")]
	static extern IntPtr Dlsym (IntPtr handle, string name);

	[DllImport ("libdl.so", EntryPoint = "dlerror")]
	static extern IntPtr Dlerror ();

	[DllImport ("libdl.so", EntryPoint = "dl_iterate_phdr")]
	static extern int IterateProgramHeaders (delegate* unmanaged<PhdrInfo*, nuint, ExtentQuery*, int> callback, ExtentQuery* query);

	[UnmanagedCallersOnly]
	static int FindReadOnlyExtent (PhdrInfo* info, nuint ignored, ExtentQuery* query)
	{
		for (int i = 0; i < info->Count; i++) {
			byte* phdr = info->Headers + i * (IntPtr.Size == 8 ? 56 : 32);
			if (*(uint*)phdr != 1)
				continue;
			uint flags = *(uint*)(phdr + (IntPtr.Size == 8 ? 4 : 24));
			nuint address = IntPtr.Size == 8 ? *(nuint*)(phdr + 16) : *(uint*)(phdr + 8);
			nuint files = IntPtr.Size == 8 ? *(nuint*)(phdr + 32) : *(uint*)(phdr + 16);
			nuint memory = IntPtr.Size == 8 ? *(nuint*)(phdr + 40) : *(uint*)(phdr + 20);
			nuint baseAddress = info->Address + address;
			nuint symbol = (nuint)query->Symbol;
			if (symbol < baseAddress || symbol - baseAddress >= memory)
				continue;
			nuint offset = symbol - baseAddress;
			query->Length = flags == 4 && files <= memory && offset < files ? files - offset : 0;
			return 1;
		}
		return 0;
	}

	internal static void Initialize ()
	{
		library = Dlopen ("libbinary_blobs.so", 2); // RTLD_NOW | RTLD_LOCAL
		if (library == IntPtr.Zero)
			throw new InvalidDataException ($"Cannot load JNI remapping blobs: {Marshal.PtrToStringUTF8 (Dlerror ())}");

		byte* blob = (byte*)Dlsym (library, "xa_jni_remapping");
		if (blob == null)
			throw new InvalidDataException ("libbinary_blobs.so has no xa_jni_remapping payload.");

		var query = new ExtentQuery { Symbol = blob };
		if (IterateProgramHeaders (&FindReadOnlyExtent, &query) != 1 || query.Length < 16)
			throw new InvalidDataException ("JNI remapping symbol is not in a read-only, non-executable ELF load segment.");

		ReadOnlySpan<byte> envelope = new (blob, 16);
		uint storedLength = BinaryPrimitives.ReadUInt32LittleEndian (envelope [8..]);
		uint rawLength = BinaryPrimitives.ReadUInt32LittleEndian (envelope [12..]);
		ushort version = BinaryPrimitives.ReadUInt16LittleEndian (envelope [4..]);
		ushort flags = BinaryPrimitives.ReadUInt16LittleEndian (envelope [6..]);
		if (BinaryPrimitives.ReadUInt32LittleEndian (envelope) != Magic ||
				version != 1 || flags > 1 || rawLength < HeaderSize ||
				rawLength > MaximumTableSize || storedLength == 0 ||
				(nuint)storedLength > query.Length - 16 || (flags == 0 && storedLength != rawLength))
			throw new InvalidDataException ("Invalid JNI remapping blob envelope or ELF extent.");

		if (flags == 0) {
			table = blob + 16;
		} else {
			byte* buffer = (byte*)Marshal.AllocHGlobal (checked ((int)rawLength));
			bool valid = false;
			try {
				if (!ZstandardDecoder.TryDecompress (
						new ReadOnlySpan<byte> (blob + 16, checked ((int)storedLength)),
						new Span<byte> (buffer, checked ((int)rawLength)), out int written) || written != rawLength)
					throw new InvalidDataException ("JNI remapping blob decompression failed or produced a different size.");
				valid = true;
			} finally {
				if (!valid)
					Marshal.FreeHGlobal ((IntPtr)buffer);
			}
			table = buffer; // Deliberately retained for the process lifetime, as are lookup string pointers.
		}
		size = rawLength;
		counts = new uint [6];
		offsets = new uint [6];
		for (int i = 0; i < 4; i++) {
			counts [i] = Read (checked ((uint)(i * 4)));
			offsets [i] = Read (checked ((uint)(16 + i * 4)));
		}
		offsets [4] = Read (32); // method entries
		offsets [5] = Read (36); // field entries
		counts [4] = Read (48);
		counts [5] = Read (52);
		uint strings = Read (40);
		uint stringsLength = Read (44);
		if (strings < HeaderSize || !Contains (strings, stringsLength) ||
				stringsLength == 0 || table [strings] != 0)
			throw new InvalidDataException ("Invalid JNI remapping string section.");
		for (int i = 0; i < 6; i++) {
			uint stride = i < 2 ? TypeStride : i < 4 ? IndexStride : i == 4 ? MethodStride : FieldStride;
			if (!Contains (offsets [i], (ulong)counts [i] * stride) || offsets [i] < HeaderSize ||
					(ulong)offsets [i] + (ulong)counts [i] * stride > strings)
				throw new InvalidDataException ("JNI remapping table index is outside the declared data.");
		}
	}

	static bool Contains (uint offset, ulong length) => offset <= size && length <= size - offset;

	static uint Read (uint offset)
	{
		if (!Contains (offset, 4))
			throw new InvalidDataException ("JNI remapping table access exceeds its bounds.");
		return BinaryPrimitives.ReadUInt32LittleEndian (new ReadOnlySpan<byte> (table + offset, 4));
	}

	static uint Entry (int index, uint position, uint stride)
	{
		uint offset = checked (offsets [index] + position * stride);
		if (position >= counts [index] || !Contains (offset, stride))
			throw new InvalidDataException ("JNI remapping entry exceeds its bounds.");
		return offset;
	}

	static ReadOnlySpan<byte> String (uint offset, uint length, bool terminated = true)
	{
		uint start = Read (40);
		uint end = checked (start + Read (44));
		if (offset < start || length > int.MaxValue || offset >= end || length >= end - offset ||
				(terminated && table [offset + length] != 0))
			throw new InvalidDataException ("JNI remapping string exceeds its bounds or is not NUL terminated.");
		return new ReadOnlySpan<byte> (table + offset, (int)length);
	}

	static byte* CString (uint offset)
	{
		if (offset == 0)
			return null;
		uint start = Read (40);
		uint end = checked (start + Read (44));
		if (offset < start || offset >= end)
			throw new InvalidDataException ("JNI remapping string offset exceeds its bounds.");
		byte* value = table + offset;
		if (new ReadOnlySpan<byte> (value, checked ((int)(end - offset))).IndexOf ((byte)0) < 0)
			throw new InvalidDataException ("JNI remapping string is not NUL terminated.");
		return value;
	}

	static int Compare (uint offset, uint length, ReadOnlySpan<char> key)
	{
		ReadOnlySpan<byte> value = String (offset, length);
		return JniRemappingLookup.CompareUtf8ToUtf16 (value, key);
	}

	static int Compare (uint offset, uint length, ReadOnlySpan<byte> key)
		=> String (offset, length).SequenceCompareTo (key);

	static uint Find (int index, uint stride, ReadOnlySpan<char> key)
	{
		uint left = 0, right = counts [index];
		while (left < right) {
			uint middle = left + (right - left) / 2;
			uint entry = Entry (index, middle, stride);
			if (Compare (Read (entry), Read (entry + 4), key) < 0)
				left = middle + 1;
			else
				right = middle;
		}
		return left;
	}

	static uint Find (int index, uint stride, ReadOnlySpan<byte> key)
	{
		uint left = 0, right = counts [index];
		while (left < right) {
			uint middle = left + (right - left) / 2;
			uint entry = Entry (index, middle, stride);
			if (Compare (Read (entry), Read (entry + 4), key) < 0)
				left = middle + 1;
			else
				right = middle;
		}
		return left;
	}

	internal static byte* LookupType (ReadOnlySpan<char> key, bool reverse)
	{
		int index = reverse ? 1 : 0;
		uint position = Find (index, TypeStride, key);
		if (position == counts [index])
			return null;
		uint entry = Entry (index, position, TypeStride);
		return Compare (Read (entry), Read (entry + 4), key) == 0 ? CString (Read (entry + 8)) : null;
	}

	static uint FindType (int index, ReadOnlySpan<char> key)
	{
		uint position = Find (index, IndexStride, key);
		if (position < counts [index]) {
			uint entry = Entry (index, position, IndexStride);
			if (Compare (Read (entry), Read (entry + 4), key) == 0)
				return entry;
		}
		return 0;
	}

	static uint FindType (int index, ReadOnlySpan<byte> key)
	{
		uint position = Find (index, IndexStride, key);
		if (position < counts [index]) {
			uint entry = Entry (index, position, IndexStride);
			if (Compare (Read (entry), Read (entry + 4), key) == 0)
				return entry;
		}
		return 0;
	}

	static uint FindMember (uint type, int index, uint stride, ReadOnlySpan<char> name, ReadOnlySpan<char> signature, bool method)
	{
		uint first = Read (type + 8), count = Read (type + 12);
		if (first > counts [index] || count > counts [index] - first)
			throw new InvalidDataException ("JNI remapping member range exceeds the table.");
		uint left = first, right = first + count;
		uint end = right;
		while (left < right) {
			uint middle = left + (right - left) / 2;
			uint entry = Entry (index, middle, stride);
			if (Compare (Read (entry), Read (entry + 4), name) < 0)
				left = middle + 1;
			else
				right = middle;
		}
		uint last = left;
		while (last < end && Compare (Read (Entry (index, last, stride)), Read (Entry (index, last, stride) + 4), name) == 0)
			last++;
		if (signature.Length > 0 || !method) {
			for (uint i = left; i < last; i++) {
				uint entry = Entry (index, i, stride);
				uint sigLength = Read (entry + 12);
				if (sigLength > 0 && Compare (Read (entry + 8), sigLength, signature) == 0)
					return entry;
			}
			if (method) {
				int close = signature.LastIndexOf (')');
				if (close >= 0 && close + 1 < signature.Length) {
					ReadOnlySpan<char> prefix = signature [..(close + 1)];
					for (uint i = left; i < last; i++) {
						uint entry = Entry (index, i, stride);
						uint sigLength = Read (entry + 12);
						if (sigLength > 0 && Compare (Read (entry + 8), sigLength, prefix) == 0)
							return entry;
					}
				}
			}
		}
		for (uint i = left; i < last; i++) {
			uint entry = Entry (index, i, stride);
			if (Read (entry + 12) == 0)
				return entry;
		}
		return 0;
	}

	internal static JniRuntime.ReplacementMethodInfo? GetMethod (ReadOnlySpan<char> source, ReadOnlySpan<byte> sourceUtf8,
		ReadOnlySpan<char> name, ReadOnlySpan<char> signature)
	{
		uint type = sourceUtf8.IsEmpty ? FindType (2, source) : FindType (2, sourceUtf8);
		if (type == 0)
			return null;
		uint entry = FindMember (type, 4, MethodStride, name, signature, method: true);
		if (entry == 0)
			return null;
		byte* targetType = CString (Read (entry + 16));
		byte* targetName = CString (Read (entry + 20));
		byte* targetSignature = CString (Read (entry + 24));
		uint flags = Read (entry + 28);
		if (targetType == null || targetName == null || flags > 1)
			throw new InvalidDataException ("Invalid JNI remapping method target or flag.");
		bool isStatic = flags == 1;
		string sourceText = isStatic || Logger.LogAssembly
			? sourceUtf8.IsEmpty ? source.ToString () : System.Text.Encoding.UTF8.GetString (sourceUtf8)
			: "";
		string? staticSignature = null;
		if (isStatic) {
			string original = signature.ToString ();
			staticSignature = targetSignature == null
				? $"(L{sourceText};" + original.Substring (1)
				: Marshal.PtrToStringUTF8 ((IntPtr)targetSignature);
		}
		uint signatureLength = Read (entry + 12);
		byte* matchedSignature = signatureLength > 0 &&
				Compare (Read (entry + 8), signatureLength, signature) == 0
			? table + Read (entry + 8) : null;
		var result = new JniRuntime.ReplacementMethodInfo {
			TargetJniTypeUtf8 = (IntPtr)targetType,
			TargetJniMethodNameUtf8 = (IntPtr)targetName,
			TargetJniMethodSignature = staticSignature,
			TargetJniMethodSignatureUtf8 = isStatic ? IntPtr.Zero :
				(IntPtr)(targetSignature == null ? matchedSignature : targetSignature),
			TargetJniMethodParameterCount = isStatic ? JniMemberSignature.GetParameterCountFromMethodSignature (signature.ToString ()) + 1 : null,
			TargetJniMethodInstanceToStatic = isStatic,
		};
		if (Logger.LogAssembly) {
			string effectiveSignature = staticSignature ??
				Marshal.PtrToStringUTF8 ((IntPtr)(targetSignature == null ? matchedSignature : targetSignature)) ?? signature.ToString ();
			Logger.Log (LogLevel.Debug, "monodroid-assembly",
				$"Remapping method `{sourceText}.{name}{signature}` to " +
				$"`{Marshal.PtrToStringUTF8 ((IntPtr)targetType)}.{Marshal.PtrToStringUTF8 ((IntPtr)targetName)}{effectiveSignature}`; " +
				$"param-count: {result.TargetJniMethodParameterCount}; instance-to-static? {isStatic}");
		}
		return result;
	}

	internal static JniRuntime.ReplacementFieldInfo? GetField (ReadOnlySpan<char> source, ReadOnlySpan<char> name, ReadOnlySpan<char> signature)
	{
		uint type = FindType (3, source);
		if (type == 0)
			return null;
		uint entry = FindMember (type, 5, FieldStride, name, signature, method: false);
		if (entry == 0)
			return null;
		byte* targetType = CString (Read (entry + 16));
		byte* targetName = CString (Read (entry + 20));
		byte* targetSignature = CString (Read (entry + 24));
		if (targetType == null || targetName == null)
			throw new InvalidDataException ("Invalid JNI remapping field target.");
		var result = new JniRuntime.ReplacementFieldInfo {
			SourceJniType = source.ToString (),
			SourceJniFieldName = name.ToString (),
			SourceJniFieldSignature = signature.ToString (),
			TargetJniType = Marshal.PtrToStringUTF8 ((IntPtr)targetType) ?? throw new InvalidDataException ("Invalid target type."),
			TargetJniFieldName = Marshal.PtrToStringUTF8 ((IntPtr)targetName) ?? throw new InvalidDataException ("Invalid target field."),
			TargetJniFieldSignature = targetSignature == null ? signature.ToString () :
				Marshal.PtrToStringUTF8 ((IntPtr)targetSignature) ?? throw new InvalidDataException ("Invalid target signature."),
		};
		if (Logger.LogAssembly) {
			Logger.Log (LogLevel.Debug, "monodroid-assembly",
				$"Remapping field `{source}.{name}:{signature}` to " +
				$"`{result.TargetJniType}.{result.TargetJniFieldName}:{result.TargetJniFieldSignature}`");
		}
		return result;
	}
}
