#if ANDROID_26
using System;
using Android.Runtime;
using Java.Time.Chrono;

namespace Java.Time
{
	public sealed partial class ZonedDateTime
	{
		public int CompareTo (Java.Lang.Object obj) => (this as IChronoZonedDateTime).CompareTo (obj.JavaCast<IChronoZonedDateTime> ());
	}
}
#endif

