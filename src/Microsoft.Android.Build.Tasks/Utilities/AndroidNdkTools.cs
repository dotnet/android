using System;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Properties = Xamarin.Android.Tasks.Properties;

namespace Microsoft.Android.Tasks;

sealed class AndroidNdkTools
{
	readonly TaskLoggingHelper log;

	public string ToolchainDirectory { get; }
	public string BinDirectory => Path.Combine (ToolchainDirectory, "bin");
	public static string HostTag => OperatingSystem.IsWindows () ? "windows-x86_64" :
		OperatingSystem.IsMacOS () ? "darwin-x86_64" : "linux-x86_64";

	AndroidNdkTools (string directory, TaskLoggingHelper log)
	{
		ToolchainDirectory = directory;
		this.log = log;
	}

	public static AndroidNdkTools? Create (string? ndkDirectory, TaskLoggingHelper log)
	{
		ArgumentNullException.ThrowIfNull (log);
		if (string.IsNullOrEmpty (ndkDirectory) || !File.Exists (Path.Combine (ndkDirectory, "source.properties"))) {
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
		string path = Path.Combine (BinDirectory, name + (OperatingSystem.IsWindows () ? ".exe" : ""));
		if (File.Exists (path)) {
			return path;
		}
		log.LogCodedError ("XA5105", Properties.Resources.XA5105, name, HostTag, BinDirectory);
		return null;
	}

	public string? GetClangDeviceLibraryPath ()
	{
		string root = Path.Combine (ToolchainDirectory, "lib", "clang");
		if (Directory.Exists (root)) {
			foreach (string directory in Directory.EnumerateDirectories (root).OrderByDescending (GetVersion)) {
				string path = Path.Combine (directory, "lib", "linux");
				if (Directory.Exists (path)) {
					return path;
				}
			}
		}
		log.LogCodedError ("XA5101", Properties.Resources.XA5101, root);
		return null;

		static Version GetVersion (string directory)
		{
			string name = Path.GetFileName (directory);
			return Version.TryParse (name.Contains ('.') ? name : name + ".0", out var version) ? version : new Version ();
		}
	}
}
