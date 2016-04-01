// Generated file; DO NOT EDIT!
//
// To make changes, edit monodroid/tools/jnienv-gen and rerun
#pragma warning disable
using System;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Android.Runtime {

	public static partial class JNIEnv {

		internal delegate int IntPtr_int_Delegate (IntPtr env);
		internal static int GetVersion ()
		{
			throw new NotImplementedException ();
		}

		internal delegate IntPtr IntPtr_string_IntPtr_IntPtr_int_IntPtr_Delegate (IntPtr env, string name, IntPtr loader, IntPtr buf, int bufLen);

		internal delegate IntPtr IntPtr_string_IntPtr_Delegate (IntPtr env, string classname);
		internal static IntPtr _FindClass (string classname)
		{
			throw new NotImplementedException ();
		}

		internal delegate IntPtr IntPtr_IntPtr_IntPtr_Delegate (IntPtr env, IntPtr method);


		internal delegate IntPtr IntPtr_IntPtr_IntPtr_bool_IntPtr_Delegate (IntPtr env, IntPtr cls, IntPtr jmethod, bool isStatic);

		public static IntPtr GetSuperclass (IntPtr jclass)
		{
			throw new NotImplementedException ();
		}

		internal delegate bool IntPtr_IntPtr_IntPtr_bool_Delegate (IntPtr env, IntPtr clazz1, IntPtr clazz2);
		public static bool IsAssignableFrom (IntPtr clazz1, IntPtr clazz2)
		{
			throw new NotImplementedException ();
		}


		internal delegate int IntPtr_IntPtr_int_Delegate (IntPtr env, IntPtr obj);

		internal delegate int IntPtr_IntPtr_string_int_Delegate (IntPtr env, IntPtr clazz, string message);

		internal delegate IntPtr IntPtr_IntPtr_Delegate (IntPtr env);
		public static IntPtr ExceptionOccurred ()
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_void_Delegate (IntPtr env);
		public static void ExceptionDescribe ()
		{
			throw new NotImplementedException ();
		}

		public static void ExceptionClear ()
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_string_void_Delegate (IntPtr env, string msg);

		internal delegate int IntPtr_int_int_Delegate (IntPtr env, int capacity);

		public static IntPtr PopLocalFrame (IntPtr result)
		{
			throw new NotImplementedException ();
		}


		internal delegate void IntPtr_IntPtr_void_Delegate (IntPtr env, IntPtr jobject);


		public static bool IsSameObject (IntPtr ref1, IntPtr ref2)
		{
			throw new NotImplementedException ();
		}



		public static IntPtr AllocObject (IntPtr jclass)
		{
			throw new NotImplementedException ();
		}

		internal delegate IntPtr IntPtr_IntPtr_IntPtr_IntPtr_Delegate (IntPtr env, IntPtr jclass, IntPtr jmethod);


		internal delegate IntPtr IntPtr_IntPtr_IntPtr_JValueArray_IntPtr_Delegate (IntPtr env, IntPtr jclass, IntPtr jmethod, JValue[] parms);

		public static IntPtr GetObjectClass (IntPtr jobject)
		{
			throw new NotImplementedException ();
		}

		public static bool IsInstanceOf (IntPtr obj, IntPtr clazz)
		{
			throw new NotImplementedException ();
		}

		internal delegate IntPtr IntPtr_IntPtr_string_string_IntPtr_Delegate (IntPtr env, IntPtr kls, string name, string signature);
		public static IntPtr GetMethodID (IntPtr kls, string name, string signature)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr CallObjectMethod (IntPtr jobject, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe IntPtr CallObjectMethod (IntPtr jobject, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr CallObjectMethod (IntPtr jobject, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		public static bool CallBooleanMethod (IntPtr jobject, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe bool CallBooleanMethod (IntPtr jobject, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate bool IntPtr_IntPtr_IntPtr_JValueArray_bool_Delegate (IntPtr env, IntPtr jobject, IntPtr jmethod, JValue[] parms);
		public static bool CallBooleanMethod (IntPtr jobject, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate sbyte IntPtr_IntPtr_IntPtr_sbyte_Delegate (IntPtr env, IntPtr jobject, IntPtr jmethod);
		public static sbyte CallByteMethod (IntPtr jobject, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe sbyte CallByteMethod (IntPtr jobject, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate sbyte IntPtr_IntPtr_IntPtr_JValueArray_sbyte_Delegate (IntPtr env, IntPtr jobject, IntPtr jmethod, JValue[] parms);
		public static sbyte CallByteMethod (IntPtr jobject, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate char IntPtr_IntPtr_IntPtr_char_Delegate (IntPtr env, IntPtr jobject, IntPtr jmethod);
		public static char CallCharMethod (IntPtr jobject, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe char CallCharMethod (IntPtr jobject, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate char IntPtr_IntPtr_IntPtr_JValueArray_char_Delegate (IntPtr env, IntPtr jobject, IntPtr jmethod, JValue[] parms);
		public static char CallCharMethod (IntPtr jobject, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate short IntPtr_IntPtr_IntPtr_short_Delegate (IntPtr env, IntPtr jobject, IntPtr jmethod);
		public static short CallShortMethod (IntPtr jobject, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe short CallShortMethod (IntPtr jobject, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate short IntPtr_IntPtr_IntPtr_JValueArray_short_Delegate (IntPtr env, IntPtr jobject, IntPtr jmethod, JValue[] parms);
		public static short CallShortMethod (IntPtr jobject, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate int IntPtr_IntPtr_IntPtr_int_Delegate (IntPtr env, IntPtr jobject, IntPtr jmethod);
		public static int CallIntMethod (IntPtr jobject, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe int CallIntMethod (IntPtr jobject, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate int IntPtr_IntPtr_IntPtr_JValueArray_int_Delegate (IntPtr env, IntPtr jobject, IntPtr jmethod, JValue[] parms);
		public static int CallIntMethod (IntPtr jobject, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate long IntPtr_IntPtr_IntPtr_long_Delegate (IntPtr env, IntPtr jobject, IntPtr jmethod);
		public static long CallLongMethod (IntPtr jobject, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe long CallLongMethod (IntPtr jobject, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate long IntPtr_IntPtr_IntPtr_JValueArray_long_Delegate (IntPtr env, IntPtr jobject, IntPtr jmethod, JValue[] parms);
		public static long CallLongMethod (IntPtr jobject, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate float IntPtr_IntPtr_IntPtr_float_Delegate (IntPtr env, IntPtr jobject, IntPtr jmethod);
		public static float CallFloatMethod (IntPtr jobject, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe float CallFloatMethod (IntPtr jobject, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate float IntPtr_IntPtr_IntPtr_JValueArray_float_Delegate (IntPtr env, IntPtr jobject, IntPtr jmethod, JValue[] parms);
		public static float CallFloatMethod (IntPtr jobject, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate double IntPtr_IntPtr_IntPtr_double_Delegate (IntPtr env, IntPtr jobject, IntPtr jmethod);
		public static double CallDoubleMethod (IntPtr jobject, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe double CallDoubleMethod (IntPtr jobject, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate double IntPtr_IntPtr_IntPtr_JValueArray_double_Delegate (IntPtr env, IntPtr jobject, IntPtr jmethod, JValue[] parms);
		public static double CallDoubleMethod (IntPtr jobject, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_IntPtr_IntPtr_void_Delegate (IntPtr env, IntPtr jobject, IntPtr jmethod);
		public static void CallVoidMethod (IntPtr jobject, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe void CallVoidMethod (IntPtr jobject, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_IntPtr_IntPtr_JValueArray_void_Delegate (IntPtr env, IntPtr jobject, IntPtr jmethod, JValue[] parms);
		public static void CallVoidMethod (IntPtr jobject, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate IntPtr IntPtr_IntPtr_IntPtr_IntPtr_IntPtr_Delegate (IntPtr env, IntPtr jobject, IntPtr jclass, IntPtr jmethod);
		public static IntPtr CallNonvirtualObjectMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe IntPtr CallNonvirtualObjectMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate IntPtr IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_IntPtr_Delegate (IntPtr env, IntPtr jobject, IntPtr jclass, IntPtr jmethod, JValue[] parms);
		public static IntPtr CallNonvirtualObjectMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate bool IntPtr_IntPtr_IntPtr_IntPtr_bool_Delegate (IntPtr env, IntPtr jobject, IntPtr jclass, IntPtr jmethod);
		public static bool CallNonvirtualBooleanMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe bool CallNonvirtualBooleanMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate bool IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_bool_Delegate (IntPtr env, IntPtr jobject, IntPtr jclass, IntPtr jmethod, JValue[] parms);
		public static bool CallNonvirtualBooleanMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate sbyte IntPtr_IntPtr_IntPtr_IntPtr_sbyte_Delegate (IntPtr env, IntPtr jobject, IntPtr jclass, IntPtr jmethod);
		public static sbyte CallNonvirtualByteMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe sbyte CallNonvirtualByteMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate sbyte IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_sbyte_Delegate (IntPtr env, IntPtr jobject, IntPtr jclass, IntPtr jmethod, JValue[] parms);
		public static sbyte CallNonvirtualByteMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate char IntPtr_IntPtr_IntPtr_IntPtr_char_Delegate (IntPtr env, IntPtr jobject, IntPtr jclass, IntPtr jmethod);
		public static char CallNonvirtualCharMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe char CallNonvirtualCharMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate char IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_char_Delegate (IntPtr env, IntPtr jobject, IntPtr jclass, IntPtr jmethod, JValue[] parms);
		public static char CallNonvirtualCharMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate short IntPtr_IntPtr_IntPtr_IntPtr_short_Delegate (IntPtr env, IntPtr jobject, IntPtr jclass, IntPtr jmethod);
		public static short CallNonvirtualShortMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe short CallNonvirtualShortMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate short IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_short_Delegate (IntPtr env, IntPtr jobject, IntPtr jclass, IntPtr jmethod, JValue[] parms);
		public static short CallNonvirtualShortMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate int IntPtr_IntPtr_IntPtr_IntPtr_int_Delegate (IntPtr env, IntPtr jobject, IntPtr jclass, IntPtr jmethod);
		public static int CallNonvirtualIntMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe int CallNonvirtualIntMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate int IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_int_Delegate (IntPtr env, IntPtr jobject, IntPtr jclass, IntPtr jmethod, JValue[] parms);
		public static int CallNonvirtualIntMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate long IntPtr_IntPtr_IntPtr_IntPtr_long_Delegate (IntPtr env, IntPtr jobject, IntPtr jclass, IntPtr jmethod);
		public static long CallNonvirtualLongMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe long CallNonvirtualLongMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate long IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_long_Delegate (IntPtr env, IntPtr jobject, IntPtr jclass, IntPtr jmethod, JValue[] parms);
		public static long CallNonvirtualLongMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate float IntPtr_IntPtr_IntPtr_IntPtr_float_Delegate (IntPtr env, IntPtr jobject, IntPtr jclass, IntPtr jmethod);
		public static float CallNonvirtualFloatMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe float CallNonvirtualFloatMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate float IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_float_Delegate (IntPtr env, IntPtr jobject, IntPtr jclass, IntPtr jmethod, JValue[] parms);
		public static float CallNonvirtualFloatMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate double IntPtr_IntPtr_IntPtr_IntPtr_double_Delegate (IntPtr env, IntPtr jobject, IntPtr jclass, IntPtr jmethod);
		public static double CallNonvirtualDoubleMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe double CallNonvirtualDoubleMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate double IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_double_Delegate (IntPtr env, IntPtr jobject, IntPtr jclass, IntPtr jmethod, JValue[] parms);
		public static double CallNonvirtualDoubleMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_IntPtr_IntPtr_IntPtr_void_Delegate (IntPtr env, IntPtr jobject, IntPtr jclass, IntPtr jmethod);
		public static void CallNonvirtualVoidMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe void CallNonvirtualVoidMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_void_Delegate (IntPtr env, IntPtr jobject, IntPtr jclass, IntPtr jmethod, JValue[] parms);
		public static void CallNonvirtualVoidMethod (IntPtr jobject, IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr GetFieldID (IntPtr jclass, string name, string sig)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr GetObjectField (IntPtr jobject, IntPtr jfieldID)
		{
			throw new NotImplementedException ();
		}

		public static bool GetBooleanField (IntPtr jobject, IntPtr jfieldID)
		{
			throw new NotImplementedException ();
		}

		public static sbyte GetByteField (IntPtr jobject, IntPtr jfieldID)
		{
			throw new NotImplementedException ();
		}

		public static char GetCharField (IntPtr jobject, IntPtr jfieldID)
		{
			throw new NotImplementedException ();
		}

		public static short GetShortField (IntPtr jobject, IntPtr jfieldID)
		{
			throw new NotImplementedException ();
		}

		public static int GetIntField (IntPtr jobject, IntPtr jfieldID)
		{
			throw new NotImplementedException ();
		}

		public static long GetLongField (IntPtr jobject, IntPtr jfieldID)
		{
			throw new NotImplementedException ();
		}

		public static float GetFloatField (IntPtr jobject, IntPtr jfieldID)
		{
			throw new NotImplementedException ();
		}

		public static double GetDoubleField (IntPtr jobject, IntPtr jfieldID)
		{
			throw new NotImplementedException ();
		}

		public static void SetField (IntPtr jobject, IntPtr jfieldID, IntPtr val)
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_IntPtr_IntPtr_bool_void_Delegate (IntPtr env, IntPtr jobject, IntPtr jfieldID, bool val);
		public static void SetField (IntPtr jobject, IntPtr jfieldID, bool val)
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_IntPtr_IntPtr_sbyte_void_Delegate (IntPtr env, IntPtr jobject, IntPtr jfieldID, sbyte val);
		public static void SetField (IntPtr jobject, IntPtr jfieldID, sbyte val)
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_IntPtr_IntPtr_char_void_Delegate (IntPtr env, IntPtr jobject, IntPtr jfieldID, char val);
		public static void SetField (IntPtr jobject, IntPtr jfieldID, char val)
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_IntPtr_IntPtr_short_void_Delegate (IntPtr env, IntPtr jobject, IntPtr jfieldID, short val);
		public static void SetField (IntPtr jobject, IntPtr jfieldID, short val)
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_IntPtr_IntPtr_int_void_Delegate (IntPtr env, IntPtr jobject, IntPtr jfieldID, int val);
		public static void SetField (IntPtr jobject, IntPtr jfieldID, int val)
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_IntPtr_IntPtr_long_void_Delegate (IntPtr env, IntPtr jobject, IntPtr jfieldID, long val);
		public static void SetField (IntPtr jobject, IntPtr jfieldID, long val)
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_IntPtr_IntPtr_float_void_Delegate (IntPtr env, IntPtr jobject, IntPtr jfieldID, float val);
		public static void SetField (IntPtr jobject, IntPtr jfieldID, float val)
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_IntPtr_IntPtr_double_void_Delegate (IntPtr env, IntPtr jobject, IntPtr jfieldID, double val);
		public static void SetField (IntPtr jobject, IntPtr jfieldID, double val)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr GetStaticMethodID (IntPtr jclass, string name, string sig)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr CallStaticObjectMethod (IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe IntPtr CallStaticObjectMethod (IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr CallStaticObjectMethod (IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		public static bool CallStaticBooleanMethod (IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe bool CallStaticBooleanMethod (IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		public static bool CallStaticBooleanMethod (IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		public static sbyte CallStaticByteMethod (IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe sbyte CallStaticByteMethod (IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		public static sbyte CallStaticByteMethod (IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		public static char CallStaticCharMethod (IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe char CallStaticCharMethod (IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		public static char CallStaticCharMethod (IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		public static short CallStaticShortMethod (IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe short CallStaticShortMethod (IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		public static short CallStaticShortMethod (IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		public static int CallStaticIntMethod (IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe int CallStaticIntMethod (IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		public static int CallStaticIntMethod (IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		public static long CallStaticLongMethod (IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe long CallStaticLongMethod (IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		public static long CallStaticLongMethod (IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		public static float CallStaticFloatMethod (IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe float CallStaticFloatMethod (IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		public static float CallStaticFloatMethod (IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		public static double CallStaticDoubleMethod (IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe double CallStaticDoubleMethod (IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		public static double CallStaticDoubleMethod (IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		public static void CallStaticVoidMethod (IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static unsafe void CallStaticVoidMethod (IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			throw new NotImplementedException ();
		}

		public static void CallStaticVoidMethod (IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr GetStaticFieldID (IntPtr jclass, string name, string sig)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr GetStaticObjectField (IntPtr jclass, IntPtr jfieldID)
		{
			throw new NotImplementedException ();
		}

		public static bool GetStaticBooleanField (IntPtr jclass, IntPtr jfieldID)
		{
			throw new NotImplementedException ();
		}

		public static sbyte GetStaticByteField (IntPtr jclass, IntPtr jfieldID)
		{
			throw new NotImplementedException ();
		}

		public static char GetStaticCharField (IntPtr jclass, IntPtr jfieldID)
		{
			throw new NotImplementedException ();
		}

		public static short GetStaticShortField (IntPtr jclass, IntPtr jfieldID)
		{
			throw new NotImplementedException ();
		}

		public static int GetStaticIntField (IntPtr jclass, IntPtr jfieldID)
		{
			throw new NotImplementedException ();
		}

		public static long GetStaticLongField (IntPtr jclass, IntPtr jfieldID)
		{
			throw new NotImplementedException ();
		}

		public static float GetStaticFloatField (IntPtr jclass, IntPtr jfieldID)
		{
			throw new NotImplementedException ();
		}

		public static double GetStaticDoubleField (IntPtr jclass, IntPtr jfieldID)
		{
			throw new NotImplementedException ();
		}

		public static void SetStaticField (IntPtr jclass, IntPtr jfieldID, IntPtr val)
		{
			throw new NotImplementedException ();
		}

		public static void SetStaticField (IntPtr jclass, IntPtr jfieldID, bool val)
		{
			throw new NotImplementedException ();
		}

		public static void SetStaticField (IntPtr jclass, IntPtr jfieldID, sbyte val)
		{
			throw new NotImplementedException ();
		}

		public static void SetStaticField (IntPtr jclass, IntPtr jfieldID, char val)
		{
			throw new NotImplementedException ();
		}

		public static void SetStaticField (IntPtr jclass, IntPtr jfieldID, short val)
		{
			throw new NotImplementedException ();
		}

		public static void SetStaticField (IntPtr jclass, IntPtr jfieldID, int val)
		{
			throw new NotImplementedException ();
		}

		public static void SetStaticField (IntPtr jclass, IntPtr jfieldID, long val)
		{
			throw new NotImplementedException ();
		}

		public static void SetStaticField (IntPtr jclass, IntPtr jfieldID, float val)
		{
			throw new NotImplementedException ();
		}

		public static void SetStaticField (IntPtr jclass, IntPtr jfieldID, double val)
		{
			throw new NotImplementedException ();
		}

		[UnmanagedFunctionPointerAttribute (CallingConvention.Cdecl, CharSet=CharSet.Unicode)]
		internal delegate IntPtr IntPtr_IntPtr_int_IntPtr_Delegate (IntPtr env, IntPtr unicodeChars, int len);

		internal static int GetStringLength (IntPtr @string)
		{
			throw new NotImplementedException ();
		}

		internal static IntPtr GetStringChars (IntPtr @string, IntPtr isCopy)
		{
			throw new NotImplementedException ();
		}

		internal static void ReleaseStringChars (IntPtr @string, IntPtr chars)
		{
			throw new NotImplementedException ();
		}



		internal delegate string IntPtr_IntPtr_IntPtr_string_Delegate (IntPtr env, IntPtr @string, IntPtr isCopy);

		internal delegate void IntPtr_IntPtr_string_void_Delegate (IntPtr env, IntPtr @string, string utf);


		internal delegate IntPtr IntPtr_int_IntPtr_IntPtr_IntPtr_Delegate (IntPtr env, int length, IntPtr elementClass, IntPtr initialElement);

		public static IntPtr GetObjectArrayElement (IntPtr array, int index)
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_IntPtr_int_IntPtr_void_Delegate (IntPtr env, IntPtr array, int index, IntPtr value);
		public static void SetObjectArrayElement (IntPtr array, int index, IntPtr value)
		{
			throw new NotImplementedException ();

		}

		internal delegate IntPtr IntPtr_int_IntPtr_Delegate (IntPtr env, int length);

		public static IntPtr NewArray (byte[] array)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr NewArray (char[] array)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr NewArray (short[] array)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr NewArray (int[] array)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr NewArray (long[] array)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr NewArray (float[] array)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr NewArray (double[] array)
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_IntPtr_int_int_byteArray_void_Delegate (IntPtr env, IntPtr array, int start, int len, byte[] buf);
		internal static void GetBooleanArrayRegion (IntPtr array, int start, int len, byte[] buf)
		{
			throw new NotImplementedException ();
		}

		public static void CopyArray (IntPtr src, byte[] dest)
		{
			throw new NotImplementedException ();
		}

		[UnmanagedFunctionPointerAttribute (CallingConvention.Cdecl, CharSet=CharSet.Unicode)]
		internal delegate void IntPtr_IntPtr_int_int_charArray_void_Delegate (IntPtr env, IntPtr array, int start, int len, char[] buf);
		public static void CopyArray (IntPtr src, char[] dest)
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_IntPtr_int_int_shortArray_void_Delegate (IntPtr env, IntPtr array, int start, int len, short[] buf);
		public static void CopyArray (IntPtr src, short[] dest)
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_IntPtr_int_int_intArray_void_Delegate (IntPtr env, IntPtr array, int start, int len, int[] buf);
		public static void CopyArray (IntPtr src, int[] dest)
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_IntPtr_int_int_longArray_void_Delegate (IntPtr env, IntPtr array, int start, int len, long[] buf);
		public static void CopyArray (IntPtr src, long[] dest)
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_IntPtr_int_int_floatArray_void_Delegate (IntPtr env, IntPtr array, int start, int len, float[] buf);
		public static void CopyArray (IntPtr src, float[] dest)
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_IntPtr_int_int_doubleArray_void_Delegate (IntPtr env, IntPtr array, int start, int len, double[] buf);
		public static void CopyArray (IntPtr src, double[] dest)
		{
			throw new NotImplementedException ();
		}

		internal static void SetBooleanArrayRegion (IntPtr array, int start, int len, byte[] buf)
		{
			throw new NotImplementedException ();
		}

		public static void CopyArray (byte[] src, IntPtr dest)
		{
			throw new NotImplementedException ();
		}

		public static void CopyArray (char[] src, IntPtr dest)
		{
			throw new NotImplementedException ();
		}

		public static void CopyArray (short[] src, IntPtr dest)
		{
			throw new NotImplementedException ();
		}

		public static void CopyArray (int[] src, IntPtr dest)
		{
			throw new NotImplementedException ();
		}

		public static void CopyArray (long[] src, IntPtr dest)
		{
			throw new NotImplementedException ();
		}

		public static void CopyArray (float[] src, IntPtr dest)
		{
			throw new NotImplementedException ();
		}

		public static void CopyArray (double[] src, IntPtr dest)
		{
			throw new NotImplementedException ();
		}

		internal delegate int IntPtr_IntPtr_JNINativeMethodArray_int_int_Delegate (IntPtr env, IntPtr jclass, JNINativeMethod [] methods, int nMethods);
		internal static int RegisterNatives (IntPtr jclass, JNINativeMethod [] methods, int nMethods)
		{
			throw new NotImplementedException ();
		}




		internal delegate int IntPtr_outIntPtr_int_Delegate (IntPtr env, out IntPtr vm);
		internal static int GetJavaVM (out IntPtr vm)
		{
			throw new NotImplementedException ();
		}

		internal delegate void IntPtr_IntPtr_int_int_IntPtr_void_Delegate (IntPtr env, IntPtr str, int start, int len, IntPtr buf);

		internal delegate bool IntPtr_bool_Delegate (IntPtr env);

		internal delegate IntPtr IntPtr_IntPtr_long_IntPtr_Delegate (IntPtr env, IntPtr address, long capacity);
		public static IntPtr NewDirectByteBuffer (IntPtr address, long capacity)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr GetDirectBufferAddress (IntPtr buf)
		{
			throw new NotImplementedException ();
		}

		internal delegate long IntPtr_IntPtr_long_Delegate (IntPtr env, IntPtr buf);
		public static long GetDirectBufferCapacity (IntPtr buf)
		{
			throw new NotImplementedException ();
		}

	}

	[StructLayout (LayoutKind.Sequential)]
	partial struct JniNativeInterfaceStruct {

		private IntPtr  reserved0;                      // void*
		private IntPtr  reserved1;                      // void*
		private IntPtr  reserved2;                      // void*
		private IntPtr  reserved3;                      // void*
		public  IntPtr  GetVersion;                     // jint        (*GetVersion)(JNIEnv*);
		public  IntPtr  DefineClass;                    // jclass      (*DefineClass)(JNIEnv*, const char, jobject, const jbyte*, jsize);
		public  IntPtr  _FindClass;                     // jclass      (*FindClass)(JNIEnv*, const char*);
		public  IntPtr  FromReflectedMethod;            // jmethodID   (*FromReflectedMethod)(JNIEnv*, jobject);
		public  IntPtr  FromReflectedField;             // jfieldID    (*FromReflectedField)(JNIEnv*, jobject);
		public  IntPtr  ToReflectedMethod;              // jobject     (*ToReflectedMethod)(JNIEnv*, jclass, jmethodID, jboolean);
		public  IntPtr  GetSuperclass;                  // jclass      (*GetSuperclass)(JNIEnv*, jclass);
		public  IntPtr  IsAssignableFrom;               // jboolean    (*IsAssignableFrom)(JNIEnv*, jclass, jclass);
		public  IntPtr  ToReflectedField;               // jobject     (*ToReflectedField)(JNIEnv*, jclass, jfieldID, jboolean);
		public  IntPtr  Throw;                          // jint        (*Throw)(JNIEnv*, jthrowable);
		public  IntPtr  ThrowNew;                       // jint        (*ThrowNew)(JNIEnv*, jclass, const char*);
		public  IntPtr  ExceptionOccurred;              // jthrowable  (*ExceptionOccurred)(JNIEnv*);
		public  IntPtr  ExceptionDescribe;              // void        (*ExceptionDescribe)(JNIEnv*);
		public  IntPtr  ExceptionClear;                 // void        (*ExceptionClear)(JNIEnv*);
		public  IntPtr  FatalError;                     // void        (*FatalError)(JNIEnv*, const char*);
		public  IntPtr  _PushLocalFrame;                // jint        (*PushLocalFrame)(JNIEnv*, jint);
		public  IntPtr  PopLocalFrame;                  // jobject     (*PopLocalFrame)(JNIEnv*, jobject);
		public  IntPtr  NewGlobalRef;                   // jobject     (*NewGlobalRef)(JNIEnv*, jobject);
		public  IntPtr  DeleteGlobalRef;                // void        (*DeleteGlobalRef)(JNIEnv*, jobject);
		public  IntPtr  DeleteLocalRef;                 // void        (*DeleteLocalRef)(JNIEnv*, jobject);
		public  IntPtr  IsSameObject;                   // jboolean    (*IsSameObject)(JNIEnv*, jobject, jobject);
		public  IntPtr  NewLocalRef;                    // jobject     (*NewLocalRef)(JNIEnv*, jobject);
		public  IntPtr  _EnsureLocalCapacity;           // jint        (*EnsureLocalCapacity)(JNIEnv*, jint);
		public  IntPtr  AllocObject;                    // jobject     (*AllocObject)(JNIEnv*, jclass);
		public  IntPtr  NewObject;                      // jobject     (*NewObject)(JNIEnv*, jclass, jmethodID, ...);
		public  IntPtr  NewObjectV;                     // jobject     (*NewObjectV)(JNIEnv*, jclass, jmethodID, va_list);
		public  IntPtr  NewObjectA;                     // jobject     (*NewObjectA)(JNIEnv*, jclass, jmethodID, jvalue*);
		public  IntPtr  GetObjectClass;                 // jclass      (*GetObjectClass)(JNIEnv*, jobject);
		public  IntPtr  IsInstanceOf;                   // jboolean    (*IsInstanceOf)(JNIEnv*, jobject, jclass);
		public  IntPtr  GetMethodID;                    // jmethodID   (*GetMethodID)(JNIEnv*, jclass, const char*, const char*);
		public  IntPtr  CallObjectMethod;               // jobject     (*CallObjectMethod)(JNIEnv*, jobject, jmethodID, ...);
		public  IntPtr  CallObjectMethodV;              // jobject     (*CallObjectMethodV)(JNIEnv*, jobject, jmethodID, va_list);
		public  IntPtr  CallObjectMethodA;              // jobject     (*CallObjectMethodA)(JNIEnv*, jobject, jmethodID, jvalue*);
		public  IntPtr  CallBooleanMethod;              // jboolean    (*CallBooleanMethod)(JNIEnv*, jobject, jmethodID, ...);
		public  IntPtr  CallBooleanMethodV;             // jboolean    (*CallBooleanMethodV)(JNIEnv*, jobject, jmethodID, va_list);
		public  IntPtr  CallBooleanMethodA;             // jboolean    (*CallBooleanMethodA)(JNIEnv*, jobject, jmethodID, jvalue*);
		public  IntPtr  CallByteMethod;                 // jbyte       (*CallByteMethod)(JNIEnv*, jobject, jmethodID, ...);
		public  IntPtr  CallByteMethodV;                // jbyte       (*CallByteMethodV)(JNIEnv*, jobject, jmethodID, va_list);
		public  IntPtr  CallByteMethodA;                // jbyte       (*CallByteMethodA)(JNIEnv*, jobject, jmethodID, jvalue*);
		public  IntPtr  CallCharMethod;                 // jchar       (*CallCharMethod)(JNIEnv*, jobject, jmethodID, ...);
		public  IntPtr  CallCharMethodV;                // jchar       (*CallCharMethodV)(JNIEnv*, jobject, jmethodID, va_list);
		public  IntPtr  CallCharMethodA;                // jchar       (*CallCharMethodA)(JNIEnv*, jobject, jmethodID, jvalue*);
		public  IntPtr  CallShortMethod;                // jshort      (*CallShortMethod)(JNIEnv*, jobject, jmethodID, ...);
		public  IntPtr  CallShortMethodV;               // jshort      (*CallShortMethodV)(JNIEnv*, jobject, jmethodID, va_list);
		public  IntPtr  CallShortMethodA;               // jshort      (*CallShortMethodA)(JNIEnv*, jobject, jmethodID, jvalue*);
		public  IntPtr  CallIntMethod;                  // jint        (*CallIntMethod)(JNIEnv*, jobject, jmethodID, ...);
		public  IntPtr  CallIntMethodV;                 // jint        (*CallIntMethodV)(JNIEnv*, jobject, jmethodID, va_list);
		public  IntPtr  CallIntMethodA;                 // jint        (*CallIntMethodA)(JNIEnv*, jobject, jmethodID, jvalue*);
		public  IntPtr  CallLongMethod;                 // jlong       (*CallLongMethod)(JNIEnv*, jobject, jmethodID, ...);
		public  IntPtr  CallLongMethodV;                // jlong       (*CallLongMethodV)(JNIEnv*, jobject, jmethodID, va_list);
		public  IntPtr  CallLongMethodA;                // jlong       (*CallLongMethodA)(JNIEnv*, jobject, jmethodID, jvalue*);
		public  IntPtr  CallFloatMethod;                // jfloat      (*CallFloatMethod)(JNIEnv*, jobject, jmethodID, ...);
		public  IntPtr  CallFloatMethodV;               // jfloat      (*CallFloatMethodV)(JNIEnv*, jobject, jmethodID, va_list);
		public  IntPtr  CallFloatMethodA;               // jfloat      (*CallFloatMethodA)(JNIEnv*, jobject, jmethodID, jvalue*);
		public  IntPtr  CallDoubleMethod;               // jdouble     (*CallDoubleMethod)(JNIEnv*, jobject, jmethodID, ...);
		public  IntPtr  CallDoubleMethodV;              // jdouble     (*CallDoubleMethodV)(JNIEnv*, jobject, jmethodID, va_list);
		public  IntPtr  CallDoubleMethodA;              // jdouble     (*CallDoubleMethodA)(JNIEnv*, jobject, jmethodID, jvalue*);
		public  IntPtr  CallVoidMethod;                 // void        (*CallVoidMethod)(JNIEnv*, jobject, jmethodID, ...);
		public  IntPtr  CallVoidMethodV;                // void        (*CallVoidMethodV)(JNIEnv*, jobject, jmethodID, va_list);
		public  IntPtr  CallVoidMethodA;                // void        (*CallVoidMethodA)(JNIEnv*, jobject, jmethodID, jvalue*);
		public  IntPtr  CallNonvirtualObjectMethod;     // jobject     (*CallNonvirtualObjectMethod)(JNIEnv*, jobject, jclass, jmethodID, ...);
		public  IntPtr  CallNonvirtualObjectMethodV;    // jobject     (*CallNonvirtualObjectMethodV)(JNIEnv*, jobject, jclass, jmethodID, va_list);
		public  IntPtr  CallNonvirtualObjectMethodA;    // jobject     (*CallNonvirtualObjectMethodA)(JNIEnv*, jobject, jclass, jmethodID, jvalue*);
		public  IntPtr  CallNonvirtualBooleanMethod;    // jboolean    (*CallNonvirtualBooleanMethod)(JNIEnv*, jobject, jclass, jmethodID, ...);
		public  IntPtr  CallNonvirtualBooleanMethodV;   // jboolean    (*CallNonvirtualBooleanMethodV)(JNIEnv*, jobject, jclass, jmethodID, va_list);
		public  IntPtr  CallNonvirtualBooleanMethodA;   // jboolean    (*CallNonvirtualBooleanMethodA)(JNIEnv*, jobject, jclass, jmethodID, jvalue*);
		public  IntPtr  CallNonvirtualByteMethod;       // jbyte       (*CallNonvirtualByteMethod)(JNIEnv*, jobject, jclass, jmethodID, ...);
		public  IntPtr  CallNonvirtualByteMethodV;      // jbyte       (*CallNonvirtualByteMethodV)(JNIEnv*, jobject, jclass, jmethodID, va_list);
		public  IntPtr  CallNonvirtualByteMethodA;      // jbyte       (*CallNonvirtualByteMethodA)(JNIEnv*, jobject, jclass, jmethodID, jvalue*);
		public  IntPtr  CallNonvirtualCharMethod;       // jchar       (*CallNonvirtualCharMethod)(JNIEnv*, jobject, jclass, jmethodID, ...);
		public  IntPtr  CallNonvirtualCharMethodV;      // jchar       (*CallNonvirtualCharMethodV)(JNIEnv*, jobject, jclass, jmethodID, va_list);
		public  IntPtr  CallNonvirtualCharMethodA;      // jchar       (*CallNonvirtualCharMethodA)(JNIEnv*, jobject, jclass, jmethodID, jvalue*);
		public  IntPtr  CallNonvirtualShortMethod;      // jshort      (*CallNonvirtualShortMethod)(JNIEnv*, jobject, jclass, jmethodID, ...);
		public  IntPtr  CallNonvirtualShortMethodV;     // jshort      (*CallNonvirtualShortMethodV)(JNIEnv*, jobject, jclass, jmethodID, va_list);
		public  IntPtr  CallNonvirtualShortMethodA;     // jshort      (*CallNonvirtualShortMethodA)(JNIEnv*, jobject, jclass, jmethodID, jvalue*);
		public  IntPtr  CallNonvirtualIntMethod;        // jint        (*CallNonvirtualIntMethod)(JNIEnv*, jobject, jclass, jmethodID, ...);
		public  IntPtr  CallNonvirtualIntMethodV;       // jint        (*CallNonvirtualIntMethodV)(JNIEnv*, jobject, jclass, jmethodID, va_list);
		public  IntPtr  CallNonvirtualIntMethodA;       // jint        (*CallNonvirtualIntMethodA)(JNIEnv*, jobject, jclass, jmethodID, jvalue*);
		public  IntPtr  CallNonvirtualLongMethod;       // jlong       (*CallNonvirtualLongMethod)(JNIEnv*, jobject, jclass, jmethodID, ...);
		public  IntPtr  CallNonvirtualLongMethodV;      // jlong       (*CallNonvirtualLongMethodV)(JNIEnv*, jobject, jclass, jmethodID, va_list);
		public  IntPtr  CallNonvirtualLongMethodA;      // jlong       (*CallNonvirtualLongMethodA)(JNIEnv*, jobject, jclass, jmethodID, jvalue*);
		public  IntPtr  CallNonvirtualFloatMethod;      // jfloat      (*CallNonvirtualFloatMethod)(JNIEnv*, jobject, jclass, jmethodID, ...);
		public  IntPtr  CallNonvirtualFloatMethodV;     // jfloat      (*CallNonvirtualFloatMethodV)(JNIEnv*, jobject, jclass, jmethodID, va_list);
		public  IntPtr  CallNonvirtualFloatMethodA;     // jfloat      (*CallNonvirtualFloatMethodA)(JNIEnv*, jobject, jclass, jmethodID, jvalue*);
		public  IntPtr  CallNonvirtualDoubleMethod;     // jdouble     (*CallNonvirtualDoubleMethod)(JNIEnv*, jobject, jclass, jmethodID, ...);
		public  IntPtr  CallNonvirtualDoubleMethodV;    // jdouble     (*CallNonvirtualDoubleMethodV)(JNIEnv*, jobject, jclass, jmethodID, va_list);
		public  IntPtr  CallNonvirtualDoubleMethodA;    // jdouble     (*CallNonvirtualDoubleMethodA)(JNIEnv*, jobject, jclass, jmethodID, jvalue*);
		public  IntPtr  CallNonvirtualVoidMethod;       // void        (*CallNonvirtualVoidMethod)(JNIEnv*, jobject, jclass, jmethodID, ...);
		public  IntPtr  CallNonvirtualVoidMethodV;      // void        (*CallNonvirtualVoidMethodV)(JNIEnv*, jobject, jclass, jmethodID, va_list);
		public  IntPtr  CallNonvirtualVoidMethodA;      // void        (*CallNonvirtualVoidMethodA)(JNIEnv*, jobject, jclass, jmethodID, jvalue*);
		public  IntPtr  GetFieldID;                     // jfieldID    (*GetFieldID)(JNIEnv*, jclass, const char*, const char*);
		public  IntPtr  GetObjectField;                 // jobject     (*GetObjectField)(JNIEnv*, jobject, jfieldID);
		public  IntPtr  GetBooleanField;                // jboolean    (*GetBooleanField)(JNIEnv*, jobject, jfieldID);
		public  IntPtr  GetByteField;                   // jbyte       (*GetByteField)(JNIEnv*, jobject, jfieldID);
		public  IntPtr  GetCharField;                   // jchar       (*GetCharField)(JNIEnv*, jobject, jfieldID);
		public  IntPtr  GetShortField;                  // jshort      (*GetShortField)(JNIEnv*, jobject, jfieldID);
		public  IntPtr  GetIntField;                    // jint        (*GetIntField)(JNIEnv*, jobject, jfieldID);
		public  IntPtr  GetLongField;                   // jlong       (*GetLongField)(JNIEnv*, jobject, jfieldID);
		public  IntPtr  GetFloatField;                  // jfloat      (*GetFloatField)(JNIEnv*, jobject, jfieldID);
		public  IntPtr  GetDoubleField;                 // jdouble     (*GetDoubleField)(JNIEnv*, jobject, jfieldID);
		public  IntPtr  SetObjectField;                 // void        (*SetObjectField)(JNIEnv*, jobject, jfieldID, jobject);
		public  IntPtr  SetBooleanField;                // void        (*SetBooleanField)(JNIEnv*, jobject, jfieldID, jboolean);
		public  IntPtr  SetByteField;                   // void        (*SetByteField)(JNIEnv*, jobject, jfieldID, jbyte);
		public  IntPtr  SetCharField;                   // void        (*SetCharField)(JNIEnv*, jobject, jfieldID, jchar);
		public  IntPtr  SetShortField;                  // void        (*SetShortField)(JNIEnv*, jobject, jfieldID, jshort);
		public  IntPtr  SetIntField;                    // void        (*SetIntField)(JNIEnv*, jobject, jfieldID, jint);
		public  IntPtr  SetLongField;                   // void        (*SetLongField)(JNIEnv*, jobject, jfieldID, jlong);
		public  IntPtr  SetFloatField;                  // void        (*SetFloatField)(JNIEnv*, jobject, jfieldID, jfloat);
		public  IntPtr  SetDoubleField;                 // void        (*SetDoubleField)(JNIEnv*, jobject, jfieldID, jdouble);
		public  IntPtr  GetStaticMethodID;              // jmethodID   (*GetStaticMethodID)(JNIEnv*, jclass, const char*, const char*);
		public  IntPtr  CallStaticObjectMethod;         // jobject     (*CallStaticObjectMethod)(JNIEnv*, jclass, jmethodID, ...);
		public  IntPtr  CallStaticObjectMethodV;        // jobject     (*CallStaticObjectMethodV)(JNIEnv*, jclass, jmethodID, va_list);
		public  IntPtr  CallStaticObjectMethodA;        // jobject     (*CallStaticObjectMethodA)(JNIEnv*, jclass, jmethodID, jvalue*);
		public  IntPtr  CallStaticBooleanMethod;        // jboolean    (*CallStaticBooleanMethod)(JNIEnv*, jclass, jmethodID, ...);
		public  IntPtr  CallStaticBooleanMethodV;       // jboolean    (*CallStaticBooleanMethodV)(JNIEnv*, jclass, jmethodID, va_list);
		public  IntPtr  CallStaticBooleanMethodA;       // jboolean    (*CallStaticBooleanMethodA)(JNIEnv*, jclass, jmethodID, jvalue*);
		public  IntPtr  CallStaticByteMethod;           // jbyte       (*CallStaticByteMethod)(JNIEnv*, jclass, jmethodID, ...);
		public  IntPtr  CallStaticByteMethodV;          // jbyte       (*CallStaticByteMethodV)(JNIEnv*, jclass, jmethodID, va_list);
		public  IntPtr  CallStaticByteMethodA;          // jbyte       (*CallStaticByteMethodA)(JNIEnv*, jclass, jmethodID, jvalue*);
		public  IntPtr  CallStaticCharMethod;           // jchar       (*CallStaticCharMethod)(JNIEnv*, jclass, jmethodID, ...);
		public  IntPtr  CallStaticCharMethodV;          // jchar       (*CallStaticCharMethodV)(JNIEnv*, jclass, jmethodID, va_list);
		public  IntPtr  CallStaticCharMethodA;          // jchar       (*CallStaticCharMethodA)(JNIEnv*, jclass, jmethodID, jvalue*);
		public  IntPtr  CallStaticShortMethod;          // jshort      (*CallStaticShortMethod)(JNIEnv*, jclass, jmethodID, ...);
		public  IntPtr  CallStaticShortMethodV;         // jshort      (*CallStaticShortMethodV)(JNIEnv*, jclass, jmethodID, va_list);
		public  IntPtr  CallStaticShortMethodA;         // jshort      (*CallStaticShortMethodA)(JNIEnv*, jclass, jmethodID, jvalue*);
		public  IntPtr  CallStaticIntMethod;            // jint        (*CallStaticIntMethod)(JNIEnv*, jclass, jmethodID, ...);
		public  IntPtr  CallStaticIntMethodV;           // jint        (*CallStaticIntMethodV)(JNIEnv*, jclass, jmethodID, va_list);
		public  IntPtr  CallStaticIntMethodA;           // jint        (*CallStaticIntMethodA)(JNIEnv*, jclass, jmethodID, jvalue*);
		public  IntPtr  CallStaticLongMethod;           // jlong       (*CallStaticLongMethod)(JNIEnv*, jclass, jmethodID, ...);
		public  IntPtr  CallStaticLongMethodV;          // jlong       (*CallStaticLongMethodV)(JNIEnv*, jclass, jmethodID, va_list);
		public  IntPtr  CallStaticLongMethodA;          // jlong       (*CallStaticLongMethodA)(JNIEnv*, jclass, jmethodID, jvalue*);
		public  IntPtr  CallStaticFloatMethod;          // jfloat      (*CallStaticFloatMethod)(JNIEnv*, jclass, jmethodID, ...);
		public  IntPtr  CallStaticFloatMethodV;         // jfloat      (*CallStaticFloatMethodV)(JNIEnv*, jclass, jmethodID, va_list);
		public  IntPtr  CallStaticFloatMethodA;         // jfloat      (*CallStaticFloatMethodA)(JNIEnv*, jclass, jmethodID, jvalue*);
		public  IntPtr  CallStaticDoubleMethod;         // jdouble     (*CallStaticDoubleMethod)(JNIEnv*, jclass, jmethodID, ...);
		public  IntPtr  CallStaticDoubleMethodV;        // jdouble     (*CallStaticDoubleMethodV)(JNIEnv*, jclass, jmethodID, va_list);
		public  IntPtr  CallStaticDoubleMethodA;        // jdouble     (*CallStaticDoubleMethodA)(JNIEnv*, jclass, jmethodID, jvalue*);
		public  IntPtr  CallStaticVoidMethod;           // void        (*CallStaticVoidMethod)(JNIEnv*, jclass, jmethodID, ...);
		public  IntPtr  CallStaticVoidMethodV;          // void        (*CallStaticVoidMethodV)(JNIEnv*, jclass, jmethodID, va_list);
		public  IntPtr  CallStaticVoidMethodA;          // void        (*CallStaticVoidMethodA)(JNIEnv*, jclass, jmethodID, jvalue*);
		public  IntPtr  GetStaticFieldID;               // jfieldID    (*GetStaticFieldID)(JNIEnv*, jclass, const char*, const char*);
		public  IntPtr  GetStaticObjectField;           // jobject     (*GetStaticObjectField)(JNIEnv*, jclass, jfieldID);
		public  IntPtr  GetStaticBooleanField;          // jboolean    (*GetStaticBooleanField)(JNIEnv*, jclass, jfieldID);
		public  IntPtr  GetStaticByteField;             // jbyte       (*GetStaticByteField)(JNIEnv*, jclass, jfieldID);
		public  IntPtr  GetStaticCharField;             // jchar       (*GetStaticCharField)(JNIEnv*, jclass, jfieldID);
		public  IntPtr  GetStaticShortField;            // jshort      (*GetStaticShortField)(JNIEnv*, jclass, jfieldID);
		public  IntPtr  GetStaticIntField;              // jint        (*GetStaticIntField)(JNIEnv*, jclass, jfieldID);
		public  IntPtr  GetStaticLongField;             // jlong       (*GetStaticLongField)(JNIEnv*, jclass, jfieldID);
		public  IntPtr  GetStaticFloatField;            // jfloat      (*GetStaticFloatField)(JNIEnv*, jclass, jfieldID);
		public  IntPtr  GetStaticDoubleField;           // jdouble     (*GetStaticDoubleField)(JNIEnv*, jclass, jfieldID);
		public  IntPtr  SetStaticObjectField;           // void        (*SetStaticObjectField)(JNIEnv*, jclass, jfieldID, jobject);
		public  IntPtr  SetStaticBooleanField;          // void        (*SetStaticBooleanField)(JNIEnv*, jclass, jfieldID, jboolean);
		public  IntPtr  SetStaticByteField;             // void        (*SetStaticByteField)(JNIEnv*, jclass, jfieldID, jbyte);
		public  IntPtr  SetStaticCharField;             // void        (*SetStaticCharField)(JNIEnv*, jclass, jfieldID, jchar);
		public  IntPtr  SetStaticShortField;            // void        (*SetStaticShortField)(JNIEnv*, jclass, jfieldID, jshort);
		public  IntPtr  SetStaticIntField;              // void        (*SetStaticIntField)(JNIEnv*, jclass, jfieldID, jint);
		public  IntPtr  SetStaticLongField;             // void        (*SetStaticLongField)(JNIEnv*, jclass, jfieldID, jlong);
		public  IntPtr  SetStaticFloatField;            // void        (*SetStaticFloatField)(JNIEnv*, jclass, jfieldID, jfloat);
		public  IntPtr  SetStaticDoubleField;           // void        (*SetStaticDoubleField)(JNIEnv*, jclass, jfieldID, jdouble);
		public  IntPtr  NewString;                      // jstring     (*NewString)(JNIEnv*, const jchar*, jsize);
		public  IntPtr  GetStringLength;                // jsize       (*GetStringLength)(JNIEnv*, jstring);
		public  IntPtr  GetStringChars;                 // const jchar* (*GetStringChars)(JNIEnv*, jstring, jboolean*);
		public  IntPtr  ReleaseStringChars;             // void        (*ReleaseStringChars)(JNIEnv*, jstring, const jchar*);
		public  IntPtr  NewStringUTF;                   // jstring     (*NewStringUTF)(JNIEnv*, const char*);
		public  IntPtr  GetStringUTFLength;             // jsize       (*GetStringUTFLength)(JNIEnv*, jstring);
		public  IntPtr  GetStringUTFChars;              // const char* (*GetStringUTFChars)(JNIEnv*, jstring, jboolean*);
		public  IntPtr  ReleaseStringUTFChars;          // void        (*ReleaseStringUTFChars)(JNIEnv*, jstring, const char*);
		public  IntPtr  GetArrayLength;                 // jsize       (*GetArrayLength)(JNIEnv*, jarray);
		public  IntPtr  NewObjectArray;                 // jobjectArray (*NewObjectArray)(JNIEnv*, jsize, jclass, jobject);
		public  IntPtr  GetObjectArrayElement;          // jobject     (*GetObjectArrayElement)(JNIEnv*, jobjectArray, jsize);
		public  IntPtr  SetObjectArrayElement;          // void        (*SetObjectArrayElement)(JNIEnv*, jobjectArray, jsize, jobject);
		public  IntPtr  NewBooleanArray;                // jbooleanArray (*NewBooleanArray)(JNIEnv*, jsize);
		public  IntPtr  NewByteArray;                   // jbyteArray  (*NewByteArray)(JNIEnv*, jsize);
		public  IntPtr  NewCharArray;                   // jcharArray  (*NewCharArray)(JNIEnv*, jsize);
		public  IntPtr  NewShortArray;                  // jshortArray (*NewShortArray)(JNIEnv*, jsize);
		public  IntPtr  NewIntArray;                    // jintArray   (*NewIntArray)(JNIEnv*, jsize);
		public  IntPtr  NewLongArray;                   // jlongArray  (*NewLongArray)(JNIEnv*, jsize);
		public  IntPtr  NewFloatArray;                  // jfloatArray (*NewFloatArray)(JNIEnv*, jsize);
		public  IntPtr  NewDoubleArray;                 // jdoubleArray (*NewDoubleArray)(JNIEnv*, jsize);
		public  IntPtr  GetBooleanArrayElements;        // jboolean*   (*GetBooleanArrayElements)(JNIEnv*, jbooleanArray, jboolean*);
		public  IntPtr  GetByteArrayElements;           // jbyte*      (*GetByteArrayElements)(JNIEnv*, jbyteArray, jboolean*);
		public  IntPtr  GetCharArrayElements;           // jchar*      (*GetCharArrayElements)(JNIEnv*, jcharArray, jboolean*);
		public  IntPtr  GetShortArrayElements;          // jshort*     (*GetShortArrayElements)(JNIEnv*, jshortArray, jboolean*);
		public  IntPtr  GetIntArrayElements;            // jint*       (*GetIntArrayElements)(JNIEnv*, jintArray, jboolean*);
		public  IntPtr  GetLongArrayElements;           // jlong*      (*GetLongArrayElements)(JNIEnv*, jlongArray, jboolean*);
		public  IntPtr  GetFloatArrayElements;          // jfloat*     (*GetFloatArrayElements)(JNIEnv*, jfloatArray, jboolean*);
		public  IntPtr  GetDoubleArrayElements;         // jdouble*    (*GetDoubleArrayElements)(JNIEnv*, jdoubleArray, jboolean*);
		public  IntPtr  ReleaseBooleanArrayElements;    // void        (*ReleaseBooleanArrayElements)(JNIEnv*, jbooleanArray, jboolean*, jint);
		public  IntPtr  ReleaseByteArrayElements;       // void        (*ReleaseByteArrayElements)(JNIEnv*, jbyteArray, jbyte*, jint);
		public  IntPtr  ReleaseCharArrayElements;       // void        (*ReleaseCharArrayElements)(JNIEnv*, jcharArray, jchar*, jint);
		public  IntPtr  ReleaseShortArrayElements;      // void        (*ReleaseShortArrayElements)(JNIEnv*, jshortArray, jshort*, jint);
		public  IntPtr  ReleaseIntArrayElements;        // void        (*ReleaseIntArrayElements)(JNIEnv*, jintArray, jint*, jint);
		public  IntPtr  ReleaseLongArrayElements;       // void        (*ReleaseLongArrayElements)(JNIEnv*, jlongArray, jlong*, jint);
		public  IntPtr  ReleaseFloatArrayElements;      // void        (*ReleaseFloatArrayElements)(JNIEnv*, jfloatArray, jfloat*, jint);
		public  IntPtr  ReleaseDoubleArrayElements;     // void        (*ReleaseDoubleArrayElements)(JNIEnv*, jdoubleArray, jdouble*, jint);
		public  IntPtr  GetBooleanArrayRegion;          // void        (*GetBooleanArrayRegion)(JNIEnv*, jbooleanArray, jsize, jsize, jboolean*);
		public  IntPtr  GetByteArrayRegion;             // void        (*GetByteArrayRegion)(JNIEnv*, jbyteArray, jsize, jsize, jbyte*);
		public  IntPtr  GetCharArrayRegion;             // void        (*GetCharArrayRegion)(JNIEnv*, jcharArray, jsize, jsize, jchar*);
		public  IntPtr  GetShortArrayRegion;            // void        (*GetShortArrayRegion)(JNIEnv*, jshortArray, jsize, jsize, jshort*);
		public  IntPtr  GetIntArrayRegion;              // void        (*GetIntArrayRegion)(JNIEnv*, jintArray, jsize, jsize, jint*);
		public  IntPtr  GetLongArrayRegion;             // void        (*GetLongArrayRegion)(JNIEnv*, jlongArray, jsize, jsize, jlong*);
		public  IntPtr  GetFloatArrayRegion;            // void        (*GetFloatArrayRegion)(JNIEnv*, jfloatArray, jsize, jsize, jfloat*);
		public  IntPtr  GetDoubleArrayRegion;           // void        (*GetDoubleArrayRegion)(JNIEnv*, jdoubleArray, jsize, jsize, jdouble*);
		public  IntPtr  SetBooleanArrayRegion;          // void        (*SetBooleanArrayRegion)(JNIEnv*, jbooleanArray, jsize, jsize, const jboolean*);
		public  IntPtr  SetByteArrayRegion;             // void        (*SetByteArrayRegion)(JNIEnv*, jbyteArray, jsize, jsize, const jbyte*);
		public  IntPtr  SetCharArrayRegion;             // void        (*SetCharArrayRegion)(JNIEnv*, jcharArray, jsize, jsize, const jchar*);
		public  IntPtr  SetShortArrayRegion;            // void        (*SetShortArrayRegion)(JNIEnv*, jshortArray, jsize, jsize, const jshort*);
		public  IntPtr  SetIntArrayRegion;              // void        (*SetIntArrayRegion)(JNIEnv*, jintArray, jsize, jsize, const jint*);
		public  IntPtr  SetLongArrayRegion;             // void        (*SetLongArrayRegion)(JNIEnv*, jlongArray, jsize, jsize, const jlong*);
		public  IntPtr  SetFloatArrayRegion;            // void        (*SetFloatArrayRegion)(JNIEnv*, jfloatArray, jsize, jsize, const jfloat*);
		public  IntPtr  SetDoubleArrayRegion;           // void        (*SetDoubleArrayRegion)(JNIEnv*, jdoubleArray, jsize, jsize, const jdouble*);
		public  IntPtr  RegisterNatives;                // jint        (*RegisterNatives)(JNIEnv*, jclass, const JNINativeMethod*, jint);
		public  IntPtr  UnregisterNatives;              // jint        (*UnregisterNatives)(JNIEnv*, jclass);
		public  IntPtr  MonitorEnter;                   // jint        (*MonitorEnter)(JNIEnv*, jobject);
		public  IntPtr  MonitorExit;                    // jint        (*MonitorExit)(JNIEnv*, jobject);
		public  IntPtr  GetJavaVM;                      // jint        (*GetJavaVM)(JNIEnv*, JavaVM**);
		public  IntPtr  GetStringRegion;                // void        (*GetStringRegion)(JNIEnv*, jstring, jsize, jsize, jchar*);
		public  IntPtr  GetStringUTFRegion;             // void        (*GetStringUTFRegion)(JNIEnv*, jstring, jsize, jsize, char*);
		public  IntPtr  GetPrimitiveArrayCritical;      // void*       (*GetPrimitiveArrayCritical)(JNIEnv*, jarray, jboolean*);
		public  IntPtr  ReleasePrimitiveArrayCritical;  // void        (*ReleasePrimitiveArrayCritical)(JNIEnv*, jarray, void*, jint);
		public  IntPtr  GetStringCritical;              // const jchar* (*GetStringCritical)(JNIEnv*, jstring, jboolean*);
		public  IntPtr  ReleaseStringCritical;          // void        (*ReleaseStringCritical)(JNIEnv*, jstring, const jchar*);
		public  IntPtr  NewWeakGlobalRef;               // jweak       (*NewWeakGlobalRef)(JNIEnv*, jobject);
		public  IntPtr  DeleteWeakGlobalRef;            // void        (*DeleteWeakGlobalRef)(JNIEnv*, jweak);
		public  IntPtr  ExceptionCheck;                 // jboolean    (*ExceptionCheck)(JNIEnv*);
		public  IntPtr  NewDirectByteBuffer;            // jobject     (*NewDirectByteBuffer)(JNIEnv*, void*, jlong);
		public  IntPtr  GetDirectBufferAddress;         // void*       (*GetDirectBufferAddress)(JNIEnv*, jobject);
		public  IntPtr  GetDirectBufferCapacity;        // jlong       (*GetDirectBufferCapacity)(JNIEnv*, jobject);
		public  IntPtr  GetObjectRefType;               // jobjectRefType (*GetObjectRefType)(JNIEnv*, jobject);
	}

	partial class JniNativeInterfaceInvoker {

		JniNativeInterfaceStruct JniEnv;

		public unsafe JniNativeInterfaceInvoker (JniNativeInterfaceStruct* p)
		{
			JniEnv = *p;
		}


		JNIEnv.IntPtr_int_Delegate _GetVersion;
		public JNIEnv.IntPtr_int_Delegate GetVersion {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_string_IntPtr_IntPtr_int_IntPtr_Delegate _DefineClass;
		public JNIEnv.IntPtr_string_IntPtr_IntPtr_int_IntPtr_Delegate DefineClass {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_string_IntPtr_Delegate __FindClass;
		public JNIEnv.IntPtr_string_IntPtr_Delegate _FindClass {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_Delegate _FromReflectedMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_Delegate FromReflectedMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_Delegate _FromReflectedField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_Delegate FromReflectedField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_bool_IntPtr_Delegate _ToReflectedMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_bool_IntPtr_Delegate ToReflectedMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_Delegate _GetSuperclass;
		public JNIEnv.IntPtr_IntPtr_IntPtr_Delegate GetSuperclass {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_bool_Delegate _IsAssignableFrom;
		public JNIEnv.IntPtr_IntPtr_IntPtr_bool_Delegate IsAssignableFrom {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_bool_IntPtr_Delegate _ToReflectedField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_bool_IntPtr_Delegate ToReflectedField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_Delegate _Throw;
		public JNIEnv.IntPtr_IntPtr_int_Delegate Throw {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_string_int_Delegate _ThrowNew;
		public JNIEnv.IntPtr_IntPtr_string_int_Delegate ThrowNew {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_Delegate _ExceptionOccurred;
		public JNIEnv.IntPtr_IntPtr_Delegate ExceptionOccurred {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_void_Delegate _ExceptionDescribe;
		public JNIEnv.IntPtr_void_Delegate ExceptionDescribe {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_void_Delegate _ExceptionClear;
		public JNIEnv.IntPtr_void_Delegate ExceptionClear {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_string_void_Delegate _FatalError;
		public JNIEnv.IntPtr_string_void_Delegate FatalError {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_int_int_Delegate __PushLocalFrame;
		public JNIEnv.IntPtr_int_int_Delegate _PushLocalFrame {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_Delegate _PopLocalFrame;
		public JNIEnv.IntPtr_IntPtr_IntPtr_Delegate PopLocalFrame {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_Delegate _NewGlobalRef;
		public JNIEnv.IntPtr_IntPtr_IntPtr_Delegate NewGlobalRef {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_void_Delegate _DeleteGlobalRef;
		public JNIEnv.IntPtr_IntPtr_void_Delegate DeleteGlobalRef {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_void_Delegate _DeleteLocalRef;
		public JNIEnv.IntPtr_IntPtr_void_Delegate DeleteLocalRef {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_bool_Delegate _IsSameObject;
		public JNIEnv.IntPtr_IntPtr_IntPtr_bool_Delegate IsSameObject {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_Delegate _NewLocalRef;
		public JNIEnv.IntPtr_IntPtr_IntPtr_Delegate NewLocalRef {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_int_int_Delegate __EnsureLocalCapacity;
		public JNIEnv.IntPtr_int_int_Delegate _EnsureLocalCapacity {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_Delegate _AllocObject;
		public JNIEnv.IntPtr_IntPtr_IntPtr_Delegate AllocObject {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate _NewObject;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate NewObject {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_IntPtr_Delegate _NewObjectA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_IntPtr_Delegate NewObjectA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_Delegate _GetObjectClass;
		public JNIEnv.IntPtr_IntPtr_IntPtr_Delegate GetObjectClass {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_bool_Delegate _IsInstanceOf;
		public JNIEnv.IntPtr_IntPtr_IntPtr_bool_Delegate IsInstanceOf {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_string_string_IntPtr_Delegate _GetMethodID;
		public JNIEnv.IntPtr_IntPtr_string_string_IntPtr_Delegate GetMethodID {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate _CallObjectMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate CallObjectMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_IntPtr_Delegate _CallObjectMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_IntPtr_Delegate CallObjectMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_bool_Delegate _CallBooleanMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_bool_Delegate CallBooleanMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_bool_Delegate _CallBooleanMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_bool_Delegate CallBooleanMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_sbyte_Delegate _CallByteMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_sbyte_Delegate CallByteMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_sbyte_Delegate _CallByteMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_sbyte_Delegate CallByteMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_char_Delegate _CallCharMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_char_Delegate CallCharMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_char_Delegate _CallCharMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_char_Delegate CallCharMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_short_Delegate _CallShortMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_short_Delegate CallShortMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_short_Delegate _CallShortMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_short_Delegate CallShortMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_int_Delegate _CallIntMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_int_Delegate CallIntMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_int_Delegate _CallIntMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_int_Delegate CallIntMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_long_Delegate _CallLongMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_long_Delegate CallLongMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_long_Delegate _CallLongMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_long_Delegate CallLongMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_float_Delegate _CallFloatMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_float_Delegate CallFloatMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_float_Delegate _CallFloatMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_float_Delegate CallFloatMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_double_Delegate _CallDoubleMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_double_Delegate CallDoubleMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_double_Delegate _CallDoubleMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_double_Delegate CallDoubleMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_void_Delegate _CallVoidMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_void_Delegate CallVoidMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_void_Delegate _CallVoidMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_void_Delegate CallVoidMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_IntPtr_Delegate _CallNonvirtualObjectMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_IntPtr_Delegate CallNonvirtualObjectMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_IntPtr_Delegate _CallNonvirtualObjectMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_IntPtr_Delegate CallNonvirtualObjectMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_bool_Delegate _CallNonvirtualBooleanMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_bool_Delegate CallNonvirtualBooleanMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_bool_Delegate _CallNonvirtualBooleanMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_bool_Delegate CallNonvirtualBooleanMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_sbyte_Delegate _CallNonvirtualByteMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_sbyte_Delegate CallNonvirtualByteMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_sbyte_Delegate _CallNonvirtualByteMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_sbyte_Delegate CallNonvirtualByteMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_char_Delegate _CallNonvirtualCharMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_char_Delegate CallNonvirtualCharMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_char_Delegate _CallNonvirtualCharMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_char_Delegate CallNonvirtualCharMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_short_Delegate _CallNonvirtualShortMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_short_Delegate CallNonvirtualShortMethod {
			get {
				if (_CallNonvirtualShortMethod == null)
					_CallNonvirtualShortMethod = (JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_short_Delegate) Marshal.GetDelegateForFunctionPointer (JniEnv.CallNonvirtualShortMethod, typeof (JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_short_Delegate));
				return _CallNonvirtualShortMethod;
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_short_Delegate _CallNonvirtualShortMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_short_Delegate CallNonvirtualShortMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_int_Delegate _CallNonvirtualIntMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_int_Delegate CallNonvirtualIntMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_int_Delegate _CallNonvirtualIntMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_int_Delegate CallNonvirtualIntMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_long_Delegate _CallNonvirtualLongMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_long_Delegate CallNonvirtualLongMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_long_Delegate _CallNonvirtualLongMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_long_Delegate CallNonvirtualLongMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_float_Delegate _CallNonvirtualFloatMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_float_Delegate CallNonvirtualFloatMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_float_Delegate _CallNonvirtualFloatMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_float_Delegate CallNonvirtualFloatMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_double_Delegate _CallNonvirtualDoubleMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_double_Delegate CallNonvirtualDoubleMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_double_Delegate _CallNonvirtualDoubleMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_double_Delegate CallNonvirtualDoubleMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_void_Delegate _CallNonvirtualVoidMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_void_Delegate CallNonvirtualVoidMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_void_Delegate _CallNonvirtualVoidMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_JValueArray_void_Delegate CallNonvirtualVoidMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_string_string_IntPtr_Delegate _GetFieldID;
		public JNIEnv.IntPtr_IntPtr_string_string_IntPtr_Delegate GetFieldID {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate _GetObjectField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate GetObjectField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_bool_Delegate _GetBooleanField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_bool_Delegate GetBooleanField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_sbyte_Delegate _GetByteField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_sbyte_Delegate GetByteField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_char_Delegate _GetCharField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_char_Delegate GetCharField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_short_Delegate _GetShortField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_short_Delegate GetShortField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_int_Delegate _GetIntField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_int_Delegate GetIntField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_long_Delegate _GetLongField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_long_Delegate GetLongField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_float_Delegate _GetFloatField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_float_Delegate GetFloatField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_double_Delegate _GetDoubleField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_double_Delegate GetDoubleField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_void_Delegate _SetObjectField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_void_Delegate SetObjectField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_bool_void_Delegate _SetBooleanField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_bool_void_Delegate SetBooleanField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_sbyte_void_Delegate _SetByteField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_sbyte_void_Delegate SetByteField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_char_void_Delegate _SetCharField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_char_void_Delegate SetCharField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_short_void_Delegate _SetShortField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_short_void_Delegate SetShortField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate _SetIntField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate SetIntField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_long_void_Delegate _SetLongField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_long_void_Delegate SetLongField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_float_void_Delegate _SetFloatField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_float_void_Delegate SetFloatField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_double_void_Delegate _SetDoubleField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_double_void_Delegate SetDoubleField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_string_string_IntPtr_Delegate _GetStaticMethodID;
		public JNIEnv.IntPtr_IntPtr_string_string_IntPtr_Delegate GetStaticMethodID {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate _CallStaticObjectMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate CallStaticObjectMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_IntPtr_Delegate _CallStaticObjectMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_IntPtr_Delegate CallStaticObjectMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_bool_Delegate _CallStaticBooleanMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_bool_Delegate CallStaticBooleanMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_bool_Delegate _CallStaticBooleanMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_bool_Delegate CallStaticBooleanMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_sbyte_Delegate _CallStaticByteMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_sbyte_Delegate CallStaticByteMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_sbyte_Delegate _CallStaticByteMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_sbyte_Delegate CallStaticByteMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_char_Delegate _CallStaticCharMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_char_Delegate CallStaticCharMethod {
			get {
				if (_CallStaticCharMethod == null)
					_CallStaticCharMethod = (JNIEnv.IntPtr_IntPtr_IntPtr_char_Delegate) Marshal.GetDelegateForFunctionPointer (JniEnv.CallStaticCharMethod, typeof (JNIEnv.IntPtr_IntPtr_IntPtr_char_Delegate));
				return _CallStaticCharMethod;
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_char_Delegate _CallStaticCharMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_char_Delegate CallStaticCharMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_short_Delegate _CallStaticShortMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_short_Delegate CallStaticShortMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_short_Delegate _CallStaticShortMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_short_Delegate CallStaticShortMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_int_Delegate _CallStaticIntMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_int_Delegate CallStaticIntMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_int_Delegate _CallStaticIntMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_int_Delegate CallStaticIntMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_long_Delegate _CallStaticLongMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_long_Delegate CallStaticLongMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_long_Delegate _CallStaticLongMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_long_Delegate CallStaticLongMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_float_Delegate _CallStaticFloatMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_float_Delegate CallStaticFloatMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_float_Delegate _CallStaticFloatMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_float_Delegate CallStaticFloatMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_double_Delegate _CallStaticDoubleMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_double_Delegate CallStaticDoubleMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_double_Delegate _CallStaticDoubleMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_double_Delegate CallStaticDoubleMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_void_Delegate _CallStaticVoidMethod;
		public JNIEnv.IntPtr_IntPtr_IntPtr_void_Delegate CallStaticVoidMethod {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_void_Delegate _CallStaticVoidMethodA;
		public JNIEnv.IntPtr_IntPtr_IntPtr_JValueArray_void_Delegate CallStaticVoidMethodA {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_string_string_IntPtr_Delegate _GetStaticFieldID;
		public JNIEnv.IntPtr_IntPtr_string_string_IntPtr_Delegate GetStaticFieldID {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate _GetStaticObjectField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate GetStaticObjectField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_bool_Delegate _GetStaticBooleanField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_bool_Delegate GetStaticBooleanField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_sbyte_Delegate _GetStaticByteField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_sbyte_Delegate GetStaticByteField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_char_Delegate _GetStaticCharField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_char_Delegate GetStaticCharField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_short_Delegate _GetStaticShortField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_short_Delegate GetStaticShortField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_int_Delegate _GetStaticIntField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_int_Delegate GetStaticIntField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_long_Delegate _GetStaticLongField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_long_Delegate GetStaticLongField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_float_Delegate _GetStaticFloatField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_float_Delegate GetStaticFloatField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_double_Delegate _GetStaticDoubleField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_double_Delegate GetStaticDoubleField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_void_Delegate _SetStaticObjectField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_void_Delegate SetStaticObjectField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_bool_void_Delegate _SetStaticBooleanField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_bool_void_Delegate SetStaticBooleanField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_sbyte_void_Delegate _SetStaticByteField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_sbyte_void_Delegate SetStaticByteField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_char_void_Delegate _SetStaticCharField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_char_void_Delegate SetStaticCharField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_short_void_Delegate _SetStaticShortField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_short_void_Delegate SetStaticShortField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate _SetStaticIntField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate SetStaticIntField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_long_void_Delegate _SetStaticLongField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_long_void_Delegate SetStaticLongField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_float_void_Delegate _SetStaticFloatField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_float_void_Delegate SetStaticFloatField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_double_void_Delegate _SetStaticDoubleField;
		public JNIEnv.IntPtr_IntPtr_IntPtr_double_void_Delegate SetStaticDoubleField {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_IntPtr_Delegate _NewString;
		public JNIEnv.IntPtr_IntPtr_int_IntPtr_Delegate NewString {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_Delegate _GetStringLength;
		public JNIEnv.IntPtr_IntPtr_int_Delegate GetStringLength {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate _GetStringChars;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate GetStringChars {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_void_Delegate _ReleaseStringChars;
		public JNIEnv.IntPtr_IntPtr_IntPtr_void_Delegate ReleaseStringChars {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_string_IntPtr_Delegate _NewStringUTF;
		public JNIEnv.IntPtr_string_IntPtr_Delegate NewStringUTF {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_Delegate _GetStringUTFLength;
		public JNIEnv.IntPtr_IntPtr_int_Delegate GetStringUTFLength {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_string_Delegate _GetStringUTFChars;
		public JNIEnv.IntPtr_IntPtr_IntPtr_string_Delegate GetStringUTFChars {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_string_void_Delegate _ReleaseStringUTFChars;
		public JNIEnv.IntPtr_IntPtr_string_void_Delegate ReleaseStringUTFChars {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_Delegate _GetArrayLength;
		public JNIEnv.IntPtr_IntPtr_int_Delegate GetArrayLength {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_int_IntPtr_IntPtr_IntPtr_Delegate _NewObjectArray;
		public JNIEnv.IntPtr_int_IntPtr_IntPtr_IntPtr_Delegate NewObjectArray {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_IntPtr_Delegate _GetObjectArrayElement;
		public JNIEnv.IntPtr_IntPtr_int_IntPtr_Delegate GetObjectArrayElement {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_IntPtr_void_Delegate _SetObjectArrayElement;
		public JNIEnv.IntPtr_IntPtr_int_IntPtr_void_Delegate SetObjectArrayElement {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_int_IntPtr_Delegate _NewBooleanArray;
		public JNIEnv.IntPtr_int_IntPtr_Delegate NewBooleanArray {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_int_IntPtr_Delegate _NewByteArray;
		public JNIEnv.IntPtr_int_IntPtr_Delegate NewByteArray {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_int_IntPtr_Delegate _NewCharArray;
		public JNIEnv.IntPtr_int_IntPtr_Delegate NewCharArray {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_int_IntPtr_Delegate _NewShortArray;
		public JNIEnv.IntPtr_int_IntPtr_Delegate NewShortArray {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_int_IntPtr_Delegate _NewIntArray;
		public JNIEnv.IntPtr_int_IntPtr_Delegate NewIntArray {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_int_IntPtr_Delegate _NewLongArray;
		public JNIEnv.IntPtr_int_IntPtr_Delegate NewLongArray {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_int_IntPtr_Delegate _NewFloatArray;
		public JNIEnv.IntPtr_int_IntPtr_Delegate NewFloatArray {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_int_IntPtr_Delegate _NewDoubleArray;
		public JNIEnv.IntPtr_int_IntPtr_Delegate NewDoubleArray {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate _GetBooleanArrayElements;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate GetBooleanArrayElements {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate _GetByteArrayElements;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate GetByteArrayElements {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate _GetCharArrayElements;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate GetCharArrayElements {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate _GetShortArrayElements;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate GetShortArrayElements {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate _GetIntArrayElements;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate GetIntArrayElements {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate _GetLongArrayElements;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate GetLongArrayElements {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate _GetFloatArrayElements;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate GetFloatArrayElements {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate _GetDoubleArrayElements;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate GetDoubleArrayElements {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate _ReleaseBooleanArrayElements;
		public JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate ReleaseBooleanArrayElements {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate _ReleaseByteArrayElements;
		public JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate ReleaseByteArrayElements {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate _ReleaseCharArrayElements;
		public JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate ReleaseCharArrayElements {
			get {
				if (_ReleaseCharArrayElements == null)
					_ReleaseCharArrayElements = (JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate) Marshal.GetDelegateForFunctionPointer (JniEnv.ReleaseCharArrayElements, typeof (JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate));
				return _ReleaseCharArrayElements;
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate _ReleaseShortArrayElements;
		public JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate ReleaseShortArrayElements {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate _ReleaseIntArrayElements;
		public JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate ReleaseIntArrayElements {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate _ReleaseLongArrayElements;
		public JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate ReleaseLongArrayElements {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate _ReleaseFloatArrayElements;
		public JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate ReleaseFloatArrayElements {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate _ReleaseDoubleArrayElements;
		public JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate ReleaseDoubleArrayElements {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_int_byteArray_void_Delegate _GetBooleanArrayRegion;
		public JNIEnv.IntPtr_IntPtr_int_int_byteArray_void_Delegate GetBooleanArrayRegion {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_int_byteArray_void_Delegate _GetByteArrayRegion;
		public JNIEnv.IntPtr_IntPtr_int_int_byteArray_void_Delegate GetByteArrayRegion {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_int_charArray_void_Delegate _GetCharArrayRegion;
		public JNIEnv.IntPtr_IntPtr_int_int_charArray_void_Delegate GetCharArrayRegion {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_int_shortArray_void_Delegate _GetShortArrayRegion;
		public JNIEnv.IntPtr_IntPtr_int_int_shortArray_void_Delegate GetShortArrayRegion {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_int_intArray_void_Delegate _GetIntArrayRegion;
		public JNIEnv.IntPtr_IntPtr_int_int_intArray_void_Delegate GetIntArrayRegion {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_int_longArray_void_Delegate _GetLongArrayRegion;
		public JNIEnv.IntPtr_IntPtr_int_int_longArray_void_Delegate GetLongArrayRegion {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_int_floatArray_void_Delegate _GetFloatArrayRegion;
		public JNIEnv.IntPtr_IntPtr_int_int_floatArray_void_Delegate GetFloatArrayRegion {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_int_doubleArray_void_Delegate _GetDoubleArrayRegion;
		public JNIEnv.IntPtr_IntPtr_int_int_doubleArray_void_Delegate GetDoubleArrayRegion {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_int_byteArray_void_Delegate _SetBooleanArrayRegion;
		public JNIEnv.IntPtr_IntPtr_int_int_byteArray_void_Delegate SetBooleanArrayRegion {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_int_byteArray_void_Delegate _SetByteArrayRegion;
		public JNIEnv.IntPtr_IntPtr_int_int_byteArray_void_Delegate SetByteArrayRegion {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_int_charArray_void_Delegate _SetCharArrayRegion;
		public JNIEnv.IntPtr_IntPtr_int_int_charArray_void_Delegate SetCharArrayRegion {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_int_shortArray_void_Delegate _SetShortArrayRegion;
		public JNIEnv.IntPtr_IntPtr_int_int_shortArray_void_Delegate SetShortArrayRegion {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_int_intArray_void_Delegate _SetIntArrayRegion;
		public JNIEnv.IntPtr_IntPtr_int_int_intArray_void_Delegate SetIntArrayRegion {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_int_longArray_void_Delegate _SetLongArrayRegion;
		public JNIEnv.IntPtr_IntPtr_int_int_longArray_void_Delegate SetLongArrayRegion {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_int_floatArray_void_Delegate _SetFloatArrayRegion;
		public JNIEnv.IntPtr_IntPtr_int_int_floatArray_void_Delegate SetFloatArrayRegion {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_int_doubleArray_void_Delegate _SetDoubleArrayRegion;
		public JNIEnv.IntPtr_IntPtr_int_int_doubleArray_void_Delegate SetDoubleArrayRegion {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_JNINativeMethodArray_int_int_Delegate _RegisterNatives;
		public JNIEnv.IntPtr_IntPtr_JNINativeMethodArray_int_int_Delegate RegisterNatives {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_Delegate _UnregisterNatives;
		public JNIEnv.IntPtr_IntPtr_int_Delegate UnregisterNatives {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_Delegate _MonitorEnter;
		public JNIEnv.IntPtr_IntPtr_int_Delegate MonitorEnter {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_Delegate _MonitorExit;
		public JNIEnv.IntPtr_IntPtr_int_Delegate MonitorExit {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_outIntPtr_int_Delegate _GetJavaVM;
		public JNIEnv.IntPtr_outIntPtr_int_Delegate GetJavaVM {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_int_IntPtr_void_Delegate _GetStringRegion;
		public JNIEnv.IntPtr_IntPtr_int_int_IntPtr_void_Delegate GetStringRegion {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_int_IntPtr_void_Delegate _GetStringUTFRegion;
		public JNIEnv.IntPtr_IntPtr_int_int_IntPtr_void_Delegate GetStringUTFRegion {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate _GetPrimitiveArrayCritical;
		public JNIEnv.IntPtr_IntPtr_IntPtr_IntPtr_Delegate GetPrimitiveArrayCritical {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate _ReleasePrimitiveArrayCritical;
		public JNIEnv.IntPtr_IntPtr_IntPtr_int_void_Delegate ReleasePrimitiveArrayCritical {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_string_Delegate _GetStringCritical;
		public JNIEnv.IntPtr_IntPtr_IntPtr_string_Delegate GetStringCritical {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_string_void_Delegate _ReleaseStringCritical;
		public JNIEnv.IntPtr_IntPtr_string_void_Delegate ReleaseStringCritical {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_Delegate _NewWeakGlobalRef;
		public JNIEnv.IntPtr_IntPtr_IntPtr_Delegate NewWeakGlobalRef {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_void_Delegate _DeleteWeakGlobalRef;
		public JNIEnv.IntPtr_IntPtr_void_Delegate DeleteWeakGlobalRef {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_bool_Delegate _ExceptionCheck;
		public JNIEnv.IntPtr_bool_Delegate ExceptionCheck {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_long_IntPtr_Delegate _NewDirectByteBuffer;
		public JNIEnv.IntPtr_IntPtr_long_IntPtr_Delegate NewDirectByteBuffer {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_IntPtr_Delegate _GetDirectBufferAddress;
		public JNIEnv.IntPtr_IntPtr_IntPtr_Delegate GetDirectBufferAddress {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_long_Delegate _GetDirectBufferCapacity;
		public JNIEnv.IntPtr_IntPtr_long_Delegate GetDirectBufferCapacity {
			get {
				throw new NotImplementedException ();
			}
		}

		JNIEnv.IntPtr_IntPtr_int_Delegate _GetObjectRefType;
		public JNIEnv.IntPtr_IntPtr_int_Delegate GetObjectRefType {
			get {
				throw new NotImplementedException ();
			}
		}
	}
}
#pragma warning restore
