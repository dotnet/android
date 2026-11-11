#nullable enable

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Copies each native library into Output, setting %(Abi) to its resolved ABI, or to an empty string if unresolved.
	/// </summary>
	[MSBuildMultiThreadableTask]
	public class GetNativeLibraryAbis : MultithreadedAndroidTask
	{
		public override string TaskPrefix => "GNLA";

		public ITaskItem []? NativeLibraries { get; set; }

		[Output]
		public ITaskItem []? Output { get; set; }

		public override bool RunTask ()
		{
			var libraries = NativeLibraries ?? [];
			Output = new ITaskItem [libraries.Length];
			for (int i = 0; i < libraries.Length; i++) {
				var output = new TaskItem (libraries [i]);
				output.SetMetadata ("Abi", AndroidRidAbiHelper.GetNativeLibraryAbi (libraries [i], TaskEnvironment) ?? "");
				Output [i] = output;
			}
			return true;
		}
	}
}
