using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;

using Java.Interop;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Android.Runtime;
using RuntimeFeature = Microsoft.Android.Runtime.RuntimeFeature;

namespace Android.Runtime {
	public static partial class JNIEnv {
		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

		public static IntPtr Handle => JniEnvironment.EnvironmentPointer;

		static Array ArrayCreateInstance (Type elementType, int length)
		{
			if (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported) {
				return Array.CreateInstance (elementType, length);
			}

			return SafeArrayFactory.CreateInstance (elementType, rank: 1, length);
		}

		/// <safety>
		/// <paramref name="v"/> must be null or a live object reference from this VM, usable on this thread
		/// until System.identityHashCode returns or throws. The result is an integer hash encoded as IntPtr, not a JNI reference.
		/// </safety>
		internal static unsafe IntPtr IdentityHash (IntPtr v)
		{
			return JniEnvironment.References.GetIdentityHashCode (new JniObjectReference (v));
		}

		/// <safety>
		/// <paramref name="jnienv"/> must be a live JNI environment for the current thread and current runtime, with readable matching JNI table storage.
		/// It and its VM must remain usable through transition disposal, including if transition setup throws.
		/// </safety>
		public static unsafe void CheckHandle (IntPtr jnienv)
		{
			new JniTransition (jnienv).Dispose ();
		}

		/// <safety>
		/// A nonzero <paramref name="value"/> must be a live JNI object reference from this VM, retained
		/// on this thread until the type test returns or throws. The zero check does not validate other addresses.
		/// </safety>
		internal static unsafe bool IsGCUserPeer (IntPtr value)
		{
			if (value == IntPtr.Zero)
				return false;

			unsafe {
				// SAFETY: value is the borrowed peer passed by runtime marshaling; grefIGCUserPeer_class is the host's retained global class, used only for this type test.
				return IsInstanceOf (value, JNIEnvInit.grefIGCUserPeer_class);
			}
		}

		internal static bool ShouldWrapJavaException (Java.Lang.Throwable? t, [CallerMemberName] string? caller = null)
		{
			if (t == null) {
				Logger.Log (LogLevel.Warn,
				               "monodroid",
				               $"ShouldWrapJavaException was not passed a valid `Java.Lang.Throwable` instance. Called from method `{caller}`");
				return false;
			}

			bool wrap = JNIEnvInit.BoundExceptionType == BoundExceptionType.System;
			if (!wrap) {
				Logger.Log (
					LogLevel.Warn,
					"monodroid",
					$"Not wrapping exception of type {t.GetType().FullName} from method `{caller}`. This will change in a future release."
				);
			}

			return wrap;
		}

		/// <safety>
		/// <paramref name="env"/> must be this thread's live JNI environment and <paramref name="javaExceptionPtr"/>
		/// a borrowed Throwable reference from the same VM, retained through marshaling/reporting or failure.
		/// The thread argument is not dereferenced by this implementation; input reference ownership is not transferred.
		/// </safety>
		[DebuggerDisableUserUnhandledExceptions]
		internal static unsafe void PropagateUncaughtException (IntPtr env, IntPtr javaThreadPtr, IntPtr javaExceptionPtr)
		{
			unsafe {
				// SAFETY: the native uncaught-exception callback supplies the live current-thread environment and borrowed thread/throwable references for this transition.
				if (!JNIEnvInit.PropagateExceptions)
					return;

				var javaException = JavaObject.GetObject<Java.Lang.Throwable> (env, javaExceptionPtr, JniHandleOwnership.DoNotTransfer)!;

				Debugger.BreakForUserUnhandledException (javaException);

				try {
					var jltp = javaException as JavaProxyThrowable;
					Exception? innerException = jltp?.InnerException;

					Logger.Log (LogLevel.Info, "MonoDroid", "UNHANDLED EXCEPTION:");
					Logger.Log (LogLevel.Info, "MonoDroid", javaException.ToString ());

					ExceptionHandling.RaiseAppDomainUnhandledExceptionEvent (innerException ?? javaException);
				} catch (Exception e) {
					Logger.Log (LogLevel.Error, "monodroid", "Exception thrown while raising AppDomain.UnhandledException event: " + e.ToString ());
				}
			}
		}

		public static void WaitForBridgeProcessing ()
		{
			JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
		}

		/// <summary>
		/// Gets the number of completed CoreCLR or NativeAOT GC bridge processing cycles.
		/// </summary>
		public static int BridgeProcessingGeneration => JavaMarshalGCBridge.BridgeProcessingGeneration;

		public static IntPtr AllocObject (string jniClassName)
		{
			unsafe {
				// SAFETY: FindClass owns a global reference for jniClassName; AllocObject borrows it only until return or throw, and finally releases that owned class reference.
				IntPtr jniClass = JNIEnv.FindClass (jniClassName);
				try {
					return AllocObject (jniClass);
				}
				finally {
					JNIEnv.DeleteGlobalRef (jniClass);
				}
			}
		}

		public static IntPtr AllocObject (Type type)
		{
			unsafe {
				// SAFETY: FindClass owns a global reference for type; AllocObject borrows it only until return or throw, and finally releases that owned class reference.
				IntPtr jniClass = JNIEnv.FindClass (type);
				try {
					return AllocObject (jniClass);
				}
				finally {
					JNIEnv.DeleteGlobalRef (jniClass);
				}
			}
		}

		/// <safety>
		/// <paramref name="jclass"/> must be a live allocatable JNI class from this VM, usable on the current thread
		/// until allocation returns or throws. No constructor ID or argument is read in this phase.
		/// </safety>
		public static unsafe IntPtr StartCreateInstance (IntPtr jclass, IntPtr constructorId, JValue* constructorParameters)
		{
			unsafe {
				// SAFETY: only jclass is consumed by AllocObject; constructorId and constructorParameters are deliberately unused in this allocation-only phase.
				return AllocObject (jclass);
			}
		}

		/// <safety>
		/// <paramref name="jclass"/> must be a live allocatable JNI class from this VM, usable on the current thread
		/// until allocation returns or throws. No constructor ID or argument is read in this phase.
		/// </safety>
		public static unsafe IntPtr StartCreateInstance (IntPtr jclass, IntPtr constructorId, params JValue[] constructorParameters)
		{
			unsafe {
				// SAFETY: constructorParameters stays pinned while forwarding, but the allocation-only pointer overload never reads it or constructorId; only jclass must be live.
				fixed (JValue* cp = constructorParameters)
					return StartCreateInstance (jclass, constructorId, cp);
			}
		}

		/// <safety>
		/// <paramref name="instance"/> must be null where supported, or a live JNI object reference of the required type usable on the current thread for the call.
		/// <paramref name="jclass"/> must be null where supported, or a live JNI class reference of the required type usable on the current thread for the call.
		/// <paramref name="constructorId"/> must be a live instance constructor ID for the supplied class with the matching JNI argument signature and void return ABI.
		/// <paramref name="constructorParameters"/> must supply every constructor argument in the declared JNI order and types; all referenced objects must stay live.
		/// The pointer storage in <paramref name="constructorParameters"/> must remain readable for that argument count throughout the call.
		/// </safety>
		public static unsafe void FinishCreateInstance (IntPtr instance, IntPtr jclass, IntPtr constructorId, JValue* constructorParameters)
		{
			unsafe {
				// SAFETY: the caller supplies instance/jclass and the void constructor ABI; constructorParameters is forwarded without a copy or any extension of its lifetime.
				CallNonvirtualVoidMethod (instance, jclass, constructorId, constructorParameters);
			}
		}

		/// <safety>
		/// <paramref name="instance"/> must be null where supported, or a live JNI object reference of the required type usable on the current thread for the call.
		/// <paramref name="jclass"/> must be null where supported, or a live JNI class reference of the required type usable on the current thread for the call.
		/// <paramref name="constructorId"/> must be a live instance constructor ID for the supplied class with the matching JNI argument signature and void return ABI.
		/// <paramref name="constructorParameters"/> must supply every constructor argument in the declared JNI order and types; all referenced objects must stay live.
		/// </safety>
		public static unsafe void FinishCreateInstance (IntPtr instance, IntPtr jclass, IntPtr constructorId, params JValue[] constructorParameters)
		{
			unsafe {
				// SAFETY: fixed pins constructorParameters until FinishCreateInstance returns or throws; the caller supplies its count, constructor ABI and referenced-object lifetimes.
				fixed (JValue* cp = constructorParameters)
					FinishCreateInstance (instance, jclass, constructorId, cp);
			}
		}

		public static IntPtr StartCreateInstance (Type type, string jniCtorSignature, JValue* constructorParameters)
		{
			return AllocObject (type);
		}

		public static IntPtr StartCreateInstance (Type type, string jniCtorSignature, params JValue[] constructorParameters)
		{
			unsafe {
				// SAFETY: this allocation-only overload never reads constructor arguments; the pin preserves legacy pointer-signature compatibility.
				fixed (JValue* cp = constructorParameters)
					return StartCreateInstance (type, jniCtorSignature, cp);
			}
		}

		public static IntPtr StartCreateInstance (string jniClassName, string jniCtorSignature, JValue* constructorParameters)
		{
			return AllocObject (jniClassName);
		}

		public static IntPtr StartCreateInstance (string jniClassName, string jniCtorSignature, params JValue[] constructorParameters)
		{
			unsafe {
				// SAFETY: this allocation-only overload never reads constructor arguments; the pin preserves legacy pointer-signature compatibility.
				fixed (JValue* cp = constructorParameters)
					return StartCreateInstance (jniClassName, jniCtorSignature, cp);
			}
		}

		/// <safety>
		/// <paramref name="instance"/> must be null where supported, or a live JNI object reference of the required type usable on the current thread for the call.
		/// <paramref name="jniCtorSignature"/> must describe the supplied arguments and a void-returning instance constructor of the requested class.
		/// <paramref name="constructorParameters"/> must supply every constructor argument in the declared JNI order and types; all referenced objects must stay live.
		/// The pointer storage in <paramref name="constructorParameters"/> must remain readable for that argument count throughout the call.
		/// </safety>
		public static unsafe void FinishCreateInstance (IntPtr instance, string jniCtorSignature, JValue* constructorParameters)
		{
			unsafe {
				// SAFETY: InvokeConstructor consumes the caller's live instance and matching constructor arguments synchronously; forwarding neither validates nor retains the raw storage.
				InvokeConstructor (instance, jniCtorSignature, constructorParameters);
			}
		}

		/// <safety>
		/// <paramref name="instance"/> must be null where supported, or a live JNI object reference of the required type usable on the current thread for the call.
		/// <paramref name="jniCtorSignature"/> must describe the supplied arguments and a void-returning instance constructor of the requested class.
		/// <paramref name="constructorParameters"/> must supply every constructor argument in the declared JNI order and types; all referenced objects must stay live.
		/// </safety>
		public static unsafe void FinishCreateInstance (IntPtr instance, string jniCtorSignature, params JValue[] constructorParameters)
		{
			unsafe {
				// SAFETY: fixed pins constructorParameters through InvokeConstructor; the caller retains instance and all referenced arguments with the declared constructor ABI.
				fixed (JValue* cp = constructorParameters)
					FinishCreateInstance (instance, jniCtorSignature, cp);
			}
		}

		/// <safety>
		/// <paramref name="instance"/> must be null where supported, or a live JNI object reference of the required type usable on the current thread for the call.
		/// <paramref name="jniCtorSignature"/> must describe the supplied arguments and a void-returning instance constructor of the requested class.
		/// <paramref name="constructorParameters"/> must supply every constructor argument in the declared JNI order and types; all referenced objects must stay live.
		/// The pointer storage in <paramref name="constructorParameters"/> must remain readable for that argument count throughout the call.
		/// </safety>
		public static unsafe void InvokeConstructor (IntPtr instance, string jniCtorSignature, JValue* constructorParameters)
		{
			unsafe {
				// SAFETY: the caller retains instance and its constructor arguments; GetObjectClass supplies the owned class used for lookup and released in finally.
				IntPtr lrefClass = GetObjectClass (instance);
				try {
					IntPtr ctor = JNIEnv.GetMethodID (lrefClass, "<init>", jniCtorSignature);
					if (ctor == IntPtr.Zero)
						throw new ArgumentException (FormattableString.Invariant (
							$"Could not find constructor JNI signature '{jniCtorSignature}' on type '{JniEnvironment.Types.GetJniTypeNameFromClass (new JniObjectReference (lrefClass))}'."));
					CallNonvirtualVoidMethod (instance, lrefClass, ctor, constructorParameters);
				} finally {
					DeleteLocalRef (lrefClass);
				}
			}
		}

