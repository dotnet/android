using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	class MonoJitRuntime : MonoRuntime
	{
		public override string Flavor => "Android JIT";

		public MonoJitRuntime (string abiName, Func<Context, bool> enabledCheck)
			: base (abiName, enabledCheck)
		{}

		public override void Init (Context context)
		{
			InstallPath                   = "lib";
			NativeLibraryExtension        = Configurables.Defaults.MonoJitRuntimeNativeLibraryExtension;
			OutputAotProfilerFilename     = Configurables.Defaults.MonoRuntimeOutputAotProfilerFilename;
			OutputMonoBtlsFilename        = Configurables.Defaults.MonoRuntimeOutputMonoBtlsFilename;
			OutputMonoPosixHelperFilename = Configurables.Defaults.MonoRuntimeOutputMonoPosixHelperFilename;
			OutputProfilerFilename        = Configurables.Defaults.MonoRuntimeOutputProfilerFilename;
			Strip                         = Path.Combine (Configurables.Paths.AndroidToolchainBinDirectory, $"{Configurables.Defaults.AndroidToolchainPrefixes [Name]}-strip");
		}
	}
}
