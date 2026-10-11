#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// <para>
/// Puts passed files inside a real ELF shared library so that they
/// pass scrutiny when examined.  The payload is placed inside its own
/// section of a file, so the entire file is a 100% valid ELF image.
/// </para>
///
/// <para>
/// The generated files have their payload section positioned at the offset of
/// 16k (0x4000) from the beginning of file.  It's done this way because it not
/// only gives us enough room for the stub part of the ELF image to precede that
/// offset, but it also complies with Google policy of aligning to 16k **and**
/// is still nicely aligned to a 4k boundary on 32-bit systems.  This helps mmapping
/// the section on both 64-bit and 32-bit systems.
/// </para>
/// </summary>
/// <remarks>
/// The generated file **MUST NOT** be stripped with `llvm-strip` etc,
/// as it will remove the payload together with other sections it deems
/// unnecessary.
/// </remarks>
class DSOWrapperGenerator
{
	public const string StubFileName = "libarchive-dso-stub.so";

	internal class Config
	{
		public Dictionary<AndroidTargetArch, string> DSOStubPaths    { get; }
		public string BaseOutputDirectory                            { get; }

		public Config (Dictionary<AndroidTargetArch, string> stubPaths, string baseOutputDirectory)
		{
			DSOStubPaths = stubPaths;
			BaseOutputDirectory = baseOutputDirectory;
		}
	};

	//
	// Must be the same avalue as ARCHIVE_DSO_STUB_PAYLOAD_SECTION_ALIGNMENT in src/native/CMakeLists.txt
	//
	const ulong PayloadSectionAlignment = 0x4000;

	public static Config GetConfig (TaskLoggingHelper log, ITaskItem[] runtimePackLibraryDirs, string baseOutputDirectory)
	{
		var stubPaths = new Dictionary<AndroidTargetArch, string> ();

		foreach (ITaskItem packLibDir in runtimePackLibraryDirs) {
			string ?packRID = packLibDir.GetMetadata ("RuntimeIdentifier");
			if (packRID.IsNullOrEmpty ()) {
				continue;
			}

			string stubPath = Path.Combine (packLibDir.ItemSpec, StubFileName);
			if (!File.Exists (stubPath)) {
				log.LogDebugMessage ($"Archive DSO stub file '{stubPath}' not found in runtime pack directory, skipping");
				continue;
			}

			AndroidTargetArch arch = MonoAndroidHelper.RidToArch (packRID);
			stubPaths.Add (arch, stubPath);
		}

		return new Config (stubPaths, baseOutputDirectory);
	}

	const string WrappedSubDirectory = "wrapped";

	static string GetArchOutputPath (AndroidTargetArch targetArch, Config config)
	{
		return Path.Combine (config.BaseOutputDirectory, MonoAndroidHelper.ArchToRid (targetArch), WrappedSubDirectory);
	}

	/// <summary>
	/// Puts the indicated file (<paramref name="payloadFilePath"/>) inside an ELF shared library and returns
	/// path to the wrapped file.
	/// </summary>
	public static string WrapIt (TaskLoggingHelper log, Config config, AndroidTargetArch targetArch, string payloadFilePath, string outputFileName)
	{
		log.LogDebugMessage ($"[{targetArch}] Putting '{payloadFilePath}' inside ELF shared library '{outputFileName}'");
		string outputDir = GetArchOutputPath (targetArch, config);
		Directory.CreateDirectory (outputDir);

		string outputFile = Path.Combine (outputDir, outputFileName);
		log.LogDebugMessage ($"  output file path: {outputFile}");

		if (!config.DSOStubPaths.TryGetValue (targetArch, out string? stubPath)) {
			throw new InvalidOperationException ($"Internal error: archive DSO stub location not known for architecture '{targetArch}'");
		}

		bool success = false;
		try {
			using var stub = File.OpenRead (stubPath);
			using var payload = File.OpenRead (payloadFilePath);
			using var output = File.Create (outputFile);
			WriteWrapper (stub, payload, output);
			success = true;
		} finally {
			if (!success) {
				File.Delete (outputFile);
			}
		}
		return outputFile;
	}

