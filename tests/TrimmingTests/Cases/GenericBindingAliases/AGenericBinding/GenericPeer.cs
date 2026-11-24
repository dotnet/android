using System;
using Android.Runtime;

namespace A;

[Register ("example/AliasPeer", DoNotGenerateAcw = true)]
public class GenericPeer<T> : Java.Lang.Object
{
	public GenericPeer (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
}
