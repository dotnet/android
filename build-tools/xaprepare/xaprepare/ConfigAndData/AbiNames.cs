using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	static class AbiNames
	{
		static HashSet <string>? allHostAbis;

		public static class TargetJit
		{
			public const string AndroidArmV7a = "armeabi-v7a";
			public const string AndroidArmV8a = "arm64-v8a";
			public const string AndroidArm64  = AndroidArmV8a;
			public const string AndroidX86    = "x86";
			public const string AndroidX86_64 = "x86_64";
		}

		public static class TargetAot
		{
			public const string ArmV7a        = "armeabi-v7a";
			public const string WinArmV7a     = "win-armeabi-v7a";
			public const string ArmV8a        = "arm64";
			public const string Arm64         = ArmV8a;
			public const string WinArmV8a     = "win-arm64";
			public const string WinArm64      = WinArmV8a;
			public const string X86           = "x86";
			public const string WinX86        = "win-x86";
			public const string X86_64        = "x86_64";
			public const string WinX86_64     = "win-x86_64";
		}

		public static class HostJit
		{
			public const string Linux         = "Linux";
			public const string Darwin        = "Darwin";
			public const string Win32         = "mxe-Win32";
			public const string Win64         = "mxe-Win64";
		}

		public static class CrossAot
		{
			public static readonly string ArmV7a    =  "cross-arm";
			public static readonly string WinArmV7a = $"{ArmV7a}-win";
			public static readonly string ArmV8a    =  "cross-arm64";
			public static readonly string Arm64     =  ArmV8a;
			public static readonly string WinArmV8a = $"{ArmV8a}-win";
			public static readonly string WinArm64  =  WinArmV8a;
			public static readonly string X86       =  "cross-x86";
			public static readonly string WinX86    = $"{X86}-win";
			public static readonly string X86_64    =  "cross-x86_64";
			public static readonly string WinX86_64 = $"{X86_64}-win";
		}

		public static class Llvm
		{
			public const string Host32Bit    = "llvm32";
			public const string Host64Bit    = "llvm64";
			public const string Windows32Bit = "llvmwin32";
			public const string Windows64Bit = "llvmwin64";
		}

		public static HashSet <string> AllHostAbis {
			get {
				if (allHostAbis == null)
					allHostAbis = Abi.GetHostAbis (includeAllHostOSes: false);
				return allHostAbis;
			}
		}

		public static readonly HashSet <string> AllNativeHostAbis      = Abi.GetHostAbis (osType: Abi.OS.NotWindows);

		public static readonly HashSet <string> AllAotAbis             = Abi.GetHostAotAbis ();

		public static readonly HashSet <string> AllLlvmHostAbis        = Abi.GetLlvmAbis ();
		public static readonly HashSet <string> AllLlvmWindowsAbis     = Abi.GetLlvmAbis (osType: Abi.OS.Windows);
		public static readonly HashSet <string> All32BitLlvmAbis       = Abi.GetLlvmAbis (bitness: Abi.Bitness.ThirtyTwo);
		public static readonly HashSet <string> All64BitLlvmAbis       = Abi.GetLlvmAbis (bitness: Abi.Bitness.SixtyFour);

		public static readonly HashSet <string> AllJitAbis             = Abi.GetTargetJitAbis ();
		public static readonly HashSet <string> All32BitTargetJitAbis  = Abi.GetTargetJitAbis (Abi.Bitness.ThirtyTwo);
		public static readonly HashSet <string> All64BitTargetJitAbis  = Abi.GetTargetJitAbis (Abi.Bitness.SixtyFour);

		public static readonly HashSet <string> AllHostAotAbis         = Abi.GetHostAotAbis (osType: Abi.OS.NotWindows);
		public static readonly HashSet <string> All32BitHostAotAbis    = Abi.GetHostAotAbis (osType: Abi.OS.NotWindows, bitness: Abi.Bitness.ThirtyTwo);
		public static readonly HashSet <string> All64BitHostAotAbis    = Abi.GetHostAotAbis (osType: Abi.OS.NotWindows, bitness: Abi.Bitness.SixtyFour);

		public static readonly HashSet <string> AllWindowsAotAbis      = Abi.GetHostAotAbis (osType: Abi.OS.Windows);
		public static readonly HashSet <string> All32BitWindowsAotAbis = Abi.GetHostAotAbis (osType: Abi.OS.Windows, bitness: Abi.Bitness.ThirtyTwo);
		public static readonly HashSet <string> All64BitWindowsAotAbis = Abi.GetHostAotAbis (osType: Abi.OS.Windows, bitness: Abi.Bitness.SixtyFour);

		public static readonly HashSet <string> AllTargetAotAbis       = Abi.GetHostAotAbis ();
		public static readonly HashSet <string> All64BitTargetAotAbis  = Abi.GetHostAotAbis (osType: Abi.OS.Any, bitness: Abi.Bitness.SixtyFour);
		public static readonly HashSet <string> All32BitTargetAotAbis  = Abi.GetHostAotAbis (osType: Abi.OS.Any, bitness: Abi.Bitness.ThirtyTwo);

		public static readonly HashSet <string> AllCrossHostAotAbis    = Abi.GetCrossAbis (osType: Abi.OS.NotWindows);
		public static readonly HashSet <string> AllCrossWindowsAotAbis = Abi.GetCrossAbis (osType: Abi.OS.Windows);
		public static readonly HashSet <string> All64BitCrossAotAbis   = Abi.GetCrossAbis (bitness: Abi.Bitness.SixtyFour);
		public static readonly HashSet <string> All32BitCrossAotAbis   = Abi.GetCrossAbis (bitness: Abi.Bitness.ThirtyTwo);

		public static readonly HashSet <string> AllMingwHostAbis       = Abi.GetHostAbis (osType: Abi.OS.Windows);
		public static readonly HashSet <string> All32BitMingwHostAbis  = Abi.GetHostAbis (osType: Abi.OS.Windows, bitness: Abi.Bitness.ThirtyTwo);
		public static readonly HashSet <string> All64BitMingwHostAbis  = Abi.GetHostAbis (osType: Abi.OS.Windows, bitness: Abi.Bitness.SixtyFour);

		public static void LogAllNames (Context context)
		{
			if (context.LoggingVerbosity < LoggingVerbosity.Verbose)
				return;

			Log.Instance.DebugLine ("All defined ABI names:");

			LogAbis (context, "AllHostAbis",            AllHostAbis);
			LogAbis (context, "AllNativeHostAbis",      AllNativeHostAbis);

			LogAbis (context, "AllLlvmHostAbis",        AllLlvmHostAbis);
			LogAbis (context, "AllLlvmWindowsAbis",     AllLlvmWindowsAbis);
			LogAbis (context, "All32BitLlvmAbis",       All32BitLlvmAbis);
			LogAbis (context, "All64BitLlvmAbis",       All64BitLlvmAbis);

			LogAbis (context, "AllJitAbis",             AllJitAbis);
			LogAbis (context, "All32BitTargetJitAbis",  All32BitTargetJitAbis);
			LogAbis (context, "All64BitTargetJitAbis",  All64BitTargetJitAbis);

			LogAbis (context, "AllHostAotAbis",         AllHostAotAbis);
			LogAbis (context, "All32BitHostAotAbis",    All32BitHostAotAbis);
			LogAbis (context, "All64BitHostAotAbis",    All64BitHostAotAbis);

			LogAbis (context, "AllWindowsAotAbis",      AllWindowsAotAbis);
			LogAbis (context, "All32BitWindowsAotAbis", All32BitWindowsAotAbis);
			LogAbis (context, "All64BitWindowsAotAbis", All64BitWindowsAotAbis);
			LogAbis (context, "All64BitTargetAotAbis",  All64BitTargetAotAbis);
			LogAbis (context, "All32BitTargetAotAbis",  All32BitTargetAotAbis);

			LogAbis (context, "AllCrossHostAotAbis",    AllCrossHostAotAbis);
			LogAbis (context, "AllCrossWindowsAotAbis", AllCrossWindowsAotAbis);
			LogAbis (context, "All64BitCrossAotAbis",   All64BitCrossAotAbis);
			LogAbis (context, "All32BitCrossAotAbis",   All32BitCrossAotAbis);

			LogAbis (context, "AllMingwHostAbis",       AllMingwHostAbis);
			LogAbis (context, "All32BitMingwHostAbis",  All32BitMingwHostAbis);
			LogAbis (context, "All64BitMingwHostAbis",  All64BitMingwHostAbis);
		}

		static void LogAbis (Context context, string name, HashSet<string> abis)
		{
			Log.Instance.DebugLine ($"  {context.Characters.Bullet} {name}");
			foreach (string abi in abis) {
				Log.Instance.DebugLine ($"    {abi}");
			}
			Log.Instance.DebugLine ();
		}

		public static string AbiToRuntimeIdentifier (string androidAbi)
		{
			if (androidAbi == TargetJit.AndroidArmV7a) {
				return "android-arm";
			} else if (androidAbi == TargetJit.AndroidArmV8a) {
				return "android-arm64";
			} else if (androidAbi == TargetJit.AndroidX86) {
				return "android-x86";
			} else if (androidAbi == TargetJit.AndroidX86_64) {
				return "android-x64";
			}
			throw new InvalidOperationException ($"Unknown abi: {androidAbi}");
		}
	}
}
