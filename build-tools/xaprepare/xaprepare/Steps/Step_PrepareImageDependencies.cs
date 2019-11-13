using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_PrepareImageDependencies : Step
	{
		public Step_PrepareImageDependencies ()
			: base ("Preparing image dependency script")
		{}

#pragma warning disable CS1998
		protected override async Task<bool> Execute (Context context)
		{
			var toolchainDirs = new List<string> {
				Path.GetFileName (context.Properties.GetRequiredValue (KnownProperties.AndroidSdkDirectory)),
				Path.GetFileName (context.Properties.GetRequiredValue (KnownProperties.AndroidNdkDirectory)),
				Path.GetFileName (Configurables.Paths.CorrettoInstallDir),
			};

			var androidToolchain = new AndroidToolchain ();
			var androidPackages = new List <string> ();
			foreach (AndroidToolchainComponent component in androidToolchain.Components) {
				if (component == null)
					continue;

				Uri pkgUrl;
				if (component.RelativeUrl != null)
					pkgUrl = new Uri (AndroidToolchain.AndroidUri, component.RelativeUrl);
				else
					pkgUrl = AndroidToolchain.AndroidUri;
				pkgUrl = new Uri (pkgUrl, $"{component.Name}.zip");
				androidPackages.Add ($"{pkgUrl} {component.DestDir}");
			}

			var brewTaps = new List<string> ();
			var brewPackages = new List<string> ();
			var pkgUrls = new List<string> ();

			GatherMacPackages (brewTaps, brewPackages, pkgUrls);

			var sb = new StringBuilder (File.ReadAllText (Configurables.Paths.PackageImageDependenciesTemplate));
			sb.Replace ("@TOOLCHAIN_DIRS@", MakeLines (toolchainDirs));
			sb.Replace ("@PACKAGES@",       MakeLines (androidPackages));
			sb.Replace ("@BREW_TAPS@",      MakeLines (brewTaps));
			sb.Replace ("@BREWS@",          MakeLines (brewPackages));
			sb.Replace ("@PKG_URLS@",       MakeLines (pkgUrls));

			string outputFile = Configurables.Paths.PackageImageDependenciesOutput;
			Log.StatusLine ($"Generating ", outputFile, tailColor: ConsoleColor.White);
			File.WriteAllText (outputFile, sb.ToString ());

			try {
				MakeExecutable (outputFile);
				return true;
			} catch (InvalidOperationException) {
				return false;
			}

			string MakeLines (List<string> list)
			{
				return String.Join ("\n", list);
			}
		}
#pragma warning restore CS1998

		partial void GatherMacPackages (List<string> brewTaps, List<string> brewPackages, List<string> pkgUrls);
		partial void MakeExecutable (string scriptPath);
	}
}
