namespace Android.Views.Accessibility {

	partial class AccessibilityEvent {

#if ANDROID_14
		public new string BeforeText {
			get {return base.BeforeText;}
			set {base.BeforeText = value;}
		}

		public new string ClassName {
			get {return base.ClassName;}
			set {base.ClassName = value;}
		}

		public new string ContentDescription {
			get {return base.ContentDescription;}
			set {base.ContentDescription = value;}
		}
#endif  // ANDROID_14
	}
}
