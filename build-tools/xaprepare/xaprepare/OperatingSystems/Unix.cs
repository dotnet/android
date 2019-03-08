using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Mono.Unix.Native;

namespace Xamarin.Android.Prepare
{
	abstract partial class Unix : OS
	{
		const FilePermissions ExecutableBits = FilePermissions.S_IXUSR | FilePermissions.S_IXGRP | FilePermissions.S_IXOTH;

		protected override List<string> ExecutableExtensions => null;
		public override bool IsWindows => false;
		public override bool IsUnix => true;
		public override string HomeDirectory => GetHomeDir ();

		protected Unix (Context context) : base (context)
		{
			Architecture = Utilities.GetStringFromStdout ("uname", "-m")?.Trim ();
		}

		protected override bool InitOS ()
		{
			JavaHome = Context.Instance.Properties.GetValue (KnownProperties.JavaSdkDirectory);
			if (String.IsNullOrEmpty (JavaHome))
				JavaHome = Environment.GetEnvironmentVariable ("JAVA_HOME") ?? String.Empty;

			return true;
		}

		protected override void DetectCompilers ()
		{
			string ccVersion = Utilities.GetStringFromStdout (Configurables.Defaults.DefaultCompiler, "--version");
			if (String.IsNullOrEmpty (ccVersion))
				throw new InvalidOperationException ($"Failed to obtain version information from the {Configurables.Defaults.DefaultCompiler} compiler");

			Log.DebugLine ($"Default compiler {Configurables.Defaults.DefaultCompiler} identifies as:");
			Log.DebugLine (ccVersion);

			bool clang = false;
			if (ccVersion.IndexOf ("Free Software Foundation", StringComparison.OrdinalIgnoreCase) >= 0) {
				CC = "gcc";
				CXX = "g++";
			} else if (ccVersion.IndexOf ("clang", StringComparison.OrdinalIgnoreCase) >= 0) {
				CC = "clang";
				CXX = "clang++";
				clang = true;
			} else {
				CC = "cc";
				CXX = "c++";
			}

			VerifyCompilersExist ();

			Triple = Utilities.GetStringFromStdout (CC, "-dumpmachine");
			if (String.IsNullOrEmpty (Triple))
				throw new InvalidOperationException ($"Failed to determine default target triple for compiler {CC}");

			if (Is64Bit) {
				CC64 = CC;
				CXX64 = CXX;
				CC32 = $"{CC} -m32";
				CXX32 = $"{CXX} -m32";

				if (clang)
					Triple32 = Utilities.GetStringFromStdout(CC, "-m32", "-dumpmachine");
				else {
					if (!Triple.StartsWith ("x86_64-", StringComparison.OrdinalIgnoreCase))
						throw new InvalidOperationException ($"Unexpected 64-bit triple '{Triple}'");

					Triple32 = Triple.Replace ("x86_64-", "i686-");
				}

				Triple64 = Triple;
			} else {
				CC64 = null;
				CXX64 = null;
				CC32 = CC;
				CXX32 =CXX;
				Triple32 = Triple;
				Triple64 = null;
			}

			LogCompilerDetails ();
		}

		protected override string AssertIsExecutable (string fullPath)
		{
			IsExecutable (fullPath, true);
			return fullPath;
		}

		public override string GetManagedProgramRunner (string programPath)
		{
			if (String.IsNullOrEmpty (programPath))
				return null;

			if (programPath.EndsWith (".exe", StringComparison.OrdinalIgnoreCase) || programPath.EndsWith (".dll", StringComparison.OrdinalIgnoreCase))
				return "mono"; // Caller will find the exact mono executable, we just provide a name

			return null;
		}

		protected static bool IsExecutable (string fullPath, bool throwOnErrors = false)
		{
			Stat sbuf;
			int ret = Syscall.stat (fullPath, out sbuf);

			if (ret < 0) {
				if (throwOnErrors)
					throw new InvalidOperationException ($"Failed to stat file '{fullPath}': {Stdlib.strerror (Stdlib.GetLastError ())}");
				return false;
			}

			if ((sbuf.st_mode & ExecutableBits) == 0) {
				if (throwOnErrors)
					throw new InvalidOperationException ($"File '{fullPath}' is not executable");
				return false;
			}

			return true;
		}

		protected override void PopulateEnvironmentVariables ()
		{
			base.PopulateEnvironmentVariables ();
			EnvironmentVariables ["NO_SUDO"] = Context.AutoProvisionUsesSudo ? "false" : "true";

			List<string> monoOptions = Context.MonoOptions;
			if (monoOptions != null && monoOptions.Count > 0)
				EnvironmentVariables ["MONO_OPTIONS" ] = String.Join (" ", monoOptions);
		}

		[MethodImplAttribute (MethodImplOptions.InternalCall)]
		internal extern static string internalGetHome ();
		string GetHomeDir ()
		{
			return internalGetHome ();
		}
	}
}
