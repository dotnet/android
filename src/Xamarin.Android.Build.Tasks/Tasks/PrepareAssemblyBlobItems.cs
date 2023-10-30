using System;
using System.IO;
using System.Collections.Generic;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

public class PrepareAssemblyBlobItems : AndroidTask
{
	// If the name is changed here, it must also be changed in:
	//   * src/monodroid/jni/embedded-assemblies.hh
	//   * src/monodroid/CMakeLists.txt
	//   * build-tools/installers/create-installers.targets
	const string BlobStubName = "AssembLyBloB.so";

	public override string TaskPrefix => "PABI";

	[Required]
	public string AndroidBinUtilsDirectory { get; set; } = "";

	[Required]
	public string[] SupportedABIs          { get; set; }

	[Required]
	public string SharedLibraryOutputPath { get; set; }

	[Output]
	public ITaskItem[] AssemblyBlobDSOs     { get; set; }

	public override bool RunTask ()
	{
		var blobs = new List<ITaskItem> ();
		foreach (string abi in SupportedABIs) {
			string stubPath = Path.Combine (SharedLibraryOutputPath, abi, BlobStubName);
			var blobItem = new TaskItem (stubPath);
			blobItem.SetMetadata (DSOMetadata.Abi, abi);
			blobItem.SetMetadata (DSOMetadata.BlobStubPath, GetStubPath (MonoAndroidHelper.AbiToTargetArch (abi)));
			blobs.Add (blobItem);
		}

		AssemblyBlobDSOs = blobs.ToArray ();
		return !Log.HasLoggedErrors;
	}

	string GetStubPath (AndroidTargetArch arch) => Path.Combine (MonoAndroidHelper.GetLibstubsArchDirectoryPath (AndroidBinUtilsDirectory, arch), $"lib{BlobStubName}");
}