		/// <safety>
		/// <paramref name="instance"/> must be null where supported, or a live JNI object reference of the required type usable on the current thread for the call.
		/// <paramref name="jniCtorSignature"/> must describe the supplied arguments and a void-returning instance constructor of the requested class.
		/// <paramref name="constructorParameters"/> must supply every constructor argument in the declared JNI order and types; all referenced objects must stay live.
		/// </safety>
		public static unsafe void InvokeConstructor (IntPtr instance, string jniCtorSignature, params JValue[] constructorParameters)
		{
			unsafe {
				// SAFETY: constructorParameters remains pinned until InvokeConstructor returns or throws; its entries and instance are the caller's matching constructor inputs.
				fixed (JValue* cp = constructorParameters)
					InvokeConstructor (instance, jniCtorSignature, cp);
			}
		}

		/// <safety>
		/// <paramref name="jniClass"/> must be null where supported, or a live JNI class reference of the required type usable on the current thread for the call.
		/// <paramref name="signature"/> must describe the supplied arguments and a void-returning instance constructor of the requested class.
		/// <paramref name="constructorParameters"/> must supply every constructor argument in the declared JNI order and types; all referenced objects must stay live.
		/// The pointer storage in <paramref name="constructorParameters"/> must remain readable for that argument count throughout the call.
		/// </safety>
		public static unsafe IntPtr CreateInstance (IntPtr jniClass, string signature, JValue* constructorParameters)
		{
			unsafe {
				// SAFETY: the caller retains jniClass; GetMethodID resolves <init> with signature, and NewObject consumes the caller's matching argument count/storage synchronously.
				IntPtr ctor = JNIEnv.GetMethodID (jniClass, "<init>", signature);
				if (ctor == IntPtr.Zero)
					throw new ArgumentException (FormattableString.Invariant (
						$"Could not find constructor JNI signature '{signature}' on type '{JniEnvironment.Types.GetJniTypeNameFromClass (new JniObjectReference (jniClass))}'."));
				return JNIEnv.NewObject (jniClass, ctor, constructorParameters);
			}
		}

		/// <safety>
		/// <paramref name="jniClass"/> must be null where supported, or a live JNI class reference of the required type usable on the current thread for the call.
		/// <paramref name="signature"/> must describe the supplied arguments and a void-returning instance constructor of the requested class.
		/// <paramref name="constructorParameters"/> must supply every constructor argument in the declared JNI order and types; all referenced objects must stay live.
		/// </safety>
		public static unsafe IntPtr CreateInstance (IntPtr jniClass, string signature, params JValue[] constructorParameters)
		{
			unsafe {
				// SAFETY: fixed pins constructorParameters through CreateInstance; jniClass, signature and referenced argument lifetimes remain the caller's contract.
				fixed (JValue* cp = constructorParameters)
					return CreateInstance (jniClass, signature, cp);
			}
		}

		/// <safety>
		/// <paramref name="signature"/> must describe the supplied arguments and a void-returning instance constructor of the requested class.
		/// <paramref name="constructorParameters"/> must supply every constructor argument in the declared JNI order and types; all referenced objects must stay live.
		/// The pointer storage in <paramref name="constructorParameters"/> must remain readable for that argument count throughout the call.
		/// </safety>
		public static unsafe IntPtr CreateInstance (string jniClassName, string signature, JValue* constructorParameters)
		{
			unsafe {
				// SAFETY: FindClass supplies an owned global class released in finally; the caller retains the matching constructor argument storage until NewObject returns or throws.
				IntPtr jniClass = JNIEnv.FindClass (jniClassName);
				try {
					return CreateInstance (jniClass, signature, constructorParameters);
				}
				finally {
					JNIEnv.DeleteGlobalRef (jniClass);
				}
			}
		}

		/// <safety>
		/// <paramref name="signature"/> must describe the supplied arguments and a void-returning instance constructor of the requested class.
		/// <paramref name="constructorParameters"/> must supply every constructor argument in the declared JNI order and types; all referenced objects must stay live.
		/// </safety>
		public static unsafe IntPtr CreateInstance (string jniClassName, string signature, params JValue[] constructorParameters)
		{
			unsafe {
				// SAFETY: fixed pins constructorParameters while forwarding the caller's signature and arguments; FindClass owns the class used by the pointer overload.
				fixed (JValue* cp = constructorParameters)
					return CreateInstance (jniClassName, signature, cp);
			}
		}

		/// <safety>
		/// <paramref name="signature"/> must describe the supplied arguments and a void-returning instance constructor of the requested class.
		/// <paramref name="constructorParameters"/> must supply every constructor argument in the declared JNI order and types; all referenced objects must stay live.
		/// The pointer storage in <paramref name="constructorParameters"/> must remain readable for that argument count throughout the call.
		/// </safety>
		public static unsafe IntPtr CreateInstance (Type type, string signature, JValue* constructorParameters)
		{
			unsafe {
				// SAFETY: FindClass supplies type's owned global class released in finally; the caller retains the argument buffer and references for the matching constructor ABI.
				IntPtr jniClass = JNIEnv.FindClass (type);
				try {
					return CreateInstance (jniClass, signature, constructorParameters);
				}
				finally {
					JNIEnv.DeleteGlobalRef (jniClass);
				}
			}
		}

		/// <safety>
		/// <paramref name="signature"/> must describe the supplied arguments and a void-returning instance constructor of the requested class.
		/// <paramref name="constructorParameters"/> must supply every constructor argument in the declared JNI order and types; all referenced objects must stay live.
		/// </safety>
		public static unsafe IntPtr CreateInstance (Type type, string signature, params JValue[] constructorParameters)
		{
			unsafe {
				// SAFETY: fixed pins constructorParameters while forwarding the caller's signature and arguments; the pointer overload owns its FindClass result.
				fixed (JValue* cp = constructorParameters)
					return CreateInstance (type, signature, cp);
			}
		}

		public static IntPtr FindClass (System.Type type)
		{
			int rank = JavaNativeTypeManager.GetArrayInfo (type, out type);
			try {
				var sig  = JNIEnvInit.androidRuntime?.TypeManager.GetTypeSignature (type) ?? default;
				if (!sig.IsValid || sig.SimpleReference == null) {
					sig = new JniTypeSignature ("java/lang/Object");
				}
				sig = sig.AddArrayRank (rank);

				return FindClass (sig.Name);
			} catch (Java.Lang.Throwable e) {
				if (!((e is Java.Lang.NoClassDefFoundError) || (e is Java.Lang.ClassNotFoundException)))
					throw;
				Logger.Log (LogLevel.Warn, "monodroid", $"JNIEnv.FindClass(Type) caught unexpected exception: {e}");
				var jni = JavaNativeTypeManager.ToJniNameFromAttributes (type);
				if (jni != null) {
					e.Dispose ();
					return FindClass (JavaNativeTypeManager.ToJniName (jni, rank));
				}
				throw;
			}
		}

		public static IntPtr FindClass (string classname)
		{
			unsafe {
				// SAFETY: FindClass returns this thread's local class; NewGlobalRef duplicates that live reference before the local reference is disposed.
				JniObjectReference local_ref = JniEnvironment.Types.FindClass (classname);
				IntPtr global_ref = NewGlobalRef (local_ref.Handle);
				JniObjectReference.Dispose (ref local_ref);
				return global_ref;
			}
		}

		/// <safety>
		/// <paramref name="classname"/> must have readable NUL-terminated UTF-8 backing storage for the call.
		/// Complete C# UTF-8 literals provide a terminator immediately beyond the span length.
		/// Do not slice away the terminator, mutate the name, or release its storage until lookup returns or throws.
		/// </safety>
		static unsafe IntPtr FindClass (ReadOnlySpan<byte> classname)
		{
			unsafe {
				// SAFETY: the caller supplies NUL-terminated UTF-8 classname storage; the found local class stays live while its global reference is created.
				JniObjectReference local_ref = JniEnvironment.Types.FindClass (classname);
				IntPtr global_ref = NewGlobalRef (local_ref.Handle);
				JniObjectReference.Dispose (ref local_ref);
				return global_ref;
			}
		}

		public static IntPtr FindClass (string className, ref IntPtr cachedJniClassHandle)
		{
			unsafe {
				// SAFETY: h is the global reference allocated by FindClass; only a losing h is deleted after CompareExchange, never the cached winning reference.
				if (cachedJniClassHandle != IntPtr.Zero)
					return cachedJniClassHandle;
				IntPtr h = FindClass (className);
				if (Interlocked.CompareExchange (ref cachedJniClassHandle, h, IntPtr.Zero) != IntPtr.Zero)
					DeleteGlobalRef (h);
				return cachedJniClassHandle;
			}
		}

		/// <safety>
		/// A nonzero <paramref name="obj"/> must be a live JNI Throwable reference from this VM on the current
		/// thread, retained until JNI returns or throws. Zero is rejected before the JNI call.
		/// </safety>
		public static unsafe void Throw (IntPtr obj)
		{
			if (obj == IntPtr.Zero)
				throw new ArgumentException ("'obj' must not be IntPtr.Zero.", "obj");

			JniEnvironment.Exceptions.Throw (new JniObjectReference (obj));
		}

		/// <safety>
		/// <paramref name="clazz"/> must be a live class reference for Throwable or a subclass with a String
		/// constructor, usable on this thread until JNI returns or throws. Retain the class during exception creation.
		/// </safety>
		public static unsafe void ThrowNew (IntPtr clazz, string message)
		{
			if (message == null)
				throw new ArgumentNullException ("message");

			JniEnvironment.Exceptions.ThrowNew (new JniObjectReference (clazz), message);
		}

		public static void PushLocalFrame (int capacity)
		{
			JniEnvironment.References.PushLocalFrame (capacity);
		}

		public static void EnsureLocalCapacity (int capacity)
		{
			JniEnvironment.References.EnsureLocalCapacity (capacity);
		}

		/// <safety>
		/// For a transfer, <paramref name="handle"/> must be null or an owned live reference of the indicated
		/// local/global kind; local references belong to this thread. Exclude concurrent release or use of aliases,
		/// and do not reuse a consumed handle even if later conversion fails. DoNotTransfer does not access it.
		/// </safety>
		internal static unsafe void DeleteRef (IntPtr handle, JniHandleOwnership transfer)
		{
			unsafe {
				// SAFETY: transfer comes from the raw marshaling boundary and identifies the caller-owned local/global reference to release; DoNotTransfer performs no native access.
				switch (transfer & (JniHandleOwnership.TransferLocalRef | JniHandleOwnership.TransferGlobalRef)) {
				case JniHandleOwnership.DoNotTransfer:
					break;
				case JniHandleOwnership.TransferLocalRef:
					JNIEnv.DeleteLocalRef (handle);
					break;
				case JniHandleOwnership.TransferGlobalRef:
					JNIEnv.DeleteGlobalRef (handle);
					break;
				}
			}
		}

		/// <summary>
		/// Converts the <see cref="JniHandleOwnership"/> given to an
		/// <c>(IntPtr, JniHandleOwnership)</c> activation constructor into the
		/// <see cref="JniObjectReferenceOptions"/> that the Java.Interop-style
		/// <c>(ref JniObjectReference, JniObjectReferenceOptions)</c> constructor expects.
		/// </summary>
		/// <remarks>
		/// Only <see cref="JniHandleOwnership.DoNotRegister"/> survives the conversion. The
		/// handle-transfer bits intentionally do not: the caller keeps the incoming handle and
		/// releases it with <see cref="DeleteRef"/> in a finally, even if construction throws, so the peer must
		/// always take a reference of its own — which is why
		/// <see cref="JniObjectReferenceOptions.Copy"/> is always set.
		/// </remarks>
		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		internal static JniObjectReferenceOptions ToJniObjectReferenceOptions (JniHandleOwnership transfer)
		{
			return (transfer & JniHandleOwnership.DoNotRegister) != 0
				? JniObjectReferenceOptions.CopyAndDoNotRegister
				: JniObjectReferenceOptions.Copy;
		}

		/// <safety>
		/// <paramref name="jobject"/> must be null where supported, or a live JNI object reference of the required type usable on the current thread for the call.
		/// </safety>
		public static unsafe IntPtr NewGlobalRef (IntPtr jobject)
		{
			var r = new JniObjectReference (jobject);
			return r.NewGlobalRef ().Handle;
		}

		/// <safety>
		/// <paramref name="jobject"/> must be null or a live JNI global reference whose release the caller owns.
		/// Exclude concurrent release/use through aliases during deletion and stop using this reference afterward;
		/// an exception does not prove the handle is still valid. Deleting it does not delete other JNI references to the same object.
		/// </safety>
		public static unsafe void DeleteGlobalRef (IntPtr jobject)
		{
			var r = new JniObjectReference (jobject, JniObjectReferenceType.Global);
			JniObjectReference.Dispose (ref r);
		}

		/// <safety>
		/// <paramref name="jobject"/> must be null where supported, or a live JNI object reference of the required type usable on the current thread for the call.
		/// </safety>
		public static unsafe IntPtr NewLocalRef (IntPtr jobject)
		{
			return new JniObjectReference (jobject).NewLocalRef ().Handle;
		}

