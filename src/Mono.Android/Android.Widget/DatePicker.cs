using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Android.Widget {

	public partial class DatePicker
	{
		public DateTime DateTime {
			get { return new DateTime (Year, Month + 1, DayOfMonth); }
			set { UpdateDate (value.Year, value.Month - 1, value.Day); }
		}
#if ANDROID_11
		public DateTime MinDateTime {
			get { return new DateTime (1970, 1, 1).AddMilliseconds (MinDate); }
		}
		public DateTime MaxDateTime {
			get { return new DateTime (1970, 1, 1).AddMilliseconds (MaxDate); }
		}
#endif
	}
}
