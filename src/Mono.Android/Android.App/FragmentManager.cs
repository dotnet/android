using Android.OS;
using Android.Runtime;
using System.Diagnostics.CodeAnalysis;

#if ANDROID_11
namespace Android.App {
	public partial class FragmentManager {
		/// <summary>
		/// Finds a <see cref="Fragment"/> that was identified by the given id, either when
		/// inflated from XML or as the container id when added in a transaction, and returns
		/// it cast to <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">The <see cref="Fragment"/> type to cast the result to.</typeparam>
		/// <param name="id">The resource id or container id used to identify the fragment.</param>
		/// <returns>
		/// The matching fragment cast to <typeparamref name="T"/>, or <see langword="null"/>
		/// if no matching fragment exists.
		/// </returns>
		public T? FindFragmentById<
				[DynamicallyAccessedMembers (Constructors)]
				T
		> (int id)
			where T : Fragment
		{
			return FindFragmentById (id).JavaCast<T> ();
		}

		/// <summary>
		/// Finds a <see cref="Fragment"/> that was identified by the given tag, either when
		/// inflated from XML or as supplied when added in a transaction, and returns it cast
		/// to <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">The <see cref="Fragment"/> type to cast the result to.</typeparam>
		/// <param name="tag">The tag used to identify the fragment.</param>
		/// <returns>
		/// The matching fragment cast to <typeparamref name="T"/>, or <see langword="null"/>
		/// if no matching fragment exists.
		/// </returns>
		public T? FindFragmentByTag<
				[DynamicallyAccessedMembers (Constructors)]
				T
		> (string tag)
			where T : Fragment
		{
			return FindFragmentByTag (tag).JavaCast<T> ();
		}

		/// <summary>
		/// Retrieves the current <see cref="Fragment"/> instance for a reference previously
		/// placed in <paramref name="bundle"/> with <c>PutFragment</c>, and returns it cast
		/// to <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">The <see cref="Fragment"/> type to cast the result to.</typeparam>
		/// <param name="bundle">The bundle in which the fragment reference was stored.</param>
		/// <param name="key">The name of the entry in the bundle.</param>
		/// <returns>
		/// The referenced fragment cast to <typeparamref name="T"/>, or <see langword="null"/>
		/// if no fragment is associated with the given key.
		/// </returns>
		public T? GetFragment<
				[DynamicallyAccessedMembers (Constructors)]
				T
		> (Bundle bundle, string key)
			where T : Fragment
		{
			return GetFragment (bundle, key).JavaCast<T> ();
		}
	}
}
#endif
