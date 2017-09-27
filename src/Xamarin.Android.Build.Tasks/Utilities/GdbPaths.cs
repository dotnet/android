using System;
using System.IO;

namespace Xamarin.Android.Tools
{
	public enum AndroidDebugServer
	{
		/// GNU's GDB debug server (provided by Android NDK)
		Gdb,
		/// Facebook's Ds2 (https://github.com/facebook/ds2)
		Ds2,
		/// LLDB's debug server (lldb-gdbserver)
		Llgs
	}
		
	public class GdbPaths
	{
		public static AndroidDebugServer? GetAndroidDebugServer (string name)
		{
			switch ((name ?? string.Empty).ToLowerInvariant().Trim ())
			{
			case "gdb":
				return AndroidDebugServer.Gdb;
			case "ds2":
				return AndroidDebugServer.Ds2;
			case "llgs":
				return AndroidDebugServer.Llgs;
			}

			return null;
		}

		public static string GetDebugServerPath (AndroidDebugServer server, AndroidTargetArch arch,
			string AndroidNdkDirectory, string SdkBinDirectory)
		{
			switch (server)
			{
			case AndroidDebugServer.Gdb:
				var abi  = GetAbiFromArch (arch);
				return Path.Combine (AndroidNdkDirectory, "prebuilt", abi, "gdbserver", "gdbserver");
			case AndroidDebugServer.Ds2:
				return Path.Combine (SdkBinDirectory, "ds2-" + arch.ToString ().ToLowerInvariant ());
			case AndroidDebugServer.Llgs:
				throw new NotImplementedException ();
			}

			return null;
		}

		public static string GetDebugServerFileName (AndroidDebugServer server)
		{
			switch (server)
			{
			case AndroidDebugServer.Gdb:
				return "libgdbserver.so";
			case AndroidDebugServer.Ds2:
				return "libds2.so";
			case AndroidDebugServer.Llgs:
				throw new NotImplementedException ();
			}

			return null;
		}

		public static AndroidTargetArch GetArchFromAbi (string abi)
		{
			if (abi.StartsWith ("arm64", StringComparison.OrdinalIgnoreCase) ||
				abi.StartsWith ("aarch64", StringComparison.OrdinalIgnoreCase))
				return AndroidTargetArch.Arm64;
			if (abi.StartsWith ("arm", StringComparison.OrdinalIgnoreCase))
				return AndroidTargetArch.Arm;
			if (abi.StartsWith ("mips", StringComparison.OrdinalIgnoreCase))
				return AndroidTargetArch.Mips;
			if (abi.StartsWith ("x86_64", StringComparison.OrdinalIgnoreCase))
				return AndroidTargetArch.X86_64;
			if (abi.StartsWith ("x86", StringComparison.OrdinalIgnoreCase))
				return AndroidTargetArch.X86;
			return AndroidTargetArch.None;
		}

		public static string GetAbiFromArch (AndroidTargetArch arch)
		{
			switch (arch) {
			case AndroidTargetArch.Arm:   return "android-arm";
			case AndroidTargetArch.Arm64: return "android-arm64";
			case AndroidTargetArch.Mips:  return "android-mips";
			case AndroidTargetArch.X86:   return "android-x86";
			case AndroidTargetArch.X86_64: return "android-x86_64";
			}
			return null;
		}
	}
}

