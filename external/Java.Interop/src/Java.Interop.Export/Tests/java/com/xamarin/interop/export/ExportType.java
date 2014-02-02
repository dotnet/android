package com.xamarin.interop.export;

public class ExportType {

	public native void action ();
	public static native void staticAction ();

	public static native void actionInt32String (int i, String s);
}