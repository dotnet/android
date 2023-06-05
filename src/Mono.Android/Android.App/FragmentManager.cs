using Android.OS;
using Android.Runtime;

#if ANDROID_11
namespace Android.App {
	public partial class FragmentManager {
		public T? FindFragmentById<T> (int id) where T : Fragment
		{
			return FindFragmentById (id).JavaCast<T> ();
		}
		public T? FindFragmentByTag<T> (string tag) where T : Fragment
		{
			return FindFragmentByTag (tag).JavaCast<T> ();
		}
		public T? GetFragment<T> (Bundle bundle, string key) where T : Fragment
		{
			return GetFragment (bundle, key).JavaCast<T> ();
		}
	}
}
#endif
