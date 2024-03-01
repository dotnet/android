using System;
using System.Diagnostics.CodeAnalysis;
using Java.Interop;

namespace Android.Views {

	partial class Window {

		public T? FindViewById<
				[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
				T
		> (int id)
			where T : Android.Views.View
		{
			return this.FindViewById (id).JavaCast<T> ();
		}
	}
}
