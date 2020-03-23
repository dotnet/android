using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	partial class MacOS
	{
		static readonly List<Program> programs = new List<Program> {
			new HomebrewProgram ("autoconf"),
			new HomebrewProgram ("automake"),
			new HomebrewProgram ("cmake"),

			new HomebrewProgram ("git", new Uri("https://raw.githubusercontent.com/Homebrew/homebrew-core/master/Formula/git.rb"), "/usr/local/bin/git") {
				MinimumVersion = "2.20.0",
			},

			new HomebrewProgram ("make"),

			new HomebrewProgram ("mingw-w64", new Uri ("https://raw.githubusercontent.com/Homebrew/homebrew-core/196509755a5afc1115bf39e02314b99d7ed22eb0/Formula/mingw-w64.rb")) {
				MinimumVersion = "7.0.0_2",
				MaximumVersion = "7.0.0_3",
				Pin = true,
			},

			new HomebrewProgram ("ninja"),
			new HomebrewProgram ("p7zip", "7za"),
			new HomebrewProgram ("xamarin/xamarin-android-windeps/mingw-zlib", "xamarin/xamarin-android-windeps", null),

			new MonoPkgProgram ("Mono", "com.xamarin.mono-MDK.pkg", new Uri (Context.Instance.Properties.GetRequiredValue (KnownProperties.MonoDarwinPackageUrl))) {
				MinimumVersion = Context.Instance.Properties.GetRequiredValue (KnownProperties.MonoRequiredMinimumVersion),
				MaximumVersion = Context.Instance.Properties.GetRequiredValue (KnownProperties.MonoRequiredMaximumVersion),
			},
		};

		protected override void InitializeDependencies ()
		{
			Dependencies.AddRange (programs);
		}
	}
}
