using System;
using Android.Runtime;

namespace Android.Graphics
{	partial class PathIterator
	{

#if ANDROID_34
		// This implements an interface method that should be marked as 'default' but isn't.
		public void Remove ()
		{
			throw new Java.Lang.UnsupportedOperationException ();
		}
#endif
	}
}
