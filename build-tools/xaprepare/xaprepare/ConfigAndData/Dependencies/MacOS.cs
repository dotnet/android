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

			new HomebrewProgram ("mingw-w64", new Uri ("https://raw.githubusercontent.com/Homebrew/homebrew-core/c6829c44d27f756b302c8ca76b75edf231d076ee/Formula/mingw-w64.rb")) {
				MinimumVersion = "7.0.0_1",
				MaximumVersion = "7.0.0_2",
				Pin = true,
			},

			new HomebrewProgram ("ninja"),
			new HomebrewProgram ("p7zip", "7za"),
			new HomebrewProgram ("xamarin/xamarin-android-windeps/mingw-zlib", "xamarin/xamarin-android-windeps", null),

			// If you change the minimum Mono version here, please change the URL as well
			new MonoPkgProgram ("Mono", "com.xamarin.mono-MDK.pkg", Configurables.Urls.MonoPackage) {
				MinimumVersion = "6.12.0.15",
				MaximumVersion = "6.99.0.0",
			},
		};

		protected override void InitializeDependencies ()
		{
			Dependencies.AddRange (programs);
		}
	}
}
