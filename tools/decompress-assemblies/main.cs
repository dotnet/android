using System;
using System.Collections.Generic;
using System.IO;

using Xamarin.Android.AssemblyStore;
using Xamarin.Android.Tools;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Tools.DecompressAssemblies
{
	class App
	{
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
			Console.WriteLine ($"Processing {fileName}");

			using var assemblyStream = new MemoryStream ();
			AssemblyCompressionFormat format;
			try {
				if (!AssemblyCompression.TryDecompress (inputStream, assemblyStream, out format)) {
					Console.WriteLine ("  assembly is not compressed");
					return true;
				}
			} catch (InvalidDataException e) {
				Console.Error.WriteLine ($"  Failed to decompress {fileName}: {e.Message}");
				return false;
			}

			string? outputDir = Path.GetDirectoryName (outputFile);
			if (!String.IsNullOrEmpty (outputDir)) {
				Directory.CreateDirectory (outputDir);
			}

			assemblyStream.Seek (0, SeekOrigin.Begin);
			using (var output = File.Open (outputFile, FileMode.Create, FileAccess.Write)) {
				assemblyStream.CopyTo (output);
				output.Flush ();
			}
			Console.WriteLine ($"  {format} assembly uncompressed to: {outputFile}");
			return true;
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

		static bool UncompressFromAPK_AssemblyStores (string filePath, string prefix)
		{
			(IList<AssemblyStoreExplorer>? stores, string? errorMessage) = AssemblyStoreExplorer.Open (filePath);
			if (stores == null) {
				Console.Error.WriteLine (errorMessage ?? $"Unable to read assembly stores from '{filePath}'");
				return false;
			}

			bool retVal = true;
			foreach (AssemblyStoreExplorer store in stores) {
				string? abi = store.TargetArch.HasValue ? GetAndroidAbi (store.TargetArch.Value) : null;
				foreach (AssemblyStoreItem assembly in store.Assemblies ?? []) {
					if (assembly.Ignore) {
						continue;
					}

					string fileName = assembly.Name.EndsWith (".dll", StringComparison.OrdinalIgnoreCase) ? assembly.Name : $"{assembly.Name}.dll";
					string assemblyName = abi == null ? fileName : $"{abi}/{fileName}";
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

		static bool HasAssemblyStore (ZipArchive apk, string assembliesPath, string nativeLibrariesPath)
		{
			if (apk.ContainsEntry ($"{assembliesPath}assemblies.blob")) {
				return true;
			}

			foreach (AndroidTargetArch arch in targetArchitectures) {
				string abi = GetAndroidAbi (arch);
				if (
					apk.ContainsEntry ($"{nativeLibrariesPath}{abi}/libassembly-store.so") ||
					apk.ContainsEntry ($"{nativeLibrariesPath}{abi}/libassemblies.{abi}.blob.so")
				) {
					return true;
				}
			}

			return false;
		}

		static bool UncompressFromAPK (string filePath, string assembliesPath, string nativeLibrariesPath)
		{
			string prefix = $"uncompressed-{Path.GetFileNameWithoutExtension (filePath)}{Path.DirectorySeparatorChar}";
			using (ZipArchive apk = ZipArchive.Open (filePath, FileMode.Open)) {
				if (HasAssemblyStore (apk, assembliesPath, nativeLibrariesPath)) {
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
