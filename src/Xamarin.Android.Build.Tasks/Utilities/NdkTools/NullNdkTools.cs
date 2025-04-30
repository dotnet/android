using System;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	class NullNdkTools : NdkTools
	{
		public NullNdkTools (TaskLoggingHelper? log = null) : base (new NdkVersion (), log)
		{
			log?.LogDebugMessage ("No Android NDK found");
		}

		public override int GetMinimumApiLevelFor (AndroidTargetArch arch) => throw new NotImplementedException ();

		public override string GetToolPath (NdkToolKind kind, AndroidTargetArch arch, int apiLevel) => throw new NotImplementedException ();

		public override string GetToolPath (string name, AndroidTargetArch arch, int apiLevel) => throw new NotImplementedException ();

		public override bool ValidateNdkPlatform (Action<string> logMessage, Action<string, string> logError, AndroidTargetArch arch, bool enableLLVM) => throw new NotImplementedException ();

		protected override string GetPlatformIncludeDirPath (AndroidTargetArch arch, int apiLevel) => throw new NotImplementedException ();
	}
}
