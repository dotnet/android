#!/usr/bin/env dotnet
#:property TargetFramework=net11.0
#:project ../../../../tools/assembly-store-reader-mk2/AssemblyStore/AssemblyStore.csproj

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

		static int Usage (int exitCode = 1)
		{
			Console.WriteLine ("Usage: extract-android-assemblies [--output DIRECTORY] {file.{dll,apk,aab}} [{file.{dll,apk,aab}} ...]");
			Console.WriteLine ();
			Console.WriteLine ("Extracts managed assemblies from .NET for Android files, including legacy/current stores and LZ4/Zstd compression.");
			Console.WriteLine ("Without --output, files are written under `uncompressed-{input-name}` in the current directory.");
			return exitCode;
		}

		static bool ExtractDLL (Stream inputStream, string fileName, string outputFile)
		{
			Console.WriteLine ($"Processing {fileName}");

			using var assemblyStream = new MemoryStream ();
			Stream outputSource = inputStream;
			AssemblyCompressionFormat? format = null;
			try {
				if (AssemblyCompression.TryDecompress (inputStream, assemblyStream, out AssemblyCompressionFormat detectedFormat)) {
					format = detectedFormat;
					assemblyStream.Seek (0, SeekOrigin.Begin);
					outputSource = assemblyStream;
				} else {
					inputStream.Seek (0, SeekOrigin.Begin);
				}
			} catch (InvalidDataException e) {
				Console.Error.WriteLine ($"  Failed to decompress {fileName}: {e.Message}");
				return false;
			}

			string? outputDir = Path.GetDirectoryName (outputFile);
			if (!String.IsNullOrEmpty (outputDir)) {
				Directory.CreateDirectory (outputDir);
			}
			if (File.Exists (outputFile)) {
				Console.Error.WriteLine ($"  Refusing to overwrite existing file '{outputFile}'");
				return false;
			}

			using (var output = File.Open (outputFile, FileMode.CreateNew, FileAccess.Write)) {
				outputSource.CopyTo (output);
				output.Flush ();
			}
			string compression = format.HasValue ? $"{format.Value} assembly decompressed and" : "assembly";
			Console.WriteLine ($"  {compression} extracted to: {outputFile}");
			return true;
		}

		static bool ExtractDLLFile (string filePath, string outputFile)
		{
			using (var fs = File.Open (filePath, FileMode.Open, FileAccess.Read)) {
				return ExtractDLL (fs, filePath, outputFile);
			}
		}

		static bool ExtractIndividualEntries (ZipArchive apk, string filePath, string assembliesPath, string nativeLibrariesPath, string outputDirectory)
		{
			bool retVal = true;
			int assemblyCount = 0;
			foreach (ZipEntry entry in apk) {
				if (!TryGetAssemblyOutputPath (entry.FullName, assembliesPath, nativeLibrariesPath, out string assemblyName)) {
					continue;
				}

				assemblyCount++;

				using (var stream = new MemoryStream ()) {
					entry.Extract (stream);
					stream.Seek (0, SeekOrigin.Begin);
					string outputFile = GetSafeOutputFile (outputDirectory, assemblyName);
					using var payload = new MemoryStream ();
					Stream assemblyInput = stream;
					try {
						if (AssemblyStorePayload.TryExtractELFPayload (stream, payload)) {
							assemblyInput = payload;
						}
					} catch (InvalidDataException e) {
						Console.Error.WriteLine ($"Unable to extract '{filePath}!{entry.FullName}': {e.Message}");
						retVal = false;
						continue;
					}
					retVal &= ExtractDLL (assemblyInput, $"{filePath}!{entry.FullName}", outputFile);
				}
			}

			if (assemblyCount == 0) {
				Console.Error.WriteLine ($"No managed assemblies were found in '{filePath}'");
				return false;
			}

			return retVal;
		}

		static bool ExtractAssemblyStores (string filePath, string outputDirectory)
		{
			(IList<AssemblyStoreExplorer>? stores, string? errorMessage) = AssemblyStoreExplorer.Open (filePath);
			if (stores == null) {
				Console.Error.WriteLine (errorMessage ?? $"Unable to read assembly stores from '{filePath}'");
				return false;
			}

			bool retVal = true;
			int assemblyCount = 0;
			foreach (AssemblyStoreExplorer store in stores) {
				string? abi = store.TargetArch.HasValue ? GetAndroidAbi (store.TargetArch.Value) : null;
				foreach (AssemblyStoreItem assembly in store.Assemblies ?? []) {
					if (assembly.Ignore) {
						continue;
					}

					assemblyCount++;
					string fileName = assembly.Name.EndsWith (".dll", StringComparison.OrdinalIgnoreCase) ? assembly.Name : $"{assembly.Name}.dll";
					string assemblyName = abi == null ? fileName : $"{abi}/{fileName}";
					using Stream? stream = store.ReadImageData (assembly);
					if (stream == null) {
						Console.Error.WriteLine ($"Unable to read '{assembly.Name}' from assembly store '{store.StorePath}'");
						retVal = false;
						continue;
					}

					string outputFile = GetSafeOutputFile (outputDirectory, assemblyName);
					retVal &= ExtractDLL (stream, $"{filePath}!{assemblyName}", outputFile);
				}
			}

			if (assemblyCount == 0) {
				Console.Error.WriteLine ($"No managed assemblies were found in stores from '{filePath}'");
				return false;
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

		static bool ExtractFromArchive (string filePath, string assembliesPath, string nativeLibrariesPath, string outputDirectory)
		{
			using (ZipArchive apk = ZipArchive.Open (filePath, FileMode.Open)) {
				if (HasAssemblyStore (apk, assembliesPath, nativeLibrariesPath)) {
					return ExtractAssemblyStores (filePath, outputDirectory);
				}

				return ExtractIndividualEntries (apk, filePath, assembliesPath, nativeLibrariesPath, outputDirectory);
			}
		}

		static int Main (string[] args)
		{
			string? outputRoot = null;
			var files = new List<string> ();
			for (int i = 0; i < args.Length; i++) {
				if (args [i] == "--help" || args [i] == "-h") {
					return Usage (0);
				} else if (args [i] == "--output" || args [i] == "-o") {
					if (++i >= args.Length) {
						return Usage ();
					}
					outputRoot = args [i];
				} else {
					files.Add (args [i]);
				}
			}

			if (files.Count == 0) {
				return Usage ();
			}
			if (!HaveUniqueOutputs (files)) {
				return 1;
			}

			bool haveErrors = false;
			foreach (string file in files) {
				try {
					string ext = Path.GetExtension (file);
					if (String.Compare (".dll", ext, StringComparison.OrdinalIgnoreCase) == 0) {
						string outputFile = outputRoot == null ?
							Path.Combine (Directory.GetCurrentDirectory (), $"uncompressed-{Path.GetFileName (file)}") :
							GetSafeOutputFile (outputRoot, Path.GetFileName (file));
						if (!ExtractDLLFile (file, outputFile)) {
							haveErrors = true;
						}
						continue;
					}

					if (!TryGetOutputName (file, out string archiveName)) {
						Console.Error.WriteLine ($"Input file '{file}' does not have a safe output name");
						haveErrors = true;
						continue;
					}
					string archiveOutput = outputRoot == null ?
						Path.Combine (Directory.GetCurrentDirectory (), $"uncompressed-{archiveName}") :
						Path.Combine (outputRoot, archiveName);
					if (
						String.Compare (".blob", ext, StringComparison.OrdinalIgnoreCase) == 0 ||
						String.Compare (".manifest", ext, StringComparison.OrdinalIgnoreCase) == 0 ||
						String.Compare (".so", ext, StringComparison.OrdinalIgnoreCase) == 0 ||
						String.IsNullOrEmpty (ext)
					) {
						if (!ExtractAssemblyStores (file, archiveOutput)) {
							haveErrors = true;
						}
						continue;
					}

					if (String.Compare (".apk", ext, StringComparison.OrdinalIgnoreCase) == 0) {
						if (!ExtractFromArchive (file, "assemblies/", "lib/", archiveOutput)) {
							haveErrors = true;
						}
						continue;
					}

					if (String.Compare (".aab", ext, StringComparison.OrdinalIgnoreCase) == 0) {
						if (!ExtractFromArchive (file, "base/root/assemblies/", "base/lib/", archiveOutput)) {
							haveErrors = true;
						}
						continue;
					}

					Console.Error.WriteLine ($"Unsupported input file '{file}'");
					haveErrors = true;
				} catch (UnauthorizedAccessException e) {
					Console.Error.WriteLine ($"Unable to extract '{file}': {e.Message}");
					haveErrors = true;
				} catch (InvalidDataException e) {
					Console.Error.WriteLine ($"Unable to extract '{file}': {e.Message}");
					haveErrors = true;
				} catch (IOException e) {
					Console.Error.WriteLine ($"Unable to extract '{file}': {e.Message}");
					haveErrors = true;
				} catch (InvalidOperationException e) {
					Console.Error.WriteLine ($"Unable to extract '{file}': {e.Message}");
					haveErrors = true;
				} catch (NotSupportedException e) {
					Console.Error.WriteLine ($"Unable to extract '{file}': {e.Message}");
					haveErrors = true;
				} catch (ArgumentException e) {
					Console.Error.WriteLine ($"Unable to extract '{file}': {e.Message}");
					haveErrors = true;
				}
			}

			return haveErrors ? 1 : 0;
		}

		static bool TryGetAssemblyOutputPath (string entryPath, string assembliesPath, string nativeLibrariesPath, out string assemblyName)
		{
			if (entryPath.StartsWith (assembliesPath, StringComparison.Ordinal) && entryPath.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)) {
				assemblyName = entryPath.Substring (assembliesPath.Length);
				return true;
			}

			foreach (AndroidTargetArch arch in targetArchitectures) {
				string abi = GetAndroidAbi (arch);
				string prefix = $"{nativeLibrariesPath}{abi}/";
				if (!entryPath.StartsWith (prefix, StringComparison.Ordinal)) {
					continue;
				}

				string fileName = entryPath.Substring (prefix.Length);
				if (fileName.EndsWith (".ni.dll.so", StringComparison.OrdinalIgnoreCase) || !fileName.EndsWith (".dll.so", StringComparison.OrdinalIgnoreCase)) {
					continue;
				}

				if (fileName.StartsWith ("lib_", StringComparison.Ordinal)) {
					assemblyName = $"{abi}/{fileName.Substring ("lib_".Length, fileName.Length - "lib_".Length - ".so".Length)}";
					return true;
				}

				if (fileName.StartsWith ("lib-", StringComparison.Ordinal)) {
					string satelliteName = fileName.Substring ("lib-".Length, fileName.Length - "lib-".Length - ".so".Length);
					assemblyName = $"{abi}/satellites/{satelliteName}";
					return true;
				}
			}

			assemblyName = "";
			return false;
		}

		static bool HaveUniqueOutputs (List<string> files)
		{
			var outputNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			foreach (string file in files) {
				if (!TryGetOutputName (file, out string outputName)) {
					Console.Error.WriteLine ($"Input file '{file}' does not have a safe output name");
					return false;
				}
				if (!outputNames.Add (outputName)) {
					Console.Error.WriteLine ($"Multiple inputs would use the same output name '{outputName}'. Run them separately or rename one input.");
					return false;
				}
			}
			return true;
		}

		static bool TryGetOutputName (string file, out string outputName)
		{
			outputName = Path.GetExtension (file).Equals (".dll", StringComparison.OrdinalIgnoreCase) ?
				Path.GetFileName (file) :
				Path.GetFileNameWithoutExtension (file);
			return !String.IsNullOrEmpty (outputName) && outputName != "." && outputName != "..";
		}

		static string GetSafeOutputFile (string outputDirectory, string relativePath)
		{
			string root = Path.GetFullPath (outputDirectory);
			string outputFile = Path.GetFullPath (Path.Combine (root, relativePath.Replace ('/', Path.DirectorySeparatorChar)));
			string relativeOutput = Path.GetRelativePath (root, outputFile);
			if (
				Path.IsPathRooted (relativeOutput) ||
				relativeOutput == ".." ||
				relativeOutput.StartsWith ($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
			) {
				throw new InvalidDataException ($"Assembly path '{relativePath}' escapes output directory '{root}'");
			}
			return outputFile;
		}
	}
}
