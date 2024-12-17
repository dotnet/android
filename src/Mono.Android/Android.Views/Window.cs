using System;
using System.Diagnostics.CodeAnalysis;
using Java.Interop;

namespace Android.Views {

	partial class Window {

		public T? FindViewById<
				[DynamicallyAccessedMembers (Constructors)]
				T
		> (int id)
			where T : Android.Views.View
		{
			return this.FindViewById (id).JavaCast<T> ();
		}
	}
}
