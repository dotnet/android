using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Registers a state object used by the DSOWrapperGenerator class later on during
/// the build.  This is to avoid having to pass parameters to some tasks (esp. BuildApk)
/// which do not necessarily need those parameters directly.  Registering the state here
/// also avoids having to update monodroid whenever any required parameter is added to
/// BuildApk.
/// </summary>
public class PrepareDSOWrapperState : AndroidTask
{
	public override string TaskPrefix => "PDWS";

	[Required]
	public ITaskItem[] ArchiveDSOStubs     { get; set; }

	[Required]
	public string AndroidBinUtilsDirectory { get; set; }

	[Required]
	public string BaseOutputDirectory      { get; set; }

	public override bool RunTask ()
	{
		var stubPaths = new Dictionary<AndroidTargetArch, ITaskItem> ();

		foreach (ITaskItem stubItem in ArchiveDSOStubs) {
			string rid = stubItem.GetRequiredMetadata ("ArchiveDSOStub", "RuntimeIdentifier", Log);
			AndroidTargetArch arch = MonoAndroidHelper.RidToArch (rid);
			if (stubPaths.ContainsKey (arch)) {
				throw new InvalidOperationException ($"Internal error: duplicate archive DSO stub architecture '{arch}' (RID: '{rid}')");
			}

			if (!File.Exists (stubItem.ItemSpec)) {
				throw new InvalidOperationException ($"Internal error: archive DSO stub file '{stubItem.ItemSpec}' does not exist");
			}

			stubPaths.Add (arch, stubItem);
		}

		var config = new DSOWrapperGenerator.Config (stubPaths, AndroidBinUtilsDirectory, BaseOutputDirectory);
		BuildEngine4.RegisterTaskObjectAssemblyLocal (ProjectSpecificTaskObjectKey (DSOWrapperGenerator.RegisteredConfigKey), config, RegisteredTaskObjectLifetime.Build);

		return !Log.HasLoggedErrors;
	}
}
