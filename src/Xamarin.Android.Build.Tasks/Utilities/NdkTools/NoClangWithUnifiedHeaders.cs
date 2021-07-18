using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	// Unified headers
	// No clang
	class NdkToolsNoClangWithUnifiedHeaders : NdkToolsNoClang
	{
		public NdkToolsNoClangWithUnifiedHeaders (string androidNdkPath, NdkVersion version, TaskLoggingHelper? log)
			: base (androidNdkPath, version, log)
		{}

		protected override string GetAsmIncludeDirPath (AndroidTargetArch arch, int apiLevel)
		{
			return UnifiedHeaders_GetAsmIncludeDirPath (arch, apiLevel);
		}

		protected override string GetPlatformIncludeDirPath (AndroidTargetArch arch, int apiLevel)
		{
			return UnifiedHeaders_GetPlatformIncludeDirPath (arch, apiLevel);
		}
	}
}
