using System;
using Android.Runtime;

namespace Java.Lang {

	public partial class Short {

		public static explicit operator short (Java.Lang.Short value)
		{
			return value.ShortValue ();
		}
	}
}
