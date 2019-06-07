using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_BuildLibZip : Step
	{
		static readonly Regex libZipDylib = new Regex ("^.*/libzip.\\d+\\.\\d+\\.dylib$");

		public Step_BuildLibZip ()
			: base ("Installing the LibZip library")
		{}

#pragma warning disable CS1998
		protected override async Task<bool> Execute (Context context)
		{
			var runner = new BrewRunner (context);
			if (!runner.List ("libzip", out List<string> lines) || lines == null || lines.Count == 0) {
				Log.ErrorLine ("Failed to retrieve libzip package contents");
				return false;
			}

			string libZipPath = null;
			foreach (string line in lines) {
				Match match = libZipDylib.Match (line);
				if (!match.Success)
					continue;
				libZipPath = line;
				break;
			}

			if (String.IsNullOrEmpty (libZipPath)) {
				Log.ErrorLine ("`libzip` package does not contain the dynamic library");
				return false;
			}

			if (!File.Exists (libZipPath)) {
				Log.ErrorLine ($"`libzip` package lists the dynamic library at {libZipPath} but the file does not exist");
				return false;
			}

			Log.DebugLine ($"`libzip` library found at {libZipPath}");
			string destFile = Path.Combine (Configurables.Paths.InstallMSBuildDir, Path.GetFileName (libZipPath));
			Log.Status ("Installing ");
			Log.Status (Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, destFile), ConsoleColor.White);
			Log.StatusLine ($" {context.Characters.LeftArrow} ", libZipPath, leadColor: ConsoleColor.Cyan, tailColor: ConsoleColor.White);

			Utilities.CopyFile (libZipPath, destFile);

			if (!File.Exists (destFile)) {
				Log.ErrorLine ("Failed to copy the libzip dynamic library.");
				return false;
			}

			return true;
		}
#pragma warning restore CS1998
	}
}
