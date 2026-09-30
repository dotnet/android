using System;
using Android.OS;
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

#if ANDROID_34
		// Add correctly enumified overloads
		[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android26.0")]
		public Intent? RegisterReceiver (BroadcastReceiver? receiver, IntentFilter? filter, ReceiverFlags flags)
#pragma warning disable CS0618 // Delegate to the legacy overload that has the underlying Java signature.
			=> RegisterReceiver (receiver, filter, (ActivityFlags)flags);
#pragma warning restore CS0618

		[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android26.0")]
		public Intent? RegisterReceiver (BroadcastReceiver? receiver, IntentFilter? filter, string? broadcastPermission, Handler? scheduler, ReceiverFlags flags)
#pragma warning disable CS0618 // Delegate to the legacy overload that has the underlying Java signature.
			=> RegisterReceiver (receiver, filter, broadcastPermission, scheduler, (ActivityFlags)flags);
#pragma warning restore CS0618
#endif
	}
}
