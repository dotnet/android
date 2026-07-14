extern alias AssemblyStoreReaderV2;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;

using Xamarin.Tools.Zip;
using Xamarin.Android.AssemblyStore;
using Xamarin.Android.Tools;
using AssemblyStoreExplorerV2 = AssemblyStoreReaderV2::Xamarin.Android.AssemblyStore.AssemblyStoreExplorer;
using AssemblyStoreItemV2 = AssemblyStoreReaderV2::Xamarin.Android.AssemblyStore.AssemblyStoreItem;
using ZstandardDecoder = System.IO.Compression.ZstandardDecoder;

namespace Xamarin.Android.Tools.DecompressAssemblies
{
	class App
	{
		const uint CompressedDataMagic = 0x535A4158; // 'XAZS', little-endian

		static readonly ArrayPool<byte> bytePool = ArrayPool<byte>.Shared;
		static readonly AndroidTargetArch[] targetArchitectures = [
			AndroidTargetArch.Arm64,
			AndroidTargetArch.Arm,
			AndroidTargetArch.X86_64,
			AndroidTargetArch.X86,
		];

		static int Usage ()
		{
			Console.WriteLine ("Usage: decompress-assemblies {file.{dll,apk,aab}} [{file.{dll,apk,aab} ...]");
			Console.WriteLine ();
			Console.WriteLine ("DLL files passed on command line are uncompressed to the current directory with the `uncompressed-` prefix added to their name.");
			Console.WriteLine ("DLL files from AAB/APK archives are uncompressed to a subdirectory of the current directory named after the archive with extension removed");
			return 1;
		}

		static bool UncompressDLL (Stream inputStream, string fileName, string filePath, string prefix)
		{
			string outputFile = $"{prefix}{filePath}";
			bool retVal = true;

			Console.WriteLine ($"Processing {fileName}");
			//
			// Zstd compressed assembly header format:
			//   uint magic;                 // 0x535A4158; 'XAZS', little-endian
			//   uint descriptor_index;      // Index into an internal assembly descriptor table
			//   uint uncompressed_length;   // Size of assembly, uncompressed
			//
			using (var reader = new BinaryReader (inputStream)) {
				uint magic = reader.ReadUInt32 ();
				if (magic == CompressedDataMagic) {
					reader.ReadUInt32 (); // descriptor index, ignore
					uint decompressedLength = reader.ReadUInt32 ();

					int inputLength = (int)(inputStream.Length - 12);
					byte[] sourceBytes = bytePool.Rent (inputLength);
					reader.Read (sourceBytes, 0, inputLength);

					byte[] assemblyBytes = bytePool.Rent ((int)decompressedLength);
					int decoded = ZstandardDecoder.TryDecompress (
						sourceBytes.AsSpan (0, inputLength),
						assemblyBytes.AsSpan (0, (int)decompressedLength),
						out int bytesWritten) ? bytesWritten : -1;
					if (decoded != (int)decompressedLength) {
						Console.Error.WriteLine ($"  Failed to decompress Zstd data of {fileName} (decoded: {decoded})");
						retVal = false;
					} else {
						string? outputDir = Path.GetDirectoryName (outputFile);
						if (!String.IsNullOrEmpty (outputDir)) {
							Directory.CreateDirectory (outputDir);
						}
						using (var fs = File.Open (outputFile, FileMode.Create, FileAccess.Write)) {
							fs.Write (assemblyBytes, 0, decoded);
							fs.Flush ();
						}
						Console.WriteLine ($"  uncompressed to: {outputFile}");
					}

					bytePool.Return (sourceBytes);
					bytePool.Return (assemblyBytes);
				} else {
					Console.WriteLine ($"  assembly is not compressed");
				}
			}

			return retVal;
		}

		static bool UncompressDLL (string filePath, string prefix)
		{
			using (var fs = File.Open (filePath, FileMode.Open, FileAccess.Read)) {
				return UncompressDLL (fs, filePath, Path.GetFileName (filePath), prefix);
			}
		}

		static bool UncompressFromAPK_IndividualEntries (ZipArchive apk, string filePath, string assembliesPath, string prefix)
		{
			bool retVal = true;
			foreach (ZipEntry entry in apk) {
				if (!entry.FullName.StartsWith (assembliesPath, StringComparison.Ordinal)) {
					continue;
				}

				if (!entry.FullName.EndsWith (".dll", StringComparison.Ordinal)) {
					continue;
				}

				using (var stream = new MemoryStream ()) {
					entry.Extract (stream);
					stream.Seek (0, SeekOrigin.Begin);
					string fileName = entry.FullName.Substring (assembliesPath.Length);
					retVal &= UncompressDLL (stream, $"{filePath}!{entry.FullName}", fileName, prefix);
				}
			}

			return retVal;
		}

