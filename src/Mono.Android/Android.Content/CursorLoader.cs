#if ANDROID_11
using System;
using Android.Runtime;

namespace Android.Content
{
	public partial class CursorLoader
	{
		[Register ("loadInBackground", "()Landroid/database/Cursor;", "GetLoadInBackgroundHandler")]
		public override unsafe Java.Lang.Object? LoadInBackground ()
		{
			const string id = "loadInBackground.()Landroid/database/Cursor;";
			try {
				var reference = _members.InstanceMethods.InvokeVirtualObjectMethod (id, this, null);
				return (Java.Lang.Object?) Java.Lang.Object.GetObject<Android.Database.ICursor> (reference.Handle, JniHandleOwnership.TransferLocalRef);
			} finally {
				GC.KeepAlive (this);
			}
		}
	}
}

#endif
