using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	partial class MonoCrossRuntime : MonoRuntime
	{
		public override string Flavor => "cross compilation";

		public MonoCrossRuntime (string name, Func<Context, bool> enabledCheck)
			: base (name, enabledCheck)
		{}

		public override void Init (Context context)
		{
			if (context.IsHostCrossAotAbi (Name)) {
				InstallPath = context.OS.Type; // Linux | Darwin | Windows
				Strip = "strip";
				StripFlags = "-S";
			} else if (Context.IsWindowsCrossAotAbi (Name)) {
				Strip = Path.Combine (Configurables.Paths.MingwBinDir, Context.Properties.GetRequiredValue (KnownProperties.MingwCommandPrefix64) + "-strip");
				ExeSuffix = Configurables.Defaults.WindowsExecutableSuffix;
			} else
				throw new InvalidOperationException ($"Unsupported cross compiler abi {Name}");

			CrossMonoName = Configurables.Defaults.CrossRuntimeNames [Name];
			ExePrefix = Configurables.Defaults.CrossRuntimeExePrefixes [Name];

			InitOS (context);
		}

		partial void InitOS (Context context);
	}
}
