using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks;
using Properties = Xamarin.Android.Tasks.Properties;

namespace Microsoft.Android.Tasks;

sealed class AndroidNdkTools
{
	readonly TaskLoggingHelper log;

	public string ToolchainDirectory { get; }
	public string BinDirectory => Path.Combine (ToolchainDirectory, "bin");
	public static string HostTag => RuntimeInformation.IsOSPlatform (OSPlatform.Windows) ? "windows-x86_64" :
		RuntimeInformation.IsOSPlatform (OSPlatform.OSX) ? "darwin-x86_64" : "linux-x86_64";

	AndroidNdkTools (string directory, TaskLoggingHelper log)
	{
		ToolchainDirectory = directory;
		this.log = log;
	}

	public static AndroidNdkTools? Create (string? ndkDirectory, TaskLoggingHelper log)
	{
		if (log == null) {
			throw new ArgumentNullException (nameof (log));
		}
		if (ndkDirectory.IsNullOrEmpty () || !File.Exists (Path.Combine (ndkDirectory, "source.properties"))) {
			log.LogCodedError ("XA5104", Properties.Resources.XA5104);
			return null;
		}
		string toolchain = Path.Combine (ndkDirectory, "toolchains", "llvm", "prebuilt", HostTag);
		if (!Directory.Exists (Path.Combine (toolchain, "bin"))) {
			log.LogCodedError ("XA5101", Properties.Resources.XA5101, toolchain);
			return null;
		}
		return new AndroidNdkTools (Path.GetFullPath (toolchain), log);
	}

	public string? GetToolPath (string name)
	{
		string path = Path.Combine (BinDirectory, name + (RuntimeInformation.IsOSPlatform (OSPlatform.Windows) ? ".exe" : ""));
		if (File.Exists (path)) {
			return path;
		}
		log.LogCodedError ("XA5105", Properties.Resources.XA5105, name, HostTag, BinDirectory);
		return null;
	}

	public string? GetClangDeviceLibraryPath ()
	{
		// Older Clang NDKs use lib64; current NDKs use lib.
		foreach (string libDirectory in new [] { "lib", "lib64" }) {
			string root = Path.Combine (ToolchainDirectory, libDirectory, "clang");
			if (!Directory.Exists (root)) {
				continue;
			}
			foreach (string directory in Directory.EnumerateDirectories (root)
				.OrderByDescending (GetVersion).ThenByDescending (directory => directory, StringComparer.Ordinal)) {
				string path = Path.Combine (directory, "lib", "linux");
				if (Directory.Exists (path)) {
					return path;
				}
			}
		}
		log.LogCodedError ("XA5101", Properties.Resources.XA5101, Path.Combine (ToolchainDirectory, "lib", "clang"));
		return null;

		static Version GetVersion (string directory)
		{
			string name = Path.GetFileName (directory);
			return Version.TryParse (name.IndexOf ('.') >= 0 ? name : name + ".0", out var version) ? version : new Version ();
		}
	}
}
