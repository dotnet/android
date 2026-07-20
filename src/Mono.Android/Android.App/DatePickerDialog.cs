using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Android.App {

	public partial class DatePickerDialog {

		public partial class DateSetEventArgs {

			public DateTime Date {
				get { return new DateTime (Year, Month + 1, DayOfMonth); }
			}

#if ANDROID_24
			[Obsolete ("This parameter in DateTimePickerDialog constructor is removed in Android API, so it will vanish from this automatically generated type too.")]
			public int MonthOfYear {
				get { return Month; }
			}
#else
			public int Month {
				get { return monthOfYear; }
			}
#endif
		}

		public DatePickerDialog (Android.Content.Context context, EventHandler<DateSetEventArgs> callBack, int year, int monthOfYear, int dayOfMonth) 
			: this (context, new IOnDateSetListenerImplementor () { Handler = callBack }, year, monthOfYear, dayOfMonth) {}

		public DatePickerDialog (Android.Content.Context context, int theme, EventHandler<DateSetEventArgs> callBack, int year, int monthOfYear, int dayOfMonth) 
			: this (context, theme, new IOnDateSetListenerImplementor () { Handler = callBack }, year, monthOfYear, dayOfMonth) {}

		/// <summary>
		/// Sets the current date shown by the dialog using a <see cref="System.DateTime" />.
		/// </summary>
		/// <remarks>
		/// This is a .NET-friendly overload that forwards to
		/// <see cref="UpdateDate(int, int, int)" />, converting the 1-based
		/// <see cref="System.DateTime.Month" /> to Android's 0-based month value.
		/// </remarks>
		/// <param name="date">The date to display in the dialog.</param>
		public void UpdateDate (DateTime date)
		{
			UpdateDate (date.Year, date.Month - 1, date.Day);
		}
	}
}
