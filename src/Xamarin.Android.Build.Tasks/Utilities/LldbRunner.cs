using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	// NOTE: export TERMINFO=path/to/terminfo on Unix, because lldb bundled with the NDK is unable to find it on its own.
	//  It searches for the database at "/buildbot/src/android/llvm-toolchain/out/lib/libncurses-linux-install/share/terminfo"
	class LldbRunner : ToolRunner
	{
		bool needPythonEnvvars;

		public LldbRunner (TaskLoggingHelper logger, string lldbPath)
			: base (logger, lldbPath)
		{
			// If we're invoking the executable directly, we need to set up Python environment variables or lldb won't run
			string ext = Path.GetExtension (lldbPath);
			if (String.Compare (".sh", ext, StringComparison.OrdinalIgnoreCase) == 0 ||
			    String.Compare (".cmd", ext, StringComparison.OrdinalIgnoreCase) == 0 ||
			    String.Compare (".bat", ext, StringComparison.OrdinalIgnoreCase) == 0) {
				needPythonEnvvars = false;
			} else {
				needPythonEnvvars = true;
			}
		}
	}
}
