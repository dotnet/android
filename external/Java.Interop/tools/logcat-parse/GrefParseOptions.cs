using System;

namespace Xamarin.Android.Tools.LogcatParse {

	[Flags]
	public enum GrefParseOptions {
		None,

		LogWarningOnMismatch            = 1 << 0,
		ThrowExceptionOnMismatch        = 1 << 1,

		CheckCounts                     = 1 << 2,
		WarnOnCountMismatch             = CheckCounts | LogWarningOnMismatch,
		ThrowOnCountMismatch            = CheckCounts | ThrowExceptionOnMismatch,

		CheckAlivePeers                 = 1 << 3,
		WarnOnAlivePeerMismatch         = CheckAlivePeers | LogWarningOnMismatch,
		ThrowOnAlivePeerMismatch        = CheckAlivePeers | ThrowExceptionOnMismatch,
	}
}
