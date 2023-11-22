package net.dot.jni.test;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

public class ExportType
		implements GCUserPeerable
{

	static  final   String  assemblyQualifiedName   = "Java.InteropTests.ExportTest, Java.Interop.Export-Tests";
	static {
		net.dot.jni.ManagedPeer.registerNativeMembers (
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

	public  static          void    staticAction () {n_StaticAction ();}
	private static  native  void    n_StaticAction ();

	public  static          void    staticActionIJavaObject (Object test) {n_StaticActionIJavaObject(test);}
	private static  native  void    n_StaticActionIJavaObject (Object test);

	public  static          void    staticActionInt32String (int i, String s) {n_StaticActionInt32String (i, s);}
	private static  native  void    n_StaticActionInt32String (int i, String s);

	public  static          int     staticFuncMyLegacyColorMyColor_MyColor (int color1, int color2) {return n_StaticFuncMyLegacyColorMyColor_MyColor (color1, color2);}
	private static  native  int     n_StaticFuncMyLegacyColorMyColor_MyColor (int color1, int color2);

	public static boolean staticFuncThisMethodTakesLotsOfParameters (
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
			long                p) {
		return n_StaticFuncThisMethodTakesLotsOfParameters (
				a,
				b,
				c,
				d,
				e,
				f,
				g,
				h,
				i,
				j,
				k,
				l,
				m,
				n,
				o,
				p);
	}
	private static native boolean n_StaticFuncThisMethodTakesLotsOfParameters (
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
	}

	public          void    action () {n_InstanceAction ();}
	private native  void    n_InstanceAction ();

	public          void    actionIJavaObject (Object test) {n_InstanceActionIJavaObject (test);}
	private native  void    n_InstanceActionIJavaObject (Object test);

	public          long    funcInt64 () {return n_FuncInt64 ();}
	private native  long    n_FuncInt64 ();

	public          Object  funcIJavaObject () {return n_FuncIJavaObject ();}
	private native  Object  n_FuncIJavaObject ();

	public          void    staticActionInt (int i) {n_StaticActionInt (i);}
	private native  void    n_StaticActionInt (int i);

	public          void    staticActionFloat (float f) {n_StaticActionFloat (f);}
	private native  void    n_StaticActionFloat (float f);

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