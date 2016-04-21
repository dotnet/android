using System;
using System.Collections.Generic;

using Android.Runtime;
using Android.Views;

using Java.Interop;

namespace Android.Widget {

	public partial class MediaController {

		WeakReference weak_implementor_NextClick;
		public event EventHandler NextClick {
			add {
				AndroidEventHelper.AddEventHandler<View.IOnClickListener, View.IOnClickListenerImplementor>(
						ref weak_implementor_NextClick,
						CreateClickImplementor,
						SetNextClickListener,
						__h => __h.Handler += value);
			}
			remove {
				AndroidEventHelper.RemoveEventHandler<View.IOnClickListener, View.IOnClickListenerImplementor>(
						ref weak_implementor_NextClick,
						View.IOnClickListenerImplementor.__IsEmpty,
						SetNextClickListener,
						__h => __h.Handler -= value);
			}
		}

		void SetNextClickListener (View.IOnClickListener value)
		{
			SetPrevNextListeners (next:value, prev:GetClickListener (weak_implementor_PrevClick));
		}

		static View.IOnClickListener GetClickListener (WeakReference value)
		{
			return value != null
				? (View.IOnClickListener) value.Target
				: null;
		}

		WeakReference weak_implementor_PrevClick;
		public event EventHandler PreviousClick {
			add {
				AndroidEventHelper.AddEventHandler<View.IOnClickListener, View.IOnClickListenerImplementor>(
						ref weak_implementor_PrevClick,
						CreateClickImplementor,
						SetPrevClickListener,
						__h => __h.Handler += value);
			}
			remove {
				AndroidEventHelper.RemoveEventHandler<View.IOnClickListener, View.IOnClickListenerImplementor>(
						ref weak_implementor_PrevClick,
						View.IOnClickListenerImplementor.__IsEmpty,
						SetPrevClickListener,
						__h => __h.Handler -= value);
			}
		}

		void SetPrevClickListener (View.IOnClickListener value)
		{
			SetPrevNextListeners (next:GetClickListener (weak_implementor_NextClick), prev:value);
		}

		View.IOnClickListenerImplementor CreateClickImplementor ()
		{
			return new View.IOnClickListenerImplementor ();
		}
	}
}