		/// <safety>
		/// <paramref name="jobject"/> must be null or a live JNI local reference owned by the current thread and caller.
		/// Exclude alias access while deleting and do not reuse this local handle afterward, including on failure.
		/// Other references to the same Java object retain their independent lifetimes.
		/// </safety>
		public static unsafe void DeleteLocalRef (IntPtr jobject)
		{
			var r = new JniObjectReference (jobject, JniObjectReferenceType.Local);
			JniObjectReference.Dispose (ref r);
		}

		/// <safety>
		/// <paramref name="jobject"/> must be null or a live JNI weak-global reference whose release the caller owns.
		/// Exclude concurrent release/use of aliases and do not reuse this weak handle afterward, including on failure.
		/// </safety>
		public static unsafe void DeleteWeakGlobalRef (IntPtr jobject)
		{
			var r = new JniObjectReference (jobject, JniObjectReferenceType.WeakGlobal);
			JniObjectReference.Dispose (ref r);
		}

		/// <safety>
		/// <paramref name="jclass"/> must be a live JNI class reference and <paramref name="jmethod"/>
		/// its live instance constructor ID with the exact zero-argument <c>()V</c> JNI signature.
		/// Both must remain usable on the current thread for the call.
		/// </safety>
		public static unsafe IntPtr NewObject (IntPtr jclass, IntPtr jmethod)
		{
			unsafe {
				// SAFETY: the caller supplies the live class and a constructor ID with the zero-argument constructor ABI.
				var r = JniEnvironment.Object.NewObject (new JniObjectReference (jclass), new JniMethodInfo (jmethod, isStatic: false));
				return r.Handle;
			}
		}

		/// <safety>
		/// <paramref name="jclass"/> must be null where supported, or a live JNI class reference of the required type usable on the current thread for the call.
		/// <paramref name="jmethod"/> must be a live instance constructor ID for the supplied class with the matching JNI argument signature and void return ABI.
		/// <paramref name="parms"/> must supply every constructor argument in the declared JNI order and types; all referenced objects must stay live.
		/// The pointer storage in <paramref name="parms"/> must remain readable for that argument count throughout the call.
		/// </safety>
		public static unsafe IntPtr NewObject (IntPtr jclass, IntPtr jmethod, JValue* parms)
		{
			unsafe {
				// SAFETY: jclass/jmethod and the matching argument buffer are caller-owned for this call; JValue and JniArgumentValue use the same JNI union layout.
				var r = JniEnvironment.Object.NewObject (new JniObjectReference (jclass), new JniMethodInfo (jmethod, isStatic: false), (JniArgumentValue*) parms);
				return r.Handle;
			}
		}

		/// <safety>
		/// <paramref name="jclass"/> must be null where supported, or a live JNI class reference of the required type usable on the current thread for the call.
		/// <paramref name="jmethod"/> must be a live instance constructor ID for the supplied class with the matching JNI argument signature and void return ABI.
		/// <paramref name="parms"/> must supply every constructor argument in the declared JNI order and types; all referenced objects must stay live.
		/// </safety>
		public static unsafe IntPtr NewObject (IntPtr jclass, IntPtr jmethod, params JValue[] parms)
		{
			unsafe {
				// SAFETY: fixed pins parms for NewObject; the caller supplies the constructor's complete argument count/types and retains all referenced objects.
				fixed (JValue* p = parms)
					return NewObject (jclass, jmethod, p);
			}
		}

		/// <safety>
		/// <paramref name="jobject"/> must be null where supported, or a live JNI object reference of the required type usable on the current thread for the call.
		/// </safety>
		public static unsafe string? GetClassNameFromInstance (IntPtr jobject)
		{
			return JniEnvironment.Types.GetJniTypeNameFromInstance (new JniObjectReference (jobject));
		}

		internal static string? TypemapManagedToJava (Type type)
		{
			return TrimmableTypeMap.Instance.TryGetJniNameForManagedType (type, out var jniName) ? jniName : null;
		}

		public static string GetJniName (Type type)
		{
			if (type == null)
				throw new ArgumentNullException ("type");

			JniTypeSignature sig = JNIEnvInit.androidRuntime?.TypeManager.GetTypeSignature (type) ?? default;

			return sig.SimpleReference == null
				? JavaNativeTypeManager.ToJniName (type)
				: sig.Name;
		}

		public static IntPtr ToJniHandle (IJavaObject? value)
		{
			if (value == null)
				return IntPtr.Zero;
			return value.Handle;
		}

		public static IntPtr ToLocalJniHandle (IJavaObject? value)
		{
			if (value == null)
				return IntPtr.Zero;
			var ex = value as IJavaObjectEx;
			if (ex != null) {
				IntPtr result = ex.ToLocalJniHandle ();
				GC.KeepAlive (value);
				return result;
			}
			return ToLocalJniHandleFallback (value);
		}

		internal static IntPtr ToLocalJniHandleFallback (IJavaObject value)
		{
			unsafe {
				// SAFETY: value.Handle is duplicated while value remains reachable; GC.KeepAlive extends that managed peer's lifetime past NewLocalRef.
				IntPtr result = NewLocalRef (value.Handle);
				GC.KeepAlive (value);
				return result;
			}
		}

		/// <safety>
		/// <paramref name="jobject"/> must be null where supported, or a live JNI object reference of the required type usable on the current thread for the call.
		/// This implementation does not consume <paramref name="transfer"/> or release the input handle.
		/// Retain it through Object.toString and copying the returned String, including exception unwinding.
		/// </safety>
		public static unsafe string? GetCharSequence (IntPtr jobject, JniHandleOwnership transfer)
		{
			if (jobject == IntPtr.Zero)
				return null;

			var r = JniEnvironment.Object.ToString (new JniObjectReference (jobject));
			return JniEnvironment.Strings.ToString (ref r, JniObjectReferenceOptions.CopyAndDispose);
		}

		/// <safety>
		/// <paramref name="value"/> must be null where supported, or a live JNI string reference of the required type usable on the current thread for the call.
		/// <paramref name="transfer"/> must describe ownership the caller holds. On successful conversion the requested
		/// reference is released; do not access aliases during or after release. A conversion exception occurs before that release.
		/// </safety>
		public static unsafe string? GetString (IntPtr value, JniHandleOwnership transfer)
		{
			if (value == IntPtr.Zero)
				return null;

			unsafe {
				// SAFETY: the caller retains value as a JNI string until conversion completes; DeleteRef releases the requested ownership only after conversion succeeds.
				var s = JniEnvironment.Strings.ToString (new JniObjectReference (value));
				DeleteRef (value, transfer);
				return s;
			}
		}

		public static IntPtr NewString (string? text)
		{
			if (text == null)
				return IntPtr.Zero;

			return JniEnvironment.Strings.NewString (text).Handle;
		}

		public static IntPtr NewString (char[]? text, int length)
		{
			if (text == null)
				return IntPtr.Zero;

			if (length < 0 || length > text.Length)
				throw new ArgumentOutOfRangeException (nameof (length));
			if (length == 0)
				return JniEnvironment.Strings.NewString ("").Handle;

			unsafe {
				// SAFETY: length is checked against the managed array and the UTF-16 buffer remains pinned for the call.
				fixed (char *s = text)
					return JniEnvironment.Strings.NewString (s, length).Handle;
			}
		}

		static void AssertCompatibleArrayTypes (Type srcElementType, IntPtr destArray)
		{
			JniObjectReference sourceClass = FindArrayClassByElementType (srcElementType);
			JniObjectReference destClass   = JniEnvironment.Types.GetObjectClass (new JniObjectReference (destArray));
			try {
				if (!JniEnvironment.Types.IsAssignableFrom (sourceClass, destClass)) {
					throw new InvalidCastException (FormattableString.Invariant (
						$"Unable to cast from '{JniEnvironment.Types.GetJniTypeNameFromClass (sourceClass)}' to '{JniEnvironment.Types.GetJniTypeNameFromClass (destClass)}'."));
				}
			} finally {
				JniObjectReference.Dispose (ref sourceClass);
				JniObjectReference.Dispose (ref destClass);
			}
		}

		static void AssertCompatibleArrayTypes (IntPtr sourceArray, Type destElementType)
		{
			JniObjectReference destClass = FindArrayClassByElementType (destElementType);
			try {
				var sourceReference = new JniObjectReference (sourceArray);
				if (JniEnvironment.Types.IsInstanceOf (sourceReference, destClass))
					return;

				JniObjectReference sourceClass = JniEnvironment.Types.GetObjectClass (sourceReference);
				try {
					throw new InvalidCastException (FormattableString.Invariant (
						$"Unable to cast from '{JniEnvironment.Types.GetJniTypeNameFromClass (sourceClass)}' to '{JniEnvironment.Types.GetJniTypeNameFromClass (destClass)}'."));
				} finally {
					JniObjectReference.Dispose (ref sourceClass);
				}
			} finally {
				JniObjectReference.Dispose (ref destClass);
			}
		}

		static JniObjectReference FindArrayClassByElementType (Type elementType)
		{
			var builtInArrayJniClassName = GetBuiltInArrayJniClassNameUtf8 (elementType);
			if (!builtInArrayJniClassName.IsEmpty) {
				unsafe {
					// SAFETY: GetBuiltInArrayJniClassNameUtf8 returns only complete, process-lifetime UTF-8 literals, never slices or copies.
					return JniEnvironment.Types.FindClass (builtInArrayJniClassName);
				}
			}

			int rank = JavaNativeTypeManager.GetArrayInfo (elementType, out elementType) + 1;
			var typeSignature = JniRuntime.CurrentRuntime.TypeManager.GetTypeSignature (elementType).AddArrayRank (rank);
			return JniEnvironment.Types.FindClass (typeSignature.Name);
		}

		static ReadOnlySpan<byte> GetBuiltInArrayJniClassNameUtf8 (Type type)
		{
			// Formatting JniTypeSignature.Name allocates for array ranks; use static UTF-8 data for hot built-in cases.
			if (type.IsEnum)
				type = Enum.GetUnderlyingType (type);

			if (type == typeof (bool))    return "[Z"u8;
			if (type == typeof (byte))    return "[B"u8;
			if (type == typeof (sbyte))   return "[B"u8;
			if (type == typeof (char))    return "[C"u8;
			if (type == typeof (short))   return "[S"u8;
			if (type == typeof (ushort))  return "[S"u8;
			if (type == typeof (int))     return "[I"u8;
			if (type == typeof (uint))    return "[I"u8;
			if (type == typeof (long))    return "[J"u8;
			if (type == typeof (ulong))   return "[J"u8;
			if (type == typeof (float))   return "[F"u8;
			if (type == typeof (double))  return "[D"u8;
			if (type == typeof (bool?))   return "[Ljava/lang/Boolean;"u8;
			if (type == typeof (byte?))   return "[Ljava/lang/Byte;"u8;
			if (type == typeof (sbyte?))  return "[Ljava/lang/Byte;"u8;
			if (type == typeof (char?))   return "[Ljava/lang/Character;"u8;
			if (type == typeof (short?))  return "[Ljava/lang/Short;"u8;
			if (type == typeof (int?))    return "[Ljava/lang/Integer;"u8;
			if (type == typeof (long?))   return "[Ljava/lang/Long;"u8;
			if (type == typeof (float?))  return "[Ljava/lang/Float;"u8;
			if (type == typeof (double?)) return "[Ljava/lang/Double;"u8;
			return [];
		}

		static ReadOnlySpan<byte> GetBoxedPrimitiveJniClassNameUtf8 (Type type)
		{
			if (type == typeof (bool?))   return "java/lang/Boolean"u8;
			if (type == typeof (byte?))   return "java/lang/Byte"u8;
			if (type == typeof (sbyte?))  return "java/lang/Byte"u8;
			if (type == typeof (char?))   return "java/lang/Character"u8;
			if (type == typeof (short?))  return "java/lang/Short"u8;
			if (type == typeof (int?))    return "java/lang/Integer"u8;
			if (type == typeof (long?))   return "java/lang/Long"u8;
			if (type == typeof (float?))  return "java/lang/Float"u8;
			if (type == typeof (double?)) return "java/lang/Double"u8;
			return [];
		}

		/// <safety>
		/// <paramref name="src"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The JNI array element type must match the requested managed conversion and all requested element ranges must lie within the live array.
		/// </safety>
		public static unsafe void CopyArray (IntPtr src, bool[] dest)
		{
			if (dest == null)
				throw new ArgumentNullException ("dest");

			unsafe {
				// SAFETY: the caller retains a Boolean array with dest.Length readable elements for the checked copy.
				AssertCompatibleArrayTypes (src, destElementType: typeof (bool));

				_GetBooleanArrayRegion (src, 0, dest.Length, dest);
			}
		}

		/// <safety>
		/// <paramref name="src"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The JNI array element type must match the requested managed conversion and all requested element ranges must lie within the live array.
		/// </safety>
		public static unsafe void CopyArray (IntPtr src, string[] dest)
		{
			if (dest == null)
				throw new ArgumentNullException ("dest");

			unsafe {
				// SAFETY: src is the caller-retained String-compatible array; each acquired local transfers to GetString.
				for (int i = 0; i < dest.Length; i++)
					dest [i] = GetString (GetObjectArrayElement (src, i), JniHandleOwnership.TransferLocalRef)!;
			}
		}

		static Dictionary<Type, Func<Type?, IntPtr, int, object?>>? nativeArrayElementToManaged;
		static Dictionary<Type, Func<Type?, IntPtr, int, object?>> NativeArrayElementToManaged {
			get {
				if (nativeArrayElementToManaged != null)
					return nativeArrayElementToManaged;

				var newValue = CreateNativeArrayElementToManaged ();
				Interlocked.CompareExchange (ref nativeArrayElementToManaged, newValue, null);
				return nativeArrayElementToManaged;
			}
		}

		static Dictionary<Type, Func<Type?, IntPtr, int, object?>> CreateNativeArrayElementToManaged ()
		{
			return new Dictionary<Type, Func<Type?, IntPtr, int, object?>> () {
				{ typeof (bool), (type, source, index) => {
					var r = new bool [1];
					unsafe {
						// SAFETY: source/index identify one live element; r has exactly one destination slot.
						_GetBooleanArrayRegion (source, index, 1, r);
					}
					return r [0];
				} },
				{ typeof (byte), (type, source, index) => {
					var r = new byte [1];
					unsafe {
						// SAFETY: source/index identify one live element; r has exactly one destination slot.
						_GetByteArrayRegion (source, index, 1, r);
					}
					return r [0];
				} },
				{ typeof (sbyte), (type, source, index) => {
					var r = new sbyte [1];
					unsafe {
						// SAFETY: source/index identify one live element; r has exactly one destination slot.
						_GetByteArrayRegion (source, index, 1, (byte[]) (object) r);
					}
					return r [0];
				} },
				{ typeof (char), (type, source, index) => {
					var r = new char [1];
					unsafe {
						// SAFETY: source/index identify one live element; r has exactly one destination slot.
						_GetCharArrayRegion (source, index, 1, r);
					}
					return r [0];
				} },
				{ typeof (short), (type, source, index) => {
					var r = new short [1];
					unsafe {
						// SAFETY: source/index identify one live element; r has exactly one destination slot.
						_GetShortArrayRegion (source, index, 1, r);
					}
					return r [0];
				} },
				{ typeof (int), (type, source, index) => {
					var r = new int [1];
					unsafe {
						// SAFETY: source/index identify one live element; r has exactly one destination slot.
						_GetIntArrayRegion (source, index, 1, r);
					}
					return r [0];
				} },
				{ typeof (long), (type, source, index) => {
					var r = new long [1];
					unsafe {
						// SAFETY: source/index identify one live element; r has exactly one destination slot.
						_GetLongArrayRegion (source, index, 1, r);
					}
					return r [0];
				} },
				{ typeof (float), (type, source, index) => {
					var r = new float [1];
					unsafe {
						// SAFETY: source/index identify one live element; r has exactly one destination slot.
						_GetFloatArrayRegion (source, index, 1, r);
					}
					return r [0];
				} },
				{ typeof (double), (type, source, index) => {
					var r = new double [1];
					unsafe {
						// SAFETY: source/index identify one live element; r has exactly one destination slot.
						_GetDoubleArrayRegion (source, index, 1, r);
					}
					return r [0];
				} },
				{ typeof (bool?),   (type, source, index) => GetNullableArrayElement (source, index, typeof (bool?)) },
				{ typeof (byte?),   (type, source, index) => GetNullableArrayElement (source, index, typeof (byte?)) },
				{ typeof (sbyte?),  (type, source, index) => GetNullableArrayElement (source, index, typeof (sbyte?)) },
				{ typeof (char?),   (type, source, index) => GetNullableArrayElement (source, index, typeof (char?)) },
				{ typeof (short?),  (type, source, index) => GetNullableArrayElement (source, index, typeof (short?)) },
				{ typeof (int?),    (type, source, index) => GetNullableArrayElement (source, index, typeof (int?)) },
				{ typeof (long?),   (type, source, index) => GetNullableArrayElement (source, index, typeof (long?)) },
				{ typeof (float?),  (type, source, index) => GetNullableArrayElement (source, index, typeof (float?)) },
				{ typeof (double?), (type, source, index) => GetNullableArrayElement (source, index, typeof (double?)) },
				{ typeof (string), (type, source, index) => {
					unsafe {
						// SAFETY: source/index supply a live element; its local transfers to the immediate String conversion.
						IntPtr elem = GetObjectArrayElement (source, index);
						if (type == typeof (Java.Lang.String))
							return new Java.Lang.String (elem, JniHandleOwnership.TransferLocalRef);
						return GetString (elem, JniHandleOwnership.TransferLocalRef);
					}
				} },
				{ typeof (IJavaObject), (type, source, index) => {
					AssertIsJavaObject (type);

					unsafe {
						// SAFETY: source/index supply a live element; GetObject consumes its local in the same conversion.
						IntPtr elem = GetObjectArrayElement (source, index);
						return GetObject (elem, type);
					}

					// FIXME: Since a Dictionary<Type, Func> is used here, the trimmer will not be able to properly analyze `Type t`
					// error IL2111: Method 'lambda expression' with parameters or return value with `DynamicallyAccessedMembersAttribute` is accessed via reflection. Trimmer can't guarantee availability of the requirements of the method.
					[UnconditionalSuppressMessage ("Trimming", "IL2067", Justification = "FIXME: https://github.com/dotnet/android/issues/8724")]
					static object? GetObject (IntPtr e, Type? t) =>
						Java.Lang.Object.GetObject (e, JniHandleOwnership.TransferLocalRef, t);
				} },
				{ typeof (Array), (type, source, index) => {
					unsafe {
						// SAFETY: source/index supply a live nested array; GetArray consumes its owned local immediately.
						IntPtr elem = GetObjectArrayElement (source, index);
						return GetArray (elem, JniHandleOwnership.TransferLocalRef, type?.GetElementType ());
					}
				} },
			};
		}

		static object? GetNullableArrayElement (IntPtr source, int index, [DynamicallyAccessedMembers (Constructors)] Type targetType)
		{
			unsafe {
				// SAFETY: the array-copy caller supplies a live object array and valid index; its new local element reference transfers to FromJniHandle.
				IntPtr elem = GetObjectArrayElement (source, index);
				return JavaConvert.FromJniHandle (elem, JniHandleOwnership.TransferLocalRef, targetType);
			}
		}

		static TValue GetConverter<TValue>(Dictionary<Type, TValue> dict, Type? elementType, IntPtr array)
		{
			TValue? converter;

			if (elementType != null) {
				if (elementType.IsEnum)
					elementType = Enum.GetUnderlyingType (elementType);
				if (dict.TryGetValue (elementType, out converter))
					return converter;
				if (elementType.IsArray)
					return dict [typeof (Array)];
			}

			if (array != IntPtr.Zero) {
				string? type;
				unsafe {
					// SAFETY: the marshaling caller retains array while its class name is read.
					type = GetClassNameFromInstance (array);
				}
				if (type == null || type.Length < 1 || type [0] != '[')
					throw new InvalidOperationException ("Unsupported java array type: " + type);

				switch (type [1]) {
				case 'B': return dict [typeof (byte)];
				case 'C': return dict [typeof (char)];
				case 'D': return dict [typeof (double)];
				case 'F': return dict [typeof (float)];
				case 'I': return dict [typeof (int)];
				case 'J': return dict [typeof (long)];
				case 'S': return dict [typeof (short)];
				case 'Z': return dict [typeof (bool)];
				case '[':
					if (elementType == null || elementType.IsArray)
						return dict [typeof (Array)];
					break;
				}

				if (type == "[Ljava/lang/String;")
					return dict [typeof (string)];
			}

			AssertIsJavaObject (elementType);
			return dict [typeof (IJavaObject)];
		}

		/// <safety>
		/// <paramref name="array"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The requested start/length range must lie within the live JNI array, whose primitive element type must match the buffer.
		/// <paramref name="length"/> must not exceed <paramref name="buffer"/>.Length; the managed buffer is pinned only for this call.
		/// </safety>
		static unsafe void _GetBooleanArrayRegion (IntPtr array, int start, int length, bool[] buffer)
		{
			unsafe {
				// SAFETY: fixed pins buffer for the synchronous primitive read; the caller supplies a matching array and nonnegative start/length within both native and managed extents.
				fixed (bool* p = buffer)
					JniEnvironment.Arrays.GetBooleanArrayRegion (new JniObjectReference (array), start, length, p);
			}
		}

		/// <safety>
		/// <paramref name="array"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The requested start/length range must lie within the live JNI array, whose primitive element type must match the buffer.
		/// <paramref name="length"/> must not exceed <paramref name="buffer"/>.Length; the managed buffer is pinned only for this call.
		/// </safety>
		static unsafe void _GetByteArrayRegion (IntPtr array, int start, int length, byte[] buffer)
		{
			unsafe {
				// SAFETY: fixed pins buffer for the synchronous primitive read; the caller supplies a matching array and nonnegative start/length within both native and managed extents.
				fixed (byte* p = buffer)
					JniEnvironment.Arrays.GetByteArrayRegion (new JniObjectReference (array), start, length, (sbyte*) p);
			}
		}

		/// <safety>
		/// <paramref name="array"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The requested start/length range must lie within the live JNI array, whose primitive element type must match the buffer.
		/// <paramref name="length"/> must not exceed <paramref name="buffer"/>.Length; the managed buffer is pinned only for this call.
		/// </safety>
		static unsafe void _GetCharArrayRegion (IntPtr array, int start, int length, char[] buffer)
		{
			unsafe {
				// SAFETY: fixed pins buffer for the synchronous primitive read; the caller supplies a matching array and nonnegative start/length within both native and managed extents.
				fixed (char* p = buffer)
					JniEnvironment.Arrays.GetCharArrayRegion (new JniObjectReference (array), start, length, p);
			}
		}

		/// <safety>
		/// <paramref name="array"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The requested start/length range must lie within the live JNI array, whose primitive element type must match the buffer.
		/// <paramref name="length"/> must not exceed <paramref name="buffer"/>.Length; the managed buffer is pinned only for this call.
		/// </safety>
		static unsafe void _GetShortArrayRegion (IntPtr array, int start, int length, short[] buffer)
		{
			unsafe {
				// SAFETY: fixed pins buffer for the synchronous primitive read; the caller supplies a matching array and nonnegative start/length within both native and managed extents.
				fixed (short* p = buffer)
					JniEnvironment.Arrays.GetShortArrayRegion (new JniObjectReference (array), start, length, p);
			}
		}

		/// <safety>
		/// <paramref name="array"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The requested start/length range must lie within the live JNI array, whose primitive element type must match the buffer.
		/// <paramref name="length"/> must not exceed <paramref name="buffer"/>.Length; the managed buffer is pinned only for this call.
		/// </safety>
		static unsafe void _GetIntArrayRegion (IntPtr array, int start, int length, int[] buffer)
		{
			unsafe {
				// SAFETY: fixed pins buffer for the synchronous primitive read; the caller supplies a matching array and nonnegative start/length within both native and managed extents.
				fixed (int* p = buffer)
					JniEnvironment.Arrays.GetIntArrayRegion (new JniObjectReference (array), start, length, p);
			}
		}

		/// <safety>
		/// <paramref name="array"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The requested start/length range must lie within the live JNI array, whose primitive element type must match the buffer.
		/// <paramref name="length"/> must not exceed <paramref name="buffer"/>.Length; the managed buffer is pinned only for this call.
		/// </safety>
		static unsafe void _GetLongArrayRegion (IntPtr array, int start, int length, long[] buffer)
		{
			unsafe {
				// SAFETY: fixed pins buffer for the synchronous primitive read; the caller supplies a matching array and nonnegative start/length within both native and managed extents.
				fixed (long* p = buffer)
					JniEnvironment.Arrays.GetLongArrayRegion (new JniObjectReference (array), start, length, p);
			}
		}

		/// <safety>
		/// <paramref name="array"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The requested start/length range must lie within the live JNI array, whose primitive element type must match the buffer.
		/// <paramref name="length"/> must not exceed <paramref name="buffer"/>.Length; the managed buffer is pinned only for this call.
		/// </safety>
		static unsafe void _GetFloatArrayRegion (IntPtr array, int start, int length, float[] buffer)
		{
			unsafe {
				// SAFETY: fixed pins buffer for the synchronous primitive read; the caller supplies a matching array and nonnegative start/length within both native and managed extents.
				fixed (float* p = buffer)
					JniEnvironment.Arrays.GetFloatArrayRegion (new JniObjectReference (array), start, length, p);
			}
		}

		/// <safety>
		/// <paramref name="array"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The requested start/length range must lie within the live JNI array, whose primitive element type must match the buffer.
		/// <paramref name="length"/> must not exceed <paramref name="buffer"/>.Length; the managed buffer is pinned only for this call.
		/// </safety>
		static unsafe void _GetDoubleArrayRegion (IntPtr array, int start, int length, double[] buffer)
		{
			unsafe {
				// SAFETY: fixed pins buffer for the synchronous primitive read; the caller supplies a matching array and nonnegative start/length within both native and managed extents.
				fixed (double* p = buffer)
					JniEnvironment.Arrays.GetDoubleArrayRegion (new JniObjectReference (array), start, length, p);
			}
		}

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
		/// <safety>
		/// <paramref name="src"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The JNI array element type must match the requested managed conversion and all requested element ranges must lie within the live array.
		/// </safety>
		public static unsafe void CopyArray (IntPtr src, Array dest, Type? elementType = null)
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
		{
			if (dest == null)
				throw new ArgumentNullException ("dest");

			unsafe {
				// SAFETY: the caller retains src and a compatible element kind for dest.Length entries; nested local arrays are released in finally after conversion.
				if (elementType != null && elementType.IsValueType)
					AssertCompatibleArrayTypes (src, destElementType: elementType);

				if (elementType != null && elementType.IsArray) {
					for (int i = 0; i < dest.Length; ++i) {
						IntPtr a = GetObjectArrayElement (src, i);
						try {
							var d = (Array?) dest.GetValue (i);
							if (d == null)
								dest.SetValue (GetArray (a, JniHandleOwnership.DoNotTransfer, elementType.GetElementType ()), i);
							else
								CopyArray (a, d, elementType.GetElementType ());
						} finally {
							DeleteLocalRef (a);
						}
					}
					return;
				}

				var converter = GetConverter (NativeArrayElementToManaged, elementType, src);

				for (int i = 0; i < dest.Length; i++)
					dest.SetValue (converter (elementType, src, i), i);
			}
		}

		static void AssertIsJavaObject (Type? targetType)
		{
			if (targetType != null && !typeof (IJavaObject).IsAssignableFrom (targetType))
				throw new NotSupportedException ("Don't know how to convert type '" + targetType.FullName + "' to an Android.Runtime.IJavaObject.");
		}

		/// <safety>
		/// <paramref name="src"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The JNI array element type must match the requested managed conversion and all requested element ranges must lie within the live array.
		/// </safety>
		public static unsafe void CopyArray<T> (IntPtr src, T[] dest)
		{
			if (dest == null)
				throw new ArgumentNullException ("dest");

			unsafe {
				// SAFETY: the caller retains src with the matching T conversion and dest.Length entries; primitive kinds are checked before the existing element converters run.
				if (typeof (T).IsValueType)
					AssertCompatibleArrayTypes (src, destElementType: typeof (T));

				if (typeof (T).IsArray) {
					CopyArray (src, dest, typeof (T));
					return;
				}

				var converter = GetConverter (NativeArrayElementToManaged, typeof (T), src);

				for (int i = 0; i < dest.Length; i++)
					dest [i] = (T) converter (typeof (T), src, i)!;
			}
		}

		/// <safety>
		/// <paramref name="dest"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The JNI array element type must match the requested managed conversion and all requested element ranges must lie within the live array.
		/// </safety>
		public static unsafe void CopyArray (bool[] src, IntPtr dest)
		{
			if (src == null)
				throw new ArgumentNullException ("src");

			unsafe {
				// SAFETY: src is pinned for src.Length values; the caller retains a matching, sufficiently long destination.
				AssertCompatibleArrayTypes (srcElementType: typeof (bool), dest);

				fixed (bool* p = src)
					JniEnvironment.Arrays.SetBooleanArrayRegion (new JniObjectReference (dest), 0, src.Length, p);
			}
		}

		/// <safety>
		/// <paramref name="dest"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The JNI array element type must match the requested managed conversion and all requested element ranges must lie within the live array.
		/// </safety>
		public static unsafe void CopyArray (string[] src, IntPtr dest)
		{
			if (src == null)
				throw new ArgumentNullException ("src");

			unsafe {
				// SAFETY: the caller retains dest as a String-compatible object array; each temporary NewString reference is passed to SetObjectArrayElement before normal-path deletion.
				for (int i = 0; i < src.Length; i++) {
					IntPtr native = NewString (src [i]);
					JniEnvironment.Arrays.SetObjectArrayElement (new JniObjectReference (dest), i, new JniObjectReference (native));
					DeleteLocalRef (native);
				}
			}
		}

		/// <safety>
		/// <paramref name="dest"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The JNI array element type must match the requested managed conversion and all requested element ranges must lie within the live array.
		/// </safety>
		public static unsafe void CopyArray (IJavaObject[] src, IntPtr dest)
		{
			if (src == null)
				throw new ArgumentNullException ("src");

			for (int i = 0; i < src.Length; i++) {
				IJavaObject o = src [i];
				JniEnvironment.Arrays.SetObjectArrayElement (new JniObjectReference (dest), i, new JniObjectReference (o == null ? IntPtr.Zero : o.Handle));
				GC.KeepAlive (o);
			}
		}

		static Dictionary<Type, Action<Array, IntPtr>>? copyManagedToNativeArray;
		static Dictionary<Type, Action<Array, IntPtr>> CopyManagedToNativeArray {
			get {
				if (copyManagedToNativeArray != null)
					return copyManagedToNativeArray;

				var newValue = CreateCopyManagedToNativeArray ();
				Interlocked.CompareExchange (ref copyManagedToNativeArray, newValue, null);
				return copyManagedToNativeArray;
			}
		}

		static Dictionary<Type, Action<Array, IntPtr>> CreateCopyManagedToNativeArray ()
		{
			return new Dictionary<Type, Action<Array, IntPtr>> () {
				{ typeof (bool),        (source, dest) => {
					unsafe {
						// SAFETY: the array marshaler retains the matching array and element range for this synchronous conversion.
						CopyArray ((bool[]) source, dest);
					}
				} },
				{ typeof (byte),        (source, dest) => {
					unsafe {
						// SAFETY: the array marshaler retains the matching array and element range for this synchronous conversion.
						CopyArray ((byte[]) source, dest);
					}
				} },
				{ typeof (sbyte),       (source, dest) => {
					unsafe {
						// SAFETY: the array marshaler retains the matching array and element range for this synchronous conversion.
						CopyArray ((byte[]) (object) (sbyte[]) source, dest);
					}
				} },
				{ typeof (char),        (source, dest) => {
					unsafe {
						// SAFETY: the array marshaler retains the matching array and element range for this synchronous conversion.
						CopyArray ((char[]) source, dest);
					}
				} },
				{ typeof (short),       (source, dest) => {
					unsafe {
						// SAFETY: the array marshaler retains the matching array and element range for this synchronous conversion.
						CopyArray ((short[]) source, dest);
					}
				} },
				{ typeof (int),         (source, dest) => {
					unsafe {
						// SAFETY: the array marshaler retains the matching array and element range for this synchronous conversion.
						CopyArray ((int[]) source, dest);
					}
				} },
				{ typeof (long),        (source, dest) => {
					unsafe {
						// SAFETY: the array marshaler retains the matching array and element range for this synchronous conversion.
						CopyArray ((long[]) source, dest);
					}
				} },
				{ typeof (float),       (source, dest) => {
					unsafe {
						// SAFETY: the array marshaler retains the matching array and element range for this synchronous conversion.
						CopyArray ((float[]) source, dest);
					}
				} },
				{ typeof (double),      (source, dest) => {
					unsafe {
						// SAFETY: the array marshaler retains the matching array and element range for this synchronous conversion.
						CopyArray ((double[]) source, dest);
					}
				} },
				{ typeof (bool?),       (source, dest) => CopyManagedObjectArray (source, dest) },
				{ typeof (byte?),       (source, dest) => CopyManagedObjectArray (source, dest) },
				{ typeof (sbyte?),      (source, dest) => CopyManagedObjectArray (source, dest) },
				{ typeof (char?),       (source, dest) => CopyManagedObjectArray (source, dest) },
				{ typeof (short?),      (source, dest) => CopyManagedObjectArray (source, dest) },
				{ typeof (int?),        (source, dest) => CopyManagedObjectArray (source, dest) },
				{ typeof (long?),       (source, dest) => CopyManagedObjectArray (source, dest) },
				{ typeof (float?),      (source, dest) => CopyManagedObjectArray (source, dest) },
				{ typeof (double?),     (source, dest) => CopyManagedObjectArray (source, dest) },
				{ typeof (string),      (source, dest) => {
					var s = source as string[];
					if (s != null) {
						unsafe {
							// SAFETY: the conversion retains its matching JNI array and owned local references for this call.
							CopyArray (s, dest);
						}
						return;
					}
					var ijo = source as IJavaObject[];
					if (ijo != null) {
						unsafe {
							// SAFETY: the conversion retains its matching JNI array and owned local references for this call.
							CopyArray (ijo, dest);
						}
						return;
					}
					unsafe {
						// SAFETY: dest is the retained array supplied to this conversion; only its diagnostic class name is read.
						throw new NotSupportedException ("Don't know how to copy '" +
								source.GetType ().FullName + "' to '" +
								GetClassNameFromInstance (dest) +
								"'.");
					}
				} },
				{ typeof (IJavaObject), (source, dest) => {
					unsafe {
						// SAFETY: the array marshaler retains the matching array and element range for this synchronous conversion.
						CopyArray ((IJavaObject[]) source, dest);
					}
				} },
				{ typeof (Array),       (source, dest) => {
					int len = source.GetLength (0);
					for (int i = 0; i < len; ++i) {
						unsafe {
							// SAFETY: dest/i supply the nested array; conversion and the existing local release stay together.
							IntPtr _dest = GetObjectArrayElement (dest, i);
							Array  _source  = (Array) source.GetValue (i)!;
							CopyArray (_source, _source.GetType ().GetElementType ()!, _dest);
							JNIEnv.DeleteLocalRef (_dest);
						}
					}
				} },
			};
		}

		static void CopyManagedObjectArray (Array source, IntPtr dest)
		{
			unsafe {
				// SAFETY: dest is the retained object array supplied by its creation/copy path; each local converted value is borrowed for the set and released in finally.
				// Inlined equivalent of JavaConvert.WithLocalJniHandle to avoid allocating a capturing
				// closure per element (this runs once per array element for both NewObjectArray and the
				// nullable-primitive CopyManagedToNativeArray entries).
				for (int i = 0; i < source.Length; i++) {
					object? value = source.GetValue (i);
					IntPtr lref = JavaConvert.ToLocalJniHandle (value);
					try {
						SetObjectArrayElement (dest, i, lref);
					} finally {
						DeleteLocalRef (lref);
						GC.KeepAlive (value);
					}
				}
			}
		}

		/// <safety>
		/// <paramref name="dest"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The JNI array element type must match the requested managed conversion and all requested element ranges must lie within the live array.
		/// </safety>
		public static unsafe void CopyArray (Array source, Type elementType, IntPtr dest)
		{
			if (source == null)
				throw new ArgumentNullException ("source");
			if (elementType == null)
				throw new ArgumentNullException ("elementType");

			if (elementType.IsValueType)
				AssertCompatibleArrayTypes (srcElementType: elementType, dest);

			Action<Array, IntPtr> converter = GetConverter (CopyManagedToNativeArray, elementType, dest);

			converter (source, dest);
		}

		/// <safety>
		/// <paramref name="dest"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The JNI array element type must match the requested managed conversion and all requested element ranges must lie within the live array.
		/// </safety>
		public static unsafe void CopyArray<T> (T[] src, IntPtr dest)
		{
			if (src == null)
				throw new ArgumentNullException ("src");

			unsafe {
				// SAFETY: the caller retains the destination for the matching T conversion; no extra reference is acquired.
				CopyArray (src, typeof (T), dest);
			}
		}

		/// <safety>
		/// <paramref name="array_ptr"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// <paramref name="transfer"/> must describe reference ownership the caller holds; transferred handles must not be used after their release.
		/// The JNI array element type must match the requested managed conversion and all requested element ranges must lie within the live array.
		/// </safety>
		public static unsafe Array? GetArray (IntPtr array_ptr, JniHandleOwnership transfer, Type? element_type = null)
		{
			try {
				return _GetArray (array_ptr, element_type);
			}
			finally {
				DeleteRef (array_ptr, transfer);
			}
		}

		static Dictionary<Type, Func<Type?, IntPtr, int, Array>>? nativeArrayToManaged;
		static Dictionary<Type, Func<Type?, IntPtr, int, Array>> NativeArrayToManaged {
			get {
				if (nativeArrayToManaged != null)
					return nativeArrayToManaged;

				var newValue = CreateNativeArrayToManaged ();
				Interlocked.CompareExchange (ref nativeArrayToManaged, newValue, null);
				return nativeArrayToManaged;
			}
		}

		static Dictionary<Type, Func<Type?, IntPtr, int, Array>> CreateNativeArrayToManaged ()
		{
			return new Dictionary<Type, Func<Type?, IntPtr, int, Array>> () {
				{ typeof (bool), (type, source, len) => {
					var r = new bool [len];
					unsafe {
						// SAFETY: source is the matching live JNI array; the managed result bounds the copy.
						CopyArray (source, r);
					}
					return r;
				} },
				{ typeof (byte), (type, source, len) => {
					var r = new byte[len];
					unsafe {
						// SAFETY: source is the matching live JNI array; the managed result bounds the copy.
						CopyArray (source, r);
					}
					return r;
				} },
				{ typeof (sbyte), (type, source, len) => {
					var r = new sbyte [len];
					unsafe {
						// SAFETY: source is the matching live JNI array; the managed result bounds the copy.
						CopyArray (source, (byte[]) (object) r);
					}
					return r;
				} },
				{ typeof (char), (type, source, len) => {
					var r = new char [len];
					unsafe {
						// SAFETY: source is the matching live JNI array; the managed result bounds the copy.
						CopyArray (source, r);
					}
					return r;
				} },
				{ typeof (short), (type, source, len) => {
					var r = new short [len];
					unsafe {
						// SAFETY: source is the matching live JNI array; the managed result bounds the copy.
						CopyArray (source, r);
					}
					return r;
				} },
				{ typeof (ushort), (type, source, len) => {
					var r = new ushort [len];
					unsafe {
						// SAFETY: source is the matching live JNI array; the managed result bounds the copy.
						CopyArray (source, r);
					}
					return r;
				} },
				{ typeof (int), (type, source, len) => {
					var r = new int[len];
					unsafe {
						// SAFETY: source is the matching live JNI array; the managed result bounds the copy.
						CopyArray (source, r);
					}
					return r;
				} },
				{ typeof (uint), (type, source, len) => {
					var r = new uint[len];
					unsafe {
						// SAFETY: source is the matching live JNI array; the managed result bounds the copy.
						CopyArray (source, r);
					}
					return r;
				} },
				{ typeof (long), (type, source, len) => {
					var r = new long[len];
					unsafe {
						// SAFETY: source is the matching live JNI array; the managed result bounds the copy.
						CopyArray (source, r);
					}
					return r;
				} },
				{ typeof (ulong), (type, source, len) => {
					var r = new ulong[len];
					unsafe {
						// SAFETY: source is the matching live JNI array; the managed result bounds the copy.
						CopyArray (source, r);
					}
					return r;
				} },
				{ typeof (float), (type, source, len) => {
					var r = new float[len];
					unsafe {
						// SAFETY: source is the matching live JNI array; the managed result bounds the copy.
						CopyArray (source, r);
					}
					return r;
				} },
				{ typeof (double), (type, source, len) => {
					var r = new double [len];
					unsafe {
						// SAFETY: source is the matching live JNI array; the managed result bounds the copy.
						CopyArray (source, r);
					}
					return r;
				} },
				{ typeof (bool?),   (type, source, len) => CreateManagedArrayFromObjectArray (type, source, len) },
				{ typeof (byte?),   (type, source, len) => CreateManagedArrayFromObjectArray (type, source, len) },
				{ typeof (sbyte?),  (type, source, len) => CreateManagedArrayFromObjectArray (type, source, len) },
				{ typeof (char?),   (type, source, len) => CreateManagedArrayFromObjectArray (type, source, len) },
				{ typeof (short?),  (type, source, len) => CreateManagedArrayFromObjectArray (type, source, len) },
				{ typeof (int?),    (type, source, len) => CreateManagedArrayFromObjectArray (type, source, len) },
				{ typeof (long?),   (type, source, len) => CreateManagedArrayFromObjectArray (type, source, len) },
				{ typeof (float?),  (type, source, len) => CreateManagedArrayFromObjectArray (type, source, len) },
				{ typeof (double?), (type, source, len) => CreateManagedArrayFromObjectArray (type, source, len) },
				{ typeof (string), (type, source, len) => {
					if (type != null && typeof (Java.Lang.Object).IsAssignableFrom (type)) {
						var r = new Java.Lang.String [len];
						unsafe {
							// SAFETY: source is the matching live JNI array; the managed result bounds the copy.
							CopyArray (source, r);
						}
						return r;
					} else {
						var r = new string [len];
						unsafe {
							// SAFETY: source is the matching live JNI array; the managed result bounds the copy.
							CopyArray (source, r);
						}
						return r;
					}
				} },
				{ typeof (IJavaObject), (type, source, len) => {
					var r = ArrayCreateInstance (type!, len);
					unsafe {
						// SAFETY: source is the matching live JNI array; the managed result bounds the copy.
						CopyArray (source, r, type);
					}
					return r;
				} },
				{ typeof (Array), (type, source, len) => {
					var r = ArrayCreateInstance (type!, len);
					unsafe {
						// SAFETY: source is the matching live JNI array; the managed result bounds the copy.
						CopyArray (source, r, type);
					}
					return r;
				} },
			};
		}

		static Array CreateManagedArrayFromObjectArray (Type? elementType, IntPtr source, int len)
		{
			if (elementType == null)
				throw new ArgumentNullException (nameof (elementType));

			unsafe {
				// SAFETY: the marshaler retains source for len elements; the result is allocated to that same length before copying.
				var r = ArrayCreateInstance (elementType, len);
				CopyArray (source, r, elementType);
				return r;
			}
		}

		static Array? _GetArray (IntPtr array_ptr, Type? element_type)
		{
			if (array_ptr == IntPtr.Zero)
				return null;

			if (element_type != null && element_type.IsValueType)
				AssertCompatibleArrayTypes (array_ptr, destElementType: element_type);

			int cnt = _GetArrayLength (array_ptr);

			var converter = GetConverter (NativeArrayToManaged, element_type, array_ptr);

			return converter (element_type, array_ptr, cnt);
		}

		static int _GetArrayLength (IntPtr array_ptr)
		{
			return JniEnvironment.Arrays.GetArrayLength (new JniObjectReference (array_ptr));
		}

		/// <safety>
		/// <paramref name="array_ptr"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The JNI array element type must match the requested managed conversion and all requested element ranges must lie within the live array.
		/// </safety>
		public static unsafe object?[]? GetObjectArray (IntPtr array_ptr, Type[] element_types)
		{
			if (array_ptr == IntPtr.Zero)
				return null;

			int cnt = _GetArrayLength (array_ptr);
			if (cnt == 0)
				return Array.Empty<object> ();

			var converter = GetConverter (NativeArrayElementToManaged, null, array_ptr);

			object?[] ret = new object [cnt];

			for (int i = 0; i < cnt; i++) {
				Type? targetType	= (element_types != null && i < element_types.Length) ? element_types [i] : null;
				object? value    = converter ((targetType == null || targetType.IsValueType) ? null : targetType,
						array_ptr, i);

				ret [i] = value;
				ret [i] = targetType == null || targetType.IsInstanceOfType (value)
					? value
					: Convert.ChangeType (value, targetType, CultureInfo.InvariantCulture);
			}

			return ret;
		}

		/// <safety>
		/// <paramref name="array_ptr"/> must be null or a live JNI object reference usable on the current thread for the call.
		/// Array kind and compatibility with <typeparamref name="T"/> are checked before native array access;
		/// the caller must still guarantee reference provenance and lifetime.
		/// </safety>
		public static unsafe T[]? GetArray<T> (IntPtr array_ptr)
		{
			if (array_ptr == IntPtr.Zero)
				return null;

			unsafe {
				// SAFETY: the caller retains a live object reference; IsInstanceOf verifies the matching array kind before any array-length or element access.
				AssertCompatibleArrayTypes (array_ptr, destElementType: typeof (T));

				int cnt = _GetArrayLength (array_ptr);
				T[] ret = new T [cnt];
				CopyArray<T> (array_ptr, ret);
				return ret;
			}
		}

		public static T[]? GetArray<
				[DynamicallyAccessedMembers (Constructors)]
				T
		> (Java.Lang.Object[] array)
		{
			if (array == null)
				return null;
			T[] ret = new T [array.Length];
			for (int i = 0; i < array.Length; i++)
				ret [i] = JavaConvert.FromJavaObject<T> (array [i])!;
			return ret;
		}

		/// <safety>
		/// <paramref name="array_ptr"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The JNI array element type must match the requested managed conversion and all requested element ranges must lie within the live array.
		/// </safety>
		public static unsafe T GetArrayItem<T> (IntPtr array_ptr, int index)
		{
			if (array_ptr == IntPtr.Zero)
				throw new ArgumentException ("array_ptr");
			unsafe {
				// SAFETY: the caller retains array_ptr with T-compatible elements; the explicit index check runs before the one-element converter accesses the JNI array.
				if (index < 0 || index >= GetArrayLength (array_ptr))
					throw new ArgumentOutOfRangeException ("index");

				var converter = GetConverter (NativeArrayElementToManaged, typeof (T), array_ptr);

				return (T) converter (typeof (T), array_ptr, index)!;
			}
		}

		/// <safety>
		/// <paramref name="array_ptr"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// </safety>
		public static unsafe int GetArrayLength (IntPtr array_ptr)
		{
			if (array_ptr == IntPtr.Zero)
				return 0;
			return _GetArrayLength (array_ptr);
		}

		public static IntPtr NewArray (bool[]? array)
		{
			if (array == null)
				return IntPtr.Zero;
			IntPtr result;

			var r   = JniEnvironment.Arrays.NewBooleanArray (array.Length);
			unsafe {
				// SAFETY: JNI allocated array.Length Boolean slots; pin the managed source for this synchronous copy.
				fixed (bool* p = array)
					JniEnvironment.Arrays.SetBooleanArrayRegion (r, 0, array.Length, p);
			}
			result  = r.Handle;

			return result;
		}

		public static IntPtr NewArray (string[]? array)
		{
			if (array == null)
				return IntPtr.Zero;

			unsafe {
				// SAFETY: NewObjectArray creates a String array of array.Length; CopyArray uses that owned local reference and exactly the managed source's elements.
				IntPtr result = NewObjectArray (array.Length, Java.Lang.Class.String, IntPtr.Zero);
				CopyArray (array, result);

				return result;
			}
		}

		// We do these translations here instead of in the binding to force a compile
		// time error if using an older Java.Interop.dll instead of a runtime error
		public static IntPtr NewArray (uint[] array) => NewArray ((int[])(object)array);

		public static IntPtr NewArray (ushort[] array) => NewArray ((short[])(object)array);

		public static IntPtr NewArray (ulong[] array) => NewArray ((long[])(object)array);

		/// <safety>
		/// <paramref name="elementClass"/> must be null where supported, or a live JNI class reference of the required type usable on the current thread for the call.
		/// </safety>
		public static unsafe IntPtr NewObjectArray (int length, IntPtr elementClass)
		{
			unsafe {
				// SAFETY: the caller retains elementClass as a live class; the three-argument overload initializes each new element with the JNI null reference.
				return NewObjectArray (length, elementClass, IntPtr.Zero);
			}
		}

		/// <safety>
		/// <paramref name="elementClass"/> must be null where supported, or a live JNI class reference of the required type usable on the current thread for the call.
		/// <paramref name="initialElement"/> must be null where supported, or a live JNI object reference of the required type usable on the current thread for the call.
		/// </safety>
		public static unsafe IntPtr NewObjectArray (int length, IntPtr elementClass, IntPtr initialElement)
		{
			return JniEnvironment.Arrays.NewObjectArray (length, new JniObjectReference (elementClass), new JniObjectReference (initialElement)).Handle;
		}

		public static IntPtr NewObjectArray<T>(params T[]? values)
		{
			unsafe {
				// SAFETY: the new object array has values.Length slots; the class global survives until finally and WithLocalJniHandle borrows each value only for its synchronous set.
				if (values == null)
					return IntPtr.Zero;

				IntPtr grefArrayElementClass = GetArrayElementClass (values);
				if (JniEnvironment.Types.GetJniTypeNameFromClass (new JniObjectReference (grefArrayElementClass)) == "mono/android/runtime/JavaObject") {
					DeleteGlobalRef (grefArrayElementClass);
					grefArrayElementClass = NewGlobalRef (Java.Lang.Class.Object);
				}
				try {
					IntPtr lrefArray = NewObjectArray (values.Length, grefArrayElementClass, IntPtr.Zero);

					for (int i = 0; i < values.Length; ++i) {
						JavaConvert.WithLocalJniHandle (values [i], lref => {
								SetObjectArrayElement (lrefArray, i, lref);
								return IntPtr.Zero;
						});
					}

					return lrefArray;
				}
				finally {
					DeleteGlobalRef (grefArrayElementClass);
				}

			}
		}

		static IntPtr GetArrayElementClass<T>(T[] values)
		{
			unsafe {
				// SAFETY: FindClass returns an owned global for the selected managed element type; the java/lang/Object fallback duplicates the runtime's retained class.
				Type    elementType = typeof (T);
				var jniClass    = JavaConvert.GetJniClassForType (elementType);
				if (jniClass != null) {
					return FindClass (jniClass);
				}

				if (elementType.IsValueType)
					return NewGlobalRef (Java.Lang.Class.Object);

				return FindClass (elementType);
			}
		}

		/// <safety>
		/// <paramref name="source"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The JNI array element type must match the requested managed conversion and all requested element ranges must lie within the live array.
		/// </safety>
		public static unsafe void CopyObjectArray<
				[DynamicallyAccessedMembers (Constructors)]
				T
		>(IntPtr source, T[] destination)
		{
			if (source == IntPtr.Zero)
				return;
			unsafe {
				// SAFETY: the caller retains source as a compatible object array; Math.Min bounds reads by both arrays, and each local element reference transfers to conversion.
				if (destination == null)
					throw new ArgumentNullException ("destination");

				int len = Math.Min (GetArrayLength (source), destination.Length);
				for (int i = 0; i < len; ++i) {
					IntPtr value = GetObjectArrayElement (source, i);
					destination [i] = JavaConvert.FromJniHandle<T>(value, JniHandleOwnership.TransferLocalRef)!;
				}
			}
		}

		/// <safety>
		/// <paramref name="destination"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The JNI array element type must match the requested managed conversion and all requested element ranges must lie within the live array.
		/// </safety>
		public static unsafe void CopyObjectArray<T>(T[]? source, IntPtr destination)
		{
			if (source == null)
				return;
			unsafe {
				// SAFETY: the caller retains destination as a compatible object array; Math.Min bounds writes, and WithLocalJniHandle retains each value through the set.
				if (destination == IntPtr.Zero)
					throw new ArgumentException ("Destination is a null JNI handle!", "destination");

				int len = Math.Min (source.Length, GetArrayLength (destination));
				for (int i = 0; i < len; ++i) {
					JavaConvert.WithLocalJniHandle (source [i], lref => {
							SetObjectArrayElement (destination, i, lref);
							return IntPtr.Zero;
					});
				}
			}
		}

		public static IntPtr NewArray (IJavaObject[]? array)
		{
			if (array == null)
				return IntPtr.Zero;

			unsafe {
				// SAFETY: FindClass owns the element-class global through allocation; the new array has array.Length slots and CopyArray retains each managed element during insertion.
				IntPtr result;
				IntPtr grefClass = FindClass (array.GetType ().GetElementType ()!);
				try {
					result = NewObjectArray (array.Length, grefClass, IntPtr.Zero);
				} finally {
					DeleteGlobalRef (grefClass);
				}

				CopyArray (array, result);

				return result;
			}
		}

		static Dictionary<Type, Func<Array, IntPtr>>? createManagedToNativeArray;
		static Dictionary<Type, Func<Array, IntPtr>> CreateManagedToNativeArray {
			get {
				if (createManagedToNativeArray != null)
					return createManagedToNativeArray;

				var newValue = CreateCreateManagedToNativeArray ();
				Interlocked.CompareExchange (ref createManagedToNativeArray, newValue, null);
				return createManagedToNativeArray;
			}
		}

		static Dictionary<Type, Func<Array, IntPtr>> CreateCreateManagedToNativeArray ()
		{
			return new Dictionary<Type, Func<Array, IntPtr>> () {
				{ typeof (bool),          (source) => NewArray ((bool[]) source) },
				{ typeof (byte),          (source) => NewArray ((byte[]) source) },
				{ typeof (sbyte),         (source) => NewArray ((byte[]) (object) (sbyte[]) source) },
				{ typeof (char),          (source) => NewArray ((char[]) source) },
				{ typeof (short),         (source) => NewArray ((short[]) source) },
				{ typeof (int),           (source) => NewArray ((int[]) source) },
				{ typeof (long),          (source) => NewArray ((long[]) source) },
				{ typeof (float),         (source) => NewArray ((float[]) source) },
				{ typeof (double),        (source) => NewArray ((double[]) source) },
				{ typeof (bool?),         (source) => NewObjectArray (source, typeof (bool?)) },
				{ typeof (byte?),         (source) => NewObjectArray (source, typeof (byte?)) },
				{ typeof (sbyte?),        (source) => NewObjectArray (source, typeof (sbyte?)) },
				{ typeof (char?),         (source) => NewObjectArray (source, typeof (char?)) },
				{ typeof (short?),        (source) => NewObjectArray (source, typeof (short?)) },
				{ typeof (int?),          (source) => NewObjectArray (source, typeof (int?)) },
				{ typeof (long?),         (source) => NewObjectArray (source, typeof (long?)) },
				{ typeof (float?),        (source) => NewObjectArray (source, typeof (float?)) },
				{ typeof (double?),       (source) => NewObjectArray (source, typeof (double?)) },
				{ typeof (string),        (source) => NewArray ((string[]) source) },
				{ typeof (IJavaObject),   (source) => NewArray ((IJavaObject[]) source) },
				{ typeof (Array),         (source) => NewArray (source) },
			};
		}

		static IntPtr NewObjectArray (Array value, Type elementType)
		{
			unsafe {
				// SAFETY: the class global survives in finally and the newly allocated array has value.Length slots; on copy failure the owned array local is deleted before rethrow.
				IntPtr grefArrayElementClass = FindObjectArrayElementClass (elementType);
				try {
					IntPtr array = IntPtr.Zero;
					try {
						array = NewObjectArray (value.Length, grefArrayElementClass, IntPtr.Zero);
						CopyManagedObjectArray (value, array);
						return array;
					} catch {
						DeleteLocalRef (array);
						throw;
					}
				} finally {
					DeleteGlobalRef (grefArrayElementClass);
				}
			}
		}

		static IntPtr FindObjectArrayElementClass (Type elementType)
		{
			var boxedPrimitiveJniClassName = GetBoxedPrimitiveJniClassNameUtf8 (elementType);
			if (!boxedPrimitiveJniClassName.IsEmpty) {
				unsafe {
					// SAFETY: GetBoxedPrimitiveJniClassNameUtf8 returns only complete UTF-8 literals with the trailing NUL outside their span length.
					return FindClass (boxedPrimitiveJniClassName);
				}
			}

			return FindClass (elementType);
		}

		public static IntPtr NewArray (Array value, Type? elementType = null)
		{
			if (value == null)
				throw new ArgumentNullException ("value");

			unsafe {
				// SAFETY: nested arrays use the class global found for elementType and released in finally; nonnested creators own their JNI allocations and use managed lengths.
				elementType ??= value.GetType ().GetElementType ()!;

				if (elementType.IsArray) {
					IntPtr grefArrayElementClass = FindClass (elementType);
					try {
						return NewArray (value, elementType, grefArrayElementClass);
					} finally {
						DeleteGlobalRef (grefArrayElementClass);
					}
				}

				Func<Array, IntPtr> creator = GetConverter (CreateManagedToNativeArray, elementType, IntPtr.Zero);

				return creator (value);
			}
		}

		public static IntPtr NewArray<T> (T[]? array)
		{
			if (array == null)
				return IntPtr.Zero;

			unsafe {
				// SAFETY: nested arrays use the class global found for T and released in finally; nonnested creators use array.Length and the matching managed element conversion.
				if (typeof (T).IsArray) {
					IntPtr grefArrayElementClass = FindClass (typeof (T));
					try {
						return NewArray (array, typeof (T), grefArrayElementClass);
					} finally {
						DeleteGlobalRef (grefArrayElementClass);
					}
				}

				Func<Array, IntPtr> creator = GetConverter (CreateManagedToNativeArray, typeof (T), IntPtr.Zero);

				return creator (array);
			}
		}

		static IntPtr NewArray (Array value, Type elementType, IntPtr elementClass)
		{
			if (value == null)
				throw new ArgumentNullException ("value");

			unsafe {
				// SAFETY: elementClass is retained by the calling factory; the new array has value.Length slots and each generated subarray is inserted before normal-path local deletion.
				IntPtr array = IntPtr.Zero;
				try {
					array = NewObjectArray (value.Length, elementClass, IntPtr.Zero);

					for (int i = 0; i < value.Length; ++i) {
						IntPtr subarray = NewArray ((Array) value.GetValue (i)!, elementType.GetElementType ());
						SetObjectArrayElement (array, i, subarray);
						DeleteLocalRef (subarray);
					}

					return array;
				} catch {
					DeleteLocalRef (array);
					throw;
				}
			}
		}

		static Dictionary<Type, Action<IntPtr, int, object?>>? setNativeArrayElement;
		static Dictionary<Type, Action<IntPtr, int, object?>> SetNativeArrayElement {
			get {
				if (setNativeArrayElement != null)
					return setNativeArrayElement;

				var newValue = CreateSetNativeArrayElement ();
				Interlocked.CompareExchange (ref setNativeArrayElement, newValue, null);
				return setNativeArrayElement;
			}
		}

		static Dictionary<Type, Action<IntPtr, int, object?>> CreateSetNativeArrayElement ()
		{
			return new Dictionary<Type, Action<IntPtr, int, object?>> () {
				{ typeof (bool), (dest, index, value) => {
					var _value = new[]{(bool) value!};
					unsafe {
						// SAFETY: dest/index identify one live element; _value has exactly one source slot.
						_SetBooleanArrayRegion (dest, index, _value.Length, _value);
					}
				} },
				{ typeof (byte), (dest, index, value) => {
					var _value = new[]{(byte) value!};
					unsafe {
						// SAFETY: dest/index identify one live element; _value has exactly one source slot.
						_SetByteArrayRegion (dest, index, _value.Length, _value);
					}
				} },
				{ typeof (sbyte), (dest, index, value) => {
					var _value = new[]{(sbyte) value!};
					unsafe {
						// SAFETY: dest/index identify one live element; _value has exactly one source slot.
						_SetByteArrayRegion (dest, index, _value.Length, (byte[]) (object) _value);
					}
				} },
				{ typeof (char), (dest, index, value) => {
					var _value = new[]{(char) value!};
					unsafe {
						// SAFETY: dest/index identify one live element; _value has exactly one source slot.
						_SetCharArrayRegion (dest, index, _value.Length, _value);
					}
				} },
				{ typeof (short), (dest, index, value) => {
					var _value = new[]{(short) value!};
					unsafe {
						// SAFETY: dest/index identify one live element; _value has exactly one source slot.
						_SetShortArrayRegion (dest, index, _value.Length, _value);
					}
				} },
				{ typeof (int), (dest, index, value) => {
					var _value = new[]{(int) value!};
					unsafe {
						// SAFETY: dest/index identify one live element; _value has exactly one source slot.
						_SetIntArrayRegion (dest, index, _value.Length, _value);
					}
				} },
				{ typeof (long), (dest, index, value) => {
					var _value = new[]{(long) value!};
					unsafe {
						// SAFETY: dest/index identify one live element; _value has exactly one source slot.
						_SetLongArrayRegion (dest, index, _value.Length, _value);
					}
				} },
				{ typeof (float), (dest, index, value) => {
					var _value = new[]{(float) value!};
					unsafe {
						// SAFETY: dest/index identify one live element; _value has exactly one source slot.
						_SetFloatArrayRegion (dest, index, _value.Length, _value);
					}
				} },
				{ typeof (double), (dest, index, value) => {
					var _value = new[]{(double) value!};
					unsafe {
						// SAFETY: dest/index identify one live element; _value has exactly one source slot.
						_SetDoubleArrayRegion (dest, index, _value.Length, _value);
					}
				} },
				{ typeof (bool?),   (dest, index, value) => SetObjectArrayElementFromManagedValue (dest, index, value) },
				{ typeof (byte?),   (dest, index, value) => SetObjectArrayElementFromManagedValue (dest, index, value) },
				{ typeof (sbyte?),  (dest, index, value) => SetObjectArrayElementFromManagedValue (dest, index, value) },
				{ typeof (char?),   (dest, index, value) => SetObjectArrayElementFromManagedValue (dest, index, value) },
				{ typeof (short?),  (dest, index, value) => SetObjectArrayElementFromManagedValue (dest, index, value) },
				{ typeof (int?),    (dest, index, value) => SetObjectArrayElementFromManagedValue (dest, index, value) },
				{ typeof (long?),   (dest, index, value) => SetObjectArrayElementFromManagedValue (dest, index, value) },
				{ typeof (float?),  (dest, index, value) => SetObjectArrayElementFromManagedValue (dest, index, value) },
				{ typeof (double?), (dest, index, value) => SetObjectArrayElementFromManagedValue (dest, index, value) },
				{ typeof (string), (dest, index, value) => {
					unsafe {
						// SAFETY: dest/index supply an object slot; the owned String local is retained through the set and finally release.
						IntPtr s = NewString (value!.ToString ());
						try {
							SetObjectArrayElement (dest, index, s);
						} finally {
							DeleteLocalRef (s);
						}
					}
				} },
				{ typeof (IJavaObject), (dest, index, value) => {
					unsafe {
						// SAFETY: the marshaler retains dest and the local element for this in-range set.
						SetObjectArrayElement (dest, index, value == null ? IntPtr.Zero : ((IJavaObject) value).Handle);
					}
					GC.KeepAlive (value);
				} },
				{ typeof (Array), (dest, index, value) => {
					IntPtr _v = NewArray ((Array) value!);
					unsafe {
						// SAFETY: the marshaler retains dest and the local element for this in-range set.
						SetObjectArrayElement (dest, index, _v);
						JNIEnv.DeleteLocalRef (_v);
					}
				} },
			};
		}

		static void SetObjectArrayElementFromManagedValue (IntPtr dest, int index, object? value)
		{
			unsafe {
				// SAFETY: dest/index come from the checked object-array setter; ToLocalJniHandle creates the borrowed value reference, released in finally with value kept alive.
				// Inlined equivalent of JavaConvert.WithLocalJniHandle to avoid allocating a capturing
				// closure on every element set (this runs once per element on the SetArrayItem path).
				IntPtr lref = JavaConvert.ToLocalJniHandle (value);
				try {
					SetObjectArrayElement (dest, index, lref);
				} finally {
					DeleteLocalRef (lref);
					GC.KeepAlive (value);
				}
			}
		}

		/// <safety>
		/// <paramref name="array"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The requested start/length range must lie within the live JNI array, whose primitive element type must match the buffer.
		/// <paramref name="length"/> must not exceed <paramref name="buffer"/>.Length; the managed buffer is pinned only for this call.
		/// </safety>
		static unsafe void _SetBooleanArrayRegion (IntPtr array, int start, int length, bool[] buffer)
		{
			unsafe {
				// SAFETY: fixed pins buffer for the synchronous primitive write; the caller supplies a matching array and nonnegative start/length within both native and managed extents.
				fixed (bool* p = buffer)
					JniEnvironment.Arrays.SetBooleanArrayRegion (new JniObjectReference (array), start, length, p);
			}
		}

		/// <safety>
		/// <paramref name="array"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The requested start/length range must lie within the live JNI array, whose primitive element type must match the buffer.
		/// <paramref name="length"/> must not exceed <paramref name="buffer"/>.Length; the managed buffer is pinned only for this call.
		/// </safety>
		static unsafe void _SetByteArrayRegion (IntPtr array, int start, int length, byte[] buffer)
		{
			unsafe {
				// SAFETY: fixed pins buffer for the synchronous primitive write; the caller supplies a matching array and nonnegative start/length within both native and managed extents.
				fixed (byte* p = buffer)
					JniEnvironment.Arrays.SetByteArrayRegion (new JniObjectReference (array), start, length, (sbyte*) p);
			}
		}

		/// <safety>
		/// <paramref name="array"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The requested start/length range must lie within the live JNI array, whose primitive element type must match the buffer.
		/// <paramref name="length"/> must not exceed <paramref name="buffer"/>.Length; the managed buffer is pinned only for this call.
		/// </safety>
		static unsafe void _SetCharArrayRegion (IntPtr array, int start, int length, char[] buffer)
		{
			unsafe {
				// SAFETY: fixed pins buffer for the synchronous primitive write; the caller supplies a matching array and nonnegative start/length within both native and managed extents.
				fixed (char* p = buffer)
					JniEnvironment.Arrays.SetCharArrayRegion (new JniObjectReference (array), start, length, p);
			}
		}

		/// <safety>
		/// <paramref name="array"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The requested start/length range must lie within the live JNI array, whose primitive element type must match the buffer.
		/// <paramref name="length"/> must not exceed <paramref name="buffer"/>.Length; the managed buffer is pinned only for this call.
		/// </safety>
		static unsafe void _SetShortArrayRegion (IntPtr array, int start, int length, short[] buffer)
		{
			unsafe {
				// SAFETY: fixed pins buffer for the synchronous primitive write; the caller supplies a matching array and nonnegative start/length within both native and managed extents.
				fixed (short* p = buffer)
					JniEnvironment.Arrays.SetShortArrayRegion (new JniObjectReference (array), start, length, p);
			}
		}

		/// <safety>
		/// <paramref name="array"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The requested start/length range must lie within the live JNI array, whose primitive element type must match the buffer.
		/// <paramref name="length"/> must not exceed <paramref name="buffer"/>.Length; the managed buffer is pinned only for this call.
		/// </safety>
		static unsafe void _SetIntArrayRegion (IntPtr array, int start, int length, int[] buffer)
		{
			unsafe {
				// SAFETY: fixed pins buffer for the synchronous primitive write; the caller supplies a matching array and nonnegative start/length within both native and managed extents.
				fixed (int* p = buffer)
					JniEnvironment.Arrays.SetIntArrayRegion (new JniObjectReference (array), start, length, p);
			}
		}

		/// <safety>
		/// <paramref name="array"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The requested start/length range must lie within the live JNI array, whose primitive element type must match the buffer.
		/// <paramref name="length"/> must not exceed <paramref name="buffer"/>.Length; the managed buffer is pinned only for this call.
		/// </safety>
		static unsafe void _SetLongArrayRegion (IntPtr array, int start, int length, long[] buffer)
		{
			unsafe {
				// SAFETY: fixed pins buffer for the synchronous primitive write; the caller supplies a matching array and nonnegative start/length within both native and managed extents.
				fixed (long* p = buffer)
					JniEnvironment.Arrays.SetLongArrayRegion (new JniObjectReference (array), start, length, p);
			}
		}

		/// <safety>
		/// <paramref name="array"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The requested start/length range must lie within the live JNI array, whose primitive element type must match the buffer.
		/// <paramref name="length"/> must not exceed <paramref name="buffer"/>.Length; the managed buffer is pinned only for this call.
		/// </safety>
		static unsafe void _SetFloatArrayRegion (IntPtr array, int start, int length, float[] buffer)
		{
			unsafe {
				// SAFETY: fixed pins buffer for the synchronous primitive write; the caller supplies a matching array and nonnegative start/length within both native and managed extents.
				fixed (float* p = buffer)
					JniEnvironment.Arrays.SetFloatArrayRegion (new JniObjectReference (array), start, length, p);
			}
		}

		/// <safety>
		/// <paramref name="array"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The requested start/length range must lie within the live JNI array, whose primitive element type must match the buffer.
		/// <paramref name="length"/> must not exceed <paramref name="buffer"/>.Length; the managed buffer is pinned only for this call.
		/// </safety>
		static unsafe void _SetDoubleArrayRegion (IntPtr array, int start, int length, double[] buffer)
		{
			unsafe {
				// SAFETY: fixed pins buffer for the synchronous primitive write; the caller supplies a matching array and nonnegative start/length within both native and managed extents.
				fixed (double* p = buffer)
					JniEnvironment.Arrays.SetDoubleArrayRegion (new JniObjectReference (array), start, length, p);
			}
		}

		/// <safety>
		/// <paramref name="array_ptr"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The JNI array element type must match the requested managed conversion and all requested element ranges must lie within the live array.
		/// </safety>
		public static unsafe void SetArrayItem<T> (IntPtr array_ptr, int index, T value)
		{
			if (array_ptr == IntPtr.Zero)
				throw new ArgumentException ("array_ptr");
			unsafe {
				// SAFETY: the caller retains array_ptr with T-compatible elements; the explicit index check precedes the selected one-element setter.
				if (index < 0 || index >= GetArrayLength (array_ptr))
					throw new ArgumentOutOfRangeException ("index");

				var setter = GetConverter (SetNativeArrayElement, typeof (T), array_ptr);

				setter (array_ptr, index, value);
			}
		}

		[return: NotNullIfNotNull (parameterName: "array")]
		public static Java.Lang.Object[]? ToObjectArray<T> (T[]? array)
		{
			if (array == null)
				return null;
			Java.Lang.Object[] ret = new Java.Lang.Object [array.Length];
			for (int i = 0; i < array.Length; i++)
				ret [i] = JavaObjectExtensions.JavaCast<Java.Lang.Object>(JavaConvert.ToJavaObject (array [i]))!;
			return ret;
		}

		/// <safety>
		/// <paramref name="src"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The JNI array element type must match the requested managed conversion and all requested element ranges must lie within the live array.
		/// </safety>
		public static unsafe void CopyArray (IntPtr src, uint[] dest)
		{
			if (src == IntPtr.Zero)
				return;
			unsafe {
				// SAFETY: fixed pins dest.Length uint values; JNI int and uint have identical widths, and the caller retains src as a matching sufficiently long int array.
				fixed (uint* __p = dest)
					JniEnvironment.Arrays.GetIntArrayRegion (new JniObjectReference (src), 0, dest.Length, (int*) __p);
			}
		}

		/// <safety>
		/// <paramref name="src"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The JNI array element type must match the requested managed conversion and all requested element ranges must lie within the live array.
		/// </safety>
		public static unsafe void CopyArray (IntPtr src, ushort[] dest)
		{
			if (src == IntPtr.Zero)
				return;
			unsafe {
				// SAFETY: fixed pins dest.Length ushort values; JNI short and ushort have identical widths, and the caller retains src as a matching sufficiently long short array.
				fixed (ushort* __p = dest)
					JniEnvironment.Arrays.GetShortArrayRegion (new JniObjectReference (src), 0, dest.Length, (short*) __p);
			}
		}

		/// <safety>
		/// <paramref name="src"/> must be null where supported, or a live JNI array reference of the required type usable on the current thread for the call.
		/// The JNI array element type must match the requested managed conversion and all requested element ranges must lie within the live array.
		/// </safety>
		public static unsafe void CopyArray (IntPtr src, ulong[] dest)
		{
			if (src == IntPtr.Zero)
				return;
			unsafe {
				// SAFETY: fixed pins dest.Length ulong values; JNI long and ulong have identical widths, and the caller retains src as a matching sufficiently long long array.
				fixed (ulong* __p = dest)
					JniEnvironment.Arrays.GetLongArrayRegion (new JniObjectReference (src), 0, dest.Length, (long*) __p);
			}
		}
#if ANDROID_8
		/// <safety>
		/// <paramref name="env"/> must be the current thread's live JNI environment; <paramref name="jbitmap"/>
		/// must be a live Bitmap reference for the call. The native output layout must match <paramref name="info"/>.
		/// </safety>
		[LibraryImport ("libjnigraphics.so")]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		private static unsafe partial int AndroidBitmap_getInfo (IntPtr env, IntPtr jbitmap, out Android.Graphics.AndroidBitmapInfo info);

		/// <safety>
		/// <paramref name="env"/> must be the current thread's live JNI environment and <paramref name="jbitmap"/>
		/// a live Bitmap reference. On success, <paramref name="addrPtr"/> belongs to the bitmap; only access its
		/// declared pixel extent while locked and retain the bitmap until the matching unlock.
		/// </safety>
		[LibraryImport ("libjnigraphics.so")]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		private static unsafe partial int AndroidBitmap_lockPixels (IntPtr env, IntPtr jbitmap, out IntPtr addrPtr);

		/// <safety>
		/// <paramref name="env"/> must be the current thread's live JNI environment and <paramref name="jbitmap"/>
		/// the live Bitmap whose pixel lock the caller owns. Stop accessing pixel-memory aliases before unlocking.
		/// </safety>
		[LibraryImport ("libjnigraphics.so")]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		private static unsafe partial int AndroidBitmap_unlockPixels (IntPtr env, IntPtr jbitmap);

		/// <safety>
		/// <paramref name="jbitmap"/> must be a live Bitmap reference usable on the current thread.
		/// </safety>
		internal static unsafe int AndroidBitmap_getInfo (IntPtr jbitmap, out Android.Graphics.AndroidBitmapInfo info)
		{
			unsafe {
				// SAFETY: Handle is this thread's environment; the caller retains the Bitmap and the stub owns the scalar output.
				return AndroidBitmap_getInfo (Handle, jbitmap, out info);
			}
		}

		/// <safety>
		/// <paramref name="jbitmap"/> must be a live Bitmap reference usable on the current thread.
		/// On success, <paramref name="addrPtr"/> is usable only within the bitmap's pixel extent and until its matching unlock.
		/// </safety>
		internal static unsafe int AndroidBitmap_lockPixels (IntPtr jbitmap, out IntPtr addrPtr)
		{
			unsafe {
				// SAFETY: Handle is this thread's environment; the caller retains the Bitmap and owns the resulting lock.
				return AndroidBitmap_lockPixels (Handle, jbitmap, out addrPtr);
			}
		}

		/// <safety>
		/// <paramref name="jbitmap"/> must remain live, the caller must own its matching pixel lock,
		/// and no pixel-memory alias may be accessed after unlocking.
		/// </safety>
		internal static unsafe int AndroidBitmap_unlockPixels (IntPtr jbitmap)
		{
			unsafe {
				// SAFETY: Handle is this thread's environment; the caller owns the matching lock and has stopped pixel access.
				return AndroidBitmap_unlockPixels (Handle, jbitmap);
			}
		}
#endif  // ANDROID_8
	}
}
