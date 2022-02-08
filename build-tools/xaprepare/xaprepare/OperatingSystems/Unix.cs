using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Mono.Unix.Native;

namespace Xamarin.Android.Prepare
{
	abstract partial class Unix : OS
	{
		const FilePermissions ExecutableBits = FilePermissions.S_IXUSR | FilePermissions.S_IXGRP | FilePermissions.S_IXOTH;

		protected override List<string>? ExecutableExtensions => null;
		public override bool IsWindows => false;
		public override bool IsUnix => true;
		public override string HomeDirectory => GetHomeDir ();

		protected Unix (Context context) : base (context)
		{
			Architecture = Utilities.GetStringFromStdout ("uname", "-m").Trim ();
			DiskInformation =  Utilities.GetStringFromStdout ("df", "-h").Trim ();
		}

		string GetCompiler (string fromEnv, string defaultName)
		{
			if (!String.IsNullOrEmpty (fromEnv))
				return fromEnv;
			return defaultName;
		}

		protected override void DetectCompilers ()
		{
			string cc = Environment.GetEnvironmentVariable ("CC")?.Trim () ?? String.Empty;
			string cxx = Environment.GetEnvironmentVariable ("CXX")?.Trim () ?? String.Empty;
			string defaultCompiler = String.Empty;

			if (!String.IsNullOrEmpty (cc)) {
				Log.DebugLine ($"Unix C compiler read from environment variable CC: {cc}");
				defaultCompiler = cc;
			}

			if (!String.IsNullOrEmpty (cxx)) {
				Log.DebugLine ($"Unix C++ compiler read from environment variable CXX: {cxx}");
				if (String.IsNullOrEmpty (defaultCompiler))
					defaultCompiler = cxx;
			}

			if (String.IsNullOrEmpty (defaultCompiler)) {
				defaultCompiler = Configurables.Defaults.DefaultCompiler;
				if (String.IsNullOrEmpty (defaultCompiler))
					throw new InvalidOperationException ("Default compiler not specified");
			}

			string ccVersion = Utilities.GetStringFromStdout (defaultCompiler, "--version");
			if (String.IsNullOrEmpty (ccVersion))
				throw new InvalidOperationException ($"Failed to obtain version information from the {defaultCompiler} compiler");

			Log.DebugLine ($"Compiler {defaultCompiler} identifies as:");
			Log.DebugLine (ccVersion);

			bool clang = false;
			if (ccVersion.IndexOf ("Free Software Foundation", StringComparison.OrdinalIgnoreCase) >= 0) {
				CC = GetCompiler (cc, "gcc");
				CXX = GetCompiler (cxx, "g++");
			} else if (ccVersion.IndexOf ("clang", StringComparison.OrdinalIgnoreCase) >= 0) {
				CC = GetCompiler (cc, "clang");
				CXX = GetCompiler (cxx, "clang++");
				clang = true;
			} else {
				CC = GetCompiler (cc, "cc");
				CXX = GetCompiler (cxx, "c++");
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
				CC64 = String.Empty;
				CXX64 = String.Empty;
				CC32 = CC;
				CXX32 =CXX;
				Triple32 = Triple;
				Triple64 = String.Empty;
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
				return String.Empty;

			if (programPath.EndsWith (".exe", StringComparison.OrdinalIgnoreCase) || programPath.EndsWith (".dll", StringComparison.OrdinalIgnoreCase))
				return "mono"; // Caller will find the exact mono executable, we just provide a name

			return String.Empty;
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
