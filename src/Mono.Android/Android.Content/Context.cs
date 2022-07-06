using System;
using Android.Runtime;

namespace Android.Content {

	public abstract partial class Context {

		public void StartActivity (Type type)
		{
			Intent intent = new Intent (this, type);
			StartActivity (intent);
		}

#if ANDROID_26
		// Added in API-26, converted to enum in API-33, constant needed for backwards compatibility
		[Obsolete ("This constant will be removed in the future version. Use Android.Content.ReceiverFlags enum directly instead of this field.")]
		public const int ReceiverVisibleToInstantApps = 1;
#endif
	}
}
