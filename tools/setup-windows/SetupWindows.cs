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

	class SymbolicLink
	{
		public string Source { get; set; }

		public string Target { get; set; }

		public bool IsFile { get; set; }

		public SymbolLinkFlag Flag => IsFile ? SymbolLinkFlag.File : SymbolLinkFlag.Directory;
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
			var prefix        = Path.GetDirectoryName (appDir);
			var hash          = XAZipFolderNameToHash (Path.GetFileName (Path.GetDirectoryName (Path.GetDirectoryName (prefix))));
			var newAssemblies = Path.Combine (prefix, "lib", "xamarin.android", "xbuild-frameworks", "MonoAndroid");

			var links = new List<SymbolicLink> ();
			var progFiles = Environment.GetEnvironmentVariable ("ProgramFiles(x86)");
			var vsInstall = Environment.GetEnvironmentVariable ("VSINSTALLDIR");

			if (string.IsNullOrEmpty (vsInstall)) {
				vsInstall = progFiles;
			} else {
				links.Add (new SymbolicLink {
					Source = Path.Combine (vsInstall, "Common7", "IDE", "ReferenceAssemblies", "Microsoft", "Framework", "MonoAndroid"),
					Target = newAssemblies,
				});
			}

			links.Add (new SymbolicLink {
				Source = Path.Combine (progFiles, "Reference Assemblies", "Microsoft", "Framework", "MonoAndroid"),
				Target = newAssemblies,
			});
			links.Add (new SymbolicLink {
				Source = Path.Combine (vsInstall, "MSBuild", "Xamarin", "Android"),
				Target = Path.Combine (prefix, "lib", "xamarin.android", "xbuild", "Xamarin", "Android"),
			});
			links.Add (new SymbolicLink {
				Source = Path.Combine (vsInstall, "MSBuild", "Xamarin", "Xamarin.Android.Sdk.props"),
				Target = Path.Combine (prefix, "lib", "xamarin.android", "xbuild", "Xamarin", "Xamarin.Android.Sdk.props"),
				IsFile = true,
			});
			links.Add (new SymbolicLink {
				Source = Path.Combine (vsInstall, "MSBuild", "Xamarin", "Xamarin.Android.Sdk.targets"),
				Target = Path.Combine (prefix, "lib", "xamarin.android", "xbuild", "Xamarin", "Xamarin.Android.Sdk.targets"),
				IsFile = true,
			});

			if (Path.DirectorySeparatorChar != '\\') {
				Console.Error.WriteLine ($"{AppName}: This program is for use on Windows.");
				return 1;
			}

			if (args.Length == 0 || args.Any (v => string.Equals (v, "install", StringComparison.OrdinalIgnoreCase) || string.Equals (v, "/install", StringComparison.OrdinalIgnoreCase))) {
				return Install (hash, links);
			}
			if (args.Any (v => string.Equals (v, "uninstall", StringComparison.OrdinalIgnoreCase) || string.Equals (v, "/uninstall", StringComparison.OrdinalIgnoreCase))) {
				return Uninstall (hash, links);
			}
			Console.Error.WriteLine ($"{AppName}: Invalid command `{string.Join (" ", args)}`.");
			return 1;
		}

		static int Install (string hash, List<SymbolicLink> links)
		{
			try {
				foreach (var link in links) {
					var backup = GetNewBackupName (link.Source, hash);
					Directory.CreateDirectory (Path.GetDirectoryName (link.Source));
					if (!CreateSymbolicLink (link, backup)) {
						return 1;
					}
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

		static bool CreateSymbolicLink (SymbolicLink link, string backup)
		{
			if (link.IsFile) {
				Console.WriteLine ($"Executing: MKLINK \"{link.Source}\" \"{link.Target}\"");
				if (File.Exists (link.Source)) {
					File.Move (link.Source, backup);
				}
			} else {
				Console.WriteLine ($"Executing: MKLINK /D \"{link.Source}\" \"{link.Target}\"");
				if (Directory.Exists (link.Source)) {
					Directory.Move (link.Source, backup);
				}
			}
			if (!CreateSymbolicLink (link.Source, link.Target, link.Flag)) {
				var error = new Win32Exception (Marshal.GetLastWin32Error ()).Message;
				Console.Error.WriteLine ($"{AppName}: Unable to create symbolic link from `{link.Source}` to `{link.Target}`: {error}");
				if (link.IsFile) {
					File.Move (backup, link.Source);
				} else {
					Directory.Move (backup, link.Source);
				}
				return false;
			}
			return true;
		}

		static int Uninstall (string hash, List<SymbolicLink> links)
		{
			foreach (var link in links) {
				var backup = GetExistingBackupName (link.Source, hash);
				if (link.IsFile) {
					File.Delete (link.Source);
					if (backup != null && File.Exists (backup)) {
						File.Move (backup, link.Source);
					}
				} else {
					Directory.Delete (link.Source);
					if (backup != null && Directory.Exists (backup)) {
						Directory.Move (backup, link.Source);
					}
				}
			}
			return 0;
		}

		static string GetExistingBackupName (string folder, string hash)
		{
			var prefix = GetBackupNamePrefix (folder, hash);
			var path    = Path.GetDirectoryName (prefix);
			var pattern = Path.GetFileName (prefix) + "*.*";
			return Directory.EnumerateFileSystemEntries (path, pattern, SearchOption.TopDirectoryOnly)
				.FirstOrDefault ();
		}

		[DllImport ("kernel32.dll")]
		[return: MarshalAs (UnmanagedType.I1)]
		static extern bool CreateSymbolicLink (string lpSymlinkFileName, string lpTargetFileName, SymbolLinkFlag dwFlags);
	}
}
