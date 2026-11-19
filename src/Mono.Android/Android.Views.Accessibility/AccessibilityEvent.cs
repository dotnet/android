using System;
using Android.AccessibilityServices;

namespace Android.Views.Accessibility {

	partial class AccessibilityEvent {

		[Obsolete ("This maps to incorrect type. Use GetAction() and SetAction() until we fix the API")]
		public GlobalAction Action {
			get { return (GlobalAction) GetAction (); }
			set { SetAction ((GlobalAction) value); }
		}

		public new string? BeforeText {
			get {return base.BeforeText;}
			set {base.BeforeText = value;}
		}

		public new string? ClassName {
			get {return base.ClassName;}
			set {base.ClassName = value;}
		}

		public new string? ContentDescription {
			get {return base.ContentDescription;}
			set {base.ContentDescription = value;}
		}
	}
}
