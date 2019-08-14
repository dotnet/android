using System;

namespace Xamarin.Android.Prepare
{
	partial class LlvmRuntime : Runtime
	{
		public override string Flavor => "LLVM";
		public bool InstallBinaries { get; protected set; } = true;

		public LlvmRuntime (string name, Func<Context, bool> enabledCheck)
			: base (name, enabledCheck)
		{}

		public override void Init (Context context)
		{
			if (Context.IsLlvmWindowsAbi (Name)) {
				ExeSuffix = Configurables.Defaults.WindowsExecutableSuffix;
				InstallPath = Configurables.Paths.InstallMSBuildDir;
			} else
				InstallPath = OSInstallPath;
		}
	}
}
