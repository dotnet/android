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

			new HomebrewProgram ("libzip", new Uri ("https://raw.githubusercontent.com/Homebrew/homebrew-core/3d9a3eb3a62ed9586cd8f6b3bf506845159f39a4/Formula/libzip.rb")) {
				MinimumVersion = "1.5.2",
				Pin = true,
			},

			new HomebrewProgram ("make"),

			new HomebrewProgram ("mingw-w64", new Uri ("https://raw.githubusercontent.com/Homebrew/homebrew-core/a6542037a48a55061a4c319e6bb174b3715f7cbe/Formula/mingw-w64.rb")) {
				MinimumVersion = "6.0.0_1",
				MaximumVersion = "6.0.0_2",
				Pin = true,
			},

			new HomebrewProgram ("ninja"),
			new HomebrewProgram ("p7zip", "7za"),
			new HomebrewProgram ("xamarin/xamarin-android-windeps/mingw-zlib", "xamarin/xamarin-android-windeps", null),

			// If you change the minimum Mono version here, please change the URL as well
			new MonoPkgProgram ("Mono", "com.xamarin.mono-MDK.pkg", Configurables.Urls.MonoPackage) {
				MinimumVersion = "6.0.0.313",
				MaximumVersion = "6.99.0.0",
			},
		};

		protected override void InitializeDependencies ()
		{
			Dependencies.AddRange (programs);
		}
	}
}
