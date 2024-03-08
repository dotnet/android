#if ANDROID_26
using System;
using Android.Runtime;
using Java.Time.Chrono;

namespace Java.Time
{
	public sealed partial class LocalDateTime
	{
		public int CompareTo (Java.Lang.Object obj) => CompareTo (obj.JavaCast<IChronoLocalDateTime> ());
	}
}
#endif