		static bool UncompressFromAPK_LegacyAssemblyStores (string filePath, string prefix)
		{
			bool retVal = true;
			var explorer = new AssemblyStoreExplorer (filePath, keepStoreInMemory: true);
			foreach (AssemblyStoreAssembly assembly in explorer.Assemblies) {
				string assemblyName = assembly.DllName;

				if (!String.IsNullOrEmpty (assembly.Store.Arch)) {
					assemblyName = $"{assembly.Store.Arch}/{assemblyName}";
				}

				using (var stream = new MemoryStream ()) {
					assembly.ExtractImage (stream);
					stream.Seek (0, SeekOrigin.Begin);
					retVal &= UncompressDLL (stream, $"{filePath}!{assemblyName}", assemblyName, prefix);
				}
			}

			return retVal;
		}

		static bool UncompressFromAPK_AssemblyStores (string filePath, string prefix)
		{
			(IList<AssemblyStoreExplorerV2>? stores, string? errorMessage) = AssemblyStoreExplorerV2.Open (filePath);
			if (stores == null) {
				Console.Error.WriteLine (errorMessage ?? $"Unable to read assembly stores from '{filePath}'");
				return false;
			}

			bool retVal = true;
			foreach (AssemblyStoreExplorerV2 store in stores) {
				if (!store.TargetArch.HasValue) {
					Console.Error.WriteLine ($"Assembly store '{store.StorePath}' does not specify a target architecture");
					retVal = false;
					continue;
				}

				string abi = GetAndroidAbi (store.TargetArch.Value);
				foreach (AssemblyStoreItemV2 assembly in store.Assemblies ?? []) {
					if (assembly.Ignore) {
						continue;
					}

					string assemblyName = $"{abi}/{assembly.Name}";
					using Stream? stream = store.ReadImageData (assembly);
					if (stream == null) {
						Console.Error.WriteLine ($"Unable to read '{assembly.Name}' from assembly store '{store.StorePath}'");
						retVal = false;
						continue;
					}

					retVal &= UncompressDLL (stream, $"{filePath}!{assemblyName}", assemblyName, prefix);
				}
			}

			return retVal;
		}

		static string GetAndroidAbi (AndroidTargetArch arch)
		{
			return arch switch {
				AndroidTargetArch.Arm64  => "arm64-v8a",
				AndroidTargetArch.Arm    => "armeabi-v7a",
				AndroidTargetArch.X86_64 => "x86_64",
				AndroidTargetArch.X86    => "x86",
				_ => throw new NotSupportedException ($"Unsupported target architecture '{arch}'"),
			};
		}

		static bool HasAssemblyStore (ZipArchive apk, string nativeLibrariesPath)
		{
			foreach (AndroidTargetArch arch in targetArchitectures) {
				string abi = GetAndroidAbi (arch);
				if (apk.ContainsEntry ($"{nativeLibrariesPath}{abi}/libassembly-store.so")) {
					return true;
				}
			}

			return false;
		}

		static bool UncompressFromAPK (string filePath, string assembliesPath, string nativeLibrariesPath)
		{
			string prefix = $"uncompressed-{Path.GetFileNameWithoutExtension (filePath)}{Path.DirectorySeparatorChar}";
			using (ZipArchive apk = ZipArchive.Open (filePath, FileMode.Open)) {
				if (apk.ContainsEntry ($"{assembliesPath}assemblies.blob")) {
					return UncompressFromAPK_LegacyAssemblyStores (filePath, prefix);
				}

				if (HasAssemblyStore (apk, nativeLibrariesPath)) {
					return UncompressFromAPK_AssemblyStores (filePath, prefix);
				}

				return UncompressFromAPK_IndividualEntries (apk, filePath, assembliesPath, prefix);
			}
		}

		static int Main (string[] args)
		{
			if (args.Length == 0) {
				return Usage ();
			}

			bool haveErrors = false;
			foreach (string file in args) {
				string ext = Path.GetExtension (file);
				if (String.Compare (".dll", ext, StringComparison.OrdinalIgnoreCase) == 0) {
					if (!UncompressDLL (file, "uncompressed-")) {
						haveErrors = true;
					}
					continue;
				}

				if (String.Compare (".apk", ext, StringComparison.OrdinalIgnoreCase) == 0) {
					if (!UncompressFromAPK (file, "assemblies/", "lib/")) {
						haveErrors = true;
					}
					continue;
				}

				if (String.Compare (".aab", ext, StringComparison.OrdinalIgnoreCase) == 0) {
					if (!UncompressFromAPK (file, "base/root/assemblies/", "base/lib/")) {
						haveErrors = true;
					}
					continue;
				}
			}

			return haveErrors ? 1 : 0;
		}
	}
}
