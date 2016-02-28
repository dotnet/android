package com.xamarin.interop;

import java.util.ArrayList;

import com.xamarin.java_interop.GCUserPeerable;

public class TestType implements GCUserPeerable {

	static  final   String  assemblyQualifiedName   = "Java.InteropTests.TestType, Java.Interop-Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
	static {
		com.xamarin.java_interop.ManagedPeer.registerNativeMembers (
				TestType.class,
				assemblyQualifiedName,
				"");
	}

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public TestType () {
		if (TestType.class == getClass ()) {
			com.xamarin.java_interop.ManagedPeer.construct (
					this,
					assemblyQualifiedName,
					""
			);
		}
	}

	// For test purposes; DO NOT provide this constructor in the TestType.cs!
	public TestType (TestType a, int b) {
		if (TestType.class == getClass ()) {
			com.xamarin.java_interop.ManagedPeer.construct (
					this,
					assemblyQualifiedName,
					assemblyQualifiedName + ":System.Int32",
					a, b
			);
		}
	}

	public  void    runTests () {
		int n = getInt32Value ();
		if (getInt32Value() != 42)
			throw new Error("Expected getInt32Value()==42; got " + n + "!");

		String s = getStringValue(64);
		if (s == null || !s.equals("64"))
			throw new Error("Expected getStringValue(64)==\"64\"; got " + s + "!");

		if (!equalsThis(this))
			throw new Error("Expected equalsThis(this)==true!");

		try {
			methodThrows ();
			throw new Exception ("should not be reached");
		} catch (Throwable t) {
			String expected = "System.InvalidOperationException: jonp: bye!";
			String actual   = t.getMessage ();
			if (actual == null || !actual.startsWith (expected))
				throw new Error ("Expected message to start with '" + expected + "'! Was: " + actual);
		}
	}

	public int updateInt32Array (int[] array) {
		if (array == null)
			return -1;
		for (int i = 0; i < array.length; ++i) {
			if (array [i] != (i+1)*1)
				return 1;
			array [i] *= 2;
		}
		return 0;
	}

	public int updateInt32ArrayArray (int[][] array) {
		if (array == null)
			return -1;
		for (int i = 0; i < array.length; ++i) {
			for (int j = 0; j < array [i].length; ++j) {
				if (array [i][j] != ((i+1)*10) + ((j+1)*1))
					return 1;
				array [i][j] *= 2;
			}
		}
		return 0;
	}

	public  int updateInt32ArrayArrayArray (int[][][] array)
	{
		if (array == null)
			return -1;
		for (int i = 0; i < array.length; ++i) {
			for (int j = 0; j < array [i].length; ++j) {
				for (int k = 0; k < array [i][j].length; ++k) {
					if (array [i][j][k] != ((i+1)*100) + ((j+1)*10) + (k+1))
						return 1;
					array [i][j][k] *= 2;
				}
			}
		}
		return 0;
	}

	public int identity (int value) {
	    return value;
	}

	public static int staticIdentity (int value) {
	    return value;
	}

	public  boolean propogateFinallyBlockExecuted;
	public  void propogateException () {
		try {
			methodThrows ();
		} finally {
			propogateFinallyBlockExecuted = true;
		}
	}

	public static TestType newTestType ()
	{
		return new TestType ();
	}

	public static TestType newTestTypeWithUnboundConstructor ()
	{
		return new TestType (null, 42);
	}

	public  native  boolean equalsThis (Object value);
	public  native  int     getInt32Value ();
	public  native  String  getStringValue (int value);
	public  native  void    methodThrows ();

	public void jiAddManagedReference (java.lang.Object obj)
	{
		managedReferences.add (obj);
	}

	public void jiClearManagedReferences ()
	{
		managedReferences.clear ();
	}
}
