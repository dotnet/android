using System;

namespace Xamarin.Android.Prepare
{
	partial class LlvmRuntime : Runtime
	{
		public override string Flavor => "LLVM";
		public bool InstallBinaries { get; protected set; }

		public LlvmRuntime (string name, Func<Context, bool> enabledCheck)
			: base (name, enabledCheck)
		{}

		public override void Init (Context context)
		{
			InstallBinaries = String.Compare (Name, AbiNames.Llvm.Windows64Bit, StringComparison.Ordinal) != 0;
			if (Context.IsLlvmWindowsAbi (Name)) {
				ExeSuffix = Configurables.Defaults.WindowsExecutableSuffix;
				InstallPath = Configurables.Paths.InstallMSBuildDir;
			} else
				InstallPath = OSInstallPath;
		}
	}
}
