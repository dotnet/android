using System;
using Android.Content;
using Android.Runtime;

using Java.Interop;

#if ANDROID_5

namespace Android.Accounts {

	public partial class AccountManager {

		public static AccountManager? FromContext (Context context)
		{
			return context.GetSystemService (Context.AccountService) as AccountManager;
		}

		WeakReference? weak_implementor_AccountsUpdated;
		public event EventHandler<AccountsUpdateEventArgs> AccountsUpdated {
			add {
				EventHelper.AddEventHandler<IOnAccountsUpdateListener, IOnAccountsUpdateListenerImplementor>(
						ref weak_implementor_AccountsUpdated,
						() => new IOnAccountsUpdateListenerImplementor (this),
						SetOnAccountsUpdatedListener,
						__h => __h.Handler += value);
			}
			remove {
				EventHelper.RemoveEventHandler<IOnAccountsUpdateListener, IOnAccountsUpdateListenerImplementor>(
						ref weak_implementor_AccountsUpdated,
						IOnAccountsUpdateListenerImplementor.__IsEmpty,
						__h => SetOnAccountsUpdatedListener (null),
						__h => __h.Handler -= value);
			}
		}

		void SetOnAccountsUpdatedListener (IOnAccountsUpdateListener? value)
		{
			AddOnAccountsUpdatedListener (value, null, false);
		}
	}
}

#endif  // ANDROID_5
