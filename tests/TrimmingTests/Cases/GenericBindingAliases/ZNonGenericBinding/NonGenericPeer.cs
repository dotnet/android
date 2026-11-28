using System;
using Android.Runtime;

namespace Z;

[Register ("example/AliasPeer", DoNotGenerateAcw = true)]
public class NonGenericPeer : Java.Lang.Object
{
	public NonGenericPeer (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
}
