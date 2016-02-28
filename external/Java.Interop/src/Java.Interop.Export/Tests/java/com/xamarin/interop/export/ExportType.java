package com.xamarin.interop.export;

import java.util.ArrayList;

import com.xamarin.java_interop.GCUserPeerable;

public class ExportType
		implements GCUserPeerable
{

	static  final   String  assemblyQualifiedName   = "Java.InteropTests.ExportTest, Java.Interop.Export-Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
	static {
		com.xamarin.java_interop.ManagedPeer.registerNativeMembers (
				ExportType.class,
				assemblyQualifiedName,
				"");
	}

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

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public void jiAddManagedReference (java.lang.Object obj)
	{
		managedReferences.add (obj);
	}

	public void jiClearManagedReferences ()
	{
		managedReferences.clear ();
	}
}