package com.xamarin;

import java.util.ArrayList;
import java.util.List;

enum JavaEnum {
	FIRST,
	SECOND;

	public int switchValue() {
		return 0;
	}
}

public class JavaType<E>
	implements Cloneable, Comparable<JavaType<E>>,
		IJavaInterface<StringBuilder, ArrayList<StringBuilder>, List<String>>
{
	@Deprecated
	public  static  final   Object  STATIC_FINAL_OBJECT     = new Object ();
	public  static  final   int     STATIC_FINAL_INT32      = 42;
	public  static  final   int     STATIC_FINAL_INT32_MIN  = Integer.MIN_VALUE;
	public  static  final   int     STATIC_FINAL_INT32_MAX  = Integer.MAX_VALUE;
	public  static  final   char    STATIC_FINAL_CHAR_MIN   = Character.MIN_VALUE;
	public  static  final   char    STATIC_FINAL_CHAR_MAX   = Character.MAX_VALUE;
	public  static  final   long    STATIC_FINAL_INT64_MIN  = Long.MIN_VALUE;
	public  static  final   long    STATIC_FINAL_INT64_MAX  = Long.MAX_VALUE;
	public  static  final   float   STATIC_FINAL_SINGLE_MIN = Float.MIN_VALUE;
	public  static  final   float   STATIC_FINAL_SINGLE_MAX = Float.MAX_VALUE;
	public  static  final   double  STATIC_FINAL_DOUBLE_MIN = Double.MIN_VALUE;
	public  static  final   double  STATIC_FINAL_DOUBLE_MAX = Double.MAX_VALUE;
	public  static  final   String  STATIC_FINAL_STRING     = "Hello, \\\"embedded\u0000Nulls\" and \uD83D\uDCA9!";
	public  static  final   boolean STATIC_FINAL_BOOL_FALSE = false;
	public  static  final   boolean STATIC_FINAL_BOOL_TRUE  = true;
	public  static  final   double  POSITIVE_INFINITY       = 1.0 / 0.0;
	public  static  final   double  NEGATIVE_INFINITY       = -1.0 / 0.0;
	public  static  final   double  NaN                     = 0.0d / 0.0;


	// The previous nested type naming convention generated names
	// which were "too long" (230 chars is too long?!), resulting in
	// build errors on *Linux* (of all places). (Wat)
	// Consequently, we can't spell everything out.
	//
	// Naming Convention:
	//  P: Public visibility
	//  R: pRotected visibility
	//  A: pAckage visibiliity
	//  V: priVate visibility
	//  S: Static inner class
	//  N: Non-static inner class
	//  C: Class
	//  I: Interface
	public static abstract class PSC {
	}

	protected abstract class RNC<E2> {

		protected RNC () {
		}

		protected RNC (E value1, E2 value2) {
		}

		public abstract E2 fromE (E value);

		public abstract class RPNC<E3> {
			public RPNC () {
			}

			public RPNC (E value1, E2 value2, E3 value3) {
			}

			public abstract E3 fromE2 (E2 value);
		}
	}

	@Deprecated
	/* package */ static class ASC {
	}

	public JavaType () {
	}

	public JavaType (String value) {
	}

	@Deprecated
	public final Object INSTANCE_FINAL_OBJECT = new Object ();
	public final E      INSTANCE_FINAL_E      = null;

	/* package */ E[]    packageInstanceEArray;
	protected List<E>    protectedInstanceEList;

	private   List<int[][]>[]   privateInstanceArrayOfListOfIntArrayArray;

	public int compareTo (JavaType<E> value) {
	    return 0;
	}

	public List<String> func (StringBuilder value) {
		return null;
	}

	public void run () {
	}

	@Deprecated
	public void action (Object value) {
		Object local = new Object ();
		local.toString ();
		int i = 42;
		Runnable r = new Runnable () {
			public void run() {
				System.out.println ("foo");
			}
		};
		r.run();
	}

	public java.lang.Integer func (String[] values) {
	    return values.length;
	}

	public static <T, TExtendsNumber extends Number & Comparable<T>, TThrowable extends Throwable>
	void staticActionWithGenerics (
			T value1,
			TExtendsNumber value2,
			List<?> unboundedList,
			List<? extends Number> extendsList,
			List<? super Throwable> superList)
		throws IllegalArgumentException, NumberFormatException, TThrowable {
	}

	public <T>
	void instanceActionWithGenerics (
            T value1,
            E value2) {
	}

	public static int sum (int first, int... remaining) {
		return -1;
	}

	protected void finalize () {
	}

	public static int finalize (int value) {
		return value;
	}
}
