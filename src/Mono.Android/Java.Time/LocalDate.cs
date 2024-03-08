#if ANDROID_26
using System;
using Android.Runtime;
using Java.Time.Chrono;

namespace Java.Time
{
	public sealed partial class LocalDate
	{
		public int CompareTo (Java.Lang.Object obj) => CompareTo (obj.JavaCast<IChronoLocalDate> ());
	}
}
#endif

