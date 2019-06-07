using System;
using System.IO;

using Mono.Unix.Native;

namespace Xamarin.Android.Prepare
{
	partial class Step_PrepareImageDependencies
	{
		partial void MakeExecutable (string scriptPath)
		{
			if (String.IsNullOrEmpty (scriptPath))
				throw new ArgumentException ("must not be null or empty", nameof (scriptPath));

			if (!File.Exists (scriptPath)) {
				Log.WarningLine ($"Script {scriptPath} does not exist");
				return;
			}

			int ret = Syscall.chmod (scriptPath,
			                         FilePermissions.S_IRGRP | FilePermissions.S_IXGRP |
			                         FilePermissions.S_IROTH | FilePermissions.S_IXOTH |
			                         FilePermissions.S_IRUSR | FilePermissions.S_IXUSR | FilePermissions.S_IWUSR
			);

			if (ret == 0)
				return;

			Log.ErrorLine ($"Failed to make {scriptPath} executable: {Stdlib.strerror (Stdlib.GetLastError ())}");
			throw new InvalidOperationException ("Failed");
		}
	}
}
