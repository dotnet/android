using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Prepare
{
	partial class Runtimes
	{
		static Context ctx => Context.Instance;

		static string GetMonoUtilitySourcePath (string utilityName)
		{
			return Path.Combine (Configurables.Paths.MonoProfileToolsDir, utilityName);
		}

		static string GetLlvmOutputSourcePath (Runtime runtime)
		{
			var llvmRuntime = EnsureRuntimeType<LlvmRuntime> (runtime, "LLVM");
			return Path.Combine (GetLlvmInputDir (runtime), "bin");
		}

		static string GetLlvmOutputDestinationPath (Runtime runtime)
		{
			var llvmRuntime = EnsureRuntimeType<LlvmRuntime> (runtime, "LLVM");
			return llvmRuntime.InstallPath;
		}

		static string GetMonoPosixHelperOutputSourcePath (Runtime runtime)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetAndroidInputLibDir (runtime), monoRuntime.NativeLibraryDirPrefix, $"{monoRuntime.OutputMonoPosixHelperFilename}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetMonoPosixHelperOutputDestinationPath (Runtime runtime, bool debug)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetRuntimeOutputDir (runtime), $"{monoRuntime.OutputMonoPosixHelperFilename}{GetDebugInfix (debug)}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetMonoBtlsOutputSourcePath (Runtime runtime)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetAndroidInputLibDir (runtime), monoRuntime.NativeLibraryDirPrefix, $"{monoRuntime.OutputMonoBtlsFilename}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetMonoBtlsOutputDestinationPath (Runtime runtime, bool debug)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetRuntimeOutputDir (runtime), $"{monoRuntime.OutputMonoBtlsFilename}{GetDebugInfix (debug)}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetAotProfilerOutputSourcePath (Runtime runtime)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetAndroidInputLibDir (runtime), monoRuntime.NativeLibraryDirPrefix, $"{monoRuntime.OutputAotProfilerFilename}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetAotProfilerOutputDestinationPath (Runtime runtime, bool debug)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetRuntimeOutputDir (runtime), $"{monoRuntime.OutputAotProfilerFilename}{GetDebugInfix (debug)}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetProfilerOutputSourcePath (Runtime runtime)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetAndroidInputLibDir (runtime), monoRuntime.NativeLibraryDirPrefix, $"{monoRuntime.OutputProfilerFilename}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetProfilerOutputDestinationPath (Runtime runtime, bool debug)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetRuntimeOutputDir (runtime), $"{monoRuntime.OutputProfilerFilename}{GetDebugInfix (debug)}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetRuntimeOutputSourcePath (Runtime runtime)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetAndroidInputLibDir (runtime), monoRuntime.NativeLibraryDirPrefix, $"{monoRuntime.OutputRuntimeFilename}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetRuntimeOutputDestinationPath (Runtime runtime, bool debug)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetRuntimeOutputDir (runtime), $"{monoRuntime.OutputRuntimeFilename}{GetDebugInfix (debug)}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetMonoNativeOutputSourcePath (Runtime runtime)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			if (IsAbi (runtime, AbiNames.HostJit.Darwin))
				return Path.Combine (GetAndroidInputLibDir (runtime), monoRuntime.NativeLibraryDirPrefix, $"libmono-native-compat{monoRuntime.NativeLibraryExtension}");

			return Path.Combine (GetAndroidInputLibDir (runtime), monoRuntime.NativeLibraryDirPrefix, $"libmono-native{monoRuntime.NativeLibraryExtension}");
		}

		static string GetMonoNativeOutputDestinationPath (Runtime runtime, bool debug)
		{
			var monoRuntime = EnsureRuntimeType<MonoRuntime> (runtime, "Mono");
			return Path.Combine (GetRuntimeOutputDir (runtime), $"libmono-native{GetDebugInfix (debug)}{monoRuntime.NativeLibraryExtension}");
		}

		static string GetDebugInfix (bool debug)
		{
			return debug ? Configurables.Defaults.DebugBinaryInfix : String.Empty;
		}

		static bool IsHostOrTargetRuntime (Runtime runtime)
		{
			return IsRuntimeType<MonoJitRuntime> (runtime);
		}

		static T EnsureRuntimeType<T> (Runtime runtime, string typeName) where T: Runtime
		{
			var ret = runtime.As<T> ();
			if (ret == null)
				throw new InvalidOperationException ($"Runtime {runtime.Name} is not a {typeName} runtime");

			return ret;
		}

		static bool IsRuntimeType <T> (Runtime runtime) where T: Runtime
		{
			return runtime.As<T>() != null;
		}

		static bool IsWindowsRuntime (Runtime runtime)
		{
			return String.Compare (runtime.ExeSuffix, Configurables.Defaults.WindowsExecutableSuffix, StringComparison.Ordinal) == 0;
		}

		static bool IsAbi (Runtime runtime, string abiName, params string[] furtherAbiNames)
		{
			if (ExpectedAbi (abiName))
				return true;

			if (furtherAbiNames == null)
				return false;

			foreach (string a in furtherAbiNames) {
				if (ExpectedAbi (a))
					return true;
			}

			return false;

			bool ExpectedAbi (string abi)
			{
				if (String.IsNullOrEmpty (abi))
					return false;

				return String.Compare (abi, runtime.Name ?? String.Empty, StringComparison.Ordinal) == 0;
			}
		}

		static string GetLlvmInputDir (Runtime runtime)
		{
			return GetLlvmInputRootDir (runtime);
		}

		static string GetLlvmInputRootDir (Runtime runtime)
		{
			return Path.Combine (Configurables.Paths.MonoSDKSRelativeOutputDir, $"llvm-{runtime.PrefixedName}");
		}

		static string GetAndroidInputLibDir (Runtime runtime)
		{
			return Path.Combine (MonoRuntimesHelpers.GetRootDir (runtime), "lib");
		}

		static string GetRuntimeOutputDir (Runtime runtime)
		{
			return Path.Combine (Configurables.Paths.RuntimeInstallRelativeLibDir, runtime.PrefixedName);
		}

		static bool IsLlvmRuntimeEnabled (Context ctx, string llvmAbi)
		{
			bool enabled = false;
			bool windows = ctx.IsLlvmWindowsAbi (llvmAbi);
			bool is64Bit = ctx.Is64BitLlvmAbi (llvmAbi);

			HashSet<string> targets;
			if (windows)
				targets = is64Bit ? AbiNames.All64BitWindowsAotAbis : AbiNames.All32BitWindowsAotAbis;
			else
				targets = is64Bit ? AbiNames.All64BitHostAotAbis : AbiNames.All32BitHostAotAbis;

			foreach (string target in targets) {
				if (Context.Instance.IsTargetAotAbiEnabled (target)) {
					enabled = true;
					break;
				}
			}

			return enabled && (!is64Bit || Context.Instance.OS.Is64Bit);
		}

		public Runtimes ()
		{
			Context c = ctx;
			foreach (Runtime runtime in Items) {
				runtime.Init (c);
			}

			DesignerHostBclFilesToInstall = new List<BclFile> ();
			DesignerWindowsBclFilesToInstall = new List<BclFile> ();

			PopulateDesignerBclFiles (DesignerHostBclFilesToInstall, DesignerWindowsBclFilesToInstall);
		}

		List<BclFile> BclToDesigner (BclFileTarget ignoreForTarget)
		{
			return BclFilesToInstall.Where (bf => ShouldIncludeDesignerBcl (bf)).Select (bf => new BclFile (bf.Name, bf.Type, bf.ExcludeDebugSymbols, version: bf.Version, target: ignoreForTarget)).ToList ();

			bool ShouldIncludeDesignerBcl (BclFile bf)
			{
				if (DesignerIgnoreFiles == null || !DesignerIgnoreFiles.TryGetValue (bf.Name, out (BclFileType Type, BclFileTarget Target) bft)) {
					return true;
				}

				if (bf.Type != bft.Type || bft.Target != ignoreForTarget)
					return true;

				Log.Instance.DebugLine ($"BCL file {bf.Name} will NOT be included in the installed Designer BCL files ({ignoreForTarget})");
				return false;
			}
		}

		partial void PopulateDesignerBclFiles (List<BclFile> designerHostBclFilesToInstall, List<BclFile> designerWindowsBclFilesToInstall);
	}
}
