using System.IO;

using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	// No unified headers
	// No clang
	class NdkToolsNoClangNoUnifiedHeaders : NdkToolsNoClang
	{
		public NdkToolsNoClangNoUnifiedHeaders (string androidNdkPath, NdkVersion version, TaskLoggingHelper? log)
			: base (androidNdkPath, version, log)
		{}

		protected override string GetPlatformIncludeDirPath (AndroidTargetArch arch, int apiLevel)
		{
			return Path.Combine (NdkRootDirectory, "platforms", $"android-{apiLevel}", $"arch-{GetPlatformArch (arch)}", "usr", "include");
		}
	}
}
