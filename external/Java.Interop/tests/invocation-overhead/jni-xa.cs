// Generated file; DO NOT EDIT!
//
// To make changes, edit monodroid/tools/jnienv-gen-interop and rerun

#if !FEATURE_HANDLES_ARE_SAFE_HANDLES && !FEATURE_HANDLES_ARE_INTPTRS
#define FEATURE_HANDLES_ARE_SAFE_HANDLES
#endif  // !FEATURE_HANDLES_ARE_SAFE_HANDLES && !FEATURE_HANDLES_ARE_INTPTRS

#if FEATURE_HANDLES_ARE_SAFE_HANDLES && FEATURE_HANDLES_ARE_INTPTRS
#define _NAMESPACE_PER_HANDLE
#endif  // FEATURE_HANDLES_ARE_SAFE_HANDLES && FEATURE_HANDLES_ARE_INTPTRS

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

using Java.Interop;

#if FEATURE_HANDLES_ARE_INTPTRS
	using JNIEnvPtr          = System.IntPtr;
	using jinstanceFieldID   = System.IntPtr;
	using jstaticFieldID     = System.IntPtr;
	using jinstanceMethodID  = System.IntPtr;
	using jstaticMethodID    = System.IntPtr;
	using jobject            = System.IntPtr;
#endif  // FEATURE_HANDLES_ARE_INTPTRS

namespace Java.Interop {
	[StructLayout (LayoutKind.Sequential)]
	partial struct JniNativeInterfaceStruct {

#pragma warning disable 0649	// Field is assigned to, and will always have its default value `null`; ignore as it'll be set in native code.
#pragma warning disable 0169	// Field never used; ignore since these fields make the structure have the right layout.
		private IntPtr  reserved0;                      // void*
		private IntPtr  reserved1;                      // void*
		private IntPtr  reserved2;                      // void*
		private IntPtr  reserved3;                      // void*
		public  IntPtr  GetVersion;                     // jint        (*GetVersion)(JNIEnv*);
		public  IntPtr  DefineClass;                    // jclass      (*DefineClass)(JNIEnv*, const char, jobject, const jbyte*, jsize);
		public  IntPtr  FindClass;                      // jclass      (*FindClass)(JNIEnv*, const char*);
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
		public  IntPtr  PushLocalFrame;                 // jint        (*PushLocalFrame)(JNIEnv*, jint);
		public  IntPtr  PopLocalFrame;                  // jobject     (*PopLocalFrame)(JNIEnv*, jobject);
		public  IntPtr  NewGlobalRef;                   // jobject     (*NewGlobalRef)(JNIEnv*, jobject);
		public  IntPtr  DeleteGlobalRef;                // void        (*DeleteGlobalRef)(JNIEnv*, jobject);
		public  IntPtr  DeleteLocalRef;                 // void        (*DeleteLocalRef)(JNIEnv*, jobject);
		public  IntPtr  IsSameObject;                   // jboolean    (*IsSameObject)(JNIEnv*, jobject, jobject);
		public  IntPtr  NewLocalRef;                    // jobject     (*NewLocalRef)(JNIEnv*, jobject);
		public  IntPtr  EnsureLocalCapacity;            // jint        (*EnsureLocalCapacity)(JNIEnv*, jint);
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
		public  IntPtr  CallSByteMethod;                // jbyte       (*CallByteMethod)(JNIEnv*, jobject, jmethodID, ...);
		public  IntPtr  CallSByteMethodV;               // jbyte       (*CallByteMethodV)(JNIEnv*, jobject, jmethodID, va_list);
		public  IntPtr  CallSByteMethodA;               // jbyte       (*CallByteMethodA)(JNIEnv*, jobject, jmethodID, jvalue*);
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
		public  IntPtr  CallNonvirtualSByteMethod;      // jbyte       (*CallNonvirtualByteMethod)(JNIEnv*, jobject, jclass, jmethodID, ...);
		public  IntPtr  CallNonvirtualSByteMethodV;     // jbyte       (*CallNonvirtualByteMethodV)(JNIEnv*, jobject, jclass, jmethodID, va_list);
		public  IntPtr  CallNonvirtualSByteMethodA;     // jbyte       (*CallNonvirtualSByteMethodA)(JNIEnv*, jobject, jclass, jmethodID, jvalue*);
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
		public  IntPtr  CallStaticSByteMethod;          // jbyte       (*CallStaticByteMethod)(JNIEnv*, jclass, jmethodID, ...);
		public  IntPtr  CallStaticSByteMethodV;         // jbyte       (*CallStaticSByteMethodV)(JNIEnv*, jclass, jmethodID, va_list);
		public  IntPtr  CallStaticSByteMethodA;         // jbyte       (*CallStaticByteMethodA)(JNIEnv*, jclass, jmethodID, jvalue*);
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
		public  IntPtr  GetStaticFieldID;               // jstaticfieldID    (*GetStaticFieldID)(JNIEnv*, jclass, const char*, const char*);
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
#pragma warning restore 0169
#pragma warning restore 0649
	}
}
#if FEATURE_HANDLES_ARE_SAFE_HANDLES
namespace
#if _NAMESPACE_PER_HANDLE
	Java.Interop.SafeHandles
#else
	Java.Interop
