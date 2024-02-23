using Android.OS;
using Android.Runtime;
using System.Diagnostics.CodeAnalysis;

#if ANDROID_11
namespace Android.App {
	public partial class FragmentManager {
		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

		public T? FindFragmentById<
				[DynamicallyAccessedMembers (Constructors)]
				T
		> (int id) where T : Fragment
		{
			return FindFragmentById (id).JavaCast<T> ();
		}

		public T? FindFragmentByTag<
				[DynamicallyAccessedMembers (Constructors)]
				T
		> (string tag) where T : Fragment
		{
			return FindFragmentByTag (tag).JavaCast<T> ();
		}

		public T? GetFragment<
				[DynamicallyAccessedMembers (Constructors)]
				T
		> (Bundle bundle, string key) where T : Fragment
		{
			return GetFragment (bundle, key).JavaCast<T> ();
		}
	}
}
#endif
