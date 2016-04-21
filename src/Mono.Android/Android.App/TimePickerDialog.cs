using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Android.App {

	public partial class TimePickerDialog {

		public TimePickerDialog (Android.Content.Context context, EventHandler<TimeSetEventArgs> callBack, int hourOfDay, int minute, bool is24HourView) 
			: this (context, new IOnTimeSetListenerImplementor () { Handler = callBack }, hourOfDay, minute, is24HourView) {}

		public TimePickerDialog (Android.Content.Context context, int theme, EventHandler<TimeSetEventArgs> callBack, int hourOfDay, int minute, bool is24HourView) 
			: this (context, theme, new IOnTimeSetListenerImplementor () { Handler = callBack }, hourOfDay, minute, is24HourView) {}

	}
}

