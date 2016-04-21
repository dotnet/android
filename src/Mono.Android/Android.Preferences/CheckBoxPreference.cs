namespace Android.Preferences {

	partial class CheckBoxPreference {

#if ANDROID_14
		public new string SummaryOff {
			get {return base.SummaryOff;}
			set {base.SummaryOff = value;}
		}

		public new string SummaryOn {
			get {return base.SummaryOn;}
			set {base.SummaryOn = value;}
		}
#endif  // ANDROID_14
	}
}