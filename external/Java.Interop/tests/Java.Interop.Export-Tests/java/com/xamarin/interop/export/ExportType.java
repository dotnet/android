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
	public static native void staticActionIJavaObject (Object test);
	public static native void staticActionInt32String (int i, String s);
	public static native int  staticFuncMyLegacyColorMyColor_MyColor (int color1, int color2);

	public static native boolean staticFuncThisMethodTakesLotsOfParameters (
			boolean             a,
			byte                b,
			char                c,
			short               d,
			int                 e,
			long                f,
			float               g,
			double              h,
			Object              i,
			String              j,
			ArrayList<String>   k,
			String              l,
			Object              m,
			double              n,
			float               o,
			long                p);

	public void testMethods () {
		action ();

		actionIJavaObject (this);

		long j = funcInt64 ();
		if (j != 42)
			throw new Error ("funcInt64() should return 42!");

		Object o = funcIJavaObject ();
		if (o != this)
			throw new Error ("funcIJavaObject() should return `this`!");

		staticActionInt (1);
		staticActionFloat (2.0f);

		/*
		boolean r = staticFuncThisMethodTakesLotsOfParameters (
				false,
				(byte) 0xb,
				'c',
				(short) 0xd,
				0xe,
				0xf,
				1.0f,
				2.0,
				new Object (),
				"j",
				new ArrayList<String>(),
				"l",
				new Object (),
				3.0,
				4.0f,
				0x70
		);
		if (r != true)
			throw new Error ("staticFuncThisMethodTakesLotsOfParameters should return true!");
		 */
	}

	public native void action ();
	public native void actionIJavaObject (Object test);
	public native long funcInt64 ();
	public native Object funcIJavaObject ();
	public native void staticActionInt (int i);
	public native void staticActionFloat (float f);

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