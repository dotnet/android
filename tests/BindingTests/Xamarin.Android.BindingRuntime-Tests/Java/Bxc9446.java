package com.xamarin.android;

import android.content.Context;

/*
This class exposes a build error in the code generator which was first 
seen in the ActionbarSherlock java bindings. 
For the Window class in that library a number of generated properties
in the WindowInvoker class were generated like so

static IntPtr id_isFloating;
public override global::System.Boolean IsFloating {
	[Register ("isFloating", "()Z", "GetGetIsFloatingHandler")]
	get {
		if (id_isFloating == IntPtr.Zero)
			id_isFloating = JNIEnv.GetMethodID (class_ref, "isFloating", "()Z");
		return global::Java.Lang.Object.GetObject<global::System.Boolean> (JNIEnv.CallBooleanMethod  (Handle, id_isFloating), JniHandleOwnership.TransferLocalRef);
	}
}

This results in a

Error CS1502: The best overloaded method match for 'Java.Lang.Object.GetObject<bool>(System.IntPtr, Android.Runtime.JniHandleOwnership)' has some invalid arguments 

when compiling the C# code. 

The correct generated code for the return statement in this case should be

return JNIEnv.CallBooleanMethod  (Handle, id_isFloating);

This class has been put in place to check for regressions in future builds.

*/
public abstract class Bxc9446 extends android.view.Window {

    public Bxc9446 (Context context) {
       super (context);
    }

}
