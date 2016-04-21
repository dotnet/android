#if ANDROID_11

using System;

using Android.Runtime;

namespace Android.App {

	partial class ActionBar {

		public class TabEventArgs : EventArgs {

			public TabEventArgs (FragmentTransaction fragmentTransaction)
			{
				FragmentTransaction = fragmentTransaction;
			}

			public FragmentTransaction FragmentTransaction {get; private set;}
		}

		partial class Tab {

			WeakReference dispatcher;
			TabEventDispatcher Dispatcher {
				get {
					if (dispatcher == null || !dispatcher.IsAlive) {
						dispatcher = new WeakReference (new TabEventDispatcher ());
						SetTabListener ((TabEventDispatcher) dispatcher.Target);
					}
					return (TabEventDispatcher) dispatcher.Target;
				}
			}

			public event EventHandler<TabEventArgs> TabReselected {
				add {
					Dispatcher.TabReselected += value;
				}
				remove {
					Dispatcher.TabReselected -= value;
				}
			}

			public event EventHandler<TabEventArgs> TabSelected {
				add {
					Dispatcher.TabSelected += value;
				}
				remove {
					Dispatcher.TabSelected -= value;
				}
			}

			public event EventHandler<TabEventArgs> TabUnselected {
				add {
					Dispatcher.TabUnselected += value;
				}
				remove {
					Dispatcher.TabUnselected -= value;
				}
			}
		}
	}

	[Register ("mono/android/app/TabEventDispatcher")]
	internal sealed class TabEventDispatcher : Java.Lang.Object, ActionBar.ITabListener {

		public TabEventDispatcher ()
			: base (
					JNIEnv.StartCreateInstance ("mono/android/app/TabEventDispatcher", "()V"),
					JniHandleOwnership.TransferLocalRef)
		{
			JNIEnv.FinishCreateInstance (Handle, "()V");
		}

		public EventHandler<ActionBar.TabEventArgs> TabReselected;
		public EventHandler<ActionBar.TabEventArgs> TabSelected;
		public EventHandler<ActionBar.TabEventArgs> TabUnselected;

		public void OnTabReselected (ActionBar.Tab tab, FragmentTransaction ft)
		{
			var h = TabReselected;
			if (h != null)
				h (tab, new ActionBar.TabEventArgs (ft));
		}

		public void OnTabSelected (ActionBar.Tab tab, FragmentTransaction ft)
		{
			var h = TabSelected;
			if (h != null)
				h (tab, new ActionBar.TabEventArgs (ft));
		}

		public void OnTabUnselected (ActionBar.Tab tab, FragmentTransaction ft)
		{
			var h = TabUnselected;
			if (h != null)
				h (tab, new ActionBar.TabEventArgs (ft));
		}
	}
}

#endif // ANDROID_11
