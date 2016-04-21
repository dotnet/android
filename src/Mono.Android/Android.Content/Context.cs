using System;
using Android.Runtime;

namespace Android.Content {

	public abstract partial class Context {

		public void StartActivity (Type type)
		{
			Intent intent = new Intent (this, type);
			StartActivity (intent);
		}
	}
}
