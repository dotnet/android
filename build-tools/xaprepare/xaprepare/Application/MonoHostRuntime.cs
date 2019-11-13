using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class MonoHostRuntime : MonoRuntime
	{
		public override string Flavor => "host";
		public bool MinGW { get; }

		public MonoHostRuntime (string name, bool mingw, Func<Context, bool> enabledCheck)
			: base (name, enabledCheck)
		{
			MinGW = mingw;
			CanStripNativeLibrary = true;
			MonoSdksPrefix = "host-";
		}

		public override void Init (Context context)
		{
			if (MinGW) {
				NativeLibraryExtension    = Configurables.Defaults.MonoHostMingwRuntimeNativeLibraryExtension;
				NativeLibraryDirPrefix    = Configurables.Paths.MonoRuntimeHostMingwNativeLibraryPrefix;
			} else
				NativeLibraryExtension    = Configurables.Defaults.NativeLibraryExtension;

			if (Context.IsNativeHostAbi (Name)) {
				OutputAotProfilerFilename = Configurables.Defaults.MonoRuntimeOutputAotProfilerFilename;
				OutputProfilerFilename    = Configurables.Defaults.MonoRuntimeOutputProfilerFilename;
			} else {
				OutputAotProfilerFilename = String.Empty;
				OutputProfilerFilename    = String.Empty;
			}
			OutputMonoBtlsFilename        = String.Empty;
			OutputMonoPosixHelperFilename = Configurables.Defaults.MonoRuntimeOutputMonoPosixHelperFilename;

			if (Context.IsMingwHostAbi (Name)) {
				string prefix;
				if (Context.Is32BitMingwHostAbi (Name)) {
					prefix = Context.Properties.GetRequiredValue (KnownProperties.MingwCommandPrefix32);
				} else if (Context.Is64BitMingwHostAbi (Name)) {
					prefix = Context.Properties.GetRequiredValue (KnownProperties.MingwCommandPrefix64);
				} else
					throw new InvalidOperationException($"MinGW host ABI {Name} is neither 32 nor 64-bit (?!)");
				Strip = Path.Combine (Configurables.Paths.MingwBinDir, $"{prefix}-strip");
			} else {
				Strip = "strip";
				StripFlags = "-S";
			}

			InitOS ();
		}

		partial void InitOS ();
	}
}