	// Keep the SDK-built stub and its load segments intact. The runtime expects the
	// non-allocated payload at 16 KiB and its section header appended last.
	static void WriteWrapper (Stream stub, Stream payload, Stream output)
	{
		using var reader = new BinaryReader (stub, System.Text.Encoding.UTF8, leaveOpen: true);
		using var writer = new BinaryWriter (output, System.Text.Encoding.UTF8, leaveOpen: true);
		if (stub.Length < 64 || stub.Length > (long)PayloadSectionAlignment ||
			reader.ReadUInt32 () != 0x464c457f) {
			throw new InvalidDataException ("Invalid discrete payload ELF stub.");
		}
		byte elfClass = reader.ReadByte ();
		if ((elfClass != 1 && elfClass != 2) || reader.ReadByte () != 1) {
			throw new InvalidDataException ("The discrete payload stub must use little-endian ELF32 or ELF64.");
		}
		bool is64Bit = elfClass == 2;
		ulong ReadWord () => is64Bit ? reader.ReadUInt64 () : reader.ReadUInt32 ();
		void WriteWord (ulong value)
		{
			if (is64Bit) writer.Write (value);
			else writer.Write (checked ((uint)value));
		}
		int sectionOffsetField = is64Bit ? 40 : 32;
		stub.Position = sectionOffsetField;
		ulong sectionOffset = ReadWord ();
		stub.Position = is64Bit ? 58 : 46;
		ushort entrySize = reader.ReadUInt16 ();
		ushort count = reader.ReadUInt16 ();
		ushort namesIndex = reader.ReadUInt16 ();
		if (entrySize != (is64Bit ? 64 : 40) || count == 0 || count == ushort.MaxValue ||
			namesIndex >= count || sectionOffset + (ulong)count * entrySize > (ulong)stub.Length) {
			throw new InvalidDataException ("Invalid discrete payload ELF section table.");
		}
		stub.Position = checked ((long)sectionOffset);
		byte [] sections = reader.ReadBytes (count * entrySize);
		stub.Position = checked ((long)sectionOffset + namesIndex * entrySize + (is64Bit ? 24 : 16));
		ulong namesOffset = ReadWord ();
		ulong namesSize = ReadWord ();
		if (namesOffset + namesSize > (ulong)stub.Length) {
			throw new InvalidDataException ("Invalid discrete payload ELF section names.");
		}
		stub.Position = checked ((long)namesOffset);
		byte [] names = reader.ReadBytes (checked ((int)namesSize));
		ulong payloadSize = checked ((ulong)payload.Length);
		ulong newNamesOffset = checked (PayloadSectionAlignment + payloadSize);
		ulong newNamesSize = checked (namesSize + 8);
		ulong newSectionOffset = checked (newNamesOffset + newNamesSize + 7) & ~7ul;
		ulong fileSize = checked (newSectionOffset + (ulong)(count + 1) * entrySize);
		if (!is64Bit && fileSize > uint.MaxValue) {
			throw new NotSupportedException ("The discrete payload exceeds the ELF32 size limit.");
		}
		stub.Position = 0;
		stub.CopyTo (output);
		output.Position = checked ((long)PayloadSectionAlignment);
		payload.CopyTo (output);
		if ((ulong)output.Position != newNamesOffset || payload.Position != payload.Length) {
			throw new IOException ("The discrete payload length changed while writing.");
		}
		writer.Write (names);
		writer.Write (System.Text.Encoding.ASCII.GetBytes ("payload\0"));
		output.Position = checked ((long)newSectionOffset);
		writer.Write (sections);
		output.Position = checked ((long)newSectionOffset + namesIndex * entrySize + (is64Bit ? 24 : 16));
		WriteWord (newNamesOffset);
		WriteWord (newNamesSize);
		output.Position = checked ((long)newSectionOffset + count * entrySize);
		writer.Write (checked ((uint)namesSize));
		writer.Write (1u); // SHT_PROGBITS
		WriteWord (0); // Non-allocated, read-only data.
		WriteWord (0); // No virtual address.
		WriteWord (PayloadSectionAlignment);
		WriteWord (payloadSize);
		writer.Write (0u);
		writer.Write (0u);
		WriteWord (PayloadSectionAlignment);
		WriteWord (0);
		output.Position = sectionOffsetField;
		WriteWord (newSectionOffset);
		output.Position = is64Bit ? 60 : 48;
		writer.Write (checked ((ushort)(count + 1)));
		output.SetLength (checked ((long)fileSize));
	}

	public static string [] GetDirectoriesToCleanUp (Config config)
	{
		var dirs = new List<string> ();

		foreach (var kvp in config.DSOStubPaths) {
			string outputDir = GetArchOutputPath (kvp.Key, config);
			if (Directory.Exists (outputDir)) {
				dirs.Add (outputDir);
			}
		}

		return dirs.ToArray ();
	}
}
