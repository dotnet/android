using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Android.Widget {

	public partial class DatePicker
	{
		/// <summary>Gets or sets the date selected in this picker.</summary>
		/// <remarks>Only the year, month, and day components are used when setting the value. This property converts between .NET's 1-based month and Android's 0-based month.</remarks>
		/// <seealso href="https://developer.android.com/reference/android/widget/DatePicker">Android documentation for <c>android.widget.DatePicker</c></seealso>
		public DateTime DateTime {
			get { return new DateTime (Year, Month + 1, DayOfMonth); }
			set { UpdateDate (value.Year, value.Month - 1, value.Day); }
		}
#if ANDROID_11
		/// <summary>Gets the minimum selectable date supported by this picker.</summary>
		/// <value>The minimum date converted from Android's millisecond value relative to January 1, 1970.</value>
		/// <seealso href="https://developer.android.com/reference/android/widget/DatePicker#getMinDate()">Android documentation for <c>android.widget.DatePicker.getMinDate</c></seealso>
		public DateTime MinDateTime {
			get { return new DateTime (1970, 1, 1).AddMilliseconds (MinDate); }
		}
		/// <summary>Gets the maximum selectable date supported by this picker.</summary>
		/// <value>The maximum date converted from Android's millisecond value relative to January 1, 1970.</value>
		/// <seealso href="https://developer.android.com/reference/android/widget/DatePicker#getMaxDate()">Android documentation for <c>android.widget.DatePicker.getMaxDate</c></seealso>
		public DateTime MaxDateTime {
			get { return new DateTime (1970, 1, 1).AddMilliseconds (MaxDate); }
		}
#endif
	}
}
