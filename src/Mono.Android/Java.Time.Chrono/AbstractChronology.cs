#if ANDROID_26
using System;
using Android.Runtime;
using Java.Time.Chrono;

namespace Java.Time.Chrono
{
	public abstract partial class AbstractChronology
	{
		public int CompareTo (Java.Lang.Object obj) => CompareTo (obj.JavaCast<IChronology> ());
	}
}
#endif

