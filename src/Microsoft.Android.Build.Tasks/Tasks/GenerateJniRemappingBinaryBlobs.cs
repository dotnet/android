#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks;
using Properties = Xamarin.Android.Tasks.Properties;

namespace Microsoft.Android.Tasks;

/// <summary>Produces self-contained, read-only ELF data libraries from merged JNI remapping XML.</summary>
public class GenerateJniRemappingBinaryBlobs : AndroidTask
{
	public override string TaskPrefix => "GJRBB";

	public string RemappingXmlFilePath { get; set; } = "";

	[Required]
	public string OutputDirectory { get; set; } = "";

	[Required]
	public string [] SupportedAbis { get; set; } = [];

	// Intended only for the private build-time compression switch.
	public bool Compress { get; set; }

	[Output]
	public ITaskItem [] BinaryBlobLibraries { get; set; } = [];

	public override bool RunTask ()
	{
		try {
			if (string.IsNullOrWhiteSpace (OutputDirectory) || SupportedAbis.Length == 0 ||
				SupportedAbis.Any (string.IsNullOrWhiteSpace)) {
				throw new InvalidDataException ("An output directory and at least one ABI are required.");
			}
			byte [] blob = JniRemappingBinaryBlob.Create (RemappingXmlFilePath, Compress);
			var libraries = new List<(string Abi, string Path, byte [] Data)> ();
			var seen = new HashSet<string> (StringComparer.Ordinal);
			foreach (string abi in SupportedAbis) {
				if (!seen.Add (abi)) throw new InvalidDataException ($"Duplicate ABI: {abi}.");
				var arch = MonoAndroidHelper.AbiToTargetArch (abi);
				if (arch is not (Xamarin.Android.Tools.AndroidTargetArch.Arm or Xamarin.Android.Tools.AndroidTargetArch.Arm64 or
					Xamarin.Android.Tools.AndroidTargetArch.X86 or Xamarin.Android.Tools.AndroidTargetArch.X86_64)) {
					throw new InvalidDataException ($"Unsupported ABI: {abi}.");
				}
				using var source = new MemoryStream (blob, writable: false);
				using var output = new MemoryStream ();
				AssemblyStoreElfWriter.Write (new [] { (JniRemappingBinaryBlob.Symbol, (Stream)source) },
					output, arch, "libbinary_blobs.so");
				byte [] elf = output.ToArray ();
				AssemblyStoreElfWriter.Validate (elf, arch, "libbinary_blobs.so",
					new [] { (JniRemappingBinaryBlob.Symbol, blob) });
				JniRemappingBinaryBlob.Validate (blob);
				libraries.Add ((abi, Path.Combine (OutputDirectory, abi, "libbinary_blobs.so"), elf));
			}
			foreach (var (abi, path, data) in libraries) {
				Directory.CreateDirectory (Path.GetDirectoryName (path) ?? throw new InvalidDataException ("No output directory."));
				File.WriteAllBytes (path, data);
				var item = new TaskItem (path);
				item.SetMetadata ("Abi", abi);
				item.SetMetadata ("ArchivePath", $"lib/{abi}/libbinary_blobs.so");
				BinaryBlobLibraries = [.. BinaryBlobLibraries, item];
			}
		} catch (Exception ex) when (ex is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException or
			NotSupportedException or OverflowException or XmlException) {
			Log.LogCodedError ("XA4325", Properties.Resources.XA4325, ex.Message);
		}
		return !Log.HasLoggedErrors;
	}
}
