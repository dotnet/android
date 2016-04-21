using System;

using Java.Interop;

namespace Android.Views {

	public partial class ViewTreeObserver {
#if false

#region "Event implementation for Android.Views.ViewTreeObserver.IOnGlobalLayoutListener"
		public event EventHandler GlobalLayout {
			add {
				global::Java.Interop.EventHelper.AddEventHandler<Android.Views.ViewTreeObserver.IOnGlobalLayoutListener, Android.Views.ViewTreeObserver.IOnGlobalLayoutListenerImplementor>(
						ref weak_implementor_AddOnGlobalLayoutListener,
						__CreateIOnGlobalLayoutListenerImplementor,
						AddOnGlobalLayoutListener,
						__h => __h.Handler += value);
			}
			remove {
#if ANDROID_16
				if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.JellyBean)
					global::Java.Interop.EventHelper.RemoveEventHandler<Android.Views.ViewTreeObserver.IOnGlobalLayoutListener, Android.Views.ViewTreeObserver.IOnGlobalLayoutListenerImplementor>(
							ref weak_implementor_AddOnGlobalLayoutListener,
							Android.Views.ViewTreeObserver.IOnGlobalLayoutListenerImplementor.__IsEmpty,
							RemoveOnGlobalLayoutListener,
							__h => __h.Handler -= value);
				else
#endif
					global::Java.Interop.EventHelper.RemoveEventHandler<Android.Views.ViewTreeObserver.IOnGlobalLayoutListener, Android.Views.ViewTreeObserver.IOnGlobalLayoutListenerImplementor>(
							ref weak_implementor_AddOnGlobalLayoutListener,
							Android.Views.ViewTreeObserver.IOnGlobalLayoutListenerImplementor.__IsEmpty,
							RemoveGlobalOnLayoutListener,
							__h => __h.Handler -= value);
			}
		}
#endregion

#endif  // false
	}
}
