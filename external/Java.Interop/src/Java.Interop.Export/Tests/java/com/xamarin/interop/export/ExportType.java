package com.xamarin.interop.export;

public class ExportType {

	public static void testStaticMethods () {
		staticAction ();
		staticActionInt32String (1, "2");

		int v = staticFuncMyLegacyColorMyColor_MyColor (1, 41);
		if (v != 42)
			throw new Error ("staticFuncMyEnum_MyEnum should return 42!");
	}

	public static native void staticAction ();
	public static native void staticActionInt32String (int i, String s);
	public static native int  staticFuncMyLegacyColorMyColor_MyColor (int color1, int color2);

	public void testMethods () {
	    action ();

	    long j = funcInt64 ();
	    if (j != 42)
	        throw new Error ("funcInt64() should return 42!");

	    Object o = funcIJavaObject ();
		if (o != this)
			throw new Error ("funcIJavaObject() should return `this`!");
	}

	public native void action ();
	public native long funcInt64 ();
	public native Object funcIJavaObject ();
}