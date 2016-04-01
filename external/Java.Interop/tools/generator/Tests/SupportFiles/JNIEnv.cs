#pragma warning disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;

namespace Android.Runtime {

	struct JnienvInitializeArgs {
		public IntPtr          javaVm;
		public IntPtr          env;
		public IntPtr          grefLoader;
		public IntPtr          Loader_loadClass;
		public uint            logCategories;
		public IntPtr          Class_getName;
		public int             version;
		public int             androidSdkVersion;
		public int             localRefsAreIndirect;
		public int             grefGcThreshold;
		public IntPtr          grefIGCUserPeer;
	}

	public static partial class JNIEnv {

		static IntPtr java_class_loader;
		static IntPtr java_vm;
		static IntPtr load_class_id;
		static JNIInvokeInterface invoke_iface;
		static int version;
		static int gref_gc_threshold;
		static int androidSdkVersion;

		static bool AllocObjectSupported;

		static IntPtr cid_System;
		static IntPtr mid_System_identityHashCode;
		internal static IntPtr mid_Class_getName;
		
		internal  static  bool  PropagateExceptions;

		[ThreadStatic] static IntPtr handle;
		[ThreadStatic] static JniNativeInterfaceInvoker env;

		static JniNativeInterfaceInvoker Env {
			get;
			set;
		}

		public static IntPtr Handle {
			get;
			set;
		}

		static void SetEnv ()
		{
			throw new NotImplementedException ();
		}

		public static void CheckHandle (IntPtr jnienv)
		{
			throw new NotImplementedException ();
		}

		[DllImport ("libc")]
		static extern int gettid ();

		delegate Delegate GetCallbackHandler ();

		static MethodInfo dynamic_callback_gen;

		static Delegate CreateDynamicCallback (MethodInfo method)
		{
			throw new NotImplementedException ();
		}

		static unsafe void RegisterJniNatives (IntPtr typeName_ptr, int typeName_len, IntPtr jniClass, IntPtr methods_ptr, int methods_len)
		{
			throw new NotImplementedException ();
		}

		internal static unsafe void Initialize (JnienvInitializeArgs* args)
		{
			throw new NotImplementedException ();
		}


		static volatile bool BridgeProcessing; // = false

		public static void WaitForBridgeProcessing ()
		{
			throw new NotImplementedException ();
		}


		internal static Func<IntPtr, IntPtr> IdentityHash;

		public static IntPtr AllocObject (string jniClassName)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr AllocObject (Type type)
		{
			throw new NotImplementedException ();
		}

		public static unsafe IntPtr StartCreateInstance (IntPtr jclass, IntPtr constructorId, JValue* constructorParameters)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr StartCreateInstance (IntPtr jclass, IntPtr constructorId, params JValue[] constructorParameters)
		{
			throw new NotImplementedException ();
		}

		public static unsafe void FinishCreateInstance (IntPtr instance, IntPtr jclass, IntPtr constructorId, JValue* constructorParameters)
		{
		}

		public static void FinishCreateInstance (IntPtr instance, IntPtr jclass, IntPtr constructorId, params JValue[] constructorParameters)
		{
			throw new NotImplementedException ();
		}

		public static unsafe IntPtr StartCreateInstance (Type type, string jniCtorSignature, JValue* constructorParameters)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr StartCreateInstance (Type type, string jniCtorSignature, params JValue[] constructorParameters)
		{
			throw new NotImplementedException ();
		}

		public static unsafe IntPtr StartCreateInstance (string jniClassName, string jniCtorSignature, JValue* constructorParameters)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr StartCreateInstance (string jniClassName, string jniCtorSignature, params JValue[] constructorParameters)
		{
			throw new NotImplementedException ();
		}

		public static unsafe void FinishCreateInstance (IntPtr instance, string jniCtorSignature, JValue* constructorParameters)
		{
			throw new NotImplementedException ();
		}

		public static void FinishCreateInstance (IntPtr instance, string jniCtorSignature, params JValue[] constructorParameters)
		{
			throw new NotImplementedException ();
		}

		public static void InvokeConstructor (IntPtr instance, string jniCtorSignature, params JValue[] constructorParameters)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr CreateInstance (IntPtr jniClass, string signature, params JValue[] constructorParameters)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr CreateInstance (string jniClassName, string signature, params JValue[] constructorParameters)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr CreateInstance (Type type, string signature, params JValue[] constructorParameters)
		{
			throw new NotImplementedException ();
		}

		static unsafe JniNativeInterfaceInvoker CreateNativeInterface ()
		{
			throw new NotImplementedException ();
		}

		public static IntPtr FindClass (System.Type type)
		{
			throw new NotImplementedException ();
		}

		static readonly int nameBufferLength = 1024;
		[ThreadStatic] static char[] nameBuffer;

		static unsafe IntPtr BinaryName (string classname)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr FindClass (string classname)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr FindClass (string className, ref IntPtr cachedJniClassHandle)
		{
			throw new NotImplementedException ();
		}

		public static void Throw (IntPtr obj)
		{
			throw new NotImplementedException ();
		}

		public static void ThrowNew (IntPtr clazz, string message)
		{
			throw new NotImplementedException ();
		}

		public static void PushLocalFrame (int capacity)
		{
			throw new NotImplementedException ();
		}

		public static void EnsureLocalCapacity (int capacity)
		{
			throw new NotImplementedException ();
		}

		internal static void DeleteRef (IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr NewGlobalRef (IntPtr jobject)
		{
			throw new NotImplementedException ();
		}

		public static void DeleteGlobalRef (IntPtr jobject)
		{
			throw new NotImplementedException ();
		}

		static IntPtr LogCreateLocalRef (IntPtr jobject)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr NewLocalRef (IntPtr jobject)
		{
			throw new NotImplementedException ();
		}

		public static void DeleteLocalRef (IntPtr jobject)
		{
			throw new NotImplementedException ();
		}

		public static void DeleteWeakGlobalRef (IntPtr jobject)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr NewObject (IntPtr jclass, IntPtr jmethod)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr NewObject (IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			throw new NotImplementedException ();
		}

		public static string GetClassNameFromInstance (IntPtr jobject)
		{
			throw new NotImplementedException ();
		}
		
		public static string GetJniName (Type type)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr ToJniHandle (IJavaObject value)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr ToLocalJniHandle (IJavaObject value)
		{
			throw new NotImplementedException ();
		}

		static JObjectRefType GetObjectRefType (IntPtr jobject)
		{
			throw new NotImplementedException ();
		}

		static byte _GetObjectRefType (IntPtr jobject)
		{
			throw new NotImplementedException ();
		}

		static IntPtr char_sequence_to_string_id;

		public static string GetCharSequence (IntPtr jobject, JniHandleOwnership transfer)
		{
			throw new NotImplementedException ();
		}

		public static unsafe string GetString (IntPtr value, JniHandleOwnership transfer)
		{
			throw new NotImplementedException ();
		}

		public static unsafe IntPtr NewString (string text)
		{
			throw new NotImplementedException ();
		}

		public static unsafe IntPtr NewString (char[] text, int length)
		{
			throw new NotImplementedException ();
		}

		static void AssertCompatibleArrayTypes (Type sourceType, IntPtr destArray)
		{
			throw new NotImplementedException ();
		}

		static void AssertCompatibleArrayTypes (IntPtr sourceArray, Type destType)
		{
			throw new NotImplementedException ();
		}

		public static void CopyArray (IntPtr src, bool[] dest)
		{
			throw new NotImplementedException ();
		}

		public static void CopyArray (IntPtr src, string[] dest)
		{
			throw new NotImplementedException ();
		}

		static Dictionary<Type, Func<Type, IntPtr, int, object>> NativeArrayElementToManaged {
			get;
			set;
		}

		static Dictionary<Type, Func<Type, IntPtr, int, object>> CreateNativeArrayElementToManaged ()
		{
			throw new NotImplementedException ();
		}

		static TValue GetConverter<TValue>(Dictionary<Type, TValue> dict, Type elementType, IntPtr array)
		{
			throw new NotImplementedException ();
		}

		public static void CopyArray (IntPtr src, Array dest, Type elementType = null)
		{
			throw new NotImplementedException ();
		}

		static void AssertIsJavaObject (Type targetType)
		{
			throw new NotImplementedException ();
		}

		public static void CopyArray<T> (IntPtr src, T[] dest)
		{
			throw new NotImplementedException ();
		}

		public static void CopyArray (bool[] src, IntPtr dest)
		{
			throw new NotImplementedException ();
		}

		public static void CopyArray (string[] src, IntPtr dest)
		{
			throw new NotImplementedException ();
		}

		public static void CopyArray (IJavaObject[] src, IntPtr dest)
		{
			throw new NotImplementedException ();
		}

		static Dictionary<Type, Action<Array, IntPtr>> CopyManagedToNativeArray {
			get;
			set;
		}

		static Dictionary<Type, Action<Array, IntPtr>> CreateCopyManagedToNativeArray ()
		{
			throw new NotImplementedException ();
		}

		public static void CopyArray (Array source, Type elementType, IntPtr dest)
		{
			throw new NotImplementedException ();
		}

		public static void CopyArray<T> (T[] src, IntPtr dest)
		{
			throw new NotImplementedException ();
		}

		public static Array GetArray (IntPtr array_ptr, JniHandleOwnership transfer, Type element_type = null)
		{
			throw new NotImplementedException ();
		}

		static Dictionary<Type, Func<Type, IntPtr, int, Array>> NativeArrayToManaged {
			get;
			set;
		}

		static Dictionary<Type, Func<Type, IntPtr, int, Array>> CreateNativeArrayToManaged ()
		{
			throw new NotImplementedException ();
		}

		static Array _GetArray (IntPtr array_ptr, Type element_type)
		{
			throw new NotImplementedException ();
		}

		public static object[] GetObjectArray (IntPtr array_ptr, Type[] element_types)
		{
			throw new NotImplementedException ();
		}

		public static T[] GetArray<T> (IntPtr array_ptr)
		{
			throw new NotImplementedException ();
		}

		public static T[] GetArray<T> (Java.Lang.Object[] array)
		{
			throw new NotImplementedException ();
		}

		public static T GetArrayItem<T> (IntPtr array_ptr, int index)
		{
			throw new NotImplementedException ();
		}

		public static int GetArrayLength (IntPtr array_ptr)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr NewArray (bool[] array)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr NewArray (string[] array)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr NewObjectArray (int length, IntPtr elementClass)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr NewObjectArray (int length, IntPtr elementClass, IntPtr initialElement)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr NewObjectArray<T>(params T[] values)
		{
			throw new NotImplementedException ();
		}

		static IntPtr GetArrayElementClass<T>(T[] values)
		{
			throw new NotImplementedException ();
		}

		public static void CopyObjectArray<T>(IntPtr source, T[] destination)
		{
			throw new NotImplementedException ();
		}

		public static void CopyObjectArray<T>(T[] source, IntPtr destination)
		{
			throw new NotImplementedException ();
		}

		public static IntPtr NewArray (IJavaObject[] array)
		{
			throw new NotImplementedException ();
		}

		static Dictionary<Type, Func<Array, IntPtr>> CreateManagedToNativeArray {
			get ;
			set;
		}

		static Dictionary<Type, Func<Array, IntPtr>> CreateCreateManagedToNativeArray ()
		{
				throw new NotImplementedException ();
		}

		public static IntPtr NewArray (Array value, Type elementType = null)
		{
				throw new NotImplementedException ();
		}

		public static IntPtr NewArray<T> (T[] array)
		{
				throw new NotImplementedException ();
		}

		static Dictionary<Type, Action<IntPtr, int, object>> SetNativeArrayElement {
			get;
			set;
		}

		static Dictionary<Type, Action<IntPtr, int, object>> CreateSetNativeArrayElement ()
		{
				throw new NotImplementedException ();
		}

		public static void SetArrayItem<T> (IntPtr array_ptr, int index, T value)
		{
				throw new NotImplementedException ();
		}

		public static Java.Lang.Object[] ToObjectArray<T> (T[] array)
		{
				throw new NotImplementedException ();
		}

		delegate int GetEnvDelegate (IntPtr javavm, out IntPtr envptr, int version);
		delegate int AttachCurrentThreadDelegate (IntPtr javavm, out IntPtr env, IntPtr args);
		delegate int DetachCurrentThreadDelegate (IntPtr javavm);

		struct JNIInvokeInterface {
			public IntPtr reserved0;
			public IntPtr reserved1;
			public IntPtr reserved2;
 
			public IntPtr DestroyJavaVM; // jint       (*DestroyJavaVM)(JavaVM*);
			public AttachCurrentThreadDelegate AttachCurrentThread;
			public DetachCurrentThreadDelegate DetachCurrentThread;
			public GetEnvDelegate GetEnv;
			public IntPtr AttachCurrentThreadAsDaemon; //jint        (*AttachCurrentThreadAsDaemon)(JavaVM*, JNIEnv**, void*);
		}

		internal struct JNINativeMethod {

			public string Name;
			public string Sig;
			public Delegate Func;

			public JNINativeMethod (string name, string sig, Delegate func)
			{
					throw new NotImplementedException ();
			}
		} 

#if ANDROID_8
		internal static int AndroidBitmap_getInfo (IntPtr jbitmap, out Android.Graphics.AndroidBitmapInfo info)
		{
			throw new NotImplementedException ();
		}

		internal static int AndroidBitmap_lockPixels (IntPtr jbitmap, out IntPtr addrPtr)
		{
			throw new NotImplementedException ();
		}

		internal static int AndroidBitmap_unlockPixels (IntPtr jbitmap)
		{
			throw new NotImplementedException ();
		}
#endif  // ANDROID_8
	}

	partial class JniNativeInterfaceInvoker {
	}
}
#pragma warning restore

