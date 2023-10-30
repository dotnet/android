using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

using BlobAssemblyInfo = AssemblyBlobDSOGenerator.BlobAssemblyInfo;

public class GenerateAssemblyBlobDSO : AndroidTask
{
	// We need our data to start at page boundary and since all stubs are (currently) smaller than 4k, we go with precisely that value
	const long MaximumStubSize = 4096;

	public override string TaskPrefix => "GABD";

	[Required]
	public ITaskItem[] Assemblies                     { get; set; }

	[Required]
	public ITaskItem[] AssemblyBlobDSOs               { get; set; }

	[Required]
	public bool EnableCompression                     { get; set; }

	[Required]
	public string CompressedAssembliesOutputDirectory { get; set; }

	[Required]
	public string SourcesOutputDirectory              { get; set; }

	AssemblyCompression? assemblyCompressor = null;

	public override bool RunTask ()
	{
		if (EnableCompression) {
			assemblyCompressor = new AssemblyCompression (Log, CompressedAssembliesOutputDirectory);
			Log.LogDebugMessage ("Assembly compression ENABLED");
		} else {
			Log.LogDebugMessage ("Assembly compression DISABLED");
		}

		Generate ();
		return !Log.HasLoggedErrors;
	}

	void Generate ()
	{
		var assemblies = new Dictionary<AndroidTargetArch, List<BlobAssemblyInfo>> ();
		var abis = new HashSet<string> (StringComparer.Ordinal);

		foreach (ITaskItem assembly in Assemblies) {
			string abi = GetRequiredMetadata (assembly, DSOMetadata.Abi);
			AndroidTargetArch arch = MonoAndroidHelper.AbiToTargetArch (abi);
			if (!assemblies.TryGetValue (arch, out List<BlobAssemblyInfo> archAssemblies)) {
				archAssemblies = new List<BlobAssemblyInfo> ();
				assemblies.Add (arch, archAssemblies);
			}
			abis.Add (abi.ToLowerInvariant ());

			var info = new BlobAssemblyInfo (assembly);
			archAssemblies.Add (info);

			string configFilePath = $"{assembly.ItemSpec}.config";
			if (File.Exists (configFilePath)) {
				info.Config = File.ReadAllText (configFilePath);
			}

			string inputAssembly;
			if (!ShouldSkipCompression (assembly)) {
				// TODO: compress
				inputAssembly = "[TODO]";
				info.IsCompressed = true;
				info.Size = GetFileSize (assembly.ItemSpec);
				info.SizeInBlob = GetFileSize (inputAssembly);
			} else {
				inputAssembly = assembly.ItemSpec;
				info.Size = info.SizeInBlob = GetFileSize (inputAssembly);

			}
			assembly.SetMetadata (DSOMetadata.InputAssemblyPath, inputAssembly);
		}

		foreach (ITaskItem blobDSO in AssemblyBlobDSOs) {
			AndroidTargetArch arch = MonoAndroidHelper.AbiToTargetArch (GetRequiredMetadata (blobDSO, DSOMetadata.Abi));
			Generate (arch, blobDSO, assemblies[arch]);
		}

		var generator = new AssemblyBlobDSOGenerator (assemblies);
		LLVMIR.LlvmIrModule module = generator.Construct ();

		foreach (string abi in abis) {
			string outputAsmFilePath = Path.Combine (SourcesOutputDirectory, MonoAndroidHelper.MakeNativeAssemblyFileName (PrepareAbiItems.AssemblyDSOBase, abi));

                        using var sw = MemoryStreamPool.Shared.CreateStreamWriter ();
                        AndroidTargetArch targetArch = MonoAndroidHelper.AbiToTargetArch (abi);
                        try {
	                        generator.Generate (module, targetArch, sw, outputAsmFilePath);
                        } catch {
	                        throw;
                        } finally {
	                        sw.Flush ();
                        }

                        if (Files.CopyIfStreamChanged (sw.BaseStream, outputAsmFilePath)) {
	                        Log.LogDebugMessage ($"File {outputAsmFilePath} was (re)generated");
                        }
		}
	}

	ulong GetFileSize (string path)
	{
		var fi = new FileInfo (path);
		return (ulong)fi.Length;
	}

	void Generate (AndroidTargetArch arch, ITaskItem blobDSO, List<BlobAssemblyInfo> assemblies)
	{
		string stubPath = GetRequiredMetadata (blobDSO, DSOMetadata.BlobStubPath);
		var stubInfo = new FileInfo (stubPath);
		long padding = MaximumStubSize - stubInfo.Length;

		// If we have a fixed stub size, in native code we can simply use a constant and thus generate faster code.  Currently, none of the stubs exceeds the size of 2.5kb
		if (padding < 0) {
			throw new InvalidOperationException ($"Internal error: stub '{stubPath}' is too big. Maximum supported size is {MaximumStubSize}b, stub is however {stubInfo.Length}b");
		}

		string outputFile = blobDSO.ItemSpec;
		string? outputDir = Path.GetDirectoryName (outputFile);

		if (!String.IsNullOrEmpty (outputDir)) {
			Directory.CreateDirectory (outputDir);
		}

		// File.Copy (stubPath, outputFile);
		// using var stubFS = File.Open (outputFile, FileMode.Open, FileAccess.Write, FileShare.Read);
		// if (padding > 0) {
		// 	long newLength = stubInfo.Length + padding;
		// 	stubFS.SetLength (newLength);
		// }
		// stubFS.Seek (0, SeekOrigin.End);
		using var stubFS = File.Open (outputFile, FileMode.Create, FileAccess.Write, FileShare.Read);
		foreach (BlobAssemblyInfo info in assemblies) {
			string inputFile = GetRequiredMetadata (info.Item, DSOMetadata.InputAssemblyPath);
			info.OffsetInBlob = (ulong)stubFS.Position;

			using var fs = File.Open (inputFile, FileMode.Open, FileAccess.Read, FileShare.Read);
			fs.CopyTo (stubFS);
		}

		stubFS.Flush ();
	}

	static string GetRequiredMetadata (ITaskItem item, string name)
	{
		string ret = item.GetMetadata (name);
		if (String.IsNullOrEmpty (ret)) {
			throw new InvalidOperationException ($"Internal error: item {item} doesn't contain required metadata item '{name}' or its value is an empty string");
		}

		return ret;
	}

	bool ShouldSkipCompression (ITaskItem item)
	{
		if (assemblyCompressor == null) {
			return true;
		}

		string val = item.GetMetadata (DSOMetadata.AndroidSkipCompression);
		if (String.IsNullOrEmpty (val)) {
			return false;
		}

		if (!Boolean.TryParse (val, out bool skipCompression)) {
			throw new InvalidOperationException ($"Internal error: unable to parse '{val}' as a boolean value, in item '{item.ItemSpec}', from the '{DSOMetadata.AndroidSkipCompression}' metadata");
		}

		return skipCompression;
	}
}
