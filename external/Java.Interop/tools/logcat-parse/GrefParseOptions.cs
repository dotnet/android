using System;

namespace Xamarin.Android.Tools.LogcatParse {

	[Flags]
	public enum GrefParseOptions {
		None,
		CheckCounts             = 1,
		WarnOnCountMismatch     = 1,
		ThrowOnCountMismatch    = 2,
	}
}
