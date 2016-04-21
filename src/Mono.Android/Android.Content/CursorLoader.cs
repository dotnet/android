#if ANDROID_11
using System;
using Android.Runtime;

namespace Android.Content
{
	public partial class CursorLoader
	{
                static IntPtr id_loadInBackground;
                [Register ("loadInBackground", "()Landroid/database/Cursor;", "GetLoadInBackgroundHandler")]
                public override Java.Lang.Object LoadInBackground ()
                {
                        if (id_loadInBackground == IntPtr.Zero)
                                id_loadInBackground = JNIEnv.GetMethodID (class_ref, "loadInBackground", "()Landroid/database/Cursor;");

                        if (GetType () == ThresholdType)
                                return (Java.Lang.Object) Java.Lang.Object.GetObject<Android.Database.ICursor> (JNIEnv.CallObjectMethod  (Handle, id_loadInBackground), JniHandleOwnership.TransferLocalRef);
                        else
                                return (Java.Lang.Object) Java.Lang.Object.GetObject<Android.Database.ICursor> (
                                        JNIEnv.CallNonvirtualObjectMethod  (
                                            Handle,
                                            ThresholdClass,
                                            JNIEnv.GetMethodID (ThresholdClass, "loadInBackground", "()Landroid/database/Cursor;")),
                                        JniHandleOwnership.TransferLocalRef);
                }
	}
}

#endif
