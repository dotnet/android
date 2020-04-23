using System.Diagnostics.CodeAnalysis;
using Android.OS;
using Android.Runtime;

#if ANDROID_11
namespace Android.App {
	public partial class FragmentManager {
		[return: MaybeNull]
		public T FindFragmentById<T> (int id) where T : Fragment
		{
			return FindFragmentById (id).JavaCast<T> ();
		}
		[return: MaybeNull]
		public T FindFragmentByTag<T> (string tag) where T : Fragment
		{
			return FindFragmentByTag (tag).JavaCast<T> ();
		}
		[return: MaybeNull]
		public T GetFragment<T> (Bundle bundle, string key) where T : Fragment
		{
			return GetFragment (bundle, key).JavaCast<T> ();
		}
	}
}
#endif
