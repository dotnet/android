package com.xamarin;

import java.util.ArrayList;
import java.util.List;

/**
 * JNI sig: Lcom/xamarin/JavaEnum;
 */
// Disconnective comment?
enum JavaEnum {
	/** FIRST; JNI sig: Lcom/xamarin/JavaEnum; */
	FIRST,

	/** SECOND; JNI sig: Lcom/xamarin/JavaEnum; */

	SECOND;

	/**
	 * summary
	 *
	 * <p>Paragraphs of text?
	 *
	 * @return some value
	 */
	public int switchValue() {
		return 0;
	}
}

/**
 * JNI sig: Lcom/xamarin/JavaType;
 *
 * @param <E>
 */

public class JavaType<E>
	implements Cloneable, Comparable<JavaType<E>>,
		IJavaInterface<StringBuilder, ArrayList<StringBuilder>, List<String>>
{
	/** JNI sig: STATIC_FINAL_OBJECT.L/java/lang/Object; */

	@Deprecated
	public  static  final   Object  STATIC_FINAL_OBJECT     = new Object ();
	/** JNI sig: STATIC_FINAL_INT32.I */
	public  static  final   int     STATIC_FINAL_INT32      = 42;
	/** JNI sig: STATIC_FINAL_INT32_MIN.I */
	public  static  final   int     STATIC_FINAL_INT32_MIN  = Integer.MIN_VALUE;
	/** JNI sig: STATIC_FINAL_INT32_MAX.I */
	public  static  final   int     STATIC_FINAL_INT32_MAX  = Integer.MAX_VALUE;
	/** JNI sig: STATIC_FINAL_CHAR_MIN.C */
	public  static  final   char    STATIC_FINAL_CHAR_MIN   = Character.MIN_VALUE;
	/** JNI sig: STATIC_FINAL_CHAR_MAX.C */
	public  static  final   char    STATIC_FINAL_CHAR_MAX   = Character.MAX_VALUE;
	/** JNI sig: STATIC_FINAL_INT64_MIN.J */
	public  static  final   long    STATIC_FINAL_INT64_MIN  = Long.MIN_VALUE;
	/** JNI sig: STATIC_FINAL_INT64_MAX.J */
	public  static  final   long    STATIC_FINAL_INT64_MAX  = Long.MAX_VALUE;
	/** JNI sig: STATIC_FINAL_SINGLE_MIN.F */
	public  static  final   float   STATIC_FINAL_SINGLE_MIN = Float.MIN_VALUE;
	/** JNI sig: STATIC_FINAL_SINGLE_MAX.F */
	public  static  final   float   STATIC_FINAL_SINGLE_MAX = Float.MAX_VALUE;
	/** JNI sig: STATIC_FINAL_DOUBLE_MIN.D */
	public  static  final   double  STATIC_FINAL_DOUBLE_MIN = Double.MIN_VALUE;
	/** JNI sig: STATIC_FINAL_DOUBLE_MAX.D */
	public  static  final   double  STATIC_FINAL_DOUBLE_MAX = Double.MAX_VALUE;
	/** JNI sig: STATIC_FINAL_STRING.Ljava/lang/String; */
	public  static  final   String  STATIC_FINAL_STRING     = "Hello, \\\"embedded\u0000Nulls\" and \uD83D\uDCA9!";
	/** JNI sig: STATIC_FINAL_BOOL_FALSE.Z */
	public  static  final   boolean STATIC_FINAL_BOOL_FALSE = false;
	/** JNI sig: STATIC_FINAL_BOOL_TRUE.Z */
	public  static  final   boolean STATIC_FINAL_BOOL_TRUE  = true;
	/** JNI sig: POSITIVE_INFINITY.D */
	public  static  final   double  POSITIVE_INFINITY       = 1.0 / 0.0;
	/** JNI sig: NEGATIVE_INFINITY.D */
	public  static  final   double  NEGATIVE_INFINITY       = -1.0 / 0.0;
	/** JNI sig: NaN.D */
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

	/** JNI sig: Lcom/xamarin/JavaType$PSC; */

	public static abstract class PSC {
	}

	/** JNI sig: Lcom/xamarin/JavaType$RNC; */
	protected abstract class RNC<E2> {

		/** JNI sig: ()V */
		protected RNC () {
		}

		/** JNI sig: (Ljava/lang/Object;Ljava/lang/Object;)V */
		protected RNC (E value1, E2 value2) {
		}

		/** JNI sig: (Ljava/lang/Object;)Ljava/lang/Object; */

		public abstract E2 fromE (E value);

		/** JNI sig: Lcom/xamarin/JavaType$RNC$RPNC; */
		public abstract class RPNC<E3> {
			/** JNI sig: ()V */
			public RPNC () {
			}

			/** JNI sig: (Ljava/lang/Object;Ljava/lang/Object;Ljava/lang/Object;)V */
			public RPNC (E value1, E2 value2, E3 value3) {
			}

			/** JNI sig: fromE2.(Ljava/lang/Object;)Ljava/lang/Object; */
			public abstract E3 fromE2 (E2 value);
		}
	}

	/** JNI sig: Lcom/xamarin/JavaType$ASC; */

	@Deprecated
	/* package */ static class ASC {
	}

	/** JNI sig: ()V */
	public JavaType () {
	}

	/** JNI sig: (Ljava/lang/String;)V */
	public JavaType (String value) {
	}

	/** JNI sig: INSTANCE_FINAL_OBJECT.Ljava/lang/Object; */
	@Deprecated
	public final Object INSTANCE_FINAL_OBJECT = new Object ();

	/** JNI sig: INSTANCE_FINAL_E.Ljava/lang/Object; */
	public final E      INSTANCE_FINAL_E      = null;

	/** JNI sig: packageInstanceEArray.[Ljava/lang/Object; */
	/* package */ E[]    packageInstanceEArray;

	/** JNI sig: protectedInstanceEList.Ljava/util/List; */
	protected List<E>    protectedInstanceEList;

	private   List<int[][]>[]   privateInstanceArrayOfListOfIntArrayArray;

	/** JNI sig: compareTo.(Lcom/xamarin/JavaType;)I */
	public int compareTo (JavaType<E> value) {
	    return 0;
	}

	/** JNI sig: func.(Ljava/lang/StringBuilder;)Ljava/util/List; */
	// Comment to "disconnect" Javadoc from the member
	public List<String> func (StringBuilder value) {
		return null;
	}

	/** JNI sig: run.()V */
	public void run () {
	}

	/** JNI sig: action.(Ljava/lang/Object;)V */
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

	/** JNI sig: func.([Ljava/lang/String;)Ljava/lang/Integer; */
	public java.lang.Integer func (String[] values) {
	    return values.length;
	}

	/** JNI sig: staticActionWithGenerics.(Ljava/lang/Object;Ljava/lang/Number;Ljava/util/List;Ljava/util/List;Ljava/util/List;)V */
	public static <T, TExtendsNumber extends Number & Comparable<T>, TThrowable extends Throwable>
	void staticActionWithGenerics (
			T value1,
			TExtendsNumber value2,
			List<?> unboundedList,
			List<? extends Number> extendsList,
			List<? super Throwable> superList)
		throws IllegalArgumentException, NumberFormatException, TThrowable {
	}

	/** JNI sig: instanceActionWithGenerics.(Ljava/lang/Object;java/lang/Object;)V */
	public <T>
	void instanceActionWithGenerics (
            T value1,
            E value2) {
	}

	/** JNI sig: sum.(I[I)I */
	public static int sum (int first, int... remaining) {
		return -1;
	}

	/** JNI sig: finalize.()V */
	protected void finalize () {
	}

	/** JNI sig: finalize.(I)I */
	public static int finalize (int value) {
		return value;
	}
}
