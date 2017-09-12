using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Tools
{
	enum SymbolLinkFlag {
		File        = 0,
		Directory   = 1,
	}

	class SetupWindows
	{
		static string AppName;

		public static int Main (string [] args)
		{
			// appPath is expected to be similar to: oss-xamarin.anroid-*/bin/Debug/bin/setup-windows.exe
			var appPath = typeof (SetupWindows).Assembly.Location;
			AppName     = Path.GetFileName (appPath);
			var appDir  = Path.GetDirectoryName (appPath);
			if (Path.GetFileName (appDir) != "bin") {
				Console.Error.WriteLine ($"{AppName}: This program must be run from the `bin` directory.");
				return 1;
			}
			// prefix should be: oss-xamarin.anroid-*/bin/Debug
			var prefix = Path.GetDirectoryName (appDir);
			var hash = XAZipFolderNameToHash (Path.GetFileName (Path.GetDirectoryName (Path.GetDirectoryName (prefix))));

			var refAssembliesDirectories = new List<string> ();
			var progFiles = Environment.GetEnvironmentVariable ("ProgramFiles(x86)");
			var vsInstall = Environment.GetEnvironmentVariable ("VSINSTALLDIR");
			if (string.IsNullOrEmpty (vsInstall)) {
				vsInstall = progFiles;
			} else {
				refAssembliesDirectories.Add (Path.Combine (vsInstall, "Common7", "IDE", "ReferenceAssemblies", "Microsoft", "Framework", "MonoAndroid"));
			}
			refAssembliesDirectories.Add (Path.Combine (progFiles, "Reference Assemblies", "Microsoft", "Framework", "MonoAndroid"));
			
			var msbuildTargets  = Path.Combine (vsInstall, "MSBuild", "Xamarin", "Android");
			var newTargets      = Path.Combine (prefix, "lib", "xamarin.android", "xbuild", "Xamarin", "Android");
			var newAssemblies   = Path.Combine (prefix, "lib", "xamarin.android", "xbuild-frameworks", "MonoAndroid");

			if (Path.DirectorySeparatorChar != '\\') {
				Console.Error.WriteLine ($"{AppName}: This program is for use on Windows.");
				return 1;
			}

			if (args.Length == 0 || args.Any (v => string.Equals (v, "install", StringComparison.OrdinalIgnoreCase) || string.Equals (v, "/install", StringComparison.OrdinalIgnoreCase))) {
				return Install (hash, msbuildTargets, newTargets, refAssembliesDirectories, newAssemblies);
			}
			if (args.Any (v => string.Equals (v, "uninstall", StringComparison.OrdinalIgnoreCase) || string.Equals (v, "/uninstall", StringComparison.OrdinalIgnoreCase))) {
				var directories = new List<string> (refAssembliesDirectories);
				directories.Add (msbuildTargets);
				return Uninstall (hash, directories);
			}
			Console.Error.WriteLine ($"{AppName}: Invalid command `{string.Join (" ", args)}`.");
			return 1;
		}

		static int Install (string hash, string msbuildTargets, string newTargets, List<string> refAssembliesDirectories, string newAssemblies)
		{
			try {
				foreach (var refAssemblies in refAssembliesDirectories) {
					var backupAssemblies = GetNewBackupName (refAssemblies, hash);
					Directory.CreateDirectory (Path.GetDirectoryName (refAssemblies));
					if (!CreateSymbolicLink (refAssemblies, newAssemblies, backupAssemblies))
						return 1;
				}

				var backupTargets = GetNewBackupName (msbuildTargets, hash);
				Directory.CreateDirectory (Path.GetDirectoryName (msbuildTargets));
				if (!CreateSymbolicLink (msbuildTargets, newTargets, backupTargets)) {
					return 1;
				}

				Console.WriteLine ("Success!");
				return 0;
			}
			catch (UnauthorizedAccessException e) {
				Console.Error.WriteLine ($"{AppName}: {e.Message}");
				Console.Error.WriteLine (e.ToString ());
				return 1;
			}
			catch (Exception e) {
				Console.Error.WriteLine ($"{AppName}: {e.Message}");
				Console.Error.WriteLine (e);
				return 1;
			}
		}

		// XAZipFolderName is build-tools/scripts/BuildEverything.mk!$(ZIP_OUTPUT_BASENAME),
		//  oss-xamarin.android_v$(PRODUCT_VERSION).$(-num-commits-since-version-change)_$(OS)-$(OS_ARCH)_$(GIT_BRANCH)_$(GIT_COMMIT)
		static string XAZipFolderNameToHash (string folderName)
		{
			var r   = new Regex (@"^oss-xamarin.android_v(?<version>[^_]+)_(?<os>[^-]+)-(?<arch>[^_]+)_(?<branch>.*)_(?<commit>[A-Za-z0-9]+)$");
			var m   = r.Match (folderName);
			if (!m.Success)
				return "Unknown";
			return m.Groups ["commit"].Value;
		}

		static string GetNewBackupName (string folder, string hash)
		{
			return GetBackupNames (folder, hash).First (d => !Directory.Exists (d));
		}

		static IEnumerable<string> GetBackupNames (string folder, string hash)
		{
			folder  = GetBackupNamePrefix (folder, hash);
			yield return folder;
			int count = 1;
			while (true) {
				yield return $"{folder}+{count}";
				count++;
			}
		}

		static string GetBackupNamePrefix (string folder, string hash)
		{
			return folder + ".pre-" + hash;
		}

		static bool CreateSymbolicLink (string source, string target, string backup)
		{
			Console.WriteLine ($"Executing: MKLINK /D \"{source}\" \"{target}\"");
			if (Directory.Exists (source)) {
				Directory.Move (source, backup);
			}
			if (!CreateSymbolicLink (source, target, SymbolLinkFlag.Directory)) {
				var error = new Win32Exception (Marshal.GetLastWin32Error ()).Message;
				Console.Error.WriteLine ($"{AppName}: Unable to create symbolic link from `{source}` to `{target}`: {error}");
				Directory.Move (backup, source);
				return false;
			}
			return true;
		}

		static int Uninstall (string hash, List<string> directories)
		{
			foreach (var directory in directories) {
				var backup = GetExistingBackupName (directory, hash);
				Directory.Delete (directory);
				if (backup != null && Directory.Exists (backup)) {
					Directory.Move (backup, directory);
				}
			}
			return 0;
		}

		static string GetExistingBackupName (string folder, string hash)
		{
			var prefix = GetBackupNamePrefix (folder, hash);
			var path    = Path.GetDirectoryName (prefix);
			var pattern = Path.GetFileName (prefix) + "*.*";
			return Directory.EnumerateDirectories (path, pattern, SearchOption.TopDirectoryOnly)
				.FirstOrDefault ();
		}

		[DllImport ("kernel32.dll")]
		[return: MarshalAs (UnmanagedType.I1)]
		static extern bool CreateSymbolicLink (string lpSymlinkFileName, string lpTargetFileName, SymbolLinkFlag dwFlags);
	}
}
