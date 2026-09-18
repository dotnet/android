using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;
using Properties = Xamarin.Android.Tasks.Properties;

namespace Microsoft.Android.Tasks;

public class ExtractTypeMapKeysFromNativeAotObject : AsyncTask
{
	const string TypeMapSymbol = "__external_type_map__";
	const string CommonFixupsSymbol = "__external_CommonFixupsTable_references";
	const long SectionGroupFlag = 0x200;

	public override string TaskPrefix => "ETMKNAO";

	[Required]
	public ITaskItem [] NativeObjectFiles { get; set; } = [];

	[Required]
	public string LlvmReadObjPath { get; set; } = "";

	[Required]
	public string OutputFile { get; set; } = "";

	public override async Task RunTaskAsync ()
	{
		string currentFile = nameof (NativeObjectFiles);
		try {
			if (NativeObjectFiles.Length == 0) {
				throw new BadImageFormatException ("No NativeAOT object files were supplied.");
			}
			currentFile = LlvmReadObjPath;
			if (!Path.IsPathFullyQualified (LlvmReadObjPath) || !File.Exists (LlvmReadObjPath)) {
				throw new FileNotFoundException ("An existing full path to llvm-readobj is required.", LlvmReadObjPath);
			}

			var keys = new SortedSet<string> (StringComparer.Ordinal);
			foreach (var file in NativeObjectFiles) {
				CancellationToken.ThrowIfCancellationRequested ();
				currentFile = file.ItemSpec;
				using var stream = File.OpenRead (currentFile);
				string metadata = await ReadObjectMetadataAsync (currentFile).ConfigureAwait (false);
				using var document = JsonDocument.Parse (metadata);
				await ReadKeysAsync (currentFile, stream, document.RootElement, keys).ConfigureAwait (false);
			}

			CancellationToken.ThrowIfCancellationRequested ();
			currentFile = OutputFile;
			string? directory = Path.GetDirectoryName (OutputFile);
			if (!string.IsNullOrEmpty (directory)) {
				Directory.CreateDirectory (directory);
			}
			using var writer = new StreamWriter (OutputFile, append: false, new UTF8Encoding (false, true)) { NewLine = "\n" };
			foreach (string key in keys) {
				writer.WriteLine (key);
			}
		} catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is BadImageFormatException ||
			e is JsonException || e is InvalidOperationException || e is FormatException || e is ArgumentException || e is Win32Exception) {
			LogCodedError ("XA4327", Properties.Resources.XA4327, currentFile, e.Message);
		}
	}

	protected virtual async Task<string> ReadObjectMetadataAsync (string objectFile)
	{
		using var stdout = new StringWriter (CultureInfo.InvariantCulture);
		using var stderr = new StringWriter (CultureInfo.InvariantCulture);
		var startInfo = ProcessUtils.CreateProcessStartInfo (LlvmReadObjPath,
			"--elf-output-style=JSON", "--file-headers", "--sections", Path.GetFullPath (objectFile));
		int exitCode = await ProcessUtils.StartProcess (startInfo, stdout, stderr, CancellationToken).ConfigureAwait (false);
		if (exitCode != 0) {
			throw new InvalidOperationException ($"llvm-readobj exited with code {exitCode}: {stderr}");
		}

		using var document = JsonDocument.Parse (stdout.ToString ());
		if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength () != 1) {
			throw new BadImageFormatException ("Expected llvm-readobj metadata for one native object.");
		}
		var file = document.RootElement [0];
		long? sectionIndex = null;
		foreach (var section in ReadSections (file).Values) {
			if (Text (Property (section, "Name"), "Name") == ".rodata" &&
				(Number (Property (section, "Flags"), "Value") & SectionGroupFlag) == 0) {
				if (sectionIndex != null) {
					throw new BadImageFormatException ("Ambiguous NativeAOT read-only data section.");
				}
				sectionIndex = Number (section, "Index");
			}
		}
		if (sectionIndex is not long dataSection) {
			throw new BadImageFormatException ("The NativeAOT read-only data section was not found.");
		}

		// A real app can have hundreds of MB of symbol JSON. Keep only the two
		// non-COMDAT .rodata symbols emitted by ILC, not a DOM of the entire symbol table.
		using var symbols = new TypeMapSymbolWriter ();
		await RunObjdumpAsync (symbols, "--syms", Path.GetFullPath (objectFile)).ConfigureAwait (false);
		symbols.Flush ();
		using var result = new MemoryStream ();
		using (var json = new Utf8JsonWriter (result)) {
			json.WriteStartArray ();
			json.WriteStartObject ();
			foreach (var property in file.EnumerateObject ()) {
				if (property.Name != "Symbols") {
					property.WriteTo (json);
				}
			}
			json.WriteStartArray ("Symbols");
			foreach (string line in symbols.Lines) {
				string [] fields = line.Split ([' ', '\t'], 6, StringSplitOptions.RemoveEmptyEntries);
				if (fields.Length != 6 || fields [2] != "O" || fields [3] != ".rodata") {
					continue;
				}
				if (!long.TryParse (fields [0], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out long value) ||
					!long.TryParse (fields [4], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out long size)) {
					throw new BadImageFormatException ("Invalid llvm-objdump type map symbol record.");
				}
				string name = fields [5].Trim ();
				if (name.StartsWith (".hidden ", StringComparison.Ordinal)) {
					name = name.Substring (".hidden ".Length).TrimStart ();
				}
				json.WriteStartObject ();
				json.WriteStartObject ("Symbol");
				json.WriteStartObject ("Name");
				json.WriteString ("Name", name);
				json.WriteEndObject ();
				json.WriteNumber ("Value", value);
				json.WriteNumber ("Size", size);
				json.WriteStartObject ("Section");
				json.WriteNumber ("Value", dataSection);
				json.WriteEndObject ();
				json.WriteEndObject ();
				json.WriteEndObject ();
			}
			json.WriteEndArray ();
			json.WriteEndObject ();
			json.WriteEndArray ();
		}
		return Encoding.UTF8.GetString (result.ToArray ());
	}

	protected virtual async Task<string> ReadObjectRelocationsAsync (string objectFile, string section, long start, long end)
	{
		using var stdout = new StringWriter (CultureInfo.InvariantCulture);
		await RunObjdumpAsync (stdout, "--reloc", "--section=" + section,
			"--start-address=0x" + start.ToString ("x", CultureInfo.InvariantCulture),
			"--stop-address=0x" + end.ToString ("x", CultureInfo.InvariantCulture), Path.GetFullPath (objectFile)).ConfigureAwait (false);
		return stdout.ToString ();
	}

	async Task RunObjdumpAsync (TextWriter stdout, params string [] arguments)
	{
		string? directory = Path.GetDirectoryName (LlvmReadObjPath);
		if (string.IsNullOrEmpty (directory)) {
			throw new ArgumentException ("Expected a full path to llvm-readobj.", nameof (LlvmReadObjPath));
		}
		string objdump = Path.Combine (directory, OperatingSystem.IsWindows () ? "llvm-objdump.exe" : "llvm-objdump");
		if (!File.Exists (objdump)) {
			throw new FileNotFoundException ("llvm-objdump is required alongside llvm-readobj.", objdump);
		}
		using var stderr = new StringWriter (CultureInfo.InvariantCulture);
		var startInfo = ProcessUtils.CreateProcessStartInfo (objdump, arguments);
		int exitCode = await ProcessUtils.StartProcess (startInfo, stdout, stderr, CancellationToken).ConfigureAwait (false);
		if (exitCode != 0) {
			throw new InvalidOperationException ($"llvm-objdump exited with code {exitCode}: {stderr}");
		}
	}

	protected virtual async Task<HashSet<uint>> GetJavaTypeMapGroupsAsync (
		string objectFile, JsonElement metadata, string mapSymbol, IReadOnlyCollection<uint> groupIndices, long fileLength)
	{
		var selected = new HashSet<uint> ();
		if (groupIndices.Count == 0) {
			return selected;
		}

		string fixupsName = mapSymbol.Substring (0, mapSymbol.Length - TypeMapSymbol.Length) + CommonFixupsSymbol;
		var sections = ReadSections (metadata);
		JsonElement? fixups = null;
		foreach (var item in Property (metadata, "Symbols").EnumerateArray ()) {
			var symbol = Property (item, "Symbol");
			if (Text (Property (symbol, "Name"), "Name") == fixupsName) {
				if (fixups != null) {
					throw new BadImageFormatException ("Duplicate NativeAOT common-fixup symbol.");
				}
				fixups = symbol;
			}
		}
		if (fixups is not JsonElement fixupSymbol) {
			throw new BadImageFormatException ("The NativeAOT common-fixup table was not found.");
		}
		var region = ReadRegion (fixupSymbol, sections, fileLength);
		if (region.Size % 4 != 0) {
			throw new BadImageFormatException ("Invalid NativeAOT common-fixup table size.");
		}

		string? relocationSection = null;
		foreach (var section in sections.Values) {
			string type = Text (Property (section, "Type"), "Name");
			if ((type == "SHT_REL" || type == "SHT_RELA") && Number (section, "Info") == region.SectionIndex) {
				if (relocationSection != null) {
					throw new BadImageFormatException ("Ambiguous NativeAOT common-fixup relocation section.");
				}
				relocationSection = Text (Property (section, "Name"), "Name");
			}
		}
		if (relocationSection == null) {
			throw new BadImageFormatException ("The NativeAOT common-fixup relocations were not found.");
		}

		var slots = new Dictionary<long, uint> ();
		long start = long.MaxValue;
		long end = 0;
		foreach (uint index in groupIndices) {
			// All supported Android targets use 32-bit self-relative common fixups.
			long relativeOffset = (long) index * 4;
			if (relativeOffset >= region.Size) {
				throw new BadImageFormatException ("A NativeAOT type map group references an invalid common-fixup index.");
			}
			long slot = region.SectionRelativeOffset + relativeOffset;
			slots.Add (slot, index);
			start = Math.Min (start, slot);
			end = Math.Max (end, slot + 4);
		}

		string architecture = Text (Property (metadata, "FileSummary"), "Arch");
		string relocationType = architecture switch {
			"aarch64" => "R_AARCH64_PREL32",
			"arm" => "R_ARM_REL32",
			"x86_64" => "R_X86_64_PC32",
			"i386" => "R_386_PC32",
			_ => throw new BadImageFormatException ("Unsupported NativeAOT relocation architecture."),
		};
		var resolved = new HashSet<uint> ();
		string relocations = await ReadObjectRelocationsAsync (objectFile, relocationSection, start, end).ConfigureAwait (false);
		using var lines = new StringReader (relocations);
		string? line;
		while ((line = lines.ReadLine ()) != null) {
			string [] fields = line.Split ([' ', '\t'], 3, StringSplitOptions.RemoveEmptyEntries);
			if (fields.Length == 0 || !long.TryParse (fields [0], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out long offset) ||
				!slots.TryGetValue (offset, out uint index)) {
				continue;
			}
			if (fields.Length != 3 || fields [1] != relocationType || !resolved.Add (index)) {
				throw new BadImageFormatException ("Invalid or ambiguous NativeAOT type map group relocation.");
			}
			string groupSymbol = fields [2].Trim ();
			bool isJavaGroup = IsJavaGroupSymbol (groupSymbol);
			LogDebugMessage ("NativeAOT type map group {0}: {1} ({2}).", index, groupSymbol, isJavaGroup ? "Java" : "not Java");
			if (isJavaGroup) {
				selected.Add (index);
			}
		}
		if (resolved.Count != slots.Count) {
			throw new BadImageFormatException ("A NativeAOT type map group relocation was not found.");
		}
		if (selected.Count == 0) {
			throw new BadImageFormatException ("No Java type map group was found in the NativeAOT object.");
		}
		return selected;
	}

	static bool IsJavaGroupSymbol (string symbol)
	{
		int marker = symbol.IndexOf ("_ZTV", StringComparison.Ordinal);
		if (marker < 0) {
			return false;
		}
		int start = marker + 4;
		int nameStart = start;
		while (nameStart < symbol.Length && symbol [nameStart] >= '0' && symbol [nameStart] <= '9') {
			nameStart++;
		}
		if (nameStart == start) {
			return false;
		}
		string name = symbol.Substring (nameStart);
		// Match the group identities emitted by TypeMapAssemblyEmitter, not the keys'
		// appearance: the built-in JavaDictionary universe contains managed type names.
		return name == "Mono_Android_Java_Lang_Object" ||
			(name.StartsWith ("_", StringComparison.Ordinal) && name.EndsWith ("_TypeMap___TypeMapAnchor", StringComparison.Ordinal));
	}

	async Task ReadKeysAsync (string objectFile, FileStream stream, JsonElement root, ISet<string> keys)
	{
		if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength () != 1) {
			throw new BadImageFormatException ("Expected llvm-readobj metadata for one native object.");
		}
		var file = root [0];
		var summary = Property (file, "FileSummary");
		string format = Text (summary, "Format");
		string architecture = Text (summary, "Arch");
		var header = Property (file, "ElfHeader");
		if (!format.StartsWith ("elf", StringComparison.Ordinal) ||
			(architecture != "aarch64" && architecture != "arm" && architecture != "x86_64" && architecture != "i386") ||
			Text (header, "Type") != "Relocatable (0x1)" ||
			Number (Property (Property (header, "Ident"), "DataEncoding"), "Value") != 1) {
			throw new BadImageFormatException ("Expected a little-endian ARM, ARM64, x86, or x64 relocatable ELF object.");
		}

		var sections = ReadSections (file);

		bool found = false;
		foreach (var item in Property (file, "Symbols").EnumerateArray ()) {
			var symbol = Property (item, "Symbol");
			string name = Text (Property (symbol, "Name"), "Name");
			if (!name.EndsWith (TypeMapSymbol, StringComparison.Ordinal)) {
				continue;
			}
			found = true;
			var region = ReadRegion (symbol, sections, stream.Length);

			// LLVM supplies the ELF layout. Only the symbol's relocation-free NativeFormat
			// payload is interpreted here; unrelated strings elsewhere in the object are ignored.
			stream.Position = region.FileOffset;
			var data = new byte [(int) region.Size];
			stream.ReadExactly (data);
			var reader = new NativeAotTypeMapReader (data);
			var javaGroups = await GetJavaTypeMapGroupsAsync (objectFile, file, name, reader.ReadGroupTypeIndices (), stream.Length).ConfigureAwait (false);
			reader.ReadKeys (keys, javaGroups);
		}
		if (!found) {
			throw new BadImageFormatException ("No NativeAOT external type map symbol was found.");
		}
	}

	static Dictionary<long, JsonElement> ReadSections (JsonElement metadata)
	{
		var sections = new Dictionary<long, JsonElement> ();
		foreach (var item in Property (metadata, "Sections").EnumerateArray ()) {
			var section = Property (item, "Section");
			if (!sections.TryAdd (Number (section, "Index"), section)) {
				throw new BadImageFormatException ("Duplicate ELF section index in llvm-readobj output.");
			}
		}
		return sections;
	}

	static (long FileOffset, long SectionRelativeOffset, long Size, long SectionIndex) ReadRegion (
		JsonElement symbol, Dictionary<long, JsonElement> sections, long fileLength)
	{
		string name = Text (Property (symbol, "Name"), "Name");
		long sectionIndex = Number (Property (symbol, "Section"), "Value");
		if (!sections.TryGetValue (sectionIndex, out var section) || Text (Property (section, "Type"), "Name") != "SHT_PROGBITS") {
			throw new BadImageFormatException ($"NativeAOT symbol '{name}' has no file-backed data section.");
		}
		long sectionOffset = Number (section, "Offset");
		long sectionSize = Number (section, "Size");
		long value = Number (symbol, "Value");
		long size = Number (symbol, "Size");
		if (sectionOffset > fileLength || sectionSize > fileLength - sectionOffset ||
			value > sectionSize || size > sectionSize - value || size == 0 || size > int.MaxValue) {
			throw new BadImageFormatException ($"NativeAOT symbol '{name}' has an invalid file range.");
		}
		return (sectionOffset + value, value, size, sectionIndex);
	}

	static JsonElement Property (JsonElement element, string name)
	{
		if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty (name, out var value)) {
			throw new BadImageFormatException ($"llvm-readobj output is missing '{name}'.");
		}
		return value;
	}

	static string Text (JsonElement element, string name)
	{
		var value = Property (element, name);
		if (value.ValueKind != JsonValueKind.String || value.GetString () is not string text) {
			throw new BadImageFormatException ($"llvm-readobj property '{name}' is not a string.");
		}
		return text;
	}

	static long Number (JsonElement element, string name)
	{
		var value = Property (element, name);
		if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64 (out long number) || number < 0) {
			throw new BadImageFormatException ($"llvm-readobj property '{name}' is not a nonnegative integer.");
		}
		return number;
	}

	sealed class TypeMapSymbolWriter : TextWriter
	{
		readonly StringBuilder line = new ();

		public List<string> Lines { get; } = [];
		public override Encoding Encoding => Encoding.UTF8;

		public override void Write (char value)
		{
			if (value == '\n') {
				Flush ();
			} else {
				line.Append (value);
			}
		}

		public override void Write (char []? buffer, int index, int count)
		{
			ArgumentNullException.ThrowIfNull (buffer);
			Write (buffer.AsSpan (index, count));
		}

		public override void Write (ReadOnlySpan<char> buffer)
		{
			int newline;
			while ((newline = buffer.IndexOf ('\n')) >= 0) {
				line.Append (buffer.Slice (0, newline));
				Flush ();
				buffer = buffer.Slice (newline + 1);
			}
			line.Append (buffer);
		}

		public override void Flush ()
		{
			while (line.Length > 0 && char.IsWhiteSpace (line [line.Length - 1])) {
				line.Length--;
			}
			if (EndsWith (TypeMapSymbol) || EndsWith (CommonFixupsSymbol)) {
				Lines.Add (line.ToString ());
			}
			line.Clear ();
		}

		bool EndsWith (string suffix)
		{
			if (line.Length < suffix.Length) {
				return false;
			}
			for (int i = 0; i < suffix.Length; i++) {
				if (line [line.Length - suffix.Length + i] != suffix [i]) {
					return false;
				}
			}
			return true;
		}
	}
}
