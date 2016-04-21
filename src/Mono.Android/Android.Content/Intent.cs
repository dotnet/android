using System;
using Android.Runtime;

namespace Android.Content {

	public partial class Intent {

		public Intent (Context packageContext, System.Type type) : this (packageContext, Java.Lang.Class.FromType (type)) {}

		public Intent (string action, Android.Net.Uri uri, Context packageContext, System.Type type) : this (action, uri, packageContext, Java.Lang.Class.FromType (type)) {}

		public Intent SetClass (Context packageContext, System.Type type)
		{
			return SetClass (packageContext, Java.Lang.Class.FromType (type));
		}
	}
}
