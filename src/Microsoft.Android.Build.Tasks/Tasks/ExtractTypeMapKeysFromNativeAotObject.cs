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
				ReadKeys (stream, document.RootElement, keys);
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
			"--elf-output-style=JSON", "--file-headers", "--sections", "--symbols", Path.GetFullPath (objectFile));
		int exitCode = await ProcessUtils.StartProcess (startInfo, stdout, stderr, CancellationToken).ConfigureAwait (false);
		if (exitCode != 0) {
			throw new InvalidOperationException ($"llvm-readobj exited with code {exitCode}: {stderr}");
		}
		return stdout.ToString ();
	}

	static void ReadKeys (FileStream stream, JsonElement root, ISet<string> keys)
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

		var sections = new Dictionary<long, JsonElement> ();
		var sectionList = Property (file, "Sections");
		foreach (var item in sectionList.EnumerateArray ()) {
			var section = Property (item, "Section");
			if (!sections.TryAdd (Number (section, "Index"), section)) {
				throw new BadImageFormatException ("Duplicate ELF section index in llvm-readobj output.");
			}
		}

		bool found = false;
		foreach (var item in Property (file, "Symbols").EnumerateArray ()) {
			var symbol = Property (item, "Symbol");
			string name = Text (Property (symbol, "Name"), "Name");
			if (!name.EndsWith (TypeMapSymbol, StringComparison.Ordinal)) {
				continue;
			}
			found = true;
			long sectionIndex = Number (Property (symbol, "Section"), "Value");
			if (!sections.TryGetValue (sectionIndex, out var section) ||
				Text (Property (section, "Type"), "Name") != "SHT_PROGBITS") {
				throw new BadImageFormatException ($"NativeAOT type map symbol '{name}' has no file-backed data section.");
			}
			long sectionOffset = Number (section, "Offset");
			long sectionSize = Number (section, "Size");
			long value = Number (symbol, "Value");
			long size = Number (symbol, "Size");
			if (sectionOffset > stream.Length || sectionSize > stream.Length - sectionOffset ||
				value > sectionSize || size > sectionSize - value || size == 0 || size > int.MaxValue) {
				throw new BadImageFormatException ($"NativeAOT type map symbol '{name}' has an invalid file range.");
			}

			// LLVM supplies the ELF layout. Only the symbol's relocation-free NativeFormat
			// payload is interpreted here; unrelated strings elsewhere in the object are ignored.
			stream.Position = sectionOffset + value;
			var data = new byte [(int) size];
			stream.ReadExactly (data);
			new NativeAotTypeMapReader (data).ReadKeys (keys);
		}
		if (!found) {
			throw new BadImageFormatException ("No NativeAOT external type map symbol was found.");
		}
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
}
