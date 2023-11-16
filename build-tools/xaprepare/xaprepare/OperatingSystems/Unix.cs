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