#endif
{

	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_int (JniEnvironmentSafeHandle env);
	unsafe delegate JniLocalReference JniFunc_JniEnvironmentSafeHandle_string_JniReferenceSafeHandle_IntPtr_int_JniLocalReference (JniEnvironmentSafeHandle env, string name, JniReferenceSafeHandle loader, IntPtr buf, int bufLen);
	unsafe delegate JniLocalReference JniFunc_JniEnvironmentSafeHandle_string_JniLocalReference (JniEnvironmentSafeHandle env, string classname);
	unsafe delegate JniInstanceMethodID JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID (JniEnvironmentSafeHandle env, JniReferenceSafeHandle method);
	unsafe delegate JniInstanceFieldID JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID (JniEnvironmentSafeHandle env, JniReferenceSafeHandle field);
	unsafe delegate JniLocalReference JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_bool_JniLocalReference (JniEnvironmentSafeHandle env, JniReferenceSafeHandle cls, JniInstanceMethodID jmethod, bool isStatic);
	unsafe delegate JniLocalReference JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass);
	unsafe delegate bool JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_bool (JniEnvironmentSafeHandle env, JniReferenceSafeHandle clazz1, JniReferenceSafeHandle clazz2);
	unsafe delegate JniLocalReference JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_bool_JniLocalReference (JniEnvironmentSafeHandle env, JniReferenceSafeHandle cls, JniInstanceFieldID jfieldID, bool isStatic);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int (JniEnvironmentSafeHandle env, JniReferenceSafeHandle obj);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_int (JniEnvironmentSafeHandle env, JniReferenceSafeHandle clazz, string message);
	unsafe delegate JniLocalReference JniFunc_JniEnvironmentSafeHandle_JniLocalReference (JniEnvironmentSafeHandle env);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle (JniEnvironmentSafeHandle env);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_string (JniEnvironmentSafeHandle env, string msg);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_int_int (JniEnvironmentSafeHandle env, int capacity);
	unsafe delegate JniGlobalReference JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniGlobalReference (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr (JniEnvironmentSafeHandle env, IntPtr jobject);
	unsafe delegate JniLocalReference JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JniLocalReference (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod);
	unsafe delegate JniLocalReference JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_JniLocalReference (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms);
	unsafe delegate JniInstanceMethodID JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_string_JniInstanceMethodID (JniEnvironmentSafeHandle env, JniReferenceSafeHandle kls, string name, string signature);
	unsafe delegate bool JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_bool (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod);
	unsafe delegate bool JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_bool (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod, JValue* parms);
	unsafe delegate sbyte JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_sbyte (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod);
	unsafe delegate sbyte JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_sbyte (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod, JValue* parms);
	unsafe delegate char JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_char (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod);
	unsafe delegate char JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_char (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod, JValue* parms);
	unsafe delegate short JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_short (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod);
	unsafe delegate short JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_short (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod, JValue* parms);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_int (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_int (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod, JValue* parms);
	unsafe delegate long JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_long (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod);
	unsafe delegate long JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_long (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod, JValue* parms);
	unsafe delegate float JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_float (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod);
	unsafe delegate float JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_float (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod, JValue* parms);
	unsafe delegate double JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_double (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod);
	unsafe delegate double JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_double (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod, JValue* parms);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod, JValue* parms);
	unsafe delegate JniLocalReference JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JniLocalReference (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod);
	unsafe delegate JniLocalReference JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_JniLocalReference (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms);
	unsafe delegate bool JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_bool (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod);
	unsafe delegate bool JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_bool (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms);
	unsafe delegate sbyte JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_sbyte (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod);
	unsafe delegate sbyte JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_sbyte (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms);
	unsafe delegate char JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_char (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod);
	unsafe delegate char JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_char (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms);
	unsafe delegate short JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_short (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod);
	unsafe delegate short JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_short (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_int (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_int (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms);
	unsafe delegate long JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_long (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod);
	unsafe delegate long JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_long (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms);
	unsafe delegate float JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_float (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod);
	unsafe delegate float JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_float (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms);
	unsafe delegate double JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_double (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod);
	unsafe delegate double JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_double (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms);
	unsafe delegate JniInstanceFieldID JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_string_JniInstanceFieldID (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, string name, string sig);
	unsafe delegate JniLocalReference JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_JniLocalReference (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID);
	unsafe delegate bool JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_bool (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID);
	unsafe delegate sbyte JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_sbyte (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID);
	unsafe delegate char JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_char (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID);
	unsafe delegate short JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_short (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_int (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID);
	unsafe delegate long JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_long (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID);
	unsafe delegate float JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_float (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID);
	unsafe delegate double JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_double (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_JniReferenceSafeHandle (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID, JniReferenceSafeHandle val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_bool (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID, bool val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_sbyte (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID, sbyte val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_char (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID, char val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_short (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID, short val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_int (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID, int val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_long (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID, long val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_float (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID, float val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_double (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID, double val);
	unsafe delegate JniStaticMethodID JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_string_JniStaticMethodID (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, string name, string sig);
	unsafe delegate JniLocalReference JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JniLocalReference (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticMethodID jmethod);
	unsafe delegate JniLocalReference JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_JniLocalReference (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticMethodID jmethod, JValue* parms);
	unsafe delegate bool JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_bool (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticMethodID jmethod);
	unsafe delegate bool JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_bool (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticMethodID jmethod, JValue* parms);
	unsafe delegate sbyte JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_sbyte (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticMethodID jmethod);
	unsafe delegate sbyte JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_sbyte (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticMethodID jmethod, JValue* parms);
	unsafe delegate char JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_char (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticMethodID jmethod);
	unsafe delegate char JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_char (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticMethodID jmethod, JValue* parms);
	unsafe delegate short JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_short (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticMethodID jmethod);
	unsafe delegate short JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_short (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticMethodID jmethod, JValue* parms);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_int (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticMethodID jmethod);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_int (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticMethodID jmethod, JValue* parms);
	unsafe delegate long JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_long (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticMethodID jmethod);
	unsafe delegate long JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_long (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticMethodID jmethod, JValue* parms);
	unsafe delegate float JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_float (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticMethodID jmethod);
	unsafe delegate float JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_float (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticMethodID jmethod, JValue* parms);
	unsafe delegate double JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_double (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticMethodID jmethod);
	unsafe delegate double JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_double (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticMethodID jmethod, JValue* parms);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticMethodID jmethod);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticMethodID jmethod, JValue* parms);
	unsafe delegate JniStaticFieldID JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_string_JniStaticFieldID (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, string name, string sig);
	unsafe delegate JniLocalReference JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_JniLocalReference (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID);
	unsafe delegate bool JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_bool (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID);
	unsafe delegate sbyte JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_sbyte (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID);
	unsafe delegate char JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_char (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID);
	unsafe delegate short JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_short (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_int (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID);
	unsafe delegate long JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_long (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID);
	unsafe delegate float JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_float (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID);
	unsafe delegate double JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_double (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_JniReferenceSafeHandle (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID, JniReferenceSafeHandle val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_bool (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID, bool val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_sbyte (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID, sbyte val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_char (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID, char val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_short (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID, short val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_int (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID, int val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_long (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID, long val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_float (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID, float val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_double (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID, double val);
	[UnmanagedFunctionPointerAttribute (CallingConvention.Cdecl, CharSet=CharSet.Unicode)]
	unsafe delegate JniLocalReference JniFunc_JniEnvironmentSafeHandle_IntPtr_int_JniLocalReference (JniEnvironmentSafeHandle env, IntPtr unicodeChars, int len);
	unsafe delegate IntPtr JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr (JniEnvironmentSafeHandle env, JniReferenceSafeHandle @string, IntPtr isCopy);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr (JniEnvironmentSafeHandle env, JniReferenceSafeHandle @string, IntPtr chars);
	unsafe delegate string JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_string (JniEnvironmentSafeHandle env, JniReferenceSafeHandle @string, IntPtr isCopy);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string (JniEnvironmentSafeHandle env, JniReferenceSafeHandle @string, string utf);
	unsafe delegate JniLocalReference JniFunc_JniEnvironmentSafeHandle_int_JniReferenceSafeHandle_JniReferenceSafeHandle_JniLocalReference (JniEnvironmentSafeHandle env, int length, JniReferenceSafeHandle elementClass, JniReferenceSafeHandle initialElement);
	unsafe delegate JniLocalReference JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_JniLocalReference (JniEnvironmentSafeHandle env, JniReferenceSafeHandle array, int index);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_JniReferenceSafeHandle (JniEnvironmentSafeHandle env, JniReferenceSafeHandle array, int index, JniReferenceSafeHandle value);
	unsafe delegate JniLocalReference JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference (JniEnvironmentSafeHandle env, int length);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int (JniEnvironmentSafeHandle env, JniReferenceSafeHandle array, IntPtr elems, int mode);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr (JniEnvironmentSafeHandle env, JniReferenceSafeHandle array, int start, int len, IntPtr buf);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniNativeMethodRegistrationArray_int_int (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jclass, JniNativeMethodRegistration [] methods, int nMethods);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_outJavaVMSafeHandle_int (JniEnvironmentSafeHandle env, out JavaVMSafeHandle vm);
	unsafe delegate JniWeakGlobalReference JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniWeakGlobalReference (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject);
	unsafe delegate bool JniFunc_JniEnvironmentSafeHandle_bool (JniEnvironmentSafeHandle env);
	unsafe delegate JniLocalReference JniFunc_JniEnvironmentSafeHandle_IntPtr_long_JniLocalReference (JniEnvironmentSafeHandle env, IntPtr address, long capacity);
	unsafe delegate IntPtr JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr (JniEnvironmentSafeHandle env, JniReferenceSafeHandle buf);
	unsafe delegate long JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_long (JniEnvironmentSafeHandle env, JniReferenceSafeHandle buf);
	unsafe delegate JniObjectReferenceType JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniObjectReferenceType (JniEnvironmentSafeHandle env, JniReferenceSafeHandle jobject);

	partial class JniEnvironment {

	internal static partial class Activator {

		public static unsafe JniLocalReference AllocObject (JniReferenceSafeHandle jclass)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");

			var tmp = JniEnvironment.Current.Invoker.AllocObject (JniEnvironment.Current.SafeHandle, jclass);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe JniLocalReference NewObject (JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.NewObject (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe JniLocalReference NewObject (JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.NewObjectA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}
	}

	public static partial class Arrays {

		public static unsafe int GetArrayLength (JniReferenceSafeHandle array_ptr)
		{
			if (array_ptr == null)
				throw new ArgumentNullException ("array_ptr");
			if (array_ptr.IsInvalid)
				throw new ArgumentException ("array_ptr");

			var tmp = JniEnvironment.Current.Invoker.GetArrayLength (JniEnvironment.Current.SafeHandle, array_ptr);
			return tmp;
		}

		public static unsafe JniLocalReference NewObjectArray (int length, JniReferenceSafeHandle elementClass, JniReferenceSafeHandle initialElement)
		{
			if (elementClass == null)
				throw new ArgumentNullException ("elementClass");
			if (elementClass.IsInvalid)
				throw new ArgumentException ("elementClass");

			var tmp = JniEnvironment.Current.Invoker.NewObjectArray (JniEnvironment.Current.SafeHandle, length, elementClass, initialElement);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe JniLocalReference GetObjectArrayElement (JniReferenceSafeHandle array, int index)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");

			var tmp = JniEnvironment.Current.Invoker.GetObjectArrayElement (JniEnvironment.Current.SafeHandle, array, index);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe void SetObjectArrayElement (JniReferenceSafeHandle array, int index, JniReferenceSafeHandle value)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");

			JniEnvironment.Current.Invoker.SetObjectArrayElement (JniEnvironment.Current.SafeHandle, array, index, value);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe JniLocalReference NewBooleanArray (int length)
		{
			var tmp = JniEnvironment.Current.Invoker.NewBooleanArray (JniEnvironment.Current.SafeHandle, length);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe JniLocalReference NewByteArray (int length)
		{
			var tmp = JniEnvironment.Current.Invoker.NewByteArray (JniEnvironment.Current.SafeHandle, length);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe JniLocalReference NewCharArray (int length)
		{
			var tmp = JniEnvironment.Current.Invoker.NewCharArray (JniEnvironment.Current.SafeHandle, length);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe JniLocalReference NewShortArray (int length)
		{
			var tmp = JniEnvironment.Current.Invoker.NewShortArray (JniEnvironment.Current.SafeHandle, length);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe JniLocalReference NewIntArray (int length)
		{
			var tmp = JniEnvironment.Current.Invoker.NewIntArray (JniEnvironment.Current.SafeHandle, length);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe JniLocalReference NewLongArray (int length)
		{
			var tmp = JniEnvironment.Current.Invoker.NewLongArray (JniEnvironment.Current.SafeHandle, length);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe JniLocalReference NewFloatArray (int length)
		{
			var tmp = JniEnvironment.Current.Invoker.NewFloatArray (JniEnvironment.Current.SafeHandle, length);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe JniLocalReference NewDoubleArray (int length)
		{
			var tmp = JniEnvironment.Current.Invoker.NewDoubleArray (JniEnvironment.Current.SafeHandle, length);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe IntPtr GetBooleanArrayElements (JniReferenceSafeHandle array, IntPtr isCopy)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");

			var tmp = JniEnvironment.Current.Invoker.GetBooleanArrayElements (JniEnvironment.Current.SafeHandle, array, isCopy);
			return tmp;
		}

		public static unsafe IntPtr GetByteArrayElements (JniReferenceSafeHandle array, IntPtr isCopy)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");

			var tmp = JniEnvironment.Current.Invoker.GetByteArrayElements (JniEnvironment.Current.SafeHandle, array, isCopy);
			return tmp;
		}

		public static unsafe IntPtr GetCharArrayElements (JniReferenceSafeHandle array, IntPtr isCopy)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");

			var tmp = JniEnvironment.Current.Invoker.GetCharArrayElements (JniEnvironment.Current.SafeHandle, array, isCopy);
			return tmp;
		}

		public static unsafe IntPtr GetShortArrayElements (JniReferenceSafeHandle array, IntPtr isCopy)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");

			var tmp = JniEnvironment.Current.Invoker.GetShortArrayElements (JniEnvironment.Current.SafeHandle, array, isCopy);
			return tmp;
		}

		public static unsafe IntPtr GetIntArrayElements (JniReferenceSafeHandle array, IntPtr isCopy)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");

			var tmp = JniEnvironment.Current.Invoker.GetIntArrayElements (JniEnvironment.Current.SafeHandle, array, isCopy);
			return tmp;
		}

		public static unsafe IntPtr GetLongArrayElements (JniReferenceSafeHandle array, IntPtr isCopy)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");

			var tmp = JniEnvironment.Current.Invoker.GetLongArrayElements (JniEnvironment.Current.SafeHandle, array, isCopy);
			return tmp;
		}

		public static unsafe IntPtr GetFloatArrayElements (JniReferenceSafeHandle array, IntPtr isCopy)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");

			var tmp = JniEnvironment.Current.Invoker.GetFloatArrayElements (JniEnvironment.Current.SafeHandle, array, isCopy);
			return tmp;
		}

		public static unsafe IntPtr GetDoubleArrayElements (JniReferenceSafeHandle array, IntPtr isCopy)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");

			var tmp = JniEnvironment.Current.Invoker.GetDoubleArrayElements (JniEnvironment.Current.SafeHandle, array, isCopy);
			return tmp;
		}

		public static unsafe void ReleaseBooleanArrayElements (JniReferenceSafeHandle array, IntPtr elems, int mode)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (elems == IntPtr.Zero)
				throw new ArgumentException ("'elems' must not be IntPtr.Zero.", "elems");

			JniEnvironment.Current.Invoker.ReleaseBooleanArrayElements (JniEnvironment.Current.SafeHandle, array, elems, mode);
		}

		public static unsafe void ReleaseByteArrayElements (JniReferenceSafeHandle array, IntPtr elems, int mode)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (elems == IntPtr.Zero)
				throw new ArgumentException ("'elems' must not be IntPtr.Zero.", "elems");

			JniEnvironment.Current.Invoker.ReleaseByteArrayElements (JniEnvironment.Current.SafeHandle, array, elems, mode);
		}

		public static unsafe void ReleaseCharArrayElements (JniReferenceSafeHandle array, IntPtr elems, int mode)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (elems == IntPtr.Zero)
				throw new ArgumentException ("'elems' must not be IntPtr.Zero.", "elems");

			JniEnvironment.Current.Invoker.ReleaseCharArrayElements (JniEnvironment.Current.SafeHandle, array, elems, mode);
		}

		public static unsafe void ReleaseShortArrayElements (JniReferenceSafeHandle array, IntPtr elems, int mode)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (elems == IntPtr.Zero)
				throw new ArgumentException ("'elems' must not be IntPtr.Zero.", "elems");

			JniEnvironment.Current.Invoker.ReleaseShortArrayElements (JniEnvironment.Current.SafeHandle, array, elems, mode);
		}

		public static unsafe void ReleaseIntArrayElements (JniReferenceSafeHandle array, IntPtr elems, int mode)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (elems == IntPtr.Zero)
				throw new ArgumentException ("'elems' must not be IntPtr.Zero.", "elems");

			JniEnvironment.Current.Invoker.ReleaseIntArrayElements (JniEnvironment.Current.SafeHandle, array, elems, mode);
		}

		public static unsafe void ReleaseLongArrayElements (JniReferenceSafeHandle array, IntPtr elems, int mode)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (elems == IntPtr.Zero)
				throw new ArgumentException ("'elems' must not be IntPtr.Zero.", "elems");

			JniEnvironment.Current.Invoker.ReleaseLongArrayElements (JniEnvironment.Current.SafeHandle, array, elems, mode);
		}

		public static unsafe void ReleaseFloatArrayElements (JniReferenceSafeHandle array, IntPtr elems, int mode)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (elems == IntPtr.Zero)
				throw new ArgumentException ("'elems' must not be IntPtr.Zero.", "elems");

			JniEnvironment.Current.Invoker.ReleaseFloatArrayElements (JniEnvironment.Current.SafeHandle, array, elems, mode);
		}

		public static unsafe void ReleaseDoubleArrayElements (JniReferenceSafeHandle array, IntPtr elems, int mode)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (elems == IntPtr.Zero)
				throw new ArgumentException ("'elems' must not be IntPtr.Zero.", "elems");

			JniEnvironment.Current.Invoker.ReleaseDoubleArrayElements (JniEnvironment.Current.SafeHandle, array, elems, mode);
		}

		internal static unsafe void GetBooleanArrayRegion (JniReferenceSafeHandle array, int start, int len, IntPtr buf)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.GetBooleanArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void GetByteArrayRegion (JniReferenceSafeHandle array, int start, int len, IntPtr buf)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.GetByteArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void GetCharArrayRegion (JniReferenceSafeHandle array, int start, int len, IntPtr buf)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.GetCharArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void GetShortArrayRegion (JniReferenceSafeHandle array, int start, int len, IntPtr buf)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.GetShortArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void GetIntArrayRegion (JniReferenceSafeHandle array, int start, int len, IntPtr buf)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.GetIntArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void GetLongArrayRegion (JniReferenceSafeHandle array, int start, int len, IntPtr buf)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.GetLongArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void GetFloatArrayRegion (JniReferenceSafeHandle array, int start, int len, IntPtr buf)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.GetFloatArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void GetDoubleArrayRegion (JniReferenceSafeHandle array, int start, int len, IntPtr buf)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.GetDoubleArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		internal static unsafe void SetBooleanArrayRegion (JniReferenceSafeHandle array, int start, int len, IntPtr buf)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.SetBooleanArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void SetByteArrayRegion (JniReferenceSafeHandle array, int start, int len, IntPtr buf)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.SetByteArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void SetCharArrayRegion (JniReferenceSafeHandle array, int start, int len, IntPtr buf)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.SetCharArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void SetShortArrayRegion (JniReferenceSafeHandle array, int start, int len, IntPtr buf)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.SetShortArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void SetIntArrayRegion (JniReferenceSafeHandle array, int start, int len, IntPtr buf)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.SetIntArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void SetLongArrayRegion (JniReferenceSafeHandle array, int start, int len, IntPtr buf)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.SetLongArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void SetFloatArrayRegion (JniReferenceSafeHandle array, int start, int len, IntPtr buf)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.SetFloatArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void SetDoubleArrayRegion (JniReferenceSafeHandle array, int start, int len, IntPtr buf)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array.IsInvalid)
				throw new ArgumentException ("array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.SetDoubleArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}
	}

	public static partial class Errors {

		public static unsafe int Throw (JniReferenceSafeHandle obj)
		{
			if (obj == null)
				throw new ArgumentNullException ("obj");
			if (obj.IsInvalid)
				throw new ArgumentException ("obj");

			var tmp = JniEnvironment.Current.Invoker.Throw (JniEnvironment.Current.SafeHandle, obj);
			return tmp;
		}

		public static unsafe int ThrowNew (JniReferenceSafeHandle clazz, string message)
		{
			if (clazz == null)
				throw new ArgumentNullException ("clazz");
			if (clazz.IsInvalid)
				throw new ArgumentException ("clazz");
			if (message == null)
				throw new ArgumentNullException ("message");

			var tmp = JniEnvironment.Current.Invoker.ThrowNew (JniEnvironment.Current.SafeHandle, clazz, message);
			return tmp;
		}

		internal static unsafe JniLocalReference ExceptionOccurred ()
		{
			var tmp = JniEnvironment.Current.Invoker.ExceptionOccurred (JniEnvironment.Current.SafeHandle);
			return tmp;
		}

		internal static unsafe void ExceptionDescribe ()
		{
			JniEnvironment.Current.Invoker.ExceptionDescribe (JniEnvironment.Current.SafeHandle);
		}

		internal static unsafe void ExceptionClear ()
		{
			JniEnvironment.Current.Invoker.ExceptionClear (JniEnvironment.Current.SafeHandle);
		}

		public static unsafe void FatalError (string msg)
		{
			if (msg == null)
				throw new ArgumentNullException ("msg");

			JniEnvironment.Current.Invoker.FatalError (JniEnvironment.Current.SafeHandle, msg);
		}

		internal static unsafe bool ExceptionCheck ()
		{
			var tmp = JniEnvironment.Current.Invoker.ExceptionCheck (JniEnvironment.Current.SafeHandle);
			return tmp;
		}
	}

	public static partial class Handles {

		public static unsafe int PushLocalFrame (int capacity)
		{
			var tmp = JniEnvironment.Current.Invoker.PushLocalFrame (JniEnvironment.Current.SafeHandle, capacity);
			return tmp;
		}

		public static unsafe JniLocalReference PopLocalFrame (JniReferenceSafeHandle result)
		{
			var tmp = JniEnvironment.Current.Invoker.PopLocalFrame (JniEnvironment.Current.SafeHandle, result);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe JniGlobalReference NewGlobalRef (JniReferenceSafeHandle jobject)
		{
			var tmp = JniEnvironment.Current.Invoker.NewGlobalRef (JniEnvironment.Current.SafeHandle, jobject);
			return tmp;
		}

		internal static unsafe void DeleteGlobalRef (IntPtr jobject)
		{
			JniEnvironment.Current.Invoker.DeleteGlobalRef (JniEnvironment.Current.SafeHandle, jobject);
		}

		internal static unsafe void DeleteLocalRef (IntPtr jobject)
		{
			JniEnvironment.Current.Invoker.DeleteLocalRef (JniEnvironment.Current.SafeHandle, jobject);
		}

		internal static unsafe JniLocalReference NewLocalRef (JniReferenceSafeHandle jobject)
		{
			var tmp = JniEnvironment.Current.Invoker.NewLocalRef (JniEnvironment.Current.SafeHandle, jobject);
			return tmp;
		}

		public static unsafe int EnsureLocalCapacity (int capacity)
		{
			var tmp = JniEnvironment.Current.Invoker.EnsureLocalCapacity (JniEnvironment.Current.SafeHandle, capacity);
			return tmp;
		}

		public static unsafe int GetJavaVM (out JavaVMSafeHandle vm)
		{
			var tmp = JniEnvironment.Current.Invoker.GetJavaVM (JniEnvironment.Current.SafeHandle, out vm);
			return tmp;
		}

		internal static unsafe JniWeakGlobalReference NewWeakGlobalRef (JniReferenceSafeHandle jobject)
		{
			var tmp = JniEnvironment.Current.Invoker.NewWeakGlobalRef (JniEnvironment.Current.SafeHandle, jobject);
			return tmp;
		}

		internal static unsafe void DeleteWeakGlobalRef (IntPtr jobject)
		{
			JniEnvironment.Current.Invoker.DeleteWeakGlobalRef (JniEnvironment.Current.SafeHandle, jobject);
		}

		internal static unsafe JniObjectReferenceType GetObjectRefType (JniReferenceSafeHandle jobject)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");

			var tmp = JniEnvironment.Current.Invoker.GetObjectRefType (JniEnvironment.Current.SafeHandle, jobject);
			return tmp;
		}
	}

	public static partial class IO {

		public static unsafe JniLocalReference NewDirectByteBuffer (IntPtr address, long capacity)
		{
			if (address == IntPtr.Zero)
				throw new ArgumentException ("'address' must not be IntPtr.Zero.", "address");

			var tmp = JniEnvironment.Current.Invoker.NewDirectByteBuffer (JniEnvironment.Current.SafeHandle, address, capacity);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe IntPtr GetDirectBufferAddress (JniReferenceSafeHandle buf)
		{
			if (buf == null)
				throw new ArgumentNullException ("buf");
			if (buf.IsInvalid)
				throw new ArgumentException ("buf");

			var tmp = JniEnvironment.Current.Invoker.GetDirectBufferAddress (JniEnvironment.Current.SafeHandle, buf);
			return tmp;
		}

		public static unsafe long GetDirectBufferCapacity (JniReferenceSafeHandle buf)
		{
			if (buf == null)
				throw new ArgumentNullException ("buf");
			if (buf.IsInvalid)
				throw new ArgumentException ("buf");

			var tmp = JniEnvironment.Current.Invoker.GetDirectBufferCapacity (JniEnvironment.Current.SafeHandle, buf);
			return tmp;
		}
	}

	internal static partial class Members {

		internal static unsafe JniLocalReference ToReflectedMethod (JniReferenceSafeHandle cls, JniInstanceMethodID jmethod, bool isStatic)
		{
			if (cls == null)
				throw new ArgumentNullException ("cls");
			if (cls.IsInvalid)
				throw new ArgumentException ("cls");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.ToReflectedMethod (JniEnvironment.Current.SafeHandle, cls, jmethod, isStatic);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe JniLocalReference ToReflectedField (JniReferenceSafeHandle cls, JniInstanceFieldID jfieldID, bool isStatic)
		{
			if (cls == null)
				throw new ArgumentNullException ("cls");
			if (cls.IsInvalid)
				throw new ArgumentException ("cls");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.ToReflectedField (JniEnvironment.Current.SafeHandle, cls, jfieldID, isStatic);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe JniInstanceMethodID GetMethodID (JniReferenceSafeHandle kls, string name, string signature)
		{
			if (kls == null)
				throw new ArgumentNullException ("kls");
			if (kls.IsInvalid)
				throw new ArgumentException ("kls");
			if (name == null)
				throw new ArgumentNullException ("name");
			if (signature == null)
				throw new ArgumentNullException ("signature");

			var tmp = JniEnvironment.Current.Invoker.GetMethodID (JniEnvironment.Current.SafeHandle, kls, name, signature);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe JniLocalReference CallObjectMethod (JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallObjectMethod (JniEnvironment.Current.SafeHandle, jobject, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe JniLocalReference CallObjectMethod (JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallObjectMethodA (JniEnvironment.Current.SafeHandle, jobject, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe bool CallBooleanMethod (JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallBooleanMethod (JniEnvironment.Current.SafeHandle, jobject, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe bool CallBooleanMethod (JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallBooleanMethodA (JniEnvironment.Current.SafeHandle, jobject, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe sbyte CallSByteMethod (JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallSByteMethod (JniEnvironment.Current.SafeHandle, jobject, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe sbyte CallSByteMethod (JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallSByteMethodA (JniEnvironment.Current.SafeHandle, jobject, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe char CallCharMethod (JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallCharMethod (JniEnvironment.Current.SafeHandle, jobject, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe char CallCharMethod (JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallCharMethodA (JniEnvironment.Current.SafeHandle, jobject, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe short CallShortMethod (JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallShortMethod (JniEnvironment.Current.SafeHandle, jobject, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe short CallShortMethod (JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallShortMethodA (JniEnvironment.Current.SafeHandle, jobject, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe int CallIntMethod (JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallIntMethod (JniEnvironment.Current.SafeHandle, jobject, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe int CallIntMethod (JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallIntMethodA (JniEnvironment.Current.SafeHandle, jobject, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe long CallLongMethod (JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallLongMethod (JniEnvironment.Current.SafeHandle, jobject, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe long CallLongMethod (JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallLongMethodA (JniEnvironment.Current.SafeHandle, jobject, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe float CallFloatMethod (JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallFloatMethod (JniEnvironment.Current.SafeHandle, jobject, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe float CallFloatMethod (JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallFloatMethodA (JniEnvironment.Current.SafeHandle, jobject, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe double CallDoubleMethod (JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallDoubleMethod (JniEnvironment.Current.SafeHandle, jobject, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe double CallDoubleMethod (JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallDoubleMethodA (JniEnvironment.Current.SafeHandle, jobject, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe void CallVoidMethod (JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			JniEnvironment.Current.Invoker.CallVoidMethod (JniEnvironment.Current.SafeHandle, jobject, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		internal static unsafe void CallVoidMethod (JniReferenceSafeHandle jobject, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			JniEnvironment.Current.Invoker.CallVoidMethodA (JniEnvironment.Current.SafeHandle, jobject, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		internal static unsafe JniLocalReference CallNonvirtualObjectMethod (JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualObjectMethod (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe JniLocalReference CallNonvirtualObjectMethod (JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualObjectMethodA (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe bool CallNonvirtualBooleanMethod (JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualBooleanMethod (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe bool CallNonvirtualBooleanMethod (JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualBooleanMethodA (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe sbyte CallNonvirtualSByteMethod (JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualSByteMethod (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe sbyte CallNonvirtualSByteMethod (JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualSByteMethodA (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe char CallNonvirtualCharMethod (JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualCharMethod (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe char CallNonvirtualCharMethod (JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualCharMethodA (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe short CallNonvirtualShortMethod (JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualShortMethod (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe short CallNonvirtualShortMethod (JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualShortMethodA (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe int CallNonvirtualIntMethod (JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualIntMethod (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe int CallNonvirtualIntMethod (JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualIntMethodA (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe long CallNonvirtualLongMethod (JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualLongMethod (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe long CallNonvirtualLongMethod (JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualLongMethodA (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe float CallNonvirtualFloatMethod (JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualFloatMethod (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe float CallNonvirtualFloatMethod (JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualFloatMethodA (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe double CallNonvirtualDoubleMethod (JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualDoubleMethod (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe double CallNonvirtualDoubleMethod (JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualDoubleMethodA (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe void CallNonvirtualVoidMethod (JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			JniEnvironment.Current.Invoker.CallNonvirtualVoidMethod (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		internal static unsafe void CallNonvirtualVoidMethod (JniReferenceSafeHandle jobject, JniReferenceSafeHandle jclass, JniInstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			JniEnvironment.Current.Invoker.CallNonvirtualVoidMethodA (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe JniInstanceFieldID GetFieldID (JniReferenceSafeHandle jclass, string name, string sig)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (name == null)
				throw new ArgumentNullException ("name");
			if (sig == null)
				throw new ArgumentNullException ("sig");

			var tmp = JniEnvironment.Current.Invoker.GetFieldID (JniEnvironment.Current.SafeHandle, jclass, name, sig);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe JniLocalReference GetObjectField (JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetObjectField (JniEnvironment.Current.SafeHandle, jobject, jfieldID);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe bool GetBooleanField (JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetBooleanField (JniEnvironment.Current.SafeHandle, jobject, jfieldID);
			return tmp;
		}

		internal static unsafe sbyte GetByteField (JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetByteField (JniEnvironment.Current.SafeHandle, jobject, jfieldID);
			return tmp;
		}

		internal static unsafe char GetCharField (JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetCharField (JniEnvironment.Current.SafeHandle, jobject, jfieldID);
			return tmp;
		}

		internal static unsafe short GetShortField (JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetShortField (JniEnvironment.Current.SafeHandle, jobject, jfieldID);
			return tmp;
		}

		internal static unsafe int GetIntField (JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetIntField (JniEnvironment.Current.SafeHandle, jobject, jfieldID);
			return tmp;
		}

		internal static unsafe long GetLongField (JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetLongField (JniEnvironment.Current.SafeHandle, jobject, jfieldID);
			return tmp;
		}

		internal static unsafe float GetFloatField (JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetFloatField (JniEnvironment.Current.SafeHandle, jobject, jfieldID);
			return tmp;
		}

		internal static unsafe double GetDoubleField (JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetDoubleField (JniEnvironment.Current.SafeHandle, jobject, jfieldID);
			return tmp;
		}

		internal static unsafe void SetField (JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID, JniReferenceSafeHandle val)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			JniEnvironment.Current.Invoker.SetObjectField (JniEnvironment.Current.SafeHandle, jobject, jfieldID, val);
		}

		internal static unsafe void SetField (JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID, bool val)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			JniEnvironment.Current.Invoker.SetBooleanField (JniEnvironment.Current.SafeHandle, jobject, jfieldID, val);
		}

		internal static unsafe void SetField (JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID, sbyte val)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			JniEnvironment.Current.Invoker.SetByteField (JniEnvironment.Current.SafeHandle, jobject, jfieldID, val);
		}

		internal static unsafe void SetField (JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID, char val)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			JniEnvironment.Current.Invoker.SetCharField (JniEnvironment.Current.SafeHandle, jobject, jfieldID, val);
		}

		internal static unsafe void SetField (JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID, short val)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			JniEnvironment.Current.Invoker.SetShortField (JniEnvironment.Current.SafeHandle, jobject, jfieldID, val);
		}

		internal static unsafe void SetField (JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID, int val)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			JniEnvironment.Current.Invoker.SetIntField (JniEnvironment.Current.SafeHandle, jobject, jfieldID, val);
		}

		internal static unsafe void SetField (JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID, long val)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			JniEnvironment.Current.Invoker.SetLongField (JniEnvironment.Current.SafeHandle, jobject, jfieldID, val);
		}

		internal static unsafe void SetField (JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID, float val)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			JniEnvironment.Current.Invoker.SetFloatField (JniEnvironment.Current.SafeHandle, jobject, jfieldID, val);
		}

		internal static unsafe void SetField (JniReferenceSafeHandle jobject, JniInstanceFieldID jfieldID, double val)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			JniEnvironment.Current.Invoker.SetDoubleField (JniEnvironment.Current.SafeHandle, jobject, jfieldID, val);
		}

		public static unsafe JniStaticMethodID GetStaticMethodID (JniReferenceSafeHandle jclass, string name, string sig)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (name == null)
				throw new ArgumentNullException ("name");
			if (sig == null)
				throw new ArgumentNullException ("sig");

			var tmp = JniEnvironment.Current.Invoker.GetStaticMethodID (JniEnvironment.Current.SafeHandle, jclass, name, sig);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe JniLocalReference CallStaticObjectMethod (JniReferenceSafeHandle jclass, JniStaticMethodID jmethod)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticObjectMethod (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe JniLocalReference CallStaticObjectMethod (JniReferenceSafeHandle jclass, JniStaticMethodID jmethod, JValue* parms)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticObjectMethodA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe bool CallStaticBooleanMethod (JniReferenceSafeHandle jclass, JniStaticMethodID jmethod)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticBooleanMethod (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe bool CallStaticBooleanMethod (JniReferenceSafeHandle jclass, JniStaticMethodID jmethod, JValue* parms)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticBooleanMethodA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe sbyte CallStaticSByteMethod (JniReferenceSafeHandle jclass, JniStaticMethodID jmethod)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticSByteMethod (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe sbyte CallStaticSByteMethod (JniReferenceSafeHandle jclass, JniStaticMethodID jmethod, JValue* parms)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticSByteMethodA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe char CallStaticCharMethod (JniReferenceSafeHandle jclass, JniStaticMethodID jmethod)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticCharMethod (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe char CallStaticCharMethod (JniReferenceSafeHandle jclass, JniStaticMethodID jmethod, JValue* parms)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticCharMethodA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe short CallStaticShortMethod (JniReferenceSafeHandle jclass, JniStaticMethodID jmethod)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticShortMethod (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe short CallStaticShortMethod (JniReferenceSafeHandle jclass, JniStaticMethodID jmethod, JValue* parms)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticShortMethodA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe int CallStaticIntMethod (JniReferenceSafeHandle jclass, JniStaticMethodID jmethod)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticIntMethod (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe int CallStaticIntMethod (JniReferenceSafeHandle jclass, JniStaticMethodID jmethod, JValue* parms)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticIntMethodA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe long CallStaticLongMethod (JniReferenceSafeHandle jclass, JniStaticMethodID jmethod)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticLongMethod (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe long CallStaticLongMethod (JniReferenceSafeHandle jclass, JniStaticMethodID jmethod, JValue* parms)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticLongMethodA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe float CallStaticFloatMethod (JniReferenceSafeHandle jclass, JniStaticMethodID jmethod)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticFloatMethod (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe float CallStaticFloatMethod (JniReferenceSafeHandle jclass, JniStaticMethodID jmethod, JValue* parms)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticFloatMethodA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe double CallStaticDoubleMethod (JniReferenceSafeHandle jclass, JniStaticMethodID jmethod)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticDoubleMethod (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe double CallStaticDoubleMethod (JniReferenceSafeHandle jclass, JniStaticMethodID jmethod, JValue* parms)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticDoubleMethodA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe void CallStaticVoidMethod (JniReferenceSafeHandle jclass, JniStaticMethodID jmethod)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			JniEnvironment.Current.Invoker.CallStaticVoidMethod (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		internal static unsafe void CallStaticVoidMethod (JniReferenceSafeHandle jclass, JniStaticMethodID jmethod, JValue* parms)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jmethod == null)
				throw new ArgumentNullException ("jmethod");
			if (jmethod.IsInvalid)
				throw new ArgumentException ("jmethod");

			JniEnvironment.Current.Invoker.CallStaticVoidMethodA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe JniStaticFieldID GetStaticFieldID (JniReferenceSafeHandle jclass, string name, string sig)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (name == null)
				throw new ArgumentNullException ("name");
			if (sig == null)
				throw new ArgumentNullException ("sig");

			var tmp = JniEnvironment.Current.Invoker.GetStaticFieldID (JniEnvironment.Current.SafeHandle, jclass, name, sig);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe JniLocalReference GetStaticObjectField (JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetStaticObjectField (JniEnvironment.Current.SafeHandle, jclass, jfieldID);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe bool GetStaticBooleanField (JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetStaticBooleanField (JniEnvironment.Current.SafeHandle, jclass, jfieldID);
			return tmp;
		}

		internal static unsafe sbyte GetStaticByteField (JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetStaticByteField (JniEnvironment.Current.SafeHandle, jclass, jfieldID);
			return tmp;
		}

		internal static unsafe char GetStaticCharField (JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetStaticCharField (JniEnvironment.Current.SafeHandle, jclass, jfieldID);
			return tmp;
		}

		internal static unsafe short GetStaticShortField (JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetStaticShortField (JniEnvironment.Current.SafeHandle, jclass, jfieldID);
			return tmp;
		}

		internal static unsafe int GetStaticIntField (JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetStaticIntField (JniEnvironment.Current.SafeHandle, jclass, jfieldID);
			return tmp;
		}

		internal static unsafe long GetStaticLongField (JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetStaticLongField (JniEnvironment.Current.SafeHandle, jclass, jfieldID);
			return tmp;
		}

		internal static unsafe float GetStaticFloatField (JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetStaticFloatField (JniEnvironment.Current.SafeHandle, jclass, jfieldID);
			return tmp;
		}

		internal static unsafe double GetStaticDoubleField (JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetStaticDoubleField (JniEnvironment.Current.SafeHandle, jclass, jfieldID);
			return tmp;
		}

		internal static unsafe void SetStaticField (JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID, JniReferenceSafeHandle val)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			JniEnvironment.Current.Invoker.SetStaticObjectField (JniEnvironment.Current.SafeHandle, jclass, jfieldID, val);
		}

		internal static unsafe void SetStaticField (JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID, bool val)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			JniEnvironment.Current.Invoker.SetStaticBooleanField (JniEnvironment.Current.SafeHandle, jclass, jfieldID, val);
		}

		internal static unsafe void SetStaticField (JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID, sbyte val)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			JniEnvironment.Current.Invoker.SetStaticByteField (JniEnvironment.Current.SafeHandle, jclass, jfieldID, val);
		}

		internal static unsafe void SetStaticField (JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID, char val)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			JniEnvironment.Current.Invoker.SetStaticCharField (JniEnvironment.Current.SafeHandle, jclass, jfieldID, val);
		}

		internal static unsafe void SetStaticField (JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID, short val)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			JniEnvironment.Current.Invoker.SetStaticShortField (JniEnvironment.Current.SafeHandle, jclass, jfieldID, val);
		}

		internal static unsafe void SetStaticField (JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID, int val)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			JniEnvironment.Current.Invoker.SetStaticIntField (JniEnvironment.Current.SafeHandle, jclass, jfieldID, val);
		}

		internal static unsafe void SetStaticField (JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID, long val)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			JniEnvironment.Current.Invoker.SetStaticLongField (JniEnvironment.Current.SafeHandle, jclass, jfieldID, val);
		}

		internal static unsafe void SetStaticField (JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID, float val)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			JniEnvironment.Current.Invoker.SetStaticFloatField (JniEnvironment.Current.SafeHandle, jclass, jfieldID, val);
		}

		internal static unsafe void SetStaticField (JniReferenceSafeHandle jclass, JniStaticFieldID jfieldID, double val)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");
			if (jfieldID == null)
				throw new ArgumentNullException ("jfieldID");
			if (jfieldID.IsInvalid)
				throw new ArgumentException ("jfieldID");

			JniEnvironment.Current.Invoker.SetStaticDoubleField (JniEnvironment.Current.SafeHandle, jclass, jfieldID, val);
		}
	}

	internal static partial class Monitors {

		public static unsafe int MonitorEnter (JniReferenceSafeHandle obj)
		{
			if (obj == null)
				throw new ArgumentNullException ("obj");
			if (obj.IsInvalid)
				throw new ArgumentException ("obj");

			var tmp = JniEnvironment.Current.Invoker.MonitorEnter (JniEnvironment.Current.SafeHandle, obj);
			return tmp;
		}

		public static unsafe int MonitorExit (JniReferenceSafeHandle obj)
		{
			if (obj == null)
				throw new ArgumentNullException ("obj");
			if (obj.IsInvalid)
				throw new ArgumentException ("obj");

			var tmp = JniEnvironment.Current.Invoker.MonitorExit (JniEnvironment.Current.SafeHandle, obj);
			return tmp;
		}
	}

	public static partial class Strings {

		internal static unsafe JniLocalReference NewString (IntPtr unicodeChars, int len)
		{
			if (unicodeChars == IntPtr.Zero)
				throw new ArgumentException ("'unicodeChars' must not be IntPtr.Zero.", "unicodeChars");

			var tmp = JniEnvironment.Current.Invoker.NewString (JniEnvironment.Current.SafeHandle, unicodeChars, len);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe int GetStringLength (JniReferenceSafeHandle @string)
		{
			if (@string == null)
				throw new ArgumentNullException ("@string");
			if (@string.IsInvalid)
				throw new ArgumentException ("@string");

			var tmp = JniEnvironment.Current.Invoker.GetStringLength (JniEnvironment.Current.SafeHandle, @string);
			return tmp;
		}

		internal static unsafe IntPtr GetStringChars (JniReferenceSafeHandle @string, IntPtr isCopy)
		{
			if (@string == null)
				throw new ArgumentNullException ("@string");
			if (@string.IsInvalid)
				throw new ArgumentException ("@string");

			var tmp = JniEnvironment.Current.Invoker.GetStringChars (JniEnvironment.Current.SafeHandle, @string, isCopy);
			return tmp;
		}

		internal static unsafe void ReleaseStringChars (JniReferenceSafeHandle @string, IntPtr chars)
		{
			if (@string == null)
				throw new ArgumentNullException ("@string");
			if (@string.IsInvalid)
				throw new ArgumentException ("@string");
			if (chars == IntPtr.Zero)
				throw new ArgumentException ("'chars' must not be IntPtr.Zero.", "chars");

			JniEnvironment.Current.Invoker.ReleaseStringChars (JniEnvironment.Current.SafeHandle, @string, chars);
		}
	}

	public static partial class Types {

		internal static unsafe JniLocalReference DefineClass (string name, JniReferenceSafeHandle loader, IntPtr buf, int bufLen)
		{
			if (name == null)
				throw new ArgumentNullException ("name");
			if (loader == null)
				throw new ArgumentNullException ("loader");
			if (loader.IsInvalid)
				throw new ArgumentException ("loader");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			var tmp = JniEnvironment.Current.Invoker.DefineClass (JniEnvironment.Current.SafeHandle, name, loader, buf, bufLen);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe JniLocalReference FindClass (string classname)
		{
			if (classname == null)
				throw new ArgumentNullException ("classname");

			var tmp = JniEnvironment.Current.Invoker.FindClass (JniEnvironment.Current.SafeHandle, classname);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe JniLocalReference GetSuperclass (JniReferenceSafeHandle jclass)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");

			var tmp = JniEnvironment.Current.Invoker.GetSuperclass (JniEnvironment.Current.SafeHandle, jclass);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe bool IsAssignableFrom (JniReferenceSafeHandle clazz1, JniReferenceSafeHandle clazz2)
		{
			if (clazz1 == null)
				throw new ArgumentNullException ("clazz1");
			if (clazz1.IsInvalid)
				throw new ArgumentException ("clazz1");
			if (clazz2 == null)
				throw new ArgumentNullException ("clazz2");
			if (clazz2.IsInvalid)
				throw new ArgumentException ("clazz2");

			var tmp = JniEnvironment.Current.Invoker.IsAssignableFrom (JniEnvironment.Current.SafeHandle, clazz1, clazz2);
			return tmp;
		}

		public static unsafe bool IsSameObject (JniReferenceSafeHandle ref1, JniReferenceSafeHandle ref2)
		{
			var tmp = JniEnvironment.Current.Invoker.IsSameObject (JniEnvironment.Current.SafeHandle, ref1, ref2);
			return tmp;
		}

		public static unsafe JniLocalReference GetObjectClass (JniReferenceSafeHandle jobject)
		{
			if (jobject == null)
				throw new ArgumentNullException ("jobject");
			if (jobject.IsInvalid)
				throw new ArgumentException ("jobject");

			var tmp = JniEnvironment.Current.Invoker.GetObjectClass (JniEnvironment.Current.SafeHandle, jobject);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe bool IsInstanceOf (JniReferenceSafeHandle obj, JniReferenceSafeHandle clazz)
		{
			if (obj == null)
				throw new ArgumentNullException ("obj");
			if (obj.IsInvalid)
				throw new ArgumentException ("obj");
			if (clazz == null)
				throw new ArgumentNullException ("clazz");
			if (clazz.IsInvalid)
				throw new ArgumentException ("clazz");

			var tmp = JniEnvironment.Current.Invoker.IsInstanceOf (JniEnvironment.Current.SafeHandle, obj, clazz);
			return tmp;
		}

		internal static unsafe int RegisterNatives (JniReferenceSafeHandle jclass, JniNativeMethodRegistration [] methods, int nMethods)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");

			var tmp = JniEnvironment.Current.Invoker.RegisterNatives (JniEnvironment.Current.SafeHandle, jclass, methods, nMethods);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe int UnregisterNatives (JniReferenceSafeHandle jclass)
		{
			if (jclass == null)
				throw new ArgumentNullException ("jclass");
			if (jclass.IsInvalid)
				throw new ArgumentException ("jclass");

			var tmp = JniEnvironment.Current.Invoker.UnregisterNatives (JniEnvironment.Current.SafeHandle, jclass);
			return tmp;
		}
	}

	internal static partial class Versions {

		internal static unsafe int GetVersion ()
		{
			var tmp = JniEnvironment.Current.Invoker.GetVersion (JniEnvironment.Current.SafeHandle);
			return tmp;
		}
	}
	}

	partial class JniEnvironmentInvoker {

		internal JniNativeInterfaceStruct env;

		public unsafe JniEnvironmentInvoker (JniNativeInterfaceStruct* p)
		{
			env = *p;
		}


		JniFunc_JniEnvironmentSafeHandle_int _GetVersion;
		public JniFunc_JniEnvironmentSafeHandle_int GetVersion {
			get {
				if (_GetVersion == null)
					_GetVersion = (JniFunc_JniEnvironmentSafeHandle_int) Marshal.GetDelegateForFunctionPointer (env.GetVersion, typeof (JniFunc_JniEnvironmentSafeHandle_int));
				return _GetVersion;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_string_JniReferenceSafeHandle_IntPtr_int_JniLocalReference _DefineClass;
		public JniFunc_JniEnvironmentSafeHandle_string_JniReferenceSafeHandle_IntPtr_int_JniLocalReference DefineClass {
			get {
				if (_DefineClass == null)
					_DefineClass = (JniFunc_JniEnvironmentSafeHandle_string_JniReferenceSafeHandle_IntPtr_int_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.DefineClass, typeof (JniFunc_JniEnvironmentSafeHandle_string_JniReferenceSafeHandle_IntPtr_int_JniLocalReference));
				return _DefineClass;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_string_JniLocalReference _FindClass;
		public JniFunc_JniEnvironmentSafeHandle_string_JniLocalReference FindClass {
			get {
				if (_FindClass == null)
					_FindClass = (JniFunc_JniEnvironmentSafeHandle_string_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.FindClass, typeof (JniFunc_JniEnvironmentSafeHandle_string_JniLocalReference));
				return _FindClass;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID _FromReflectedMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID FromReflectedMethod {
			get {
				if (_FromReflectedMethod == null)
					_FromReflectedMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID) Marshal.GetDelegateForFunctionPointer (env.FromReflectedMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID));
				return _FromReflectedMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID _FromReflectedField;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID FromReflectedField {
			get {
				if (_FromReflectedField == null)
					_FromReflectedField = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID) Marshal.GetDelegateForFunctionPointer (env.FromReflectedField, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID));
				return _FromReflectedField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_bool_JniLocalReference _ToReflectedMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_bool_JniLocalReference ToReflectedMethod {
			get {
				if (_ToReflectedMethod == null)
					_ToReflectedMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_bool_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.ToReflectedMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_bool_JniLocalReference));
				return _ToReflectedMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference _GetSuperclass;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference GetSuperclass {
			get {
				if (_GetSuperclass == null)
					_GetSuperclass = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.GetSuperclass, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference));
				return _GetSuperclass;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_bool _IsAssignableFrom;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_bool IsAssignableFrom {
			get {
				if (_IsAssignableFrom == null)
					_IsAssignableFrom = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_bool) Marshal.GetDelegateForFunctionPointer (env.IsAssignableFrom, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_bool));
				return _IsAssignableFrom;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_bool_JniLocalReference _ToReflectedField;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_bool_JniLocalReference ToReflectedField {
			get {
				if (_ToReflectedField == null)
					_ToReflectedField = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_bool_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.ToReflectedField, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_bool_JniLocalReference));
				return _ToReflectedField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int _Throw;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int Throw {
			get {
				if (_Throw == null)
					_Throw = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int) Marshal.GetDelegateForFunctionPointer (env.Throw, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int));
				return _Throw;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_int _ThrowNew;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_int ThrowNew {
			get {
				if (_ThrowNew == null)
					_ThrowNew = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_int) Marshal.GetDelegateForFunctionPointer (env.ThrowNew, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_int));
				return _ThrowNew;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniLocalReference _ExceptionOccurred;
		public JniFunc_JniEnvironmentSafeHandle_JniLocalReference ExceptionOccurred {
			get {
				if (_ExceptionOccurred == null)
					_ExceptionOccurred = (JniFunc_JniEnvironmentSafeHandle_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.ExceptionOccurred, typeof (JniFunc_JniEnvironmentSafeHandle_JniLocalReference));
				return _ExceptionOccurred;
			}
		}

		JniAction_JniEnvironmentSafeHandle _ExceptionDescribe;
		public JniAction_JniEnvironmentSafeHandle ExceptionDescribe {
			get {
				if (_ExceptionDescribe == null)
					_ExceptionDescribe = (JniAction_JniEnvironmentSafeHandle) Marshal.GetDelegateForFunctionPointer (env.ExceptionDescribe, typeof (JniAction_JniEnvironmentSafeHandle));
				return _ExceptionDescribe;
			}
		}

		JniAction_JniEnvironmentSafeHandle _ExceptionClear;
		public JniAction_JniEnvironmentSafeHandle ExceptionClear {
			get {
				if (_ExceptionClear == null)
					_ExceptionClear = (JniAction_JniEnvironmentSafeHandle) Marshal.GetDelegateForFunctionPointer (env.ExceptionClear, typeof (JniAction_JniEnvironmentSafeHandle));
				return _ExceptionClear;
			}
		}

		JniAction_JniEnvironmentSafeHandle_string _FatalError;
		public JniAction_JniEnvironmentSafeHandle_string FatalError {
			get {
				if (_FatalError == null)
					_FatalError = (JniAction_JniEnvironmentSafeHandle_string) Marshal.GetDelegateForFunctionPointer (env.FatalError, typeof (JniAction_JniEnvironmentSafeHandle_string));
				return _FatalError;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_int _PushLocalFrame;
		public JniFunc_JniEnvironmentSafeHandle_int_int PushLocalFrame {
			get {
				if (_PushLocalFrame == null)
					_PushLocalFrame = (JniFunc_JniEnvironmentSafeHandle_int_int) Marshal.GetDelegateForFunctionPointer (env.PushLocalFrame, typeof (JniFunc_JniEnvironmentSafeHandle_int_int));
				return _PushLocalFrame;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference _PopLocalFrame;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference PopLocalFrame {
			get {
				if (_PopLocalFrame == null)
					_PopLocalFrame = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.PopLocalFrame, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference));
				return _PopLocalFrame;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniGlobalReference _NewGlobalRef;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniGlobalReference NewGlobalRef {
			get {
				if (_NewGlobalRef == null)
					_NewGlobalRef = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniGlobalReference) Marshal.GetDelegateForFunctionPointer (env.NewGlobalRef, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniGlobalReference));
				return _NewGlobalRef;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr _DeleteGlobalRef;
		public JniAction_JniEnvironmentSafeHandle_IntPtr DeleteGlobalRef {
			get {
				if (_DeleteGlobalRef == null)
					_DeleteGlobalRef = (JniAction_JniEnvironmentSafeHandle_IntPtr) Marshal.GetDelegateForFunctionPointer (env.DeleteGlobalRef, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr));
				return _DeleteGlobalRef;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr _DeleteLocalRef;
		public JniAction_JniEnvironmentSafeHandle_IntPtr DeleteLocalRef {
			get {
				if (_DeleteLocalRef == null)
					_DeleteLocalRef = (JniAction_JniEnvironmentSafeHandle_IntPtr) Marshal.GetDelegateForFunctionPointer (env.DeleteLocalRef, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr));
				return _DeleteLocalRef;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_bool _IsSameObject;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_bool IsSameObject {
			get {
				if (_IsSameObject == null)
					_IsSameObject = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_bool) Marshal.GetDelegateForFunctionPointer (env.IsSameObject, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_bool));
				return _IsSameObject;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference _NewLocalRef;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference NewLocalRef {
			get {
				if (_NewLocalRef == null)
					_NewLocalRef = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.NewLocalRef, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference));
				return _NewLocalRef;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_int _EnsureLocalCapacity;
		public JniFunc_JniEnvironmentSafeHandle_int_int EnsureLocalCapacity {
			get {
				if (_EnsureLocalCapacity == null)
					_EnsureLocalCapacity = (JniFunc_JniEnvironmentSafeHandle_int_int) Marshal.GetDelegateForFunctionPointer (env.EnsureLocalCapacity, typeof (JniFunc_JniEnvironmentSafeHandle_int_int));
				return _EnsureLocalCapacity;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference _AllocObject;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference AllocObject {
			get {
				if (_AllocObject == null)
					_AllocObject = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.AllocObject, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference));
				return _AllocObject;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JniLocalReference _NewObject;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JniLocalReference NewObject {
			get {
				if (_NewObject == null)
					_NewObject = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.NewObject, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JniLocalReference));
				return _NewObject;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_JniLocalReference _NewObjectA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_JniLocalReference NewObjectA {
			get {
				if (_NewObjectA == null)
					_NewObjectA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.NewObjectA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_JniLocalReference));
				return _NewObjectA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference _GetObjectClass;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference GetObjectClass {
			get {
				if (_GetObjectClass == null)
					_GetObjectClass = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.GetObjectClass, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniLocalReference));
				return _GetObjectClass;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_bool _IsInstanceOf;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_bool IsInstanceOf {
			get {
				if (_IsInstanceOf == null)
					_IsInstanceOf = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_bool) Marshal.GetDelegateForFunctionPointer (env.IsInstanceOf, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_bool));
				return _IsInstanceOf;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_string_JniInstanceMethodID _GetMethodID;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_string_JniInstanceMethodID GetMethodID {
			get {
				if (_GetMethodID == null)
					_GetMethodID = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_string_JniInstanceMethodID) Marshal.GetDelegateForFunctionPointer (env.GetMethodID, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_string_JniInstanceMethodID));
				return _GetMethodID;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JniLocalReference _CallObjectMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JniLocalReference CallObjectMethod {
			get {
				if (_CallObjectMethod == null)
					_CallObjectMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.CallObjectMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JniLocalReference));
				return _CallObjectMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_JniLocalReference _CallObjectMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_JniLocalReference CallObjectMethodA {
			get {
				if (_CallObjectMethodA == null)
					_CallObjectMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.CallObjectMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_JniLocalReference));
				return _CallObjectMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_bool _CallBooleanMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_bool CallBooleanMethod {
			get {
				if (_CallBooleanMethod == null)
					_CallBooleanMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_bool) Marshal.GetDelegateForFunctionPointer (env.CallBooleanMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_bool));
				return _CallBooleanMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_bool _CallBooleanMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_bool CallBooleanMethodA {
			get {
				if (_CallBooleanMethodA == null)
					_CallBooleanMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_bool) Marshal.GetDelegateForFunctionPointer (env.CallBooleanMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_bool));
				return _CallBooleanMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_sbyte _CallSByteMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_sbyte CallSByteMethod {
			get {
				if (_CallSByteMethod == null)
					_CallSByteMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_sbyte) Marshal.GetDelegateForFunctionPointer (env.CallSByteMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_sbyte));
				return _CallSByteMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_sbyte _CallSByteMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_sbyte CallSByteMethodA {
			get {
				if (_CallSByteMethodA == null)
					_CallSByteMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_sbyte) Marshal.GetDelegateForFunctionPointer (env.CallSByteMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_sbyte));
				return _CallSByteMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_char _CallCharMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_char CallCharMethod {
			get {
				if (_CallCharMethod == null)
					_CallCharMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_char) Marshal.GetDelegateForFunctionPointer (env.CallCharMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_char));
				return _CallCharMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_char _CallCharMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_char CallCharMethodA {
			get {
				if (_CallCharMethodA == null)
					_CallCharMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_char) Marshal.GetDelegateForFunctionPointer (env.CallCharMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_char));
				return _CallCharMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_short _CallShortMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_short CallShortMethod {
			get {
				if (_CallShortMethod == null)
					_CallShortMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_short) Marshal.GetDelegateForFunctionPointer (env.CallShortMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_short));
				return _CallShortMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_short _CallShortMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_short CallShortMethodA {
			get {
				if (_CallShortMethodA == null)
					_CallShortMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_short) Marshal.GetDelegateForFunctionPointer (env.CallShortMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_short));
				return _CallShortMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_int _CallIntMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_int CallIntMethod {
			get {
				if (_CallIntMethod == null)
					_CallIntMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_int) Marshal.GetDelegateForFunctionPointer (env.CallIntMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_int));
				return _CallIntMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_int _CallIntMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_int CallIntMethodA {
			get {
				if (_CallIntMethodA == null)
					_CallIntMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_int) Marshal.GetDelegateForFunctionPointer (env.CallIntMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_int));
				return _CallIntMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_long _CallLongMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_long CallLongMethod {
			get {
				if (_CallLongMethod == null)
					_CallLongMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_long) Marshal.GetDelegateForFunctionPointer (env.CallLongMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_long));
				return _CallLongMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_long _CallLongMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_long CallLongMethodA {
			get {
				if (_CallLongMethodA == null)
					_CallLongMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_long) Marshal.GetDelegateForFunctionPointer (env.CallLongMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_long));
				return _CallLongMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_float _CallFloatMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_float CallFloatMethod {
			get {
				if (_CallFloatMethod == null)
					_CallFloatMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_float) Marshal.GetDelegateForFunctionPointer (env.CallFloatMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_float));
				return _CallFloatMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_float _CallFloatMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_float CallFloatMethodA {
			get {
				if (_CallFloatMethodA == null)
					_CallFloatMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_float) Marshal.GetDelegateForFunctionPointer (env.CallFloatMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_float));
				return _CallFloatMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_double _CallDoubleMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_double CallDoubleMethod {
			get {
				if (_CallDoubleMethod == null)
					_CallDoubleMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_double) Marshal.GetDelegateForFunctionPointer (env.CallDoubleMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_double));
				return _CallDoubleMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_double _CallDoubleMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_double CallDoubleMethodA {
			get {
				if (_CallDoubleMethodA == null)
					_CallDoubleMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_double) Marshal.GetDelegateForFunctionPointer (env.CallDoubleMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_double));
				return _CallDoubleMethodA;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID _CallVoidMethod;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID CallVoidMethod {
			get {
				if (_CallVoidMethod == null)
					_CallVoidMethod = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID) Marshal.GetDelegateForFunctionPointer (env.CallVoidMethod, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID));
				return _CallVoidMethod;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef _CallVoidMethodA;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef CallVoidMethodA {
			get {
				if (_CallVoidMethodA == null)
					_CallVoidMethodA = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef) Marshal.GetDelegateForFunctionPointer (env.CallVoidMethodA, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef));
				return _CallVoidMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JniLocalReference _CallNonvirtualObjectMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JniLocalReference CallNonvirtualObjectMethod {
			get {
				if (_CallNonvirtualObjectMethod == null)
					_CallNonvirtualObjectMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualObjectMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JniLocalReference));
				return _CallNonvirtualObjectMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_JniLocalReference _CallNonvirtualObjectMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_JniLocalReference CallNonvirtualObjectMethodA {
			get {
				if (_CallNonvirtualObjectMethodA == null)
					_CallNonvirtualObjectMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualObjectMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_JniLocalReference));
				return _CallNonvirtualObjectMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_bool _CallNonvirtualBooleanMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_bool CallNonvirtualBooleanMethod {
			get {
				if (_CallNonvirtualBooleanMethod == null)
					_CallNonvirtualBooleanMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_bool) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualBooleanMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_bool));
				return _CallNonvirtualBooleanMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_bool _CallNonvirtualBooleanMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_bool CallNonvirtualBooleanMethodA {
			get {
				if (_CallNonvirtualBooleanMethodA == null)
					_CallNonvirtualBooleanMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_bool) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualBooleanMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_bool));
				return _CallNonvirtualBooleanMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_sbyte _CallNonvirtualSByteMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_sbyte CallNonvirtualSByteMethod {
			get {
				if (_CallNonvirtualSByteMethod == null)
					_CallNonvirtualSByteMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_sbyte) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualSByteMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_sbyte));
				return _CallNonvirtualSByteMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_sbyte _CallNonvirtualSByteMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_sbyte CallNonvirtualSByteMethodA {
			get {
				if (_CallNonvirtualSByteMethodA == null)
					_CallNonvirtualSByteMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_sbyte) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualSByteMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_sbyte));
				return _CallNonvirtualSByteMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_char _CallNonvirtualCharMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_char CallNonvirtualCharMethod {
			get {
				if (_CallNonvirtualCharMethod == null)
					_CallNonvirtualCharMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_char) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualCharMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_char));
				return _CallNonvirtualCharMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_char _CallNonvirtualCharMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_char CallNonvirtualCharMethodA {
			get {
				if (_CallNonvirtualCharMethodA == null)
					_CallNonvirtualCharMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_char) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualCharMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_char));
				return _CallNonvirtualCharMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_short _CallNonvirtualShortMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_short CallNonvirtualShortMethod {
			get {
				if (_CallNonvirtualShortMethod == null)
					_CallNonvirtualShortMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_short) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualShortMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_short));
				return _CallNonvirtualShortMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_short _CallNonvirtualShortMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_short CallNonvirtualShortMethodA {
			get {
				if (_CallNonvirtualShortMethodA == null)
					_CallNonvirtualShortMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_short) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualShortMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_short));
				return _CallNonvirtualShortMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_int _CallNonvirtualIntMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_int CallNonvirtualIntMethod {
			get {
				if (_CallNonvirtualIntMethod == null)
					_CallNonvirtualIntMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_int) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualIntMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_int));
				return _CallNonvirtualIntMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_int _CallNonvirtualIntMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_int CallNonvirtualIntMethodA {
			get {
				if (_CallNonvirtualIntMethodA == null)
					_CallNonvirtualIntMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_int) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualIntMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_int));
				return _CallNonvirtualIntMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_long _CallNonvirtualLongMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_long CallNonvirtualLongMethod {
			get {
				if (_CallNonvirtualLongMethod == null)
					_CallNonvirtualLongMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_long) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualLongMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_long));
				return _CallNonvirtualLongMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_long _CallNonvirtualLongMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_long CallNonvirtualLongMethodA {
			get {
				if (_CallNonvirtualLongMethodA == null)
					_CallNonvirtualLongMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_long) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualLongMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_long));
				return _CallNonvirtualLongMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_float _CallNonvirtualFloatMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_float CallNonvirtualFloatMethod {
			get {
				if (_CallNonvirtualFloatMethod == null)
					_CallNonvirtualFloatMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_float) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualFloatMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_float));
				return _CallNonvirtualFloatMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_float _CallNonvirtualFloatMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_float CallNonvirtualFloatMethodA {
			get {
				if (_CallNonvirtualFloatMethodA == null)
					_CallNonvirtualFloatMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_float) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualFloatMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_float));
				return _CallNonvirtualFloatMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_double _CallNonvirtualDoubleMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_double CallNonvirtualDoubleMethod {
			get {
				if (_CallNonvirtualDoubleMethod == null)
					_CallNonvirtualDoubleMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_double) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualDoubleMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_double));
				return _CallNonvirtualDoubleMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_double _CallNonvirtualDoubleMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_double CallNonvirtualDoubleMethodA {
			get {
				if (_CallNonvirtualDoubleMethodA == null)
					_CallNonvirtualDoubleMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_double) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualDoubleMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef_double));
				return _CallNonvirtualDoubleMethodA;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID _CallNonvirtualVoidMethod;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID CallNonvirtualVoidMethod {
			get {
				if (_CallNonvirtualVoidMethod == null)
					_CallNonvirtualVoidMethod = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualVoidMethod, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID));
				return _CallNonvirtualVoidMethod;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef _CallNonvirtualVoidMethodA;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef CallNonvirtualVoidMethodA {
			get {
				if (_CallNonvirtualVoidMethodA == null)
					_CallNonvirtualVoidMethodA = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualVoidMethodA, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniReferenceSafeHandle_JniInstanceMethodID_JValueRef));
				return _CallNonvirtualVoidMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_string_JniInstanceFieldID _GetFieldID;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_string_JniInstanceFieldID GetFieldID {
			get {
				if (_GetFieldID == null)
					_GetFieldID = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_string_JniInstanceFieldID) Marshal.GetDelegateForFunctionPointer (env.GetFieldID, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_string_JniInstanceFieldID));
				return _GetFieldID;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_JniLocalReference _GetObjectField;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_JniLocalReference GetObjectField {
			get {
				if (_GetObjectField == null)
					_GetObjectField = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.GetObjectField, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_JniLocalReference));
				return _GetObjectField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_bool _GetBooleanField;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_bool GetBooleanField {
			get {
				if (_GetBooleanField == null)
					_GetBooleanField = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_bool) Marshal.GetDelegateForFunctionPointer (env.GetBooleanField, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_bool));
				return _GetBooleanField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_sbyte _GetByteField;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_sbyte GetByteField {
			get {
				if (_GetByteField == null)
					_GetByteField = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_sbyte) Marshal.GetDelegateForFunctionPointer (env.GetByteField, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_sbyte));
				return _GetByteField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_char _GetCharField;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_char GetCharField {
			get {
				if (_GetCharField == null)
					_GetCharField = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_char) Marshal.GetDelegateForFunctionPointer (env.GetCharField, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_char));
				return _GetCharField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_short _GetShortField;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_short GetShortField {
			get {
				if (_GetShortField == null)
					_GetShortField = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_short) Marshal.GetDelegateForFunctionPointer (env.GetShortField, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_short));
				return _GetShortField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_int _GetIntField;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_int GetIntField {
			get {
				if (_GetIntField == null)
					_GetIntField = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_int) Marshal.GetDelegateForFunctionPointer (env.GetIntField, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_int));
				return _GetIntField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_long _GetLongField;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_long GetLongField {
			get {
				if (_GetLongField == null)
					_GetLongField = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_long) Marshal.GetDelegateForFunctionPointer (env.GetLongField, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_long));
				return _GetLongField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_float _GetFloatField;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_float GetFloatField {
			get {
				if (_GetFloatField == null)
					_GetFloatField = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_float) Marshal.GetDelegateForFunctionPointer (env.GetFloatField, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_float));
				return _GetFloatField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_double _GetDoubleField;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_double GetDoubleField {
			get {
				if (_GetDoubleField == null)
					_GetDoubleField = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_double) Marshal.GetDelegateForFunctionPointer (env.GetDoubleField, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_double));
				return _GetDoubleField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_JniReferenceSafeHandle _SetObjectField;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_JniReferenceSafeHandle SetObjectField {
			get {
				if (_SetObjectField == null)
					_SetObjectField = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_JniReferenceSafeHandle) Marshal.GetDelegateForFunctionPointer (env.SetObjectField, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_JniReferenceSafeHandle));
				return _SetObjectField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_bool _SetBooleanField;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_bool SetBooleanField {
			get {
				if (_SetBooleanField == null)
					_SetBooleanField = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_bool) Marshal.GetDelegateForFunctionPointer (env.SetBooleanField, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_bool));
				return _SetBooleanField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_sbyte _SetByteField;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_sbyte SetByteField {
			get {
				if (_SetByteField == null)
					_SetByteField = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_sbyte) Marshal.GetDelegateForFunctionPointer (env.SetByteField, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_sbyte));
				return _SetByteField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_char _SetCharField;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_char SetCharField {
			get {
				if (_SetCharField == null)
					_SetCharField = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_char) Marshal.GetDelegateForFunctionPointer (env.SetCharField, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_char));
				return _SetCharField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_short _SetShortField;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_short SetShortField {
			get {
				if (_SetShortField == null)
					_SetShortField = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_short) Marshal.GetDelegateForFunctionPointer (env.SetShortField, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_short));
				return _SetShortField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_int _SetIntField;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_int SetIntField {
			get {
				if (_SetIntField == null)
					_SetIntField = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_int) Marshal.GetDelegateForFunctionPointer (env.SetIntField, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_int));
				return _SetIntField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_long _SetLongField;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_long SetLongField {
			get {
				if (_SetLongField == null)
					_SetLongField = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_long) Marshal.GetDelegateForFunctionPointer (env.SetLongField, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_long));
				return _SetLongField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_float _SetFloatField;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_float SetFloatField {
			get {
				if (_SetFloatField == null)
					_SetFloatField = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_float) Marshal.GetDelegateForFunctionPointer (env.SetFloatField, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_float));
				return _SetFloatField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_double _SetDoubleField;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_double SetDoubleField {
			get {
				if (_SetDoubleField == null)
					_SetDoubleField = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_double) Marshal.GetDelegateForFunctionPointer (env.SetDoubleField, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniInstanceFieldID_double));
				return _SetDoubleField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_string_JniStaticMethodID _GetStaticMethodID;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_string_JniStaticMethodID GetStaticMethodID {
			get {
				if (_GetStaticMethodID == null)
					_GetStaticMethodID = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_string_JniStaticMethodID) Marshal.GetDelegateForFunctionPointer (env.GetStaticMethodID, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_string_JniStaticMethodID));
				return _GetStaticMethodID;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JniLocalReference _CallStaticObjectMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JniLocalReference CallStaticObjectMethod {
			get {
				if (_CallStaticObjectMethod == null)
					_CallStaticObjectMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.CallStaticObjectMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JniLocalReference));
				return _CallStaticObjectMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_JniLocalReference _CallStaticObjectMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_JniLocalReference CallStaticObjectMethodA {
			get {
				if (_CallStaticObjectMethodA == null)
					_CallStaticObjectMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.CallStaticObjectMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_JniLocalReference));
				return _CallStaticObjectMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_bool _CallStaticBooleanMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_bool CallStaticBooleanMethod {
			get {
				if (_CallStaticBooleanMethod == null)
					_CallStaticBooleanMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_bool) Marshal.GetDelegateForFunctionPointer (env.CallStaticBooleanMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_bool));
				return _CallStaticBooleanMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_bool _CallStaticBooleanMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_bool CallStaticBooleanMethodA {
			get {
				if (_CallStaticBooleanMethodA == null)
					_CallStaticBooleanMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_bool) Marshal.GetDelegateForFunctionPointer (env.CallStaticBooleanMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_bool));
				return _CallStaticBooleanMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_sbyte _CallStaticSByteMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_sbyte CallStaticSByteMethod {
			get {
				if (_CallStaticSByteMethod == null)
					_CallStaticSByteMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_sbyte) Marshal.GetDelegateForFunctionPointer (env.CallStaticSByteMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_sbyte));
				return _CallStaticSByteMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_sbyte _CallStaticSByteMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_sbyte CallStaticSByteMethodA {
			get {
				if (_CallStaticSByteMethodA == null)
					_CallStaticSByteMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_sbyte) Marshal.GetDelegateForFunctionPointer (env.CallStaticSByteMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_sbyte));
				return _CallStaticSByteMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_char _CallStaticCharMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_char CallStaticCharMethod {
			get {
				if (_CallStaticCharMethod == null)
					_CallStaticCharMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_char) Marshal.GetDelegateForFunctionPointer (env.CallStaticCharMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_char));
				return _CallStaticCharMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_char _CallStaticCharMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_char CallStaticCharMethodA {
			get {
				if (_CallStaticCharMethodA == null)
					_CallStaticCharMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_char) Marshal.GetDelegateForFunctionPointer (env.CallStaticCharMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_char));
				return _CallStaticCharMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_short _CallStaticShortMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_short CallStaticShortMethod {
			get {
				if (_CallStaticShortMethod == null)
					_CallStaticShortMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_short) Marshal.GetDelegateForFunctionPointer (env.CallStaticShortMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_short));
				return _CallStaticShortMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_short _CallStaticShortMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_short CallStaticShortMethodA {
			get {
				if (_CallStaticShortMethodA == null)
					_CallStaticShortMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_short) Marshal.GetDelegateForFunctionPointer (env.CallStaticShortMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_short));
				return _CallStaticShortMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_int _CallStaticIntMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_int CallStaticIntMethod {
			get {
				if (_CallStaticIntMethod == null)
					_CallStaticIntMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_int) Marshal.GetDelegateForFunctionPointer (env.CallStaticIntMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_int));
				return _CallStaticIntMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_int _CallStaticIntMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_int CallStaticIntMethodA {
			get {
				if (_CallStaticIntMethodA == null)
					_CallStaticIntMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_int) Marshal.GetDelegateForFunctionPointer (env.CallStaticIntMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_int));
				return _CallStaticIntMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_long _CallStaticLongMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_long CallStaticLongMethod {
			get {
				if (_CallStaticLongMethod == null)
					_CallStaticLongMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_long) Marshal.GetDelegateForFunctionPointer (env.CallStaticLongMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_long));
				return _CallStaticLongMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_long _CallStaticLongMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_long CallStaticLongMethodA {
			get {
				if (_CallStaticLongMethodA == null)
					_CallStaticLongMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_long) Marshal.GetDelegateForFunctionPointer (env.CallStaticLongMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_long));
				return _CallStaticLongMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_float _CallStaticFloatMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_float CallStaticFloatMethod {
			get {
				if (_CallStaticFloatMethod == null)
					_CallStaticFloatMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_float) Marshal.GetDelegateForFunctionPointer (env.CallStaticFloatMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_float));
				return _CallStaticFloatMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_float _CallStaticFloatMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_float CallStaticFloatMethodA {
			get {
				if (_CallStaticFloatMethodA == null)
					_CallStaticFloatMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_float) Marshal.GetDelegateForFunctionPointer (env.CallStaticFloatMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_float));
				return _CallStaticFloatMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_double _CallStaticDoubleMethod;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_double CallStaticDoubleMethod {
			get {
				if (_CallStaticDoubleMethod == null)
					_CallStaticDoubleMethod = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_double) Marshal.GetDelegateForFunctionPointer (env.CallStaticDoubleMethod, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_double));
				return _CallStaticDoubleMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_double _CallStaticDoubleMethodA;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_double CallStaticDoubleMethodA {
			get {
				if (_CallStaticDoubleMethodA == null)
					_CallStaticDoubleMethodA = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_double) Marshal.GetDelegateForFunctionPointer (env.CallStaticDoubleMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef_double));
				return _CallStaticDoubleMethodA;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID _CallStaticVoidMethod;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID CallStaticVoidMethod {
			get {
				if (_CallStaticVoidMethod == null)
					_CallStaticVoidMethod = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID) Marshal.GetDelegateForFunctionPointer (env.CallStaticVoidMethod, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID));
				return _CallStaticVoidMethod;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef _CallStaticVoidMethodA;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef CallStaticVoidMethodA {
			get {
				if (_CallStaticVoidMethodA == null)
					_CallStaticVoidMethodA = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef) Marshal.GetDelegateForFunctionPointer (env.CallStaticVoidMethodA, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticMethodID_JValueRef));
				return _CallStaticVoidMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_string_JniStaticFieldID _GetStaticFieldID;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_string_JniStaticFieldID GetStaticFieldID {
			get {
				if (_GetStaticFieldID == null)
					_GetStaticFieldID = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_string_JniStaticFieldID) Marshal.GetDelegateForFunctionPointer (env.GetStaticFieldID, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string_string_JniStaticFieldID));
				return _GetStaticFieldID;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_JniLocalReference _GetStaticObjectField;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_JniLocalReference GetStaticObjectField {
			get {
				if (_GetStaticObjectField == null)
					_GetStaticObjectField = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.GetStaticObjectField, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_JniLocalReference));
				return _GetStaticObjectField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_bool _GetStaticBooleanField;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_bool GetStaticBooleanField {
			get {
				if (_GetStaticBooleanField == null)
					_GetStaticBooleanField = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_bool) Marshal.GetDelegateForFunctionPointer (env.GetStaticBooleanField, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_bool));
				return _GetStaticBooleanField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_sbyte _GetStaticByteField;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_sbyte GetStaticByteField {
			get {
				if (_GetStaticByteField == null)
					_GetStaticByteField = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_sbyte) Marshal.GetDelegateForFunctionPointer (env.GetStaticByteField, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_sbyte));
				return _GetStaticByteField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_char _GetStaticCharField;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_char GetStaticCharField {
			get {
				if (_GetStaticCharField == null)
					_GetStaticCharField = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_char) Marshal.GetDelegateForFunctionPointer (env.GetStaticCharField, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_char));
				return _GetStaticCharField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_short _GetStaticShortField;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_short GetStaticShortField {
			get {
				if (_GetStaticShortField == null)
					_GetStaticShortField = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_short) Marshal.GetDelegateForFunctionPointer (env.GetStaticShortField, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_short));
				return _GetStaticShortField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_int _GetStaticIntField;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_int GetStaticIntField {
			get {
				if (_GetStaticIntField == null)
					_GetStaticIntField = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_int) Marshal.GetDelegateForFunctionPointer (env.GetStaticIntField, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_int));
				return _GetStaticIntField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_long _GetStaticLongField;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_long GetStaticLongField {
			get {
				if (_GetStaticLongField == null)
					_GetStaticLongField = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_long) Marshal.GetDelegateForFunctionPointer (env.GetStaticLongField, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_long));
				return _GetStaticLongField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_float _GetStaticFloatField;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_float GetStaticFloatField {
			get {
				if (_GetStaticFloatField == null)
					_GetStaticFloatField = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_float) Marshal.GetDelegateForFunctionPointer (env.GetStaticFloatField, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_float));
				return _GetStaticFloatField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_double _GetStaticDoubleField;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_double GetStaticDoubleField {
			get {
				if (_GetStaticDoubleField == null)
					_GetStaticDoubleField = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_double) Marshal.GetDelegateForFunctionPointer (env.GetStaticDoubleField, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_double));
				return _GetStaticDoubleField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_JniReferenceSafeHandle _SetStaticObjectField;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_JniReferenceSafeHandle SetStaticObjectField {
			get {
				if (_SetStaticObjectField == null)
					_SetStaticObjectField = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_JniReferenceSafeHandle) Marshal.GetDelegateForFunctionPointer (env.SetStaticObjectField, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_JniReferenceSafeHandle));
				return _SetStaticObjectField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_bool _SetStaticBooleanField;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_bool SetStaticBooleanField {
			get {
				if (_SetStaticBooleanField == null)
					_SetStaticBooleanField = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_bool) Marshal.GetDelegateForFunctionPointer (env.SetStaticBooleanField, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_bool));
				return _SetStaticBooleanField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_sbyte _SetStaticByteField;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_sbyte SetStaticByteField {
			get {
				if (_SetStaticByteField == null)
					_SetStaticByteField = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_sbyte) Marshal.GetDelegateForFunctionPointer (env.SetStaticByteField, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_sbyte));
				return _SetStaticByteField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_char _SetStaticCharField;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_char SetStaticCharField {
			get {
				if (_SetStaticCharField == null)
					_SetStaticCharField = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_char) Marshal.GetDelegateForFunctionPointer (env.SetStaticCharField, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_char));
				return _SetStaticCharField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_short _SetStaticShortField;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_short SetStaticShortField {
			get {
				if (_SetStaticShortField == null)
					_SetStaticShortField = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_short) Marshal.GetDelegateForFunctionPointer (env.SetStaticShortField, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_short));
				return _SetStaticShortField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_int _SetStaticIntField;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_int SetStaticIntField {
			get {
				if (_SetStaticIntField == null)
					_SetStaticIntField = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_int) Marshal.GetDelegateForFunctionPointer (env.SetStaticIntField, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_int));
				return _SetStaticIntField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_long _SetStaticLongField;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_long SetStaticLongField {
			get {
				if (_SetStaticLongField == null)
					_SetStaticLongField = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_long) Marshal.GetDelegateForFunctionPointer (env.SetStaticLongField, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_long));
				return _SetStaticLongField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_float _SetStaticFloatField;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_float SetStaticFloatField {
			get {
				if (_SetStaticFloatField == null)
					_SetStaticFloatField = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_float) Marshal.GetDelegateForFunctionPointer (env.SetStaticFloatField, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_float));
				return _SetStaticFloatField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_double _SetStaticDoubleField;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_double SetStaticDoubleField {
			get {
				if (_SetStaticDoubleField == null)
					_SetStaticDoubleField = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_double) Marshal.GetDelegateForFunctionPointer (env.SetStaticDoubleField, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniStaticFieldID_double));
				return _SetStaticDoubleField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_int_JniLocalReference _NewString;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_int_JniLocalReference NewString {
			get {
				if (_NewString == null)
					_NewString = (JniFunc_JniEnvironmentSafeHandle_IntPtr_int_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.NewString, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_int_JniLocalReference));
				return _NewString;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int _GetStringLength;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int GetStringLength {
			get {
				if (_GetStringLength == null)
					_GetStringLength = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int) Marshal.GetDelegateForFunctionPointer (env.GetStringLength, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int));
				return _GetStringLength;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr _GetStringChars;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr GetStringChars {
			get {
				if (_GetStringChars == null)
					_GetStringChars = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetStringChars, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr));
				return _GetStringChars;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr _ReleaseStringChars;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr ReleaseStringChars {
			get {
				if (_ReleaseStringChars == null)
					_ReleaseStringChars = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr) Marshal.GetDelegateForFunctionPointer (env.ReleaseStringChars, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr));
				return _ReleaseStringChars;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_string_JniLocalReference _NewStringUTF;
		public JniFunc_JniEnvironmentSafeHandle_string_JniLocalReference NewStringUTF {
			get {
				if (_NewStringUTF == null)
					_NewStringUTF = (JniFunc_JniEnvironmentSafeHandle_string_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.NewStringUTF, typeof (JniFunc_JniEnvironmentSafeHandle_string_JniLocalReference));
				return _NewStringUTF;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int _GetStringUTFLength;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int GetStringUTFLength {
			get {
				if (_GetStringUTFLength == null)
					_GetStringUTFLength = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int) Marshal.GetDelegateForFunctionPointer (env.GetStringUTFLength, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int));
				return _GetStringUTFLength;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_string _GetStringUTFChars;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_string GetStringUTFChars {
			get {
				if (_GetStringUTFChars == null)
					_GetStringUTFChars = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_string) Marshal.GetDelegateForFunctionPointer (env.GetStringUTFChars, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_string));
				return _GetStringUTFChars;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string _ReleaseStringUTFChars;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string ReleaseStringUTFChars {
			get {
				if (_ReleaseStringUTFChars == null)
					_ReleaseStringUTFChars = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string) Marshal.GetDelegateForFunctionPointer (env.ReleaseStringUTFChars, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string));
				return _ReleaseStringUTFChars;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int _GetArrayLength;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int GetArrayLength {
			get {
				if (_GetArrayLength == null)
					_GetArrayLength = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int) Marshal.GetDelegateForFunctionPointer (env.GetArrayLength, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int));
				return _GetArrayLength;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_JniReferenceSafeHandle_JniReferenceSafeHandle_JniLocalReference _NewObjectArray;
		public JniFunc_JniEnvironmentSafeHandle_int_JniReferenceSafeHandle_JniReferenceSafeHandle_JniLocalReference NewObjectArray {
			get {
				if (_NewObjectArray == null)
					_NewObjectArray = (JniFunc_JniEnvironmentSafeHandle_int_JniReferenceSafeHandle_JniReferenceSafeHandle_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.NewObjectArray, typeof (JniFunc_JniEnvironmentSafeHandle_int_JniReferenceSafeHandle_JniReferenceSafeHandle_JniLocalReference));
				return _NewObjectArray;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_JniLocalReference _GetObjectArrayElement;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_JniLocalReference GetObjectArrayElement {
			get {
				if (_GetObjectArrayElement == null)
					_GetObjectArrayElement = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.GetObjectArrayElement, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_JniLocalReference));
				return _GetObjectArrayElement;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_JniReferenceSafeHandle _SetObjectArrayElement;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_JniReferenceSafeHandle SetObjectArrayElement {
			get {
				if (_SetObjectArrayElement == null)
					_SetObjectArrayElement = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_JniReferenceSafeHandle) Marshal.GetDelegateForFunctionPointer (env.SetObjectArrayElement, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_JniReferenceSafeHandle));
				return _SetObjectArrayElement;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference _NewBooleanArray;
		public JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference NewBooleanArray {
			get {
				if (_NewBooleanArray == null)
					_NewBooleanArray = (JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.NewBooleanArray, typeof (JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference));
				return _NewBooleanArray;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference _NewByteArray;
		public JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference NewByteArray {
			get {
				if (_NewByteArray == null)
					_NewByteArray = (JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.NewByteArray, typeof (JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference));
				return _NewByteArray;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference _NewCharArray;
		public JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference NewCharArray {
			get {
				if (_NewCharArray == null)
					_NewCharArray = (JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.NewCharArray, typeof (JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference));
				return _NewCharArray;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference _NewShortArray;
		public JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference NewShortArray {
			get {
				if (_NewShortArray == null)
					_NewShortArray = (JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.NewShortArray, typeof (JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference));
				return _NewShortArray;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference _NewIntArray;
		public JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference NewIntArray {
			get {
				if (_NewIntArray == null)
					_NewIntArray = (JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.NewIntArray, typeof (JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference));
				return _NewIntArray;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference _NewLongArray;
		public JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference NewLongArray {
			get {
				if (_NewLongArray == null)
					_NewLongArray = (JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.NewLongArray, typeof (JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference));
				return _NewLongArray;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference _NewFloatArray;
		public JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference NewFloatArray {
			get {
				if (_NewFloatArray == null)
					_NewFloatArray = (JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.NewFloatArray, typeof (JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference));
				return _NewFloatArray;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference _NewDoubleArray;
		public JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference NewDoubleArray {
			get {
				if (_NewDoubleArray == null)
					_NewDoubleArray = (JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.NewDoubleArray, typeof (JniFunc_JniEnvironmentSafeHandle_int_JniLocalReference));
				return _NewDoubleArray;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr _GetBooleanArrayElements;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr GetBooleanArrayElements {
			get {
				if (_GetBooleanArrayElements == null)
					_GetBooleanArrayElements = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetBooleanArrayElements, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr));
				return _GetBooleanArrayElements;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr _GetByteArrayElements;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr GetByteArrayElements {
			get {
				if (_GetByteArrayElements == null)
					_GetByteArrayElements = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetByteArrayElements, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr));
				return _GetByteArrayElements;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr _GetCharArrayElements;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr GetCharArrayElements {
			get {
				if (_GetCharArrayElements == null)
					_GetCharArrayElements = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetCharArrayElements, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr));
				return _GetCharArrayElements;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr _GetShortArrayElements;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr GetShortArrayElements {
			get {
				if (_GetShortArrayElements == null)
					_GetShortArrayElements = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetShortArrayElements, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr));
				return _GetShortArrayElements;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr _GetIntArrayElements;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr GetIntArrayElements {
			get {
				if (_GetIntArrayElements == null)
					_GetIntArrayElements = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetIntArrayElements, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr));
				return _GetIntArrayElements;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr _GetLongArrayElements;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr GetLongArrayElements {
			get {
				if (_GetLongArrayElements == null)
					_GetLongArrayElements = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetLongArrayElements, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr));
				return _GetLongArrayElements;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr _GetFloatArrayElements;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr GetFloatArrayElements {
			get {
				if (_GetFloatArrayElements == null)
					_GetFloatArrayElements = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetFloatArrayElements, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr));
				return _GetFloatArrayElements;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr _GetDoubleArrayElements;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr GetDoubleArrayElements {
			get {
				if (_GetDoubleArrayElements == null)
					_GetDoubleArrayElements = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetDoubleArrayElements, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr));
				return _GetDoubleArrayElements;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int _ReleaseBooleanArrayElements;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int ReleaseBooleanArrayElements {
			get {
				if (_ReleaseBooleanArrayElements == null)
					_ReleaseBooleanArrayElements = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.ReleaseBooleanArrayElements, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int));
				return _ReleaseBooleanArrayElements;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int _ReleaseByteArrayElements;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int ReleaseByteArrayElements {
			get {
				if (_ReleaseByteArrayElements == null)
					_ReleaseByteArrayElements = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.ReleaseByteArrayElements, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int));
				return _ReleaseByteArrayElements;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int _ReleaseCharArrayElements;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int ReleaseCharArrayElements {
			get {
				if (_ReleaseCharArrayElements == null)
					_ReleaseCharArrayElements = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.ReleaseCharArrayElements, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int));
				return _ReleaseCharArrayElements;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int _ReleaseShortArrayElements;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int ReleaseShortArrayElements {
			get {
				if (_ReleaseShortArrayElements == null)
					_ReleaseShortArrayElements = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.ReleaseShortArrayElements, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int));
				return _ReleaseShortArrayElements;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int _ReleaseIntArrayElements;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int ReleaseIntArrayElements {
			get {
				if (_ReleaseIntArrayElements == null)
					_ReleaseIntArrayElements = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.ReleaseIntArrayElements, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int));
				return _ReleaseIntArrayElements;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int _ReleaseLongArrayElements;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int ReleaseLongArrayElements {
			get {
				if (_ReleaseLongArrayElements == null)
					_ReleaseLongArrayElements = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.ReleaseLongArrayElements, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int));
				return _ReleaseLongArrayElements;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int _ReleaseFloatArrayElements;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int ReleaseFloatArrayElements {
			get {
				if (_ReleaseFloatArrayElements == null)
					_ReleaseFloatArrayElements = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.ReleaseFloatArrayElements, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int));
				return _ReleaseFloatArrayElements;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int _ReleaseDoubleArrayElements;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int ReleaseDoubleArrayElements {
			get {
				if (_ReleaseDoubleArrayElements == null)
					_ReleaseDoubleArrayElements = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.ReleaseDoubleArrayElements, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int));
				return _ReleaseDoubleArrayElements;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr _GetBooleanArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr GetBooleanArrayRegion {
			get {
				if (_GetBooleanArrayRegion == null)
					_GetBooleanArrayRegion = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetBooleanArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr));
				return _GetBooleanArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr _GetByteArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr GetByteArrayRegion {
			get {
				if (_GetByteArrayRegion == null)
					_GetByteArrayRegion = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetByteArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr));
				return _GetByteArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr _GetCharArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr GetCharArrayRegion {
			get {
				if (_GetCharArrayRegion == null)
					_GetCharArrayRegion = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetCharArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr));
				return _GetCharArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr _GetShortArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr GetShortArrayRegion {
			get {
				if (_GetShortArrayRegion == null)
					_GetShortArrayRegion = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetShortArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr));
				return _GetShortArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr _GetIntArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr GetIntArrayRegion {
			get {
				if (_GetIntArrayRegion == null)
					_GetIntArrayRegion = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetIntArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr));
				return _GetIntArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr _GetLongArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr GetLongArrayRegion {
			get {
				if (_GetLongArrayRegion == null)
					_GetLongArrayRegion = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetLongArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr));
				return _GetLongArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr _GetFloatArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr GetFloatArrayRegion {
			get {
				if (_GetFloatArrayRegion == null)
					_GetFloatArrayRegion = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetFloatArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr));
				return _GetFloatArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr _GetDoubleArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr GetDoubleArrayRegion {
			get {
				if (_GetDoubleArrayRegion == null)
					_GetDoubleArrayRegion = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetDoubleArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr));
				return _GetDoubleArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr _SetBooleanArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr SetBooleanArrayRegion {
			get {
				if (_SetBooleanArrayRegion == null)
					_SetBooleanArrayRegion = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.SetBooleanArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr));
				return _SetBooleanArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr _SetByteArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr SetByteArrayRegion {
			get {
				if (_SetByteArrayRegion == null)
					_SetByteArrayRegion = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.SetByteArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr));
				return _SetByteArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr _SetCharArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr SetCharArrayRegion {
			get {
				if (_SetCharArrayRegion == null)
					_SetCharArrayRegion = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.SetCharArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr));
				return _SetCharArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr _SetShortArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr SetShortArrayRegion {
			get {
				if (_SetShortArrayRegion == null)
					_SetShortArrayRegion = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.SetShortArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr));
				return _SetShortArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr _SetIntArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr SetIntArrayRegion {
			get {
				if (_SetIntArrayRegion == null)
					_SetIntArrayRegion = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.SetIntArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr));
				return _SetIntArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr _SetLongArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr SetLongArrayRegion {
			get {
				if (_SetLongArrayRegion == null)
					_SetLongArrayRegion = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.SetLongArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr));
				return _SetLongArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr _SetFloatArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr SetFloatArrayRegion {
			get {
				if (_SetFloatArrayRegion == null)
					_SetFloatArrayRegion = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.SetFloatArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr));
				return _SetFloatArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr _SetDoubleArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr SetDoubleArrayRegion {
			get {
				if (_SetDoubleArrayRegion == null)
					_SetDoubleArrayRegion = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.SetDoubleArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr));
				return _SetDoubleArrayRegion;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniNativeMethodRegistrationArray_int_int _RegisterNatives;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniNativeMethodRegistrationArray_int_int RegisterNatives {
			get {
				if (_RegisterNatives == null)
					_RegisterNatives = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniNativeMethodRegistrationArray_int_int) Marshal.GetDelegateForFunctionPointer (env.RegisterNatives, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniNativeMethodRegistrationArray_int_int));
				return _RegisterNatives;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int _UnregisterNatives;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int UnregisterNatives {
			get {
				if (_UnregisterNatives == null)
					_UnregisterNatives = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int) Marshal.GetDelegateForFunctionPointer (env.UnregisterNatives, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int));
				return _UnregisterNatives;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int _MonitorEnter;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int MonitorEnter {
			get {
				if (_MonitorEnter == null)
					_MonitorEnter = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int) Marshal.GetDelegateForFunctionPointer (env.MonitorEnter, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int));
				return _MonitorEnter;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int _MonitorExit;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int MonitorExit {
			get {
				if (_MonitorExit == null)
					_MonitorExit = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int) Marshal.GetDelegateForFunctionPointer (env.MonitorExit, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int));
				return _MonitorExit;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_outJavaVMSafeHandle_int _GetJavaVM;
		public JniFunc_JniEnvironmentSafeHandle_outJavaVMSafeHandle_int GetJavaVM {
			get {
				if (_GetJavaVM == null)
					_GetJavaVM = (JniFunc_JniEnvironmentSafeHandle_outJavaVMSafeHandle_int) Marshal.GetDelegateForFunctionPointer (env.GetJavaVM, typeof (JniFunc_JniEnvironmentSafeHandle_outJavaVMSafeHandle_int));
				return _GetJavaVM;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr _GetStringRegion;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr GetStringRegion {
			get {
				if (_GetStringRegion == null)
					_GetStringRegion = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetStringRegion, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr));
				return _GetStringRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr _GetStringUTFRegion;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr GetStringUTFRegion {
			get {
				if (_GetStringUTFRegion == null)
					_GetStringUTFRegion = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetStringUTFRegion, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_int_int_IntPtr));
				return _GetStringUTFRegion;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr _GetPrimitiveArrayCritical;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr GetPrimitiveArrayCritical {
			get {
				if (_GetPrimitiveArrayCritical == null)
					_GetPrimitiveArrayCritical = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetPrimitiveArrayCritical, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_IntPtr));
				return _GetPrimitiveArrayCritical;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int _ReleasePrimitiveArrayCritical;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int ReleasePrimitiveArrayCritical {
			get {
				if (_ReleasePrimitiveArrayCritical == null)
					_ReleasePrimitiveArrayCritical = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.ReleasePrimitiveArrayCritical, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_int));
				return _ReleasePrimitiveArrayCritical;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_string _GetStringCritical;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_string GetStringCritical {
			get {
				if (_GetStringCritical == null)
					_GetStringCritical = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_string) Marshal.GetDelegateForFunctionPointer (env.GetStringCritical, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr_string));
				return _GetStringCritical;
			}
		}

		JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string _ReleaseStringCritical;
		public JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string ReleaseStringCritical {
			get {
				if (_ReleaseStringCritical == null)
					_ReleaseStringCritical = (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string) Marshal.GetDelegateForFunctionPointer (env.ReleaseStringCritical, typeof (JniAction_JniEnvironmentSafeHandle_JniReferenceSafeHandle_string));
				return _ReleaseStringCritical;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniWeakGlobalReference _NewWeakGlobalRef;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniWeakGlobalReference NewWeakGlobalRef {
			get {
				if (_NewWeakGlobalRef == null)
					_NewWeakGlobalRef = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniWeakGlobalReference) Marshal.GetDelegateForFunctionPointer (env.NewWeakGlobalRef, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniWeakGlobalReference));
				return _NewWeakGlobalRef;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr _DeleteWeakGlobalRef;
		public JniAction_JniEnvironmentSafeHandle_IntPtr DeleteWeakGlobalRef {
			get {
				if (_DeleteWeakGlobalRef == null)
					_DeleteWeakGlobalRef = (JniAction_JniEnvironmentSafeHandle_IntPtr) Marshal.GetDelegateForFunctionPointer (env.DeleteWeakGlobalRef, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr));
				return _DeleteWeakGlobalRef;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_bool _ExceptionCheck;
		public JniFunc_JniEnvironmentSafeHandle_bool ExceptionCheck {
			get {
				if (_ExceptionCheck == null)
					_ExceptionCheck = (JniFunc_JniEnvironmentSafeHandle_bool) Marshal.GetDelegateForFunctionPointer (env.ExceptionCheck, typeof (JniFunc_JniEnvironmentSafeHandle_bool));
				return _ExceptionCheck;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_long_JniLocalReference _NewDirectByteBuffer;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_long_JniLocalReference NewDirectByteBuffer {
			get {
				if (_NewDirectByteBuffer == null)
					_NewDirectByteBuffer = (JniFunc_JniEnvironmentSafeHandle_IntPtr_long_JniLocalReference) Marshal.GetDelegateForFunctionPointer (env.NewDirectByteBuffer, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_long_JniLocalReference));
				return _NewDirectByteBuffer;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr _GetDirectBufferAddress;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr GetDirectBufferAddress {
			get {
				if (_GetDirectBufferAddress == null)
					_GetDirectBufferAddress = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetDirectBufferAddress, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_IntPtr));
				return _GetDirectBufferAddress;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_long _GetDirectBufferCapacity;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_long GetDirectBufferCapacity {
			get {
				if (_GetDirectBufferCapacity == null)
					_GetDirectBufferCapacity = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_long) Marshal.GetDelegateForFunctionPointer (env.GetDirectBufferCapacity, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_long));
				return _GetDirectBufferCapacity;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniObjectReferenceType _GetObjectRefType;
		public JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniObjectReferenceType GetObjectRefType {
			get {
				if (_GetObjectRefType == null)
					_GetObjectRefType = (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniObjectReferenceType) Marshal.GetDelegateForFunctionPointer (env.GetObjectRefType, typeof (JniFunc_JniEnvironmentSafeHandle_JniReferenceSafeHandle_JniObjectReferenceType));
				return _GetObjectRefType;
			}
		}
	}
}
#endif  // FEATURE_HANDLES_ARE_SAFE_HANDLES
#if FEATURE_HANDLES_ARE_INTPTRS
namespace
#if _NAMESPACE_PER_HANDLE
	Java.Interop.IntPtrs
#else
	Java.Interop
#endif
{

	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_int (JNIEnvPtr env);
	unsafe delegate jobject JniFunc_JniEnvironmentSafeHandle_string_IntPtr_IntPtr_int_jobject (JNIEnvPtr env, string name, IntPtr loader, IntPtr buf, int bufLen);
	unsafe delegate jobject JniFunc_JniEnvironmentSafeHandle_string_jobject (JNIEnvPtr env, string classname);
	unsafe delegate jinstanceMethodID JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID (JNIEnvPtr env, IntPtr method);
	unsafe delegate jinstanceFieldID JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID (JNIEnvPtr env, IntPtr field);
	unsafe delegate jobject JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_bool_jobject (JNIEnvPtr env, IntPtr cls, jinstanceMethodID jmethod, bool isStatic);
	unsafe delegate jobject JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject (JNIEnvPtr env, IntPtr jclass);
	unsafe delegate bool JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_bool (JNIEnvPtr env, IntPtr clazz1, IntPtr clazz2);
	unsafe delegate jobject JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_bool_jobject (JNIEnvPtr env, IntPtr cls, jinstanceFieldID jfieldID, bool isStatic);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_IntPtr_int (JNIEnvPtr env, IntPtr obj);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_IntPtr_string_int (JNIEnvPtr env, IntPtr clazz, string message);
	unsafe delegate jobject JniFunc_JniEnvironmentSafeHandle_jobject (JNIEnvPtr env);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle (JNIEnvPtr env);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_string (JNIEnvPtr env, string msg);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_int_int (JNIEnvPtr env, int capacity);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr (JNIEnvPtr env, IntPtr jobject);
	unsafe delegate jobject JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_jobject (JNIEnvPtr env, IntPtr jclass, jinstanceMethodID jmethod);
	unsafe delegate jobject JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_jobject (JNIEnvPtr env, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms);
	unsafe delegate jinstanceMethodID JniFunc_JniEnvironmentSafeHandle_IntPtr_string_string_jinstanceMethodID (JNIEnvPtr env, IntPtr kls, string name, string signature);
	unsafe delegate bool JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_bool (JNIEnvPtr env, IntPtr jobject, jinstanceMethodID jmethod);
	unsafe delegate bool JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_bool (JNIEnvPtr env, IntPtr jobject, jinstanceMethodID jmethod, JValue* parms);
	unsafe delegate sbyte JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_sbyte (JNIEnvPtr env, IntPtr jobject, jinstanceMethodID jmethod);
	unsafe delegate sbyte JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_sbyte (JNIEnvPtr env, IntPtr jobject, jinstanceMethodID jmethod, JValue* parms);
	unsafe delegate char JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_char (JNIEnvPtr env, IntPtr jobject, jinstanceMethodID jmethod);
	unsafe delegate char JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_char (JNIEnvPtr env, IntPtr jobject, jinstanceMethodID jmethod, JValue* parms);
	unsafe delegate short JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_short (JNIEnvPtr env, IntPtr jobject, jinstanceMethodID jmethod);
	unsafe delegate short JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_short (JNIEnvPtr env, IntPtr jobject, jinstanceMethodID jmethod, JValue* parms);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_int (JNIEnvPtr env, IntPtr jobject, jinstanceMethodID jmethod);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_int (JNIEnvPtr env, IntPtr jobject, jinstanceMethodID jmethod, JValue* parms);
	unsafe delegate long JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_long (JNIEnvPtr env, IntPtr jobject, jinstanceMethodID jmethod);
	unsafe delegate long JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_long (JNIEnvPtr env, IntPtr jobject, jinstanceMethodID jmethod, JValue* parms);
	unsafe delegate float JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_float (JNIEnvPtr env, IntPtr jobject, jinstanceMethodID jmethod);
	unsafe delegate float JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_float (JNIEnvPtr env, IntPtr jobject, jinstanceMethodID jmethod, JValue* parms);
	unsafe delegate double JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_double (JNIEnvPtr env, IntPtr jobject, jinstanceMethodID jmethod);
	unsafe delegate double JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_double (JNIEnvPtr env, IntPtr jobject, jinstanceMethodID jmethod, JValue* parms);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID (JNIEnvPtr env, IntPtr jobject, jinstanceMethodID jmethod);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef (JNIEnvPtr env, IntPtr jobject, jinstanceMethodID jmethod, JValue* parms);
	unsafe delegate jobject JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_jobject (JNIEnvPtr env, IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod);
	unsafe delegate jobject JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_jobject (JNIEnvPtr env, IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms);
	unsafe delegate bool JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_bool (JNIEnvPtr env, IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod);
	unsafe delegate bool JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_bool (JNIEnvPtr env, IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms);
	unsafe delegate sbyte JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_sbyte (JNIEnvPtr env, IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod);
	unsafe delegate sbyte JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_sbyte (JNIEnvPtr env, IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms);
	unsafe delegate char JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_char (JNIEnvPtr env, IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod);
	unsafe delegate char JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_char (JNIEnvPtr env, IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms);
	unsafe delegate short JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_short (JNIEnvPtr env, IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod);
	unsafe delegate short JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_short (JNIEnvPtr env, IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_int (JNIEnvPtr env, IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_int (JNIEnvPtr env, IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms);
	unsafe delegate long JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_long (JNIEnvPtr env, IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod);
	unsafe delegate long JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_long (JNIEnvPtr env, IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms);
	unsafe delegate float JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_float (JNIEnvPtr env, IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod);
	unsafe delegate float JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_float (JNIEnvPtr env, IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms);
	unsafe delegate double JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_double (JNIEnvPtr env, IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod);
	unsafe delegate double JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_double (JNIEnvPtr env, IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID (JNIEnvPtr env, IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef (JNIEnvPtr env, IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms);
	unsafe delegate jinstanceFieldID JniFunc_JniEnvironmentSafeHandle_IntPtr_string_string_jinstanceFieldID (JNIEnvPtr env, IntPtr jclass, string name, string sig);
	unsafe delegate jobject JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_jobject (JNIEnvPtr env, IntPtr jobject, jinstanceFieldID jfieldID);
	unsafe delegate bool JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_bool (JNIEnvPtr env, IntPtr jobject, jinstanceFieldID jfieldID);
	unsafe delegate sbyte JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_sbyte (JNIEnvPtr env, IntPtr jobject, jinstanceFieldID jfieldID);
	unsafe delegate char JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_char (JNIEnvPtr env, IntPtr jobject, jinstanceFieldID jfieldID);
	unsafe delegate short JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_short (JNIEnvPtr env, IntPtr jobject, jinstanceFieldID jfieldID);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_int (JNIEnvPtr env, IntPtr jobject, jinstanceFieldID jfieldID);
	unsafe delegate long JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_long (JNIEnvPtr env, IntPtr jobject, jinstanceFieldID jfieldID);
	unsafe delegate float JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_float (JNIEnvPtr env, IntPtr jobject, jinstanceFieldID jfieldID);
	unsafe delegate double JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_double (JNIEnvPtr env, IntPtr jobject, jinstanceFieldID jfieldID);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_IntPtr (JNIEnvPtr env, IntPtr jobject, jinstanceFieldID jfieldID, IntPtr val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_bool (JNIEnvPtr env, IntPtr jobject, jinstanceFieldID jfieldID, bool val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_sbyte (JNIEnvPtr env, IntPtr jobject, jinstanceFieldID jfieldID, sbyte val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_char (JNIEnvPtr env, IntPtr jobject, jinstanceFieldID jfieldID, char val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_short (JNIEnvPtr env, IntPtr jobject, jinstanceFieldID jfieldID, short val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_int (JNIEnvPtr env, IntPtr jobject, jinstanceFieldID jfieldID, int val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_long (JNIEnvPtr env, IntPtr jobject, jinstanceFieldID jfieldID, long val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_float (JNIEnvPtr env, IntPtr jobject, jinstanceFieldID jfieldID, float val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_double (JNIEnvPtr env, IntPtr jobject, jinstanceFieldID jfieldID, double val);
	unsafe delegate jstaticMethodID JniFunc_JniEnvironmentSafeHandle_IntPtr_string_string_jstaticMethodID (JNIEnvPtr env, IntPtr jclass, string name, string sig);
	unsafe delegate jobject JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_jobject (JNIEnvPtr env, IntPtr jclass, jstaticMethodID jmethod);
	unsafe delegate jobject JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_jobject (JNIEnvPtr env, IntPtr jclass, jstaticMethodID jmethod, JValue* parms);
	unsafe delegate bool JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_bool (JNIEnvPtr env, IntPtr jclass, jstaticMethodID jmethod);
	unsafe delegate bool JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_bool (JNIEnvPtr env, IntPtr jclass, jstaticMethodID jmethod, JValue* parms);
	unsafe delegate sbyte JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_sbyte (JNIEnvPtr env, IntPtr jclass, jstaticMethodID jmethod);
	unsafe delegate sbyte JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_sbyte (JNIEnvPtr env, IntPtr jclass, jstaticMethodID jmethod, JValue* parms);
	unsafe delegate char JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_char (JNIEnvPtr env, IntPtr jclass, jstaticMethodID jmethod);
	unsafe delegate char JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_char (JNIEnvPtr env, IntPtr jclass, jstaticMethodID jmethod, JValue* parms);
	unsafe delegate short JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_short (JNIEnvPtr env, IntPtr jclass, jstaticMethodID jmethod);
	unsafe delegate short JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_short (JNIEnvPtr env, IntPtr jclass, jstaticMethodID jmethod, JValue* parms);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_int (JNIEnvPtr env, IntPtr jclass, jstaticMethodID jmethod);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_int (JNIEnvPtr env, IntPtr jclass, jstaticMethodID jmethod, JValue* parms);
	unsafe delegate long JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_long (JNIEnvPtr env, IntPtr jclass, jstaticMethodID jmethod);
	unsafe delegate long JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_long (JNIEnvPtr env, IntPtr jclass, jstaticMethodID jmethod, JValue* parms);
	unsafe delegate float JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_float (JNIEnvPtr env, IntPtr jclass, jstaticMethodID jmethod);
	unsafe delegate float JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_float (JNIEnvPtr env, IntPtr jclass, jstaticMethodID jmethod, JValue* parms);
	unsafe delegate double JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_double (JNIEnvPtr env, IntPtr jclass, jstaticMethodID jmethod);
	unsafe delegate double JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_double (JNIEnvPtr env, IntPtr jclass, jstaticMethodID jmethod, JValue* parms);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID (JNIEnvPtr env, IntPtr jclass, jstaticMethodID jmethod);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef (JNIEnvPtr env, IntPtr jclass, jstaticMethodID jmethod, JValue* parms);
	unsafe delegate jstaticFieldID JniFunc_JniEnvironmentSafeHandle_IntPtr_string_string_jstaticFieldID (JNIEnvPtr env, IntPtr jclass, string name, string sig);
	unsafe delegate jobject JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_jobject (JNIEnvPtr env, IntPtr jclass, jstaticFieldID jfieldID);
	unsafe delegate bool JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_bool (JNIEnvPtr env, IntPtr jclass, jstaticFieldID jfieldID);
	unsafe delegate sbyte JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_sbyte (JNIEnvPtr env, IntPtr jclass, jstaticFieldID jfieldID);
	unsafe delegate char JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_char (JNIEnvPtr env, IntPtr jclass, jstaticFieldID jfieldID);
	unsafe delegate short JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_short (JNIEnvPtr env, IntPtr jclass, jstaticFieldID jfieldID);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_int (JNIEnvPtr env, IntPtr jclass, jstaticFieldID jfieldID);
	unsafe delegate long JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_long (JNIEnvPtr env, IntPtr jclass, jstaticFieldID jfieldID);
	unsafe delegate float JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_float (JNIEnvPtr env, IntPtr jclass, jstaticFieldID jfieldID);
	unsafe delegate double JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_double (JNIEnvPtr env, IntPtr jclass, jstaticFieldID jfieldID);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_IntPtr (JNIEnvPtr env, IntPtr jclass, jstaticFieldID jfieldID, IntPtr val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_bool (JNIEnvPtr env, IntPtr jclass, jstaticFieldID jfieldID, bool val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_sbyte (JNIEnvPtr env, IntPtr jclass, jstaticFieldID jfieldID, sbyte val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_char (JNIEnvPtr env, IntPtr jclass, jstaticFieldID jfieldID, char val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_short (JNIEnvPtr env, IntPtr jclass, jstaticFieldID jfieldID, short val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_int (JNIEnvPtr env, IntPtr jclass, jstaticFieldID jfieldID, int val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_long (JNIEnvPtr env, IntPtr jclass, jstaticFieldID jfieldID, long val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_float (JNIEnvPtr env, IntPtr jclass, jstaticFieldID jfieldID, float val);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_double (JNIEnvPtr env, IntPtr jclass, jstaticFieldID jfieldID, double val);
	[UnmanagedFunctionPointerAttribute (CallingConvention.Cdecl, CharSet=CharSet.Unicode)]
	unsafe delegate jobject JniFunc_JniEnvironmentSafeHandle_IntPtr_int_jobject (JNIEnvPtr env, IntPtr unicodeChars, int len);
	unsafe delegate IntPtr JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr (JNIEnvPtr env, IntPtr @string, IntPtr isCopy);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr (JNIEnvPtr env, IntPtr @string, IntPtr chars);
	unsafe delegate string JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_string (JNIEnvPtr env, IntPtr @string, IntPtr isCopy);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_string (JNIEnvPtr env, IntPtr @string, string utf);
	unsafe delegate jobject JniFunc_JniEnvironmentSafeHandle_int_IntPtr_IntPtr_jobject (JNIEnvPtr env, int length, IntPtr elementClass, IntPtr initialElement);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_int_IntPtr (JNIEnvPtr env, IntPtr array, int index, IntPtr value);
	unsafe delegate jobject JniFunc_JniEnvironmentSafeHandle_int_jobject (JNIEnvPtr env, int length);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int (JNIEnvPtr env, IntPtr array, IntPtr elems, int mode);
	unsafe delegate void JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr (JNIEnvPtr env, IntPtr array, int start, int len, IntPtr buf);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_IntPtr_JniNativeMethodRegistrationArray_int_int (JNIEnvPtr env, IntPtr jclass, JniNativeMethodRegistration [] methods, int nMethods);
	unsafe delegate int JniFunc_JniEnvironmentSafeHandle_outIntPtr_int (JNIEnvPtr env, out IntPtr vm);
	unsafe delegate bool JniFunc_JniEnvironmentSafeHandle_bool (JNIEnvPtr env);
	unsafe delegate jobject JniFunc_JniEnvironmentSafeHandle_IntPtr_long_jobject (JNIEnvPtr env, IntPtr address, long capacity);
	unsafe delegate IntPtr JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr (JNIEnvPtr env, IntPtr buf);
	unsafe delegate long JniFunc_JniEnvironmentSafeHandle_IntPtr_long (JNIEnvPtr env, IntPtr buf);
	unsafe delegate JniObjectReferenceType JniFunc_JniEnvironmentSafeHandle_IntPtr_JniObjectReferenceType (JNIEnvPtr env, IntPtr jobject);

	partial class JniEnvironment {

	internal static partial class Activator {

		public static unsafe IntPtr AllocObject (IntPtr jclass)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");

			var tmp = JniEnvironment.Current.Invoker.AllocObject (JniEnvironment.Current.SafeHandle, jclass);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe IntPtr NewObject (IntPtr jclass, jinstanceMethodID jmethod)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.NewObject (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe IntPtr NewObject (IntPtr jclass, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.NewObjectA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}
	}

	public static partial class Arrays {

		public static unsafe int GetArrayLength (IntPtr array_ptr)
		{
			if (array_ptr == IntPtr.Zero)
				throw new ArgumentException ("`array_ptr` must not be IntPtr.Zero.", "array_ptr");

			var tmp = JniEnvironment.Current.Invoker.GetArrayLength (JniEnvironment.Current.SafeHandle, array_ptr);
			return tmp;
		}

		public static unsafe IntPtr NewObjectArray (int length, IntPtr elementClass, IntPtr initialElement)
		{
			if (elementClass == IntPtr.Zero)
				throw new ArgumentException ("`elementClass` must not be IntPtr.Zero.", "elementClass");

			var tmp = JniEnvironment.Current.Invoker.NewObjectArray (JniEnvironment.Current.SafeHandle, length, elementClass, initialElement);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe IntPtr GetObjectArrayElement (IntPtr array, int index)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");

			var tmp = JniEnvironment.Current.Invoker.GetObjectArrayElement (JniEnvironment.Current.SafeHandle, array, index);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe void SetObjectArrayElement (IntPtr array, int index, IntPtr value)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");

			JniEnvironment.Current.Invoker.SetObjectArrayElement (JniEnvironment.Current.SafeHandle, array, index, value);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe IntPtr NewBooleanArray (int length)
		{
			var tmp = JniEnvironment.Current.Invoker.NewBooleanArray (JniEnvironment.Current.SafeHandle, length);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe IntPtr NewByteArray (int length)
		{
			var tmp = JniEnvironment.Current.Invoker.NewByteArray (JniEnvironment.Current.SafeHandle, length);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe IntPtr NewCharArray (int length)
		{
			var tmp = JniEnvironment.Current.Invoker.NewCharArray (JniEnvironment.Current.SafeHandle, length);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe IntPtr NewShortArray (int length)
		{
			var tmp = JniEnvironment.Current.Invoker.NewShortArray (JniEnvironment.Current.SafeHandle, length);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe IntPtr NewIntArray (int length)
		{
			var tmp = JniEnvironment.Current.Invoker.NewIntArray (JniEnvironment.Current.SafeHandle, length);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe IntPtr NewLongArray (int length)
		{
			var tmp = JniEnvironment.Current.Invoker.NewLongArray (JniEnvironment.Current.SafeHandle, length);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe IntPtr NewFloatArray (int length)
		{
			var tmp = JniEnvironment.Current.Invoker.NewFloatArray (JniEnvironment.Current.SafeHandle, length);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe IntPtr NewDoubleArray (int length)
		{
			var tmp = JniEnvironment.Current.Invoker.NewDoubleArray (JniEnvironment.Current.SafeHandle, length);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe IntPtr GetBooleanArrayElements (IntPtr array, IntPtr isCopy)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");

			var tmp = JniEnvironment.Current.Invoker.GetBooleanArrayElements (JniEnvironment.Current.SafeHandle, array, isCopy);
			return tmp;
		}

		public static unsafe IntPtr GetByteArrayElements (IntPtr array, IntPtr isCopy)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");

			var tmp = JniEnvironment.Current.Invoker.GetByteArrayElements (JniEnvironment.Current.SafeHandle, array, isCopy);
			return tmp;
		}

		public static unsafe IntPtr GetCharArrayElements (IntPtr array, IntPtr isCopy)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");

			var tmp = JniEnvironment.Current.Invoker.GetCharArrayElements (JniEnvironment.Current.SafeHandle, array, isCopy);
			return tmp;
		}

		public static unsafe IntPtr GetShortArrayElements (IntPtr array, IntPtr isCopy)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");

			var tmp = JniEnvironment.Current.Invoker.GetShortArrayElements (JniEnvironment.Current.SafeHandle, array, isCopy);
			return tmp;
		}

		public static unsafe IntPtr GetIntArrayElements (IntPtr array, IntPtr isCopy)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");

			var tmp = JniEnvironment.Current.Invoker.GetIntArrayElements (JniEnvironment.Current.SafeHandle, array, isCopy);
			return tmp;
		}

		public static unsafe IntPtr GetLongArrayElements (IntPtr array, IntPtr isCopy)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");

			var tmp = JniEnvironment.Current.Invoker.GetLongArrayElements (JniEnvironment.Current.SafeHandle, array, isCopy);
			return tmp;
		}

		public static unsafe IntPtr GetFloatArrayElements (IntPtr array, IntPtr isCopy)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");

			var tmp = JniEnvironment.Current.Invoker.GetFloatArrayElements (JniEnvironment.Current.SafeHandle, array, isCopy);
			return tmp;
		}

		public static unsafe IntPtr GetDoubleArrayElements (IntPtr array, IntPtr isCopy)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");

			var tmp = JniEnvironment.Current.Invoker.GetDoubleArrayElements (JniEnvironment.Current.SafeHandle, array, isCopy);
			return tmp;
		}

		public static unsafe void ReleaseBooleanArrayElements (IntPtr array, IntPtr elems, int mode)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (elems == IntPtr.Zero)
				throw new ArgumentException ("'elems' must not be IntPtr.Zero.", "elems");

			JniEnvironment.Current.Invoker.ReleaseBooleanArrayElements (JniEnvironment.Current.SafeHandle, array, elems, mode);
		}

		public static unsafe void ReleaseByteArrayElements (IntPtr array, IntPtr elems, int mode)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (elems == IntPtr.Zero)
				throw new ArgumentException ("'elems' must not be IntPtr.Zero.", "elems");

			JniEnvironment.Current.Invoker.ReleaseByteArrayElements (JniEnvironment.Current.SafeHandle, array, elems, mode);
		}

		public static unsafe void ReleaseCharArrayElements (IntPtr array, IntPtr elems, int mode)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (elems == IntPtr.Zero)
				throw new ArgumentException ("'elems' must not be IntPtr.Zero.", "elems");

			JniEnvironment.Current.Invoker.ReleaseCharArrayElements (JniEnvironment.Current.SafeHandle, array, elems, mode);
		}

		public static unsafe void ReleaseShortArrayElements (IntPtr array, IntPtr elems, int mode)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (elems == IntPtr.Zero)
				throw new ArgumentException ("'elems' must not be IntPtr.Zero.", "elems");

			JniEnvironment.Current.Invoker.ReleaseShortArrayElements (JniEnvironment.Current.SafeHandle, array, elems, mode);
		}

		public static unsafe void ReleaseIntArrayElements (IntPtr array, IntPtr elems, int mode)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (elems == IntPtr.Zero)
				throw new ArgumentException ("'elems' must not be IntPtr.Zero.", "elems");

			JniEnvironment.Current.Invoker.ReleaseIntArrayElements (JniEnvironment.Current.SafeHandle, array, elems, mode);
		}

		public static unsafe void ReleaseLongArrayElements (IntPtr array, IntPtr elems, int mode)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (elems == IntPtr.Zero)
				throw new ArgumentException ("'elems' must not be IntPtr.Zero.", "elems");

			JniEnvironment.Current.Invoker.ReleaseLongArrayElements (JniEnvironment.Current.SafeHandle, array, elems, mode);
		}

		public static unsafe void ReleaseFloatArrayElements (IntPtr array, IntPtr elems, int mode)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (elems == IntPtr.Zero)
				throw new ArgumentException ("'elems' must not be IntPtr.Zero.", "elems");

			JniEnvironment.Current.Invoker.ReleaseFloatArrayElements (JniEnvironment.Current.SafeHandle, array, elems, mode);
		}

		public static unsafe void ReleaseDoubleArrayElements (IntPtr array, IntPtr elems, int mode)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (elems == IntPtr.Zero)
				throw new ArgumentException ("'elems' must not be IntPtr.Zero.", "elems");

			JniEnvironment.Current.Invoker.ReleaseDoubleArrayElements (JniEnvironment.Current.SafeHandle, array, elems, mode);
		}

		internal static unsafe void GetBooleanArrayRegion (IntPtr array, int start, int len, IntPtr buf)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.GetBooleanArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void GetByteArrayRegion (IntPtr array, int start, int len, IntPtr buf)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.GetByteArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void GetCharArrayRegion (IntPtr array, int start, int len, IntPtr buf)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.GetCharArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void GetShortArrayRegion (IntPtr array, int start, int len, IntPtr buf)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.GetShortArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void GetIntArrayRegion (IntPtr array, int start, int len, IntPtr buf)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.GetIntArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void GetLongArrayRegion (IntPtr array, int start, int len, IntPtr buf)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.GetLongArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void GetFloatArrayRegion (IntPtr array, int start, int len, IntPtr buf)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.GetFloatArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void GetDoubleArrayRegion (IntPtr array, int start, int len, IntPtr buf)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.GetDoubleArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		internal static unsafe void SetBooleanArrayRegion (IntPtr array, int start, int len, IntPtr buf)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.SetBooleanArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void SetByteArrayRegion (IntPtr array, int start, int len, IntPtr buf)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.SetByteArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void SetCharArrayRegion (IntPtr array, int start, int len, IntPtr buf)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.SetCharArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void SetShortArrayRegion (IntPtr array, int start, int len, IntPtr buf)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.SetShortArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void SetIntArrayRegion (IntPtr array, int start, int len, IntPtr buf)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.SetIntArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void SetLongArrayRegion (IntPtr array, int start, int len, IntPtr buf)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.SetLongArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void SetFloatArrayRegion (IntPtr array, int start, int len, IntPtr buf)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.SetFloatArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe void SetDoubleArrayRegion (IntPtr array, int start, int len, IntPtr buf)
		{
			if (array == IntPtr.Zero)
				throw new ArgumentException ("`array` must not be IntPtr.Zero.", "array");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			JniEnvironment.Current.Invoker.SetDoubleArrayRegion (JniEnvironment.Current.SafeHandle, array, start, len, buf);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}
	}

	public static partial class Errors {

		public static unsafe int Throw (IntPtr obj)
		{
			if (obj == IntPtr.Zero)
				throw new ArgumentException ("`obj` must not be IntPtr.Zero.", "obj");

			var tmp = JniEnvironment.Current.Invoker.Throw (JniEnvironment.Current.SafeHandle, obj);
			return tmp;
		}

		public static unsafe int ThrowNew (IntPtr clazz, string message)
		{
			if (clazz == IntPtr.Zero)
				throw new ArgumentException ("`clazz` must not be IntPtr.Zero.", "clazz");
			if (message == null)
				throw new ArgumentNullException ("message");

			var tmp = JniEnvironment.Current.Invoker.ThrowNew (JniEnvironment.Current.SafeHandle, clazz, message);
			return tmp;
		}

		internal static unsafe IntPtr ExceptionOccurred ()
		{
			var tmp = JniEnvironment.Current.Invoker.ExceptionOccurred (JniEnvironment.Current.SafeHandle);
			return tmp;
		}

		internal static unsafe void ExceptionDescribe ()
		{
			JniEnvironment.Current.Invoker.ExceptionDescribe (JniEnvironment.Current.SafeHandle);
		}

		internal static unsafe void ExceptionClear ()
		{
			JniEnvironment.Current.Invoker.ExceptionClear (JniEnvironment.Current.SafeHandle);
		}

		public static unsafe void FatalError (string msg)
		{
			if (msg == null)
				throw new ArgumentNullException ("msg");

			JniEnvironment.Current.Invoker.FatalError (JniEnvironment.Current.SafeHandle, msg);
		}

		internal static unsafe bool ExceptionCheck ()
		{
			var tmp = JniEnvironment.Current.Invoker.ExceptionCheck (JniEnvironment.Current.SafeHandle);
			return tmp;
		}
	}

	public static partial class Handles {

		public static unsafe int PushLocalFrame (int capacity)
		{
			var tmp = JniEnvironment.Current.Invoker.PushLocalFrame (JniEnvironment.Current.SafeHandle, capacity);
			return tmp;
		}

		public static unsafe IntPtr PopLocalFrame (IntPtr result)
		{
			var tmp = JniEnvironment.Current.Invoker.PopLocalFrame (JniEnvironment.Current.SafeHandle, result);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe IntPtr NewGlobalRef (IntPtr jobject)
		{
			var tmp = JniEnvironment.Current.Invoker.NewGlobalRef (JniEnvironment.Current.SafeHandle, jobject);
			return tmp;
		}

		internal static unsafe void DeleteGlobalRef (IntPtr jobject)
		{
			JniEnvironment.Current.Invoker.DeleteGlobalRef (JniEnvironment.Current.SafeHandle, jobject);
		}

		internal static unsafe void DeleteLocalRef (IntPtr jobject)
		{
			JniEnvironment.Current.Invoker.DeleteLocalRef (JniEnvironment.Current.SafeHandle, jobject);
		}

		internal static unsafe IntPtr NewLocalRef (IntPtr jobject)
		{
			var tmp = JniEnvironment.Current.Invoker.NewLocalRef (JniEnvironment.Current.SafeHandle, jobject);
			return tmp;
		}

		public static unsafe int EnsureLocalCapacity (int capacity)
		{
			var tmp = JniEnvironment.Current.Invoker.EnsureLocalCapacity (JniEnvironment.Current.SafeHandle, capacity);
			return tmp;
		}

		public static unsafe int GetJavaVM (out IntPtr vm)
		{
			var tmp = JniEnvironment.Current.Invoker.GetJavaVM (JniEnvironment.Current.SafeHandle, out vm);
			return tmp;
		}

		internal static unsafe IntPtr NewWeakGlobalRef (IntPtr jobject)
		{
			var tmp = JniEnvironment.Current.Invoker.NewWeakGlobalRef (JniEnvironment.Current.SafeHandle, jobject);
			return tmp;
		}

		internal static unsafe void DeleteWeakGlobalRef (IntPtr jobject)
		{
			JniEnvironment.Current.Invoker.DeleteWeakGlobalRef (JniEnvironment.Current.SafeHandle, jobject);
		}

		internal static unsafe JniObjectReferenceType GetObjectRefType (IntPtr jobject)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");

			var tmp = JniEnvironment.Current.Invoker.GetObjectRefType (JniEnvironment.Current.SafeHandle, jobject);
			return tmp;
		}
	}

	public static partial class IO {

		public static unsafe IntPtr NewDirectByteBuffer (IntPtr address, long capacity)
		{
			if (address == IntPtr.Zero)
				throw new ArgumentException ("'address' must not be IntPtr.Zero.", "address");

			var tmp = JniEnvironment.Current.Invoker.NewDirectByteBuffer (JniEnvironment.Current.SafeHandle, address, capacity);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe IntPtr GetDirectBufferAddress (IntPtr buf)
		{
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("`buf` must not be IntPtr.Zero.", "buf");

			var tmp = JniEnvironment.Current.Invoker.GetDirectBufferAddress (JniEnvironment.Current.SafeHandle, buf);
			return tmp;
		}

		public static unsafe long GetDirectBufferCapacity (IntPtr buf)
		{
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("`buf` must not be IntPtr.Zero.", "buf");

			var tmp = JniEnvironment.Current.Invoker.GetDirectBufferCapacity (JniEnvironment.Current.SafeHandle, buf);
			return tmp;
		}
	}

	internal static partial class Members {

		internal static unsafe IntPtr ToReflectedMethod (IntPtr cls, jinstanceMethodID jmethod, bool isStatic)
		{
			if (cls == IntPtr.Zero)
				throw new ArgumentException ("`cls` must not be IntPtr.Zero.", "cls");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.ToReflectedMethod (JniEnvironment.Current.SafeHandle, cls, jmethod, isStatic);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe IntPtr ToReflectedField (IntPtr cls, jinstanceFieldID jfieldID, bool isStatic)
		{
			if (cls == IntPtr.Zero)
				throw new ArgumentException ("`cls` must not be IntPtr.Zero.", "cls");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.ToReflectedField (JniEnvironment.Current.SafeHandle, cls, jfieldID, isStatic);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe jinstanceMethodID GetMethodID (IntPtr kls, string name, string signature)
		{
			if (kls == IntPtr.Zero)
				throw new ArgumentException ("`kls` must not be IntPtr.Zero.", "kls");
			if (name == null)
				throw new ArgumentNullException ("name");
			if (signature == null)
				throw new ArgumentNullException ("signature");

			var tmp = JniEnvironment.Current.Invoker.GetMethodID (JniEnvironment.Current.SafeHandle, kls, name, signature);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe IntPtr CallObjectMethod (IntPtr jobject, jinstanceMethodID jmethod)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallObjectMethod (JniEnvironment.Current.SafeHandle, jobject, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe IntPtr CallObjectMethod (IntPtr jobject, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallObjectMethodA (JniEnvironment.Current.SafeHandle, jobject, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe bool CallBooleanMethod (IntPtr jobject, jinstanceMethodID jmethod)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallBooleanMethod (JniEnvironment.Current.SafeHandle, jobject, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe bool CallBooleanMethod (IntPtr jobject, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallBooleanMethodA (JniEnvironment.Current.SafeHandle, jobject, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe sbyte CallSByteMethod (IntPtr jobject, jinstanceMethodID jmethod)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallSByteMethod (JniEnvironment.Current.SafeHandle, jobject, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe sbyte CallSByteMethod (IntPtr jobject, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallSByteMethodA (JniEnvironment.Current.SafeHandle, jobject, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe char CallCharMethod (IntPtr jobject, jinstanceMethodID jmethod)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallCharMethod (JniEnvironment.Current.SafeHandle, jobject, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe char CallCharMethod (IntPtr jobject, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallCharMethodA (JniEnvironment.Current.SafeHandle, jobject, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe short CallShortMethod (IntPtr jobject, jinstanceMethodID jmethod)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallShortMethod (JniEnvironment.Current.SafeHandle, jobject, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe short CallShortMethod (IntPtr jobject, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallShortMethodA (JniEnvironment.Current.SafeHandle, jobject, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe int CallIntMethod (IntPtr jobject, jinstanceMethodID jmethod)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallIntMethod (JniEnvironment.Current.SafeHandle, jobject, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe int CallIntMethod (IntPtr jobject, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallIntMethodA (JniEnvironment.Current.SafeHandle, jobject, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe long CallLongMethod (IntPtr jobject, jinstanceMethodID jmethod)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallLongMethod (JniEnvironment.Current.SafeHandle, jobject, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe long CallLongMethod (IntPtr jobject, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallLongMethodA (JniEnvironment.Current.SafeHandle, jobject, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe float CallFloatMethod (IntPtr jobject, jinstanceMethodID jmethod)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallFloatMethod (JniEnvironment.Current.SafeHandle, jobject, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe float CallFloatMethod (IntPtr jobject, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallFloatMethodA (JniEnvironment.Current.SafeHandle, jobject, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe double CallDoubleMethod (IntPtr jobject, jinstanceMethodID jmethod)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallDoubleMethod (JniEnvironment.Current.SafeHandle, jobject, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe double CallDoubleMethod (IntPtr jobject, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallDoubleMethodA (JniEnvironment.Current.SafeHandle, jobject, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe void CallVoidMethod (IntPtr jobject, jinstanceMethodID jmethod)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			JniEnvironment.Current.Invoker.CallVoidMethod (JniEnvironment.Current.SafeHandle, jobject, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		internal static unsafe void CallVoidMethod (IntPtr jobject, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			JniEnvironment.Current.Invoker.CallVoidMethodA (JniEnvironment.Current.SafeHandle, jobject, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		internal static unsafe IntPtr CallNonvirtualObjectMethod (IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualObjectMethod (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe IntPtr CallNonvirtualObjectMethod (IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualObjectMethodA (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe bool CallNonvirtualBooleanMethod (IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualBooleanMethod (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe bool CallNonvirtualBooleanMethod (IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualBooleanMethodA (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe sbyte CallNonvirtualSByteMethod (IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualSByteMethod (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe sbyte CallNonvirtualSByteMethod (IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualSByteMethodA (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe char CallNonvirtualCharMethod (IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualCharMethod (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe char CallNonvirtualCharMethod (IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualCharMethodA (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe short CallNonvirtualShortMethod (IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualShortMethod (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe short CallNonvirtualShortMethod (IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualShortMethodA (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe int CallNonvirtualIntMethod (IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualIntMethod (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe int CallNonvirtualIntMethod (IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualIntMethodA (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe long CallNonvirtualLongMethod (IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualLongMethod (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe long CallNonvirtualLongMethod (IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualLongMethodA (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe float CallNonvirtualFloatMethod (IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualFloatMethod (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe float CallNonvirtualFloatMethod (IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualFloatMethodA (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe double CallNonvirtualDoubleMethod (IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualDoubleMethod (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe double CallNonvirtualDoubleMethod (IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallNonvirtualDoubleMethodA (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe void CallNonvirtualVoidMethod (IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			JniEnvironment.Current.Invoker.CallNonvirtualVoidMethod (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		internal static unsafe void CallNonvirtualVoidMethod (IntPtr jobject, IntPtr jclass, jinstanceMethodID jmethod, JValue* parms)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			JniEnvironment.Current.Invoker.CallNonvirtualVoidMethodA (JniEnvironment.Current.SafeHandle, jobject, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe jinstanceFieldID GetFieldID (IntPtr jclass, string name, string sig)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (name == null)
				throw new ArgumentNullException ("name");
			if (sig == null)
				throw new ArgumentNullException ("sig");

			var tmp = JniEnvironment.Current.Invoker.GetFieldID (JniEnvironment.Current.SafeHandle, jclass, name, sig);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe IntPtr GetObjectField (IntPtr jobject, jinstanceFieldID jfieldID)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetObjectField (JniEnvironment.Current.SafeHandle, jobject, jfieldID);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe bool GetBooleanField (IntPtr jobject, jinstanceFieldID jfieldID)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetBooleanField (JniEnvironment.Current.SafeHandle, jobject, jfieldID);
			return tmp;
		}

		internal static unsafe sbyte GetByteField (IntPtr jobject, jinstanceFieldID jfieldID)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetByteField (JniEnvironment.Current.SafeHandle, jobject, jfieldID);
			return tmp;
		}

		internal static unsafe char GetCharField (IntPtr jobject, jinstanceFieldID jfieldID)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetCharField (JniEnvironment.Current.SafeHandle, jobject, jfieldID);
			return tmp;
		}

		internal static unsafe short GetShortField (IntPtr jobject, jinstanceFieldID jfieldID)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetShortField (JniEnvironment.Current.SafeHandle, jobject, jfieldID);
			return tmp;
		}

		internal static unsafe int GetIntField (IntPtr jobject, jinstanceFieldID jfieldID)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetIntField (JniEnvironment.Current.SafeHandle, jobject, jfieldID);
			return tmp;
		}

		internal static unsafe long GetLongField (IntPtr jobject, jinstanceFieldID jfieldID)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetLongField (JniEnvironment.Current.SafeHandle, jobject, jfieldID);
			return tmp;
		}

		internal static unsafe float GetFloatField (IntPtr jobject, jinstanceFieldID jfieldID)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetFloatField (JniEnvironment.Current.SafeHandle, jobject, jfieldID);
			return tmp;
		}

		internal static unsafe double GetDoubleField (IntPtr jobject, jinstanceFieldID jfieldID)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetDoubleField (JniEnvironment.Current.SafeHandle, jobject, jfieldID);
			return tmp;
		}

		internal static unsafe void SetField (IntPtr jobject, jinstanceFieldID jfieldID, IntPtr val)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			JniEnvironment.Current.Invoker.SetObjectField (JniEnvironment.Current.SafeHandle, jobject, jfieldID, val);
		}

		internal static unsafe void SetField (IntPtr jobject, jinstanceFieldID jfieldID, bool val)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			JniEnvironment.Current.Invoker.SetBooleanField (JniEnvironment.Current.SafeHandle, jobject, jfieldID, val);
		}

		internal static unsafe void SetField (IntPtr jobject, jinstanceFieldID jfieldID, sbyte val)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			JniEnvironment.Current.Invoker.SetByteField (JniEnvironment.Current.SafeHandle, jobject, jfieldID, val);
		}

		internal static unsafe void SetField (IntPtr jobject, jinstanceFieldID jfieldID, char val)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			JniEnvironment.Current.Invoker.SetCharField (JniEnvironment.Current.SafeHandle, jobject, jfieldID, val);
		}

		internal static unsafe void SetField (IntPtr jobject, jinstanceFieldID jfieldID, short val)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			JniEnvironment.Current.Invoker.SetShortField (JniEnvironment.Current.SafeHandle, jobject, jfieldID, val);
		}

		internal static unsafe void SetField (IntPtr jobject, jinstanceFieldID jfieldID, int val)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			JniEnvironment.Current.Invoker.SetIntField (JniEnvironment.Current.SafeHandle, jobject, jfieldID, val);
		}

		internal static unsafe void SetField (IntPtr jobject, jinstanceFieldID jfieldID, long val)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			JniEnvironment.Current.Invoker.SetLongField (JniEnvironment.Current.SafeHandle, jobject, jfieldID, val);
		}

		internal static unsafe void SetField (IntPtr jobject, jinstanceFieldID jfieldID, float val)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			JniEnvironment.Current.Invoker.SetFloatField (JniEnvironment.Current.SafeHandle, jobject, jfieldID, val);
		}

		internal static unsafe void SetField (IntPtr jobject, jinstanceFieldID jfieldID, double val)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			JniEnvironment.Current.Invoker.SetDoubleField (JniEnvironment.Current.SafeHandle, jobject, jfieldID, val);
		}

		public static unsafe jstaticMethodID GetStaticMethodID (IntPtr jclass, string name, string sig)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (name == null)
				throw new ArgumentNullException ("name");
			if (sig == null)
				throw new ArgumentNullException ("sig");

			var tmp = JniEnvironment.Current.Invoker.GetStaticMethodID (JniEnvironment.Current.SafeHandle, jclass, name, sig);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe IntPtr CallStaticObjectMethod (IntPtr jclass, jstaticMethodID jmethod)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticObjectMethod (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe IntPtr CallStaticObjectMethod (IntPtr jclass, jstaticMethodID jmethod, JValue* parms)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticObjectMethodA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe bool CallStaticBooleanMethod (IntPtr jclass, jstaticMethodID jmethod)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticBooleanMethod (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe bool CallStaticBooleanMethod (IntPtr jclass, jstaticMethodID jmethod, JValue* parms)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticBooleanMethodA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe sbyte CallStaticSByteMethod (IntPtr jclass, jstaticMethodID jmethod)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticSByteMethod (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe sbyte CallStaticSByteMethod (IntPtr jclass, jstaticMethodID jmethod, JValue* parms)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticSByteMethodA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe char CallStaticCharMethod (IntPtr jclass, jstaticMethodID jmethod)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticCharMethod (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe char CallStaticCharMethod (IntPtr jclass, jstaticMethodID jmethod, JValue* parms)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticCharMethodA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe short CallStaticShortMethod (IntPtr jclass, jstaticMethodID jmethod)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticShortMethod (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe short CallStaticShortMethod (IntPtr jclass, jstaticMethodID jmethod, JValue* parms)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticShortMethodA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe int CallStaticIntMethod (IntPtr jclass, jstaticMethodID jmethod)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticIntMethod (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe int CallStaticIntMethod (IntPtr jclass, jstaticMethodID jmethod, JValue* parms)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticIntMethodA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe long CallStaticLongMethod (IntPtr jclass, jstaticMethodID jmethod)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticLongMethod (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe long CallStaticLongMethod (IntPtr jclass, jstaticMethodID jmethod, JValue* parms)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticLongMethodA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe float CallStaticFloatMethod (IntPtr jclass, jstaticMethodID jmethod)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticFloatMethod (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe float CallStaticFloatMethod (IntPtr jclass, jstaticMethodID jmethod, JValue* parms)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticFloatMethodA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe double CallStaticDoubleMethod (IntPtr jclass, jstaticMethodID jmethod)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticDoubleMethod (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe double CallStaticDoubleMethod (IntPtr jclass, jstaticMethodID jmethod, JValue* parms)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			var tmp = JniEnvironment.Current.Invoker.CallStaticDoubleMethodA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe void CallStaticVoidMethod (IntPtr jclass, jstaticMethodID jmethod)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			JniEnvironment.Current.Invoker.CallStaticVoidMethod (JniEnvironment.Current.SafeHandle, jclass, jmethod);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		internal static unsafe void CallStaticVoidMethod (IntPtr jclass, jstaticMethodID jmethod, JValue* parms)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jmethod == IntPtr.Zero)
				throw new ArgumentNullException ("jmethod");

			JniEnvironment.Current.Invoker.CallStaticVoidMethodA (JniEnvironment.Current.SafeHandle, jclass, jmethod, parms);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

		}

		public static unsafe jstaticFieldID GetStaticFieldID (IntPtr jclass, string name, string sig)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (name == null)
				throw new ArgumentNullException ("name");
			if (sig == null)
				throw new ArgumentNullException ("sig");

			var tmp = JniEnvironment.Current.Invoker.GetStaticFieldID (JniEnvironment.Current.SafeHandle, jclass, name, sig);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe IntPtr GetStaticObjectField (IntPtr jclass, jstaticFieldID jfieldID)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetStaticObjectField (JniEnvironment.Current.SafeHandle, jclass, jfieldID);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe bool GetStaticBooleanField (IntPtr jclass, jstaticFieldID jfieldID)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetStaticBooleanField (JniEnvironment.Current.SafeHandle, jclass, jfieldID);
			return tmp;
		}

		internal static unsafe sbyte GetStaticByteField (IntPtr jclass, jstaticFieldID jfieldID)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetStaticByteField (JniEnvironment.Current.SafeHandle, jclass, jfieldID);
			return tmp;
		}

		internal static unsafe char GetStaticCharField (IntPtr jclass, jstaticFieldID jfieldID)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetStaticCharField (JniEnvironment.Current.SafeHandle, jclass, jfieldID);
			return tmp;
		}

		internal static unsafe short GetStaticShortField (IntPtr jclass, jstaticFieldID jfieldID)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetStaticShortField (JniEnvironment.Current.SafeHandle, jclass, jfieldID);
			return tmp;
		}

		internal static unsafe int GetStaticIntField (IntPtr jclass, jstaticFieldID jfieldID)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetStaticIntField (JniEnvironment.Current.SafeHandle, jclass, jfieldID);
			return tmp;
		}

		internal static unsafe long GetStaticLongField (IntPtr jclass, jstaticFieldID jfieldID)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetStaticLongField (JniEnvironment.Current.SafeHandle, jclass, jfieldID);
			return tmp;
		}

		internal static unsafe float GetStaticFloatField (IntPtr jclass, jstaticFieldID jfieldID)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetStaticFloatField (JniEnvironment.Current.SafeHandle, jclass, jfieldID);
			return tmp;
		}

		internal static unsafe double GetStaticDoubleField (IntPtr jclass, jstaticFieldID jfieldID)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			var tmp = JniEnvironment.Current.Invoker.GetStaticDoubleField (JniEnvironment.Current.SafeHandle, jclass, jfieldID);
			return tmp;
		}

		internal static unsafe void SetStaticField (IntPtr jclass, jstaticFieldID jfieldID, IntPtr val)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			JniEnvironment.Current.Invoker.SetStaticObjectField (JniEnvironment.Current.SafeHandle, jclass, jfieldID, val);
		}

		internal static unsafe void SetStaticField (IntPtr jclass, jstaticFieldID jfieldID, bool val)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			JniEnvironment.Current.Invoker.SetStaticBooleanField (JniEnvironment.Current.SafeHandle, jclass, jfieldID, val);
		}

		internal static unsafe void SetStaticField (IntPtr jclass, jstaticFieldID jfieldID, sbyte val)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			JniEnvironment.Current.Invoker.SetStaticByteField (JniEnvironment.Current.SafeHandle, jclass, jfieldID, val);
		}

		internal static unsafe void SetStaticField (IntPtr jclass, jstaticFieldID jfieldID, char val)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			JniEnvironment.Current.Invoker.SetStaticCharField (JniEnvironment.Current.SafeHandle, jclass, jfieldID, val);
		}

		internal static unsafe void SetStaticField (IntPtr jclass, jstaticFieldID jfieldID, short val)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			JniEnvironment.Current.Invoker.SetStaticShortField (JniEnvironment.Current.SafeHandle, jclass, jfieldID, val);
		}

		internal static unsafe void SetStaticField (IntPtr jclass, jstaticFieldID jfieldID, int val)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			JniEnvironment.Current.Invoker.SetStaticIntField (JniEnvironment.Current.SafeHandle, jclass, jfieldID, val);
		}

		internal static unsafe void SetStaticField (IntPtr jclass, jstaticFieldID jfieldID, long val)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			JniEnvironment.Current.Invoker.SetStaticLongField (JniEnvironment.Current.SafeHandle, jclass, jfieldID, val);
		}

		internal static unsafe void SetStaticField (IntPtr jclass, jstaticFieldID jfieldID, float val)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			JniEnvironment.Current.Invoker.SetStaticFloatField (JniEnvironment.Current.SafeHandle, jclass, jfieldID, val);
		}

		internal static unsafe void SetStaticField (IntPtr jclass, jstaticFieldID jfieldID, double val)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");
			if (jfieldID == IntPtr.Zero)
				throw new ArgumentNullException ("jfieldID");

			JniEnvironment.Current.Invoker.SetStaticDoubleField (JniEnvironment.Current.SafeHandle, jclass, jfieldID, val);
		}
	}

	internal static partial class Monitors {

		public static unsafe int MonitorEnter (IntPtr obj)
		{
			if (obj == IntPtr.Zero)
				throw new ArgumentException ("`obj` must not be IntPtr.Zero.", "obj");

			var tmp = JniEnvironment.Current.Invoker.MonitorEnter (JniEnvironment.Current.SafeHandle, obj);
			return tmp;
		}

		public static unsafe int MonitorExit (IntPtr obj)
		{
			if (obj == IntPtr.Zero)
				throw new ArgumentException ("`obj` must not be IntPtr.Zero.", "obj");

			var tmp = JniEnvironment.Current.Invoker.MonitorExit (JniEnvironment.Current.SafeHandle, obj);
			return tmp;
		}
	}

	public static partial class Strings {

		internal static unsafe IntPtr NewString (IntPtr unicodeChars, int len)
		{
			if (unicodeChars == IntPtr.Zero)
				throw new ArgumentException ("'unicodeChars' must not be IntPtr.Zero.", "unicodeChars");

			var tmp = JniEnvironment.Current.Invoker.NewString (JniEnvironment.Current.SafeHandle, unicodeChars, len);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		internal static unsafe int GetStringLength (IntPtr @string)
		{
			if (@string == IntPtr.Zero)
				throw new ArgumentException ("`@string` must not be IntPtr.Zero.", "@string");

			var tmp = JniEnvironment.Current.Invoker.GetStringLength (JniEnvironment.Current.SafeHandle, @string);
			return tmp;
		}

		internal static unsafe IntPtr GetStringChars (IntPtr @string, IntPtr isCopy)
		{
			if (@string == IntPtr.Zero)
				throw new ArgumentException ("`@string` must not be IntPtr.Zero.", "@string");

			var tmp = JniEnvironment.Current.Invoker.GetStringChars (JniEnvironment.Current.SafeHandle, @string, isCopy);
			return tmp;
		}

		internal static unsafe void ReleaseStringChars (IntPtr @string, IntPtr chars)
		{
			if (@string == IntPtr.Zero)
				throw new ArgumentException ("`@string` must not be IntPtr.Zero.", "@string");
			if (chars == IntPtr.Zero)
				throw new ArgumentException ("'chars' must not be IntPtr.Zero.", "chars");

			JniEnvironment.Current.Invoker.ReleaseStringChars (JniEnvironment.Current.SafeHandle, @string, chars);
		}
	}

	public static partial class Types {

		internal static unsafe IntPtr DefineClass (string name, IntPtr loader, IntPtr buf, int bufLen)
		{
			if (name == null)
				throw new ArgumentNullException ("name");
			if (loader == IntPtr.Zero)
				throw new ArgumentException ("`loader` must not be IntPtr.Zero.", "loader");
			if (buf == IntPtr.Zero)
				throw new ArgumentException ("'buf' must not be IntPtr.Zero.", "buf");

			var tmp = JniEnvironment.Current.Invoker.DefineClass (JniEnvironment.Current.SafeHandle, name, loader, buf, bufLen);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe IntPtr FindClass (string classname)
		{
			if (classname == null)
				throw new ArgumentNullException ("classname");

			var tmp = JniEnvironment.Current.Invoker.FindClass (JniEnvironment.Current.SafeHandle, classname);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe IntPtr GetSuperclass (IntPtr jclass)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");

			var tmp = JniEnvironment.Current.Invoker.GetSuperclass (JniEnvironment.Current.SafeHandle, jclass);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe bool IsAssignableFrom (IntPtr clazz1, IntPtr clazz2)
		{
			if (clazz1 == IntPtr.Zero)
				throw new ArgumentException ("`clazz1` must not be IntPtr.Zero.", "clazz1");
			if (clazz2 == IntPtr.Zero)
				throw new ArgumentException ("`clazz2` must not be IntPtr.Zero.", "clazz2");

			var tmp = JniEnvironment.Current.Invoker.IsAssignableFrom (JniEnvironment.Current.SafeHandle, clazz1, clazz2);
			return tmp;
		}

		public static unsafe bool IsSameObject (IntPtr ref1, IntPtr ref2)
		{
			var tmp = JniEnvironment.Current.Invoker.IsSameObject (JniEnvironment.Current.SafeHandle, ref1, ref2);
			return tmp;
		}

		public static unsafe IntPtr GetObjectClass (IntPtr jobject)
		{
			if (jobject == IntPtr.Zero)
				throw new ArgumentException ("`jobject` must not be IntPtr.Zero.", "jobject");

			var tmp = JniEnvironment.Current.Invoker.GetObjectClass (JniEnvironment.Current.SafeHandle, jobject);
			JniEnvironment.Current.LogCreateLocalRef (tmp);
			return tmp;
		}

		public static unsafe bool IsInstanceOf (IntPtr obj, IntPtr clazz)
		{
			if (obj == IntPtr.Zero)
				throw new ArgumentException ("`obj` must not be IntPtr.Zero.", "obj");
			if (clazz == IntPtr.Zero)
				throw new ArgumentException ("`clazz` must not be IntPtr.Zero.", "clazz");

			var tmp = JniEnvironment.Current.Invoker.IsInstanceOf (JniEnvironment.Current.SafeHandle, obj, clazz);
			return tmp;
		}

		internal static unsafe int RegisterNatives (IntPtr jclass, JniNativeMethodRegistration [] methods, int nMethods)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");

			var tmp = JniEnvironment.Current.Invoker.RegisterNatives (JniEnvironment.Current.SafeHandle, jclass, methods, nMethods);

			Exception __e = JniEnvironment.Current.GetExceptionForLastThrowable ();
			if (__e != null)
				throw __e;

			return tmp;
		}

		internal static unsafe int UnregisterNatives (IntPtr jclass)
		{
			if (jclass == IntPtr.Zero)
				throw new ArgumentException ("`jclass` must not be IntPtr.Zero.", "jclass");

			var tmp = JniEnvironment.Current.Invoker.UnregisterNatives (JniEnvironment.Current.SafeHandle, jclass);
			return tmp;
		}
	}

	internal static partial class Versions {

		internal static unsafe int GetVersion ()
		{
			var tmp = JniEnvironment.Current.Invoker.GetVersion (JniEnvironment.Current.SafeHandle);
			return tmp;
		}
	}
	}

	partial class JniEnvironmentInvoker {

		internal JniNativeInterfaceStruct env;

		public unsafe JniEnvironmentInvoker (JniNativeInterfaceStruct* p)
		{
			env = *p;
		}


		JniFunc_JniEnvironmentSafeHandle_int _GetVersion;
		public JniFunc_JniEnvironmentSafeHandle_int GetVersion {
			get {
				if (_GetVersion == null)
					_GetVersion = (JniFunc_JniEnvironmentSafeHandle_int) Marshal.GetDelegateForFunctionPointer (env.GetVersion, typeof (JniFunc_JniEnvironmentSafeHandle_int));
				return _GetVersion;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_string_IntPtr_IntPtr_int_jobject _DefineClass;
		public JniFunc_JniEnvironmentSafeHandle_string_IntPtr_IntPtr_int_jobject DefineClass {
			get {
				if (_DefineClass == null)
					_DefineClass = (JniFunc_JniEnvironmentSafeHandle_string_IntPtr_IntPtr_int_jobject) Marshal.GetDelegateForFunctionPointer (env.DefineClass, typeof (JniFunc_JniEnvironmentSafeHandle_string_IntPtr_IntPtr_int_jobject));
				return _DefineClass;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_string_jobject _FindClass;
		public JniFunc_JniEnvironmentSafeHandle_string_jobject FindClass {
			get {
				if (_FindClass == null)
					_FindClass = (JniFunc_JniEnvironmentSafeHandle_string_jobject) Marshal.GetDelegateForFunctionPointer (env.FindClass, typeof (JniFunc_JniEnvironmentSafeHandle_string_jobject));
				return _FindClass;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID _FromReflectedMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID FromReflectedMethod {
			get {
				if (_FromReflectedMethod == null)
					_FromReflectedMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID) Marshal.GetDelegateForFunctionPointer (env.FromReflectedMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID));
				return _FromReflectedMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID _FromReflectedField;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID FromReflectedField {
			get {
				if (_FromReflectedField == null)
					_FromReflectedField = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID) Marshal.GetDelegateForFunctionPointer (env.FromReflectedField, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID));
				return _FromReflectedField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_bool_jobject _ToReflectedMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_bool_jobject ToReflectedMethod {
			get {
				if (_ToReflectedMethod == null)
					_ToReflectedMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_bool_jobject) Marshal.GetDelegateForFunctionPointer (env.ToReflectedMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_bool_jobject));
				return _ToReflectedMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject _GetSuperclass;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject GetSuperclass {
			get {
				if (_GetSuperclass == null)
					_GetSuperclass = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject) Marshal.GetDelegateForFunctionPointer (env.GetSuperclass, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject));
				return _GetSuperclass;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_bool _IsAssignableFrom;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_bool IsAssignableFrom {
			get {
				if (_IsAssignableFrom == null)
					_IsAssignableFrom = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_bool) Marshal.GetDelegateForFunctionPointer (env.IsAssignableFrom, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_bool));
				return _IsAssignableFrom;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_bool_jobject _ToReflectedField;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_bool_jobject ToReflectedField {
			get {
				if (_ToReflectedField == null)
					_ToReflectedField = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_bool_jobject) Marshal.GetDelegateForFunctionPointer (env.ToReflectedField, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_bool_jobject));
				return _ToReflectedField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_int _Throw;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_int Throw {
			get {
				if (_Throw == null)
					_Throw = (JniFunc_JniEnvironmentSafeHandle_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.Throw, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_int));
				return _Throw;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_string_int _ThrowNew;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_string_int ThrowNew {
			get {
				if (_ThrowNew == null)
					_ThrowNew = (JniFunc_JniEnvironmentSafeHandle_IntPtr_string_int) Marshal.GetDelegateForFunctionPointer (env.ThrowNew, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_string_int));
				return _ThrowNew;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_jobject _ExceptionOccurred;
		public JniFunc_JniEnvironmentSafeHandle_jobject ExceptionOccurred {
			get {
				if (_ExceptionOccurred == null)
					_ExceptionOccurred = (JniFunc_JniEnvironmentSafeHandle_jobject) Marshal.GetDelegateForFunctionPointer (env.ExceptionOccurred, typeof (JniFunc_JniEnvironmentSafeHandle_jobject));
				return _ExceptionOccurred;
			}
		}

		JniAction_JniEnvironmentSafeHandle _ExceptionDescribe;
		public JniAction_JniEnvironmentSafeHandle ExceptionDescribe {
			get {
				if (_ExceptionDescribe == null)
					_ExceptionDescribe = (JniAction_JniEnvironmentSafeHandle) Marshal.GetDelegateForFunctionPointer (env.ExceptionDescribe, typeof (JniAction_JniEnvironmentSafeHandle));
				return _ExceptionDescribe;
			}
		}

		JniAction_JniEnvironmentSafeHandle _ExceptionClear;
		public JniAction_JniEnvironmentSafeHandle ExceptionClear {
			get {
				if (_ExceptionClear == null)
					_ExceptionClear = (JniAction_JniEnvironmentSafeHandle) Marshal.GetDelegateForFunctionPointer (env.ExceptionClear, typeof (JniAction_JniEnvironmentSafeHandle));
				return _ExceptionClear;
			}
		}

		JniAction_JniEnvironmentSafeHandle_string _FatalError;
		public JniAction_JniEnvironmentSafeHandle_string FatalError {
			get {
				if (_FatalError == null)
					_FatalError = (JniAction_JniEnvironmentSafeHandle_string) Marshal.GetDelegateForFunctionPointer (env.FatalError, typeof (JniAction_JniEnvironmentSafeHandle_string));
				return _FatalError;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_int _PushLocalFrame;
		public JniFunc_JniEnvironmentSafeHandle_int_int PushLocalFrame {
			get {
				if (_PushLocalFrame == null)
					_PushLocalFrame = (JniFunc_JniEnvironmentSafeHandle_int_int) Marshal.GetDelegateForFunctionPointer (env.PushLocalFrame, typeof (JniFunc_JniEnvironmentSafeHandle_int_int));
				return _PushLocalFrame;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject _PopLocalFrame;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject PopLocalFrame {
			get {
				if (_PopLocalFrame == null)
					_PopLocalFrame = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject) Marshal.GetDelegateForFunctionPointer (env.PopLocalFrame, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject));
				return _PopLocalFrame;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject _NewGlobalRef;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject NewGlobalRef {
			get {
				if (_NewGlobalRef == null)
					_NewGlobalRef = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject) Marshal.GetDelegateForFunctionPointer (env.NewGlobalRef, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject));
				return _NewGlobalRef;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr _DeleteGlobalRef;
		public JniAction_JniEnvironmentSafeHandle_IntPtr DeleteGlobalRef {
			get {
				if (_DeleteGlobalRef == null)
					_DeleteGlobalRef = (JniAction_JniEnvironmentSafeHandle_IntPtr) Marshal.GetDelegateForFunctionPointer (env.DeleteGlobalRef, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr));
				return _DeleteGlobalRef;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr _DeleteLocalRef;
		public JniAction_JniEnvironmentSafeHandle_IntPtr DeleteLocalRef {
			get {
				if (_DeleteLocalRef == null)
					_DeleteLocalRef = (JniAction_JniEnvironmentSafeHandle_IntPtr) Marshal.GetDelegateForFunctionPointer (env.DeleteLocalRef, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr));
				return _DeleteLocalRef;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_bool _IsSameObject;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_bool IsSameObject {
			get {
				if (_IsSameObject == null)
					_IsSameObject = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_bool) Marshal.GetDelegateForFunctionPointer (env.IsSameObject, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_bool));
				return _IsSameObject;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject _NewLocalRef;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject NewLocalRef {
			get {
				if (_NewLocalRef == null)
					_NewLocalRef = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject) Marshal.GetDelegateForFunctionPointer (env.NewLocalRef, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject));
				return _NewLocalRef;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_int _EnsureLocalCapacity;
		public JniFunc_JniEnvironmentSafeHandle_int_int EnsureLocalCapacity {
			get {
				if (_EnsureLocalCapacity == null)
					_EnsureLocalCapacity = (JniFunc_JniEnvironmentSafeHandle_int_int) Marshal.GetDelegateForFunctionPointer (env.EnsureLocalCapacity, typeof (JniFunc_JniEnvironmentSafeHandle_int_int));
				return _EnsureLocalCapacity;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject _AllocObject;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject AllocObject {
			get {
				if (_AllocObject == null)
					_AllocObject = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject) Marshal.GetDelegateForFunctionPointer (env.AllocObject, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject));
				return _AllocObject;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_jobject _NewObject;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_jobject NewObject {
			get {
				if (_NewObject == null)
					_NewObject = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_jobject) Marshal.GetDelegateForFunctionPointer (env.NewObject, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_jobject));
				return _NewObject;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_jobject _NewObjectA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_jobject NewObjectA {
			get {
				if (_NewObjectA == null)
					_NewObjectA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_jobject) Marshal.GetDelegateForFunctionPointer (env.NewObjectA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_jobject));
				return _NewObjectA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject _GetObjectClass;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject GetObjectClass {
			get {
				if (_GetObjectClass == null)
					_GetObjectClass = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject) Marshal.GetDelegateForFunctionPointer (env.GetObjectClass, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject));
				return _GetObjectClass;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_bool _IsInstanceOf;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_bool IsInstanceOf {
			get {
				if (_IsInstanceOf == null)
					_IsInstanceOf = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_bool) Marshal.GetDelegateForFunctionPointer (env.IsInstanceOf, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_bool));
				return _IsInstanceOf;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_string_string_jinstanceMethodID _GetMethodID;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_string_string_jinstanceMethodID GetMethodID {
			get {
				if (_GetMethodID == null)
					_GetMethodID = (JniFunc_JniEnvironmentSafeHandle_IntPtr_string_string_jinstanceMethodID) Marshal.GetDelegateForFunctionPointer (env.GetMethodID, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_string_string_jinstanceMethodID));
				return _GetMethodID;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_jobject _CallObjectMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_jobject CallObjectMethod {
			get {
				if (_CallObjectMethod == null)
					_CallObjectMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_jobject) Marshal.GetDelegateForFunctionPointer (env.CallObjectMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_jobject));
				return _CallObjectMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_jobject _CallObjectMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_jobject CallObjectMethodA {
			get {
				if (_CallObjectMethodA == null)
					_CallObjectMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_jobject) Marshal.GetDelegateForFunctionPointer (env.CallObjectMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_jobject));
				return _CallObjectMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_bool _CallBooleanMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_bool CallBooleanMethod {
			get {
				if (_CallBooleanMethod == null)
					_CallBooleanMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_bool) Marshal.GetDelegateForFunctionPointer (env.CallBooleanMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_bool));
				return _CallBooleanMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_bool _CallBooleanMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_bool CallBooleanMethodA {
			get {
				if (_CallBooleanMethodA == null)
					_CallBooleanMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_bool) Marshal.GetDelegateForFunctionPointer (env.CallBooleanMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_bool));
				return _CallBooleanMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_sbyte _CallSByteMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_sbyte CallSByteMethod {
			get {
				if (_CallSByteMethod == null)
					_CallSByteMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_sbyte) Marshal.GetDelegateForFunctionPointer (env.CallSByteMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_sbyte));
				return _CallSByteMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_sbyte _CallSByteMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_sbyte CallSByteMethodA {
			get {
				if (_CallSByteMethodA == null)
					_CallSByteMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_sbyte) Marshal.GetDelegateForFunctionPointer (env.CallSByteMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_sbyte));
				return _CallSByteMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_char _CallCharMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_char CallCharMethod {
			get {
				if (_CallCharMethod == null)
					_CallCharMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_char) Marshal.GetDelegateForFunctionPointer (env.CallCharMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_char));
				return _CallCharMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_char _CallCharMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_char CallCharMethodA {
			get {
				if (_CallCharMethodA == null)
					_CallCharMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_char) Marshal.GetDelegateForFunctionPointer (env.CallCharMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_char));
				return _CallCharMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_short _CallShortMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_short CallShortMethod {
			get {
				if (_CallShortMethod == null)
					_CallShortMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_short) Marshal.GetDelegateForFunctionPointer (env.CallShortMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_short));
				return _CallShortMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_short _CallShortMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_short CallShortMethodA {
			get {
				if (_CallShortMethodA == null)
					_CallShortMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_short) Marshal.GetDelegateForFunctionPointer (env.CallShortMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_short));
				return _CallShortMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_int _CallIntMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_int CallIntMethod {
			get {
				if (_CallIntMethod == null)
					_CallIntMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_int) Marshal.GetDelegateForFunctionPointer (env.CallIntMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_int));
				return _CallIntMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_int _CallIntMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_int CallIntMethodA {
			get {
				if (_CallIntMethodA == null)
					_CallIntMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_int) Marshal.GetDelegateForFunctionPointer (env.CallIntMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_int));
				return _CallIntMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_long _CallLongMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_long CallLongMethod {
			get {
				if (_CallLongMethod == null)
					_CallLongMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_long) Marshal.GetDelegateForFunctionPointer (env.CallLongMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_long));
				return _CallLongMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_long _CallLongMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_long CallLongMethodA {
			get {
				if (_CallLongMethodA == null)
					_CallLongMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_long) Marshal.GetDelegateForFunctionPointer (env.CallLongMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_long));
				return _CallLongMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_float _CallFloatMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_float CallFloatMethod {
			get {
				if (_CallFloatMethod == null)
					_CallFloatMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_float) Marshal.GetDelegateForFunctionPointer (env.CallFloatMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_float));
				return _CallFloatMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_float _CallFloatMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_float CallFloatMethodA {
			get {
				if (_CallFloatMethodA == null)
					_CallFloatMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_float) Marshal.GetDelegateForFunctionPointer (env.CallFloatMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_float));
				return _CallFloatMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_double _CallDoubleMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_double CallDoubleMethod {
			get {
				if (_CallDoubleMethod == null)
					_CallDoubleMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_double) Marshal.GetDelegateForFunctionPointer (env.CallDoubleMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_double));
				return _CallDoubleMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_double _CallDoubleMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_double CallDoubleMethodA {
			get {
				if (_CallDoubleMethodA == null)
					_CallDoubleMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_double) Marshal.GetDelegateForFunctionPointer (env.CallDoubleMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef_double));
				return _CallDoubleMethodA;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID _CallVoidMethod;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID CallVoidMethod {
			get {
				if (_CallVoidMethod == null)
					_CallVoidMethod = (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID) Marshal.GetDelegateForFunctionPointer (env.CallVoidMethod, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID));
				return _CallVoidMethod;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef _CallVoidMethodA;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef CallVoidMethodA {
			get {
				if (_CallVoidMethodA == null)
					_CallVoidMethodA = (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef) Marshal.GetDelegateForFunctionPointer (env.CallVoidMethodA, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceMethodID_JValueRef));
				return _CallVoidMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_jobject _CallNonvirtualObjectMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_jobject CallNonvirtualObjectMethod {
			get {
				if (_CallNonvirtualObjectMethod == null)
					_CallNonvirtualObjectMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_jobject) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualObjectMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_jobject));
				return _CallNonvirtualObjectMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_jobject _CallNonvirtualObjectMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_jobject CallNonvirtualObjectMethodA {
			get {
				if (_CallNonvirtualObjectMethodA == null)
					_CallNonvirtualObjectMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_jobject) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualObjectMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_jobject));
				return _CallNonvirtualObjectMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_bool _CallNonvirtualBooleanMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_bool CallNonvirtualBooleanMethod {
			get {
				if (_CallNonvirtualBooleanMethod == null)
					_CallNonvirtualBooleanMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_bool) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualBooleanMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_bool));
				return _CallNonvirtualBooleanMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_bool _CallNonvirtualBooleanMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_bool CallNonvirtualBooleanMethodA {
			get {
				if (_CallNonvirtualBooleanMethodA == null)
					_CallNonvirtualBooleanMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_bool) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualBooleanMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_bool));
				return _CallNonvirtualBooleanMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_sbyte _CallNonvirtualSByteMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_sbyte CallNonvirtualSByteMethod {
			get {
				if (_CallNonvirtualSByteMethod == null)
					_CallNonvirtualSByteMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_sbyte) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualSByteMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_sbyte));
				return _CallNonvirtualSByteMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_sbyte _CallNonvirtualSByteMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_sbyte CallNonvirtualSByteMethodA {
			get {
				if (_CallNonvirtualSByteMethodA == null)
					_CallNonvirtualSByteMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_sbyte) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualSByteMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_sbyte));
				return _CallNonvirtualSByteMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_char _CallNonvirtualCharMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_char CallNonvirtualCharMethod {
			get {
				if (_CallNonvirtualCharMethod == null)
					_CallNonvirtualCharMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_char) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualCharMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_char));
				return _CallNonvirtualCharMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_char _CallNonvirtualCharMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_char CallNonvirtualCharMethodA {
			get {
				if (_CallNonvirtualCharMethodA == null)
					_CallNonvirtualCharMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_char) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualCharMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_char));
				return _CallNonvirtualCharMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_short _CallNonvirtualShortMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_short CallNonvirtualShortMethod {
			get {
				if (_CallNonvirtualShortMethod == null)
					_CallNonvirtualShortMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_short) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualShortMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_short));
				return _CallNonvirtualShortMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_short _CallNonvirtualShortMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_short CallNonvirtualShortMethodA {
			get {
				if (_CallNonvirtualShortMethodA == null)
					_CallNonvirtualShortMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_short) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualShortMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_short));
				return _CallNonvirtualShortMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_int _CallNonvirtualIntMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_int CallNonvirtualIntMethod {
			get {
				if (_CallNonvirtualIntMethod == null)
					_CallNonvirtualIntMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_int) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualIntMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_int));
				return _CallNonvirtualIntMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_int _CallNonvirtualIntMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_int CallNonvirtualIntMethodA {
			get {
				if (_CallNonvirtualIntMethodA == null)
					_CallNonvirtualIntMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_int) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualIntMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_int));
				return _CallNonvirtualIntMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_long _CallNonvirtualLongMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_long CallNonvirtualLongMethod {
			get {
				if (_CallNonvirtualLongMethod == null)
					_CallNonvirtualLongMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_long) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualLongMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_long));
				return _CallNonvirtualLongMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_long _CallNonvirtualLongMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_long CallNonvirtualLongMethodA {
			get {
				if (_CallNonvirtualLongMethodA == null)
					_CallNonvirtualLongMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_long) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualLongMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_long));
				return _CallNonvirtualLongMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_float _CallNonvirtualFloatMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_float CallNonvirtualFloatMethod {
			get {
				if (_CallNonvirtualFloatMethod == null)
					_CallNonvirtualFloatMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_float) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualFloatMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_float));
				return _CallNonvirtualFloatMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_float _CallNonvirtualFloatMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_float CallNonvirtualFloatMethodA {
			get {
				if (_CallNonvirtualFloatMethodA == null)
					_CallNonvirtualFloatMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_float) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualFloatMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_float));
				return _CallNonvirtualFloatMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_double _CallNonvirtualDoubleMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_double CallNonvirtualDoubleMethod {
			get {
				if (_CallNonvirtualDoubleMethod == null)
					_CallNonvirtualDoubleMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_double) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualDoubleMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_double));
				return _CallNonvirtualDoubleMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_double _CallNonvirtualDoubleMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_double CallNonvirtualDoubleMethodA {
			get {
				if (_CallNonvirtualDoubleMethodA == null)
					_CallNonvirtualDoubleMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_double) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualDoubleMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef_double));
				return _CallNonvirtualDoubleMethodA;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID _CallNonvirtualVoidMethod;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID CallNonvirtualVoidMethod {
			get {
				if (_CallNonvirtualVoidMethod == null)
					_CallNonvirtualVoidMethod = (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualVoidMethod, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID));
				return _CallNonvirtualVoidMethod;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef _CallNonvirtualVoidMethodA;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef CallNonvirtualVoidMethodA {
			get {
				if (_CallNonvirtualVoidMethodA == null)
					_CallNonvirtualVoidMethodA = (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef) Marshal.GetDelegateForFunctionPointer (env.CallNonvirtualVoidMethodA, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_jinstanceMethodID_JValueRef));
				return _CallNonvirtualVoidMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_string_string_jinstanceFieldID _GetFieldID;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_string_string_jinstanceFieldID GetFieldID {
			get {
				if (_GetFieldID == null)
					_GetFieldID = (JniFunc_JniEnvironmentSafeHandle_IntPtr_string_string_jinstanceFieldID) Marshal.GetDelegateForFunctionPointer (env.GetFieldID, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_string_string_jinstanceFieldID));
				return _GetFieldID;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_jobject _GetObjectField;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_jobject GetObjectField {
			get {
				if (_GetObjectField == null)
					_GetObjectField = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_jobject) Marshal.GetDelegateForFunctionPointer (env.GetObjectField, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_jobject));
				return _GetObjectField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_bool _GetBooleanField;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_bool GetBooleanField {
			get {
				if (_GetBooleanField == null)
					_GetBooleanField = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_bool) Marshal.GetDelegateForFunctionPointer (env.GetBooleanField, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_bool));
				return _GetBooleanField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_sbyte _GetByteField;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_sbyte GetByteField {
			get {
				if (_GetByteField == null)
					_GetByteField = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_sbyte) Marshal.GetDelegateForFunctionPointer (env.GetByteField, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_sbyte));
				return _GetByteField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_char _GetCharField;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_char GetCharField {
			get {
				if (_GetCharField == null)
					_GetCharField = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_char) Marshal.GetDelegateForFunctionPointer (env.GetCharField, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_char));
				return _GetCharField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_short _GetShortField;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_short GetShortField {
			get {
				if (_GetShortField == null)
					_GetShortField = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_short) Marshal.GetDelegateForFunctionPointer (env.GetShortField, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_short));
				return _GetShortField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_int _GetIntField;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_int GetIntField {
			get {
				if (_GetIntField == null)
					_GetIntField = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_int) Marshal.GetDelegateForFunctionPointer (env.GetIntField, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_int));
				return _GetIntField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_long _GetLongField;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_long GetLongField {
			get {
				if (_GetLongField == null)
					_GetLongField = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_long) Marshal.GetDelegateForFunctionPointer (env.GetLongField, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_long));
				return _GetLongField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_float _GetFloatField;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_float GetFloatField {
			get {
				if (_GetFloatField == null)
					_GetFloatField = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_float) Marshal.GetDelegateForFunctionPointer (env.GetFloatField, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_float));
				return _GetFloatField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_double _GetDoubleField;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_double GetDoubleField {
			get {
				if (_GetDoubleField == null)
					_GetDoubleField = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_double) Marshal.GetDelegateForFunctionPointer (env.GetDoubleField, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_double));
				return _GetDoubleField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_IntPtr _SetObjectField;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_IntPtr SetObjectField {
			get {
				if (_SetObjectField == null)
					_SetObjectField = (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_IntPtr) Marshal.GetDelegateForFunctionPointer (env.SetObjectField, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_IntPtr));
				return _SetObjectField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_bool _SetBooleanField;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_bool SetBooleanField {
			get {
				if (_SetBooleanField == null)
					_SetBooleanField = (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_bool) Marshal.GetDelegateForFunctionPointer (env.SetBooleanField, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_bool));
				return _SetBooleanField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_sbyte _SetByteField;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_sbyte SetByteField {
			get {
				if (_SetByteField == null)
					_SetByteField = (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_sbyte) Marshal.GetDelegateForFunctionPointer (env.SetByteField, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_sbyte));
				return _SetByteField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_char _SetCharField;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_char SetCharField {
			get {
				if (_SetCharField == null)
					_SetCharField = (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_char) Marshal.GetDelegateForFunctionPointer (env.SetCharField, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_char));
				return _SetCharField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_short _SetShortField;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_short SetShortField {
			get {
				if (_SetShortField == null)
					_SetShortField = (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_short) Marshal.GetDelegateForFunctionPointer (env.SetShortField, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_short));
				return _SetShortField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_int _SetIntField;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_int SetIntField {
			get {
				if (_SetIntField == null)
					_SetIntField = (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_int) Marshal.GetDelegateForFunctionPointer (env.SetIntField, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_int));
				return _SetIntField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_long _SetLongField;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_long SetLongField {
			get {
				if (_SetLongField == null)
					_SetLongField = (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_long) Marshal.GetDelegateForFunctionPointer (env.SetLongField, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_long));
				return _SetLongField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_float _SetFloatField;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_float SetFloatField {
			get {
				if (_SetFloatField == null)
					_SetFloatField = (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_float) Marshal.GetDelegateForFunctionPointer (env.SetFloatField, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_float));
				return _SetFloatField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_double _SetDoubleField;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_double SetDoubleField {
			get {
				if (_SetDoubleField == null)
					_SetDoubleField = (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_double) Marshal.GetDelegateForFunctionPointer (env.SetDoubleField, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jinstanceFieldID_double));
				return _SetDoubleField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_string_string_jstaticMethodID _GetStaticMethodID;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_string_string_jstaticMethodID GetStaticMethodID {
			get {
				if (_GetStaticMethodID == null)
					_GetStaticMethodID = (JniFunc_JniEnvironmentSafeHandle_IntPtr_string_string_jstaticMethodID) Marshal.GetDelegateForFunctionPointer (env.GetStaticMethodID, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_string_string_jstaticMethodID));
				return _GetStaticMethodID;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_jobject _CallStaticObjectMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_jobject CallStaticObjectMethod {
			get {
				if (_CallStaticObjectMethod == null)
					_CallStaticObjectMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_jobject) Marshal.GetDelegateForFunctionPointer (env.CallStaticObjectMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_jobject));
				return _CallStaticObjectMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_jobject _CallStaticObjectMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_jobject CallStaticObjectMethodA {
			get {
				if (_CallStaticObjectMethodA == null)
					_CallStaticObjectMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_jobject) Marshal.GetDelegateForFunctionPointer (env.CallStaticObjectMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_jobject));
				return _CallStaticObjectMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_bool _CallStaticBooleanMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_bool CallStaticBooleanMethod {
			get {
				if (_CallStaticBooleanMethod == null)
					_CallStaticBooleanMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_bool) Marshal.GetDelegateForFunctionPointer (env.CallStaticBooleanMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_bool));
				return _CallStaticBooleanMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_bool _CallStaticBooleanMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_bool CallStaticBooleanMethodA {
			get {
				if (_CallStaticBooleanMethodA == null)
					_CallStaticBooleanMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_bool) Marshal.GetDelegateForFunctionPointer (env.CallStaticBooleanMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_bool));
				return _CallStaticBooleanMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_sbyte _CallStaticSByteMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_sbyte CallStaticSByteMethod {
			get {
				if (_CallStaticSByteMethod == null)
					_CallStaticSByteMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_sbyte) Marshal.GetDelegateForFunctionPointer (env.CallStaticSByteMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_sbyte));
				return _CallStaticSByteMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_sbyte _CallStaticSByteMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_sbyte CallStaticSByteMethodA {
			get {
				if (_CallStaticSByteMethodA == null)
					_CallStaticSByteMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_sbyte) Marshal.GetDelegateForFunctionPointer (env.CallStaticSByteMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_sbyte));
				return _CallStaticSByteMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_char _CallStaticCharMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_char CallStaticCharMethod {
			get {
				if (_CallStaticCharMethod == null)
					_CallStaticCharMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_char) Marshal.GetDelegateForFunctionPointer (env.CallStaticCharMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_char));
				return _CallStaticCharMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_char _CallStaticCharMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_char CallStaticCharMethodA {
			get {
				if (_CallStaticCharMethodA == null)
					_CallStaticCharMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_char) Marshal.GetDelegateForFunctionPointer (env.CallStaticCharMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_char));
				return _CallStaticCharMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_short _CallStaticShortMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_short CallStaticShortMethod {
			get {
				if (_CallStaticShortMethod == null)
					_CallStaticShortMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_short) Marshal.GetDelegateForFunctionPointer (env.CallStaticShortMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_short));
				return _CallStaticShortMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_short _CallStaticShortMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_short CallStaticShortMethodA {
			get {
				if (_CallStaticShortMethodA == null)
					_CallStaticShortMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_short) Marshal.GetDelegateForFunctionPointer (env.CallStaticShortMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_short));
				return _CallStaticShortMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_int _CallStaticIntMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_int CallStaticIntMethod {
			get {
				if (_CallStaticIntMethod == null)
					_CallStaticIntMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_int) Marshal.GetDelegateForFunctionPointer (env.CallStaticIntMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_int));
				return _CallStaticIntMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_int _CallStaticIntMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_int CallStaticIntMethodA {
			get {
				if (_CallStaticIntMethodA == null)
					_CallStaticIntMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_int) Marshal.GetDelegateForFunctionPointer (env.CallStaticIntMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_int));
				return _CallStaticIntMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_long _CallStaticLongMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_long CallStaticLongMethod {
			get {
				if (_CallStaticLongMethod == null)
					_CallStaticLongMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_long) Marshal.GetDelegateForFunctionPointer (env.CallStaticLongMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_long));
				return _CallStaticLongMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_long _CallStaticLongMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_long CallStaticLongMethodA {
			get {
				if (_CallStaticLongMethodA == null)
					_CallStaticLongMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_long) Marshal.GetDelegateForFunctionPointer (env.CallStaticLongMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_long));
				return _CallStaticLongMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_float _CallStaticFloatMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_float CallStaticFloatMethod {
			get {
				if (_CallStaticFloatMethod == null)
					_CallStaticFloatMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_float) Marshal.GetDelegateForFunctionPointer (env.CallStaticFloatMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_float));
				return _CallStaticFloatMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_float _CallStaticFloatMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_float CallStaticFloatMethodA {
			get {
				if (_CallStaticFloatMethodA == null)
					_CallStaticFloatMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_float) Marshal.GetDelegateForFunctionPointer (env.CallStaticFloatMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_float));
				return _CallStaticFloatMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_double _CallStaticDoubleMethod;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_double CallStaticDoubleMethod {
			get {
				if (_CallStaticDoubleMethod == null)
					_CallStaticDoubleMethod = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_double) Marshal.GetDelegateForFunctionPointer (env.CallStaticDoubleMethod, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_double));
				return _CallStaticDoubleMethod;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_double _CallStaticDoubleMethodA;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_double CallStaticDoubleMethodA {
			get {
				if (_CallStaticDoubleMethodA == null)
					_CallStaticDoubleMethodA = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_double) Marshal.GetDelegateForFunctionPointer (env.CallStaticDoubleMethodA, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef_double));
				return _CallStaticDoubleMethodA;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID _CallStaticVoidMethod;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID CallStaticVoidMethod {
			get {
				if (_CallStaticVoidMethod == null)
					_CallStaticVoidMethod = (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID) Marshal.GetDelegateForFunctionPointer (env.CallStaticVoidMethod, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID));
				return _CallStaticVoidMethod;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef _CallStaticVoidMethodA;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef CallStaticVoidMethodA {
			get {
				if (_CallStaticVoidMethodA == null)
					_CallStaticVoidMethodA = (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef) Marshal.GetDelegateForFunctionPointer (env.CallStaticVoidMethodA, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticMethodID_JValueRef));
				return _CallStaticVoidMethodA;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_string_string_jstaticFieldID _GetStaticFieldID;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_string_string_jstaticFieldID GetStaticFieldID {
			get {
				if (_GetStaticFieldID == null)
					_GetStaticFieldID = (JniFunc_JniEnvironmentSafeHandle_IntPtr_string_string_jstaticFieldID) Marshal.GetDelegateForFunctionPointer (env.GetStaticFieldID, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_string_string_jstaticFieldID));
				return _GetStaticFieldID;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_jobject _GetStaticObjectField;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_jobject GetStaticObjectField {
			get {
				if (_GetStaticObjectField == null)
					_GetStaticObjectField = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_jobject) Marshal.GetDelegateForFunctionPointer (env.GetStaticObjectField, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_jobject));
				return _GetStaticObjectField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_bool _GetStaticBooleanField;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_bool GetStaticBooleanField {
			get {
				if (_GetStaticBooleanField == null)
					_GetStaticBooleanField = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_bool) Marshal.GetDelegateForFunctionPointer (env.GetStaticBooleanField, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_bool));
				return _GetStaticBooleanField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_sbyte _GetStaticByteField;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_sbyte GetStaticByteField {
			get {
				if (_GetStaticByteField == null)
					_GetStaticByteField = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_sbyte) Marshal.GetDelegateForFunctionPointer (env.GetStaticByteField, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_sbyte));
				return _GetStaticByteField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_char _GetStaticCharField;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_char GetStaticCharField {
			get {
				if (_GetStaticCharField == null)
					_GetStaticCharField = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_char) Marshal.GetDelegateForFunctionPointer (env.GetStaticCharField, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_char));
				return _GetStaticCharField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_short _GetStaticShortField;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_short GetStaticShortField {
			get {
				if (_GetStaticShortField == null)
					_GetStaticShortField = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_short) Marshal.GetDelegateForFunctionPointer (env.GetStaticShortField, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_short));
				return _GetStaticShortField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_int _GetStaticIntField;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_int GetStaticIntField {
			get {
				if (_GetStaticIntField == null)
					_GetStaticIntField = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_int) Marshal.GetDelegateForFunctionPointer (env.GetStaticIntField, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_int));
				return _GetStaticIntField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_long _GetStaticLongField;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_long GetStaticLongField {
			get {
				if (_GetStaticLongField == null)
					_GetStaticLongField = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_long) Marshal.GetDelegateForFunctionPointer (env.GetStaticLongField, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_long));
				return _GetStaticLongField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_float _GetStaticFloatField;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_float GetStaticFloatField {
			get {
				if (_GetStaticFloatField == null)
					_GetStaticFloatField = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_float) Marshal.GetDelegateForFunctionPointer (env.GetStaticFloatField, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_float));
				return _GetStaticFloatField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_double _GetStaticDoubleField;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_double GetStaticDoubleField {
			get {
				if (_GetStaticDoubleField == null)
					_GetStaticDoubleField = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_double) Marshal.GetDelegateForFunctionPointer (env.GetStaticDoubleField, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_double));
				return _GetStaticDoubleField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_IntPtr _SetStaticObjectField;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_IntPtr SetStaticObjectField {
			get {
				if (_SetStaticObjectField == null)
					_SetStaticObjectField = (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_IntPtr) Marshal.GetDelegateForFunctionPointer (env.SetStaticObjectField, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_IntPtr));
				return _SetStaticObjectField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_bool _SetStaticBooleanField;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_bool SetStaticBooleanField {
			get {
				if (_SetStaticBooleanField == null)
					_SetStaticBooleanField = (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_bool) Marshal.GetDelegateForFunctionPointer (env.SetStaticBooleanField, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_bool));
				return _SetStaticBooleanField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_sbyte _SetStaticByteField;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_sbyte SetStaticByteField {
			get {
				if (_SetStaticByteField == null)
					_SetStaticByteField = (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_sbyte) Marshal.GetDelegateForFunctionPointer (env.SetStaticByteField, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_sbyte));
				return _SetStaticByteField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_char _SetStaticCharField;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_char SetStaticCharField {
			get {
				if (_SetStaticCharField == null)
					_SetStaticCharField = (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_char) Marshal.GetDelegateForFunctionPointer (env.SetStaticCharField, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_char));
				return _SetStaticCharField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_short _SetStaticShortField;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_short SetStaticShortField {
			get {
				if (_SetStaticShortField == null)
					_SetStaticShortField = (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_short) Marshal.GetDelegateForFunctionPointer (env.SetStaticShortField, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_short));
				return _SetStaticShortField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_int _SetStaticIntField;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_int SetStaticIntField {
			get {
				if (_SetStaticIntField == null)
					_SetStaticIntField = (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_int) Marshal.GetDelegateForFunctionPointer (env.SetStaticIntField, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_int));
				return _SetStaticIntField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_long _SetStaticLongField;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_long SetStaticLongField {
			get {
				if (_SetStaticLongField == null)
					_SetStaticLongField = (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_long) Marshal.GetDelegateForFunctionPointer (env.SetStaticLongField, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_long));
				return _SetStaticLongField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_float _SetStaticFloatField;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_float SetStaticFloatField {
			get {
				if (_SetStaticFloatField == null)
					_SetStaticFloatField = (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_float) Marshal.GetDelegateForFunctionPointer (env.SetStaticFloatField, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_float));
				return _SetStaticFloatField;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_double _SetStaticDoubleField;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_double SetStaticDoubleField {
			get {
				if (_SetStaticDoubleField == null)
					_SetStaticDoubleField = (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_double) Marshal.GetDelegateForFunctionPointer (env.SetStaticDoubleField, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_jstaticFieldID_double));
				return _SetStaticDoubleField;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_int_jobject _NewString;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_int_jobject NewString {
			get {
				if (_NewString == null)
					_NewString = (JniFunc_JniEnvironmentSafeHandle_IntPtr_int_jobject) Marshal.GetDelegateForFunctionPointer (env.NewString, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_int_jobject));
				return _NewString;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_int _GetStringLength;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_int GetStringLength {
			get {
				if (_GetStringLength == null)
					_GetStringLength = (JniFunc_JniEnvironmentSafeHandle_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.GetStringLength, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_int));
				return _GetStringLength;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr _GetStringChars;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr GetStringChars {
			get {
				if (_GetStringChars == null)
					_GetStringChars = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetStringChars, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr));
				return _GetStringChars;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr _ReleaseStringChars;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr ReleaseStringChars {
			get {
				if (_ReleaseStringChars == null)
					_ReleaseStringChars = (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.ReleaseStringChars, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr));
				return _ReleaseStringChars;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_string_jobject _NewStringUTF;
		public JniFunc_JniEnvironmentSafeHandle_string_jobject NewStringUTF {
			get {
				if (_NewStringUTF == null)
					_NewStringUTF = (JniFunc_JniEnvironmentSafeHandle_string_jobject) Marshal.GetDelegateForFunctionPointer (env.NewStringUTF, typeof (JniFunc_JniEnvironmentSafeHandle_string_jobject));
				return _NewStringUTF;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_int _GetStringUTFLength;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_int GetStringUTFLength {
			get {
				if (_GetStringUTFLength == null)
					_GetStringUTFLength = (JniFunc_JniEnvironmentSafeHandle_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.GetStringUTFLength, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_int));
				return _GetStringUTFLength;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_string _GetStringUTFChars;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_string GetStringUTFChars {
			get {
				if (_GetStringUTFChars == null)
					_GetStringUTFChars = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_string) Marshal.GetDelegateForFunctionPointer (env.GetStringUTFChars, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_string));
				return _GetStringUTFChars;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_string _ReleaseStringUTFChars;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_string ReleaseStringUTFChars {
			get {
				if (_ReleaseStringUTFChars == null)
					_ReleaseStringUTFChars = (JniAction_JniEnvironmentSafeHandle_IntPtr_string) Marshal.GetDelegateForFunctionPointer (env.ReleaseStringUTFChars, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_string));
				return _ReleaseStringUTFChars;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_int _GetArrayLength;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_int GetArrayLength {
			get {
				if (_GetArrayLength == null)
					_GetArrayLength = (JniFunc_JniEnvironmentSafeHandle_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.GetArrayLength, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_int));
				return _GetArrayLength;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_IntPtr_IntPtr_jobject _NewObjectArray;
		public JniFunc_JniEnvironmentSafeHandle_int_IntPtr_IntPtr_jobject NewObjectArray {
			get {
				if (_NewObjectArray == null)
					_NewObjectArray = (JniFunc_JniEnvironmentSafeHandle_int_IntPtr_IntPtr_jobject) Marshal.GetDelegateForFunctionPointer (env.NewObjectArray, typeof (JniFunc_JniEnvironmentSafeHandle_int_IntPtr_IntPtr_jobject));
				return _NewObjectArray;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_int_jobject _GetObjectArrayElement;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_int_jobject GetObjectArrayElement {
			get {
				if (_GetObjectArrayElement == null)
					_GetObjectArrayElement = (JniFunc_JniEnvironmentSafeHandle_IntPtr_int_jobject) Marshal.GetDelegateForFunctionPointer (env.GetObjectArrayElement, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_int_jobject));
				return _GetObjectArrayElement;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_int_IntPtr _SetObjectArrayElement;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_int_IntPtr SetObjectArrayElement {
			get {
				if (_SetObjectArrayElement == null)
					_SetObjectArrayElement = (JniAction_JniEnvironmentSafeHandle_IntPtr_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.SetObjectArrayElement, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_int_IntPtr));
				return _SetObjectArrayElement;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_jobject _NewBooleanArray;
		public JniFunc_JniEnvironmentSafeHandle_int_jobject NewBooleanArray {
			get {
				if (_NewBooleanArray == null)
					_NewBooleanArray = (JniFunc_JniEnvironmentSafeHandle_int_jobject) Marshal.GetDelegateForFunctionPointer (env.NewBooleanArray, typeof (JniFunc_JniEnvironmentSafeHandle_int_jobject));
				return _NewBooleanArray;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_jobject _NewByteArray;
		public JniFunc_JniEnvironmentSafeHandle_int_jobject NewByteArray {
			get {
				if (_NewByteArray == null)
					_NewByteArray = (JniFunc_JniEnvironmentSafeHandle_int_jobject) Marshal.GetDelegateForFunctionPointer (env.NewByteArray, typeof (JniFunc_JniEnvironmentSafeHandle_int_jobject));
				return _NewByteArray;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_jobject _NewCharArray;
		public JniFunc_JniEnvironmentSafeHandle_int_jobject NewCharArray {
			get {
				if (_NewCharArray == null)
					_NewCharArray = (JniFunc_JniEnvironmentSafeHandle_int_jobject) Marshal.GetDelegateForFunctionPointer (env.NewCharArray, typeof (JniFunc_JniEnvironmentSafeHandle_int_jobject));
				return _NewCharArray;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_jobject _NewShortArray;
		public JniFunc_JniEnvironmentSafeHandle_int_jobject NewShortArray {
			get {
				if (_NewShortArray == null)
					_NewShortArray = (JniFunc_JniEnvironmentSafeHandle_int_jobject) Marshal.GetDelegateForFunctionPointer (env.NewShortArray, typeof (JniFunc_JniEnvironmentSafeHandle_int_jobject));
				return _NewShortArray;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_jobject _NewIntArray;
		public JniFunc_JniEnvironmentSafeHandle_int_jobject NewIntArray {
			get {
				if (_NewIntArray == null)
					_NewIntArray = (JniFunc_JniEnvironmentSafeHandle_int_jobject) Marshal.GetDelegateForFunctionPointer (env.NewIntArray, typeof (JniFunc_JniEnvironmentSafeHandle_int_jobject));
				return _NewIntArray;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_jobject _NewLongArray;
		public JniFunc_JniEnvironmentSafeHandle_int_jobject NewLongArray {
			get {
				if (_NewLongArray == null)
					_NewLongArray = (JniFunc_JniEnvironmentSafeHandle_int_jobject) Marshal.GetDelegateForFunctionPointer (env.NewLongArray, typeof (JniFunc_JniEnvironmentSafeHandle_int_jobject));
				return _NewLongArray;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_jobject _NewFloatArray;
		public JniFunc_JniEnvironmentSafeHandle_int_jobject NewFloatArray {
			get {
				if (_NewFloatArray == null)
					_NewFloatArray = (JniFunc_JniEnvironmentSafeHandle_int_jobject) Marshal.GetDelegateForFunctionPointer (env.NewFloatArray, typeof (JniFunc_JniEnvironmentSafeHandle_int_jobject));
				return _NewFloatArray;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_int_jobject _NewDoubleArray;
		public JniFunc_JniEnvironmentSafeHandle_int_jobject NewDoubleArray {
			get {
				if (_NewDoubleArray == null)
					_NewDoubleArray = (JniFunc_JniEnvironmentSafeHandle_int_jobject) Marshal.GetDelegateForFunctionPointer (env.NewDoubleArray, typeof (JniFunc_JniEnvironmentSafeHandle_int_jobject));
				return _NewDoubleArray;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr _GetBooleanArrayElements;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr GetBooleanArrayElements {
			get {
				if (_GetBooleanArrayElements == null)
					_GetBooleanArrayElements = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetBooleanArrayElements, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr));
				return _GetBooleanArrayElements;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr _GetByteArrayElements;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr GetByteArrayElements {
			get {
				if (_GetByteArrayElements == null)
					_GetByteArrayElements = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetByteArrayElements, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr));
				return _GetByteArrayElements;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr _GetCharArrayElements;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr GetCharArrayElements {
			get {
				if (_GetCharArrayElements == null)
					_GetCharArrayElements = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetCharArrayElements, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr));
				return _GetCharArrayElements;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr _GetShortArrayElements;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr GetShortArrayElements {
			get {
				if (_GetShortArrayElements == null)
					_GetShortArrayElements = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetShortArrayElements, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr));
				return _GetShortArrayElements;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr _GetIntArrayElements;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr GetIntArrayElements {
			get {
				if (_GetIntArrayElements == null)
					_GetIntArrayElements = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetIntArrayElements, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr));
				return _GetIntArrayElements;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr _GetLongArrayElements;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr GetLongArrayElements {
			get {
				if (_GetLongArrayElements == null)
					_GetLongArrayElements = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetLongArrayElements, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr));
				return _GetLongArrayElements;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr _GetFloatArrayElements;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr GetFloatArrayElements {
			get {
				if (_GetFloatArrayElements == null)
					_GetFloatArrayElements = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetFloatArrayElements, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr));
				return _GetFloatArrayElements;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr _GetDoubleArrayElements;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr GetDoubleArrayElements {
			get {
				if (_GetDoubleArrayElements == null)
					_GetDoubleArrayElements = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetDoubleArrayElements, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr));
				return _GetDoubleArrayElements;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int _ReleaseBooleanArrayElements;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int ReleaseBooleanArrayElements {
			get {
				if (_ReleaseBooleanArrayElements == null)
					_ReleaseBooleanArrayElements = (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.ReleaseBooleanArrayElements, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int));
				return _ReleaseBooleanArrayElements;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int _ReleaseByteArrayElements;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int ReleaseByteArrayElements {
			get {
				if (_ReleaseByteArrayElements == null)
					_ReleaseByteArrayElements = (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.ReleaseByteArrayElements, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int));
				return _ReleaseByteArrayElements;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int _ReleaseCharArrayElements;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int ReleaseCharArrayElements {
			get {
				if (_ReleaseCharArrayElements == null)
					_ReleaseCharArrayElements = (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.ReleaseCharArrayElements, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int));
				return _ReleaseCharArrayElements;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int _ReleaseShortArrayElements;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int ReleaseShortArrayElements {
			get {
				if (_ReleaseShortArrayElements == null)
					_ReleaseShortArrayElements = (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.ReleaseShortArrayElements, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int));
				return _ReleaseShortArrayElements;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int _ReleaseIntArrayElements;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int ReleaseIntArrayElements {
			get {
				if (_ReleaseIntArrayElements == null)
					_ReleaseIntArrayElements = (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.ReleaseIntArrayElements, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int));
				return _ReleaseIntArrayElements;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int _ReleaseLongArrayElements;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int ReleaseLongArrayElements {
			get {
				if (_ReleaseLongArrayElements == null)
					_ReleaseLongArrayElements = (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.ReleaseLongArrayElements, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int));
				return _ReleaseLongArrayElements;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int _ReleaseFloatArrayElements;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int ReleaseFloatArrayElements {
			get {
				if (_ReleaseFloatArrayElements == null)
					_ReleaseFloatArrayElements = (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.ReleaseFloatArrayElements, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int));
				return _ReleaseFloatArrayElements;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int _ReleaseDoubleArrayElements;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int ReleaseDoubleArrayElements {
			get {
				if (_ReleaseDoubleArrayElements == null)
					_ReleaseDoubleArrayElements = (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.ReleaseDoubleArrayElements, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int));
				return _ReleaseDoubleArrayElements;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr _GetBooleanArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr GetBooleanArrayRegion {
			get {
				if (_GetBooleanArrayRegion == null)
					_GetBooleanArrayRegion = (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetBooleanArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr));
				return _GetBooleanArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr _GetByteArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr GetByteArrayRegion {
			get {
				if (_GetByteArrayRegion == null)
					_GetByteArrayRegion = (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetByteArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr));
				return _GetByteArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr _GetCharArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr GetCharArrayRegion {
			get {
				if (_GetCharArrayRegion == null)
					_GetCharArrayRegion = (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetCharArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr));
				return _GetCharArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr _GetShortArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr GetShortArrayRegion {
			get {
				if (_GetShortArrayRegion == null)
					_GetShortArrayRegion = (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetShortArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr));
				return _GetShortArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr _GetIntArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr GetIntArrayRegion {
			get {
				if (_GetIntArrayRegion == null)
					_GetIntArrayRegion = (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetIntArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr));
				return _GetIntArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr _GetLongArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr GetLongArrayRegion {
			get {
				if (_GetLongArrayRegion == null)
					_GetLongArrayRegion = (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetLongArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr));
				return _GetLongArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr _GetFloatArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr GetFloatArrayRegion {
			get {
				if (_GetFloatArrayRegion == null)
					_GetFloatArrayRegion = (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetFloatArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr));
				return _GetFloatArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr _GetDoubleArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr GetDoubleArrayRegion {
			get {
				if (_GetDoubleArrayRegion == null)
					_GetDoubleArrayRegion = (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetDoubleArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr));
				return _GetDoubleArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr _SetBooleanArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr SetBooleanArrayRegion {
			get {
				if (_SetBooleanArrayRegion == null)
					_SetBooleanArrayRegion = (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.SetBooleanArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr));
				return _SetBooleanArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr _SetByteArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr SetByteArrayRegion {
			get {
				if (_SetByteArrayRegion == null)
					_SetByteArrayRegion = (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.SetByteArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr));
				return _SetByteArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr _SetCharArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr SetCharArrayRegion {
			get {
				if (_SetCharArrayRegion == null)
					_SetCharArrayRegion = (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.SetCharArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr));
				return _SetCharArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr _SetShortArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr SetShortArrayRegion {
			get {
				if (_SetShortArrayRegion == null)
					_SetShortArrayRegion = (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.SetShortArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr));
				return _SetShortArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr _SetIntArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr SetIntArrayRegion {
			get {
				if (_SetIntArrayRegion == null)
					_SetIntArrayRegion = (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.SetIntArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr));
				return _SetIntArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr _SetLongArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr SetLongArrayRegion {
			get {
				if (_SetLongArrayRegion == null)
					_SetLongArrayRegion = (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.SetLongArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr));
				return _SetLongArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr _SetFloatArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr SetFloatArrayRegion {
			get {
				if (_SetFloatArrayRegion == null)
					_SetFloatArrayRegion = (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.SetFloatArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr));
				return _SetFloatArrayRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr _SetDoubleArrayRegion;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr SetDoubleArrayRegion {
			get {
				if (_SetDoubleArrayRegion == null)
					_SetDoubleArrayRegion = (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.SetDoubleArrayRegion, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr));
				return _SetDoubleArrayRegion;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_JniNativeMethodRegistrationArray_int_int _RegisterNatives;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_JniNativeMethodRegistrationArray_int_int RegisterNatives {
			get {
				if (_RegisterNatives == null)
					_RegisterNatives = (JniFunc_JniEnvironmentSafeHandle_IntPtr_JniNativeMethodRegistrationArray_int_int) Marshal.GetDelegateForFunctionPointer (env.RegisterNatives, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_JniNativeMethodRegistrationArray_int_int));
				return _RegisterNatives;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_int _UnregisterNatives;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_int UnregisterNatives {
			get {
				if (_UnregisterNatives == null)
					_UnregisterNatives = (JniFunc_JniEnvironmentSafeHandle_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.UnregisterNatives, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_int));
				return _UnregisterNatives;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_int _MonitorEnter;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_int MonitorEnter {
			get {
				if (_MonitorEnter == null)
					_MonitorEnter = (JniFunc_JniEnvironmentSafeHandle_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.MonitorEnter, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_int));
				return _MonitorEnter;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_int _MonitorExit;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_int MonitorExit {
			get {
				if (_MonitorExit == null)
					_MonitorExit = (JniFunc_JniEnvironmentSafeHandle_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.MonitorExit, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_int));
				return _MonitorExit;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_outIntPtr_int _GetJavaVM;
		public JniFunc_JniEnvironmentSafeHandle_outIntPtr_int GetJavaVM {
			get {
				if (_GetJavaVM == null)
					_GetJavaVM = (JniFunc_JniEnvironmentSafeHandle_outIntPtr_int) Marshal.GetDelegateForFunctionPointer (env.GetJavaVM, typeof (JniFunc_JniEnvironmentSafeHandle_outIntPtr_int));
				return _GetJavaVM;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr _GetStringRegion;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr GetStringRegion {
			get {
				if (_GetStringRegion == null)
					_GetStringRegion = (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetStringRegion, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr));
				return _GetStringRegion;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr _GetStringUTFRegion;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr GetStringUTFRegion {
			get {
				if (_GetStringUTFRegion == null)
					_GetStringUTFRegion = (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetStringUTFRegion, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_int_int_IntPtr));
				return _GetStringUTFRegion;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr _GetPrimitiveArrayCritical;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr GetPrimitiveArrayCritical {
			get {
				if (_GetPrimitiveArrayCritical == null)
					_GetPrimitiveArrayCritical = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetPrimitiveArrayCritical, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_IntPtr));
				return _GetPrimitiveArrayCritical;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int _ReleasePrimitiveArrayCritical;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int ReleasePrimitiveArrayCritical {
			get {
				if (_ReleasePrimitiveArrayCritical == null)
					_ReleasePrimitiveArrayCritical = (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int) Marshal.GetDelegateForFunctionPointer (env.ReleasePrimitiveArrayCritical, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_IntPtr_int));
				return _ReleasePrimitiveArrayCritical;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_string _GetStringCritical;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_string GetStringCritical {
			get {
				if (_GetStringCritical == null)
					_GetStringCritical = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_string) Marshal.GetDelegateForFunctionPointer (env.GetStringCritical, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr_string));
				return _GetStringCritical;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr_string _ReleaseStringCritical;
		public JniAction_JniEnvironmentSafeHandle_IntPtr_string ReleaseStringCritical {
			get {
				if (_ReleaseStringCritical == null)
					_ReleaseStringCritical = (JniAction_JniEnvironmentSafeHandle_IntPtr_string) Marshal.GetDelegateForFunctionPointer (env.ReleaseStringCritical, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr_string));
				return _ReleaseStringCritical;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject _NewWeakGlobalRef;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject NewWeakGlobalRef {
			get {
				if (_NewWeakGlobalRef == null)
					_NewWeakGlobalRef = (JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject) Marshal.GetDelegateForFunctionPointer (env.NewWeakGlobalRef, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_jobject));
				return _NewWeakGlobalRef;
			}
		}

		JniAction_JniEnvironmentSafeHandle_IntPtr _DeleteWeakGlobalRef;
		public JniAction_JniEnvironmentSafeHandle_IntPtr DeleteWeakGlobalRef {
			get {
				if (_DeleteWeakGlobalRef == null)
					_DeleteWeakGlobalRef = (JniAction_JniEnvironmentSafeHandle_IntPtr) Marshal.GetDelegateForFunctionPointer (env.DeleteWeakGlobalRef, typeof (JniAction_JniEnvironmentSafeHandle_IntPtr));
				return _DeleteWeakGlobalRef;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_bool _ExceptionCheck;
		public JniFunc_JniEnvironmentSafeHandle_bool ExceptionCheck {
			get {
				if (_ExceptionCheck == null)
					_ExceptionCheck = (JniFunc_JniEnvironmentSafeHandle_bool) Marshal.GetDelegateForFunctionPointer (env.ExceptionCheck, typeof (JniFunc_JniEnvironmentSafeHandle_bool));
				return _ExceptionCheck;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_long_jobject _NewDirectByteBuffer;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_long_jobject NewDirectByteBuffer {
			get {
				if (_NewDirectByteBuffer == null)
					_NewDirectByteBuffer = (JniFunc_JniEnvironmentSafeHandle_IntPtr_long_jobject) Marshal.GetDelegateForFunctionPointer (env.NewDirectByteBuffer, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_long_jobject));
				return _NewDirectByteBuffer;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr _GetDirectBufferAddress;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr GetDirectBufferAddress {
			get {
				if (_GetDirectBufferAddress == null)
					_GetDirectBufferAddress = (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr) Marshal.GetDelegateForFunctionPointer (env.GetDirectBufferAddress, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_IntPtr));
				return _GetDirectBufferAddress;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_long _GetDirectBufferCapacity;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_long GetDirectBufferCapacity {
			get {
				if (_GetDirectBufferCapacity == null)
					_GetDirectBufferCapacity = (JniFunc_JniEnvironmentSafeHandle_IntPtr_long) Marshal.GetDelegateForFunctionPointer (env.GetDirectBufferCapacity, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_long));
				return _GetDirectBufferCapacity;
			}
		}

		JniFunc_JniEnvironmentSafeHandle_IntPtr_JniObjectReferenceType _GetObjectRefType;
		public JniFunc_JniEnvironmentSafeHandle_IntPtr_JniObjectReferenceType GetObjectRefType {
			get {
				if (_GetObjectRefType == null)
					_GetObjectRefType = (JniFunc_JniEnvironmentSafeHandle_IntPtr_JniObjectReferenceType) Marshal.GetDelegateForFunctionPointer (env.GetObjectRefType, typeof (JniFunc_JniEnvironmentSafeHandle_IntPtr_JniObjectReferenceType));
				return _GetObjectRefType;
			}
		}
	}
}
#endif // FEATURE_HANDLES_ARE_INTPTRS
