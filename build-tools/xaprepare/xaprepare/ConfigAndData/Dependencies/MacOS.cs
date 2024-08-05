using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	partial class MacOS
	{
		static readonly List<Program> programs = new List<Program> {
			new HomebrewProgram ("autoconf"),
			new HomebrewProgram ("automake"),
			new HomebrewProgram ("ccache"),
			new HomebrewProgram ("cmake"),
			new HomebrewProgram ("make"),
			new HomebrewProgram ("ninja"),
			new HomebrewProgram ("p7zip", "7za"),
		};

		static readonly HomebrewProgram git = new HomebrewProgram ("git") {
			MinimumVersion = "2.20.0",
		};

		protected override void InitializeDependencies ()
		{
			Dependencies.AddRange (programs);

			if (Context.Instance.CheckCondition (KnownConditions.AllowMonoUpdate)) {
				Dependencies.Add (
					new MonoPkgProgram ("Mono", "com.xamarin.mono-MDK.pkg", new Uri (Context.Instance.Properties.GetRequiredValue (KnownProperties.MonoDarwinPackageUrl))) {
						MinimumVersion = Context.Instance.Properties.GetRequiredValue (KnownProperties.MonoRequiredMinimumVersion),
						MaximumVersion = Context.Instance.Properties.GetRequiredValue (KnownProperties.MonoRequiredMaximumVersion),
					}
				);
			}

			// Allow using git from $PATH if it has the right version
			(bool success, string bv) = Utilities.GetProgramVersion (git.Name);
			if (success && Version.TryParse (bv, out Version? gitVersion) &&
					Version.TryParse (git.MinimumVersion, out Version? gitMinVersion)) {
				if (gitVersion < gitMinVersion)
					Dependencies.Add (git);

			} else {
				Dependencies.Add (git);
			}
		}
	}
}
