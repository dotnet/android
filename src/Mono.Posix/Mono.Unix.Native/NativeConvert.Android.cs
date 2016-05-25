using System;
using System.Runtime.InteropServices;

using Mono.Unix.Android;

namespace Mono.Unix.Native
{
	partial class NativeConvert
	{
		[DllImport (LIB, EntryPoint="Mono_Posix_FromRealTimeSignum")]
		static extern int HelperFromRealTimeSignum (Int32 offset, out Int32 rval);

		static int FromRealTimeSignum (Int32 offset, out Int32 rval)
		{
			if (!AndroidUtils.AreRealTimeSignalsSafe ())
				throw new PlatformNotSupportedException ("Real-time signals are not supported on this Android architecture");
			return HelperFromRealTimeSignum (offset, out rval);
		}
	}
}