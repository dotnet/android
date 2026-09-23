using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Reflection;

using Java.Interop;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Android.Runtime;
using System.Diagnostics.CodeAnalysis;
using RuntimeFeature = Microsoft.Android.Runtime.RuntimeFeature;

#if JAVA_INTEROP
namespace Android.Runtime {

	class AndroidRuntime : JniRuntime {

		public const string InternalDllName = RuntimeConstants.InternalDllName;

		internal AndroidRuntime (IntPtr jnienv,
				IntPtr vm,
				IntPtr classLoader,
				JniRuntime.JniTypeManager typeManager,
				JniRuntime.JniValueManager valueManager,
				bool jniAddNativeMethodRegistrationAttributePresent)
			: base (new AndroidRuntimeOptions (jnienv,
					vm,
					classLoader,
					typeManager,
					valueManager,
					jniAddNativeMethodRegistrationAttributePresent))
		{
			// This is not ideal, but we need to set this while the runtime is initializing but we can't do it directly from the `JNIEnvInit.Initialize` method, since
			// it lives in an assembly that does not reference Mono.Android.  So we do it here, because this class is instantiated by JNIEnvInit.Initialize.
			AndroidEnvironmentInternal.UnhandledExceptionHandler = AndroidEnvironment.UnhandledException;
		}

		public override void FailFast (string? message)
		{
			AndroidEnvironment.FailFast (message);
		}

		public override string GetCurrentManagedThreadName ()
		{
			return Thread.CurrentThread.Name!;
		}

		public override string GetCurrentManagedThreadStackTrace (int skipFrames, bool fNeedFileInfo)
		{
			return new StackTrace (skipFrames, fNeedFileInfo)
				.ToString ();
		}

		public override Exception? GetExceptionForThrowable (ref JniObjectReference reference, JniObjectReferenceOptions options)
		{
			if (!reference.IsValid)
				return null;
			var peeked      = JniEnvironment.Runtime.ValueManager.PeekPeer (reference);
			if (peeked is JavaProxyThrowable proxyThrowable) {
				JniObjectReference.Dispose (ref reference, options);
				return proxyThrowable.InnerException;
			}
			var peekedExc   = peeked as Exception;
			if (peekedExc == null) {
				var throwable = Java.Lang.Object.GetObject<Java.Lang.Throwable> (reference.Handle, JniHandleOwnership.DoNotTransfer);
				JniObjectReference.Dispose (ref reference, options);
				return throwable;
			}
			JniObjectReference.Dispose (ref reference, options);
			var unwrapped = JniEnvironment.Runtime.ValueManager.PeekValue (peeked!.PeerReference) as Exception;
			if (unwrapped != null) {
				return unwrapped;
			}
			return peekedExc;
		}

		public override void OnUserUnhandledException (ref JniTransition transition, Exception e)
		{
			// Raise the UnhandledExceptionRaiser event via TryRaiseUnhandledException().
			// If a subscriber sets Handled = true, the exception is considered handled
			// and we return without transitioning to JNI.
			// See: https://github.com/dotnet/android/issues/10654
			if (AndroidEnvironment.TryRaiseUnhandledException (e)) {
				return;
			}

			base.OnUserUnhandledException (ref transition, e);
		}

		public override void RaisePendingException (Exception pendingException)
		{
			var je  = pendingException as JavaException;
			if (je == null) {
				je  = JavaProxyThrowable.Create (pendingException);
			}
			JniEnvironment.Exceptions.Throw (je.PeerReference);
			GC.KeepAlive (je);
		}
	}

	class AndroidRuntimeOptions : JniRuntime.CreationOptions {
		public AndroidRuntimeOptions (IntPtr jnienv,
				IntPtr vm,
				IntPtr classLoader,
				JniRuntime.JniTypeManager typeManager,
				JniRuntime.JniValueManager valueManager,
				bool jniAddNativeMethodRegistrationAttributePresent)
		{
			EnvironmentPointer      = jnienv;
			ClassLoader             = new JniObjectReference (classLoader, JniObjectReferenceType.Global);
			InvocationPointer       = vm;
			ObjectReferenceManager  = new AndroidObjectReferenceManager ();
			TypeManager             = typeManager;
			ValueManager            = valueManager;
			JniAddNativeMethodRegistrationAttributePresent = jniAddNativeMethodRegistrationAttributePresent;
		}
	}

	internal class AndroidObjectReferenceManager : JniRuntime.JniObjectReferenceManager {
		public override int GlobalReferenceCount {
			get {return RuntimeNativeMethods._monodroid_gref_get ();}
		}

		public override int WeakGlobalReferenceCount {
			get {return RuntimeNativeMethods._monodroid_weak_gref_get ();}
		}

		public override JniObjectReference CreateLocalReference (JniObjectReference value, ref int localReferenceCount)
		{
			var r = base.CreateLocalReference (value, ref localReferenceCount);

			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogLocalRef) {
					var tname = Thread.CurrentThread.Name;
					var tid   = Thread.CurrentThread.ManagedThreadId;
					var from  = new StackTrace (true).ToString ();
					RuntimeNativeMethods._monodroid_lref_log_new (localReferenceCount, r.Handle, (byte) 'L', tname, tid, from, 1);
				}
			}

			return r;
		}

		public override void DeleteLocalReference (ref JniObjectReference value, ref int localReferenceCount)
		{
			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogLocalRef) {
					var tname = Thread.CurrentThread.Name;
					var tid   = Thread.CurrentThread.ManagedThreadId;
					var from  = new StackTrace (true).ToString ();
					RuntimeNativeMethods._monodroid_lref_log_delete (localReferenceCount - 1, value.Handle, (byte) 'L', tname, tid, from, 1);
				}
			}
			base.DeleteLocalReference (ref value, ref localReferenceCount);
		}

		public override void CreatedLocalReference (JniObjectReference value, ref int localReferenceCount)
		{
			base.CreatedLocalReference (value, ref localReferenceCount);
			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogLocalRef) {
					var tname = Thread.CurrentThread.Name;
					var tid   = Thread.CurrentThread.ManagedThreadId;
					var from  = new StackTrace (true).ToString ();
					RuntimeNativeMethods._monodroid_lref_log_new (localReferenceCount, value.Handle, (byte) 'L', tname, tid, from, 1);
				}
			}
		}

		public override IntPtr ReleaseLocalReference (ref JniObjectReference value, ref int localReferenceCount)
		{
			var r = base.ReleaseLocalReference (ref value, ref localReferenceCount);
			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogLocalRef) {
					var tname = Thread.CurrentThread.Name;
					var tid   = Thread.CurrentThread.ManagedThreadId;
					var from  = new StackTrace (true).ToString ();
					RuntimeNativeMethods._monodroid_lref_log_delete (localReferenceCount - 1, value.Handle, (byte) 'L', tname, tid, from, 1);
				}
			}
			return r;
		}

		public override bool LogGlobalReferenceMessages => Logger.LogGlobalRef;
		public override bool LogLocalReferenceMessages  => Logger.LogLocalRef;

		public override void WriteLocalReferenceLine (string format, params object?[] args)
		{
			if (!RuntimeFeature.ObjectReferenceLogging) {
				return;
			}
			if (!Logger.LogLocalRef) {
				return;
			}

			RuntimeNativeMethods._monodroid_gref_log ("[LREF] " + string.Format (CultureInfo.InvariantCulture, format, args));
			RuntimeNativeMethods._monodroid_gref_log ("\n");
		}

		public override void WriteGlobalReferenceLine (string format, params object?[] args)
		{
			if (!RuntimeFeature.ObjectReferenceLogging) {
				return;
			}
			if (!Logger.LogGlobalRef) {
				return;
			}

			RuntimeNativeMethods._monodroid_gref_log (string.Format (CultureInfo.InvariantCulture, format, args));
			RuntimeNativeMethods._monodroid_gref_log ("\n");
		}

		public override JniObjectReference CreateGlobalReference (JniObjectReference value)
		{
			var r = base.CreateGlobalReference (value);

			int gc;
			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogGlobalRef) {
					var ctype = GetObjectRefType (value.Type);
					var ntype = GetObjectRefType (r.Type);
					var tname = Thread.CurrentThread.Name;
					var tid   = Thread.CurrentThread.ManagedThreadId;
					var from  = new StackTrace (true).ToString ();
					gc = RuntimeNativeMethods._monodroid_gref_log_new (value.Handle, ctype, r.Handle, ntype, tname, tid, from, 1);
				} else {
					gc = RuntimeNativeMethods._monodroid_gref_inc ();
				}
			} else {
				// Duplicated intentionally: the trimmer removes the outer `if` block entirely when
				// ObjectReferenceLogging is disabled, so the counter increment must appear in both branches.
				gc = RuntimeNativeMethods._monodroid_gref_inc ();
			}

			if (gc >= JNIEnvInit.gref_gc_threshold) {
				Logger.Log (LogLevel.Warn, "monodroid-gc", gc + " outstanding GREFs. Performing a full GC!");
				System.GC.WaitForPendingFinalizers ();
				System.GC.Collect ();
			}

			return r;
		}

		static byte GetObjectRefType (JniObjectReferenceType type)
		{
			switch (type) {
				case JniObjectReferenceType.Invalid:	    return (byte) 'I';
				case JniObjectReferenceType.Local:        return (byte) 'L';
				case JniObjectReferenceType.Global:       return (byte) 'G';
				case JniObjectReferenceType.WeakGlobal:   return (byte) 'W';
				default:                                  return (byte) '*';
			}
		}

		public override void DeleteGlobalReference (ref JniObjectReference value)
		{
			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogGlobalRef) {
					var ctype = GetObjectRefType (value.Type);
					var tname = Thread.CurrentThread.Name;
					var tid   = Thread.CurrentThread.ManagedThreadId;
					var from  = new StackTrace (true).ToString ();
					RuntimeNativeMethods._monodroid_gref_log_delete (value.Handle, ctype, tname, tid, from, 1);
				} else {
					RuntimeNativeMethods._monodroid_gref_dec ();
				}
			} else {
				RuntimeNativeMethods._monodroid_gref_dec ();
			}

			base.DeleteGlobalReference (ref value);
		}

		public override JniObjectReference CreateWeakGlobalReference (JniObjectReference value)
		{
			var r = base.CreateWeakGlobalReference (value);

			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogGlobalRef) {
					var ctype = GetObjectRefType (value.Type);
					var ntype = GetObjectRefType (r.Type);
					var tname = Thread.CurrentThread.Name;
					var tid   = Thread.CurrentThread.ManagedThreadId;
					var from  = new StackTrace (true).ToString ();
					RuntimeNativeMethods._monodroid_weak_gref_new (value.Handle, ctype, r.Handle, ntype, tname, tid, from, 1);
				} else {
					RuntimeNativeMethods._monodroid_weak_gref_inc ();
				}
			} else {
				RuntimeNativeMethods._monodroid_weak_gref_inc ();
			}

			return r;
		}

		public override void DeleteWeakGlobalReference (ref JniObjectReference value)
		{
			if (RuntimeFeature.ObjectReferenceLogging) {
				if (Logger.LogGlobalRef) {
					var ctype = GetObjectRefType (value.Type);
					var tname = Thread.CurrentThread.Name;
					var tid   = Thread.CurrentThread.ManagedThreadId;
					var from  = new StackTrace (true).ToString ();
					RuntimeNativeMethods._monodroid_weak_gref_delete (value.Handle, ctype, tname, tid, from, 1);
				} else {
					RuntimeNativeMethods._monodroid_weak_gref_dec ();
				}
			} else {
				RuntimeNativeMethods._monodroid_weak_gref_dec ();
			}

			base.DeleteWeakGlobalReference (ref value);
		}
	}

	[RequiresDynamicCode ("This type manager is reflection-backed and is not compatible with Native AOT.")]
	[RequiresUnreferencedCode ("This type manager is reflection-backed and is not trimming-compatible.")]
	class AndroidTypeManager : JniRuntime.ReflectionJniTypeManager {
		bool jniAddNativeMethodRegistrationAttributePresent;

		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

		public AndroidTypeManager (bool jniAddNativeMethodRegistrationAttributePresent)
		{
			this.jniAddNativeMethodRegistrationAttributePresent = jniAddNativeMethodRegistrationAttributePresent;
		}

		protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
		{
			foreach (var ti in base.GetTypesForSimpleReference (jniSimpleReference))
				yield return ti;

			var t = Java.Interop.TypeManager.GetJavaToManagedType (jniSimpleReference);
			if (t != null)
				yield return t;
		}

		protected override Type? GetTypeForSimpleReference (string jniSimpleReference)
		{
			var type = base.GetTypeForSimpleReference (jniSimpleReference);
			if (type != null) {
				return type;
			}

			return Java.Interop.TypeManager.GetJavaToManagedType (jniSimpleReference);
		}

		protected override string? GetSimpleReference (Type type)
		{
			string? j = JNIEnv.TypemapManagedToJava (type);
			if (j != null) {
				return GetReplacementTypeCore (j) ?? j;
			}
			// Intentionally don't call base.GetSimpleReference(type): Android's
			// non-trimmable runtime uses the generated/registered typemap, not
			// Java.Interop's JniTypeSignatureAttribute fallback.
			return null;
		}

		protected override IEnumerable<string> GetSimpleReferences (Type type)
		{
			string? j = JNIEnv.TypemapManagedToJava (type);
			j	   = GetReplacementTypeCore (j) ?? j;

			if (j != null) {
				return [j];
			}
			// Keep this in sync with GetSimpleReference(): no base fallback.
			return [];
		}

		protected override IReadOnlyList<string>? GetStaticMethodFallbackTypesCore (string jniSimpleReference)
		{
			return JniRemappingLookup.GetStaticMethodFallbackTypes (jniSimpleReference, useReplacementTypes: true);
		}

		protected override string? GetReplacementTypeCore (string? jniSimpleReference)
		{
			return JniRemappingLookup.GetReplacementType (jniSimpleReference);
		}

		protected override JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfoCore (string jniSourceType, string jniMethodName, string jniMethodSignature)
		{
			return JniRemappingLookup.GetReplacementMethodInfo (jniSourceType, jniMethodName, jniMethodSignature);
		}

		protected override JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfoCore (string jniSourceType, ReadOnlySpan<char> jniMethodName, ReadOnlySpan<char> jniMethodSignature)
		{
			return JniRemappingLookup.GetReplacementMethodInfo (jniSourceType, jniMethodName, jniMethodSignature);
		}

		protected override Type? GetInvokerTypeCore (Type type)
		{
			if (type.IsInterface || type.IsAbstract) {
				return JavaObjectExtensions.GetInvokerType (type)
					?? base.GetInvokerTypeCore (type);
			}

			return null;
		}

		delegate Delegate GetCallbackHandler ();

		// [Export] callback delegates are created dynamically via DynamicCallbackCodeGenerator and are not
		// cached in static fields (unlike non-[Export] connector delegates). Without rooting them here,
		// CoreCLR's GC can collect them between JNI registration and first invocation, causing a crash.
		static readonly Lock prevent_delegate_gc_lock = new Lock ();
		static readonly List<Delegate> prevent_delegate_gc = new List<Delegate> ();
		static List<JniNativeMethodRegistration> sharedRegistrations = new List<JniNativeMethodRegistration> ();

		static bool FastRegisterNativeMembers (JniType nativeClass, Type type, ReadOnlySpan<char> methods)
		{
			if (!MagicRegistrationMap.Filled)
				return false;

			bool lockTaken = false;
			bool rv = false;

			try {
				Monitor.TryEnter (sharedRegistrations, ref lockTaken);
				List<JniNativeMethodRegistration> registrations;
				if (lockTaken) {
					sharedRegistrations.Clear ();
					registrations = sharedRegistrations;
				} else {
					registrations = new List<JniNativeMethodRegistration> ();
				}
				JniNativeMethodRegistrationArguments arguments = new JniNativeMethodRegistrationArguments (registrations, methods.ToString ());
				rv = MagicRegistrationMap.CallRegisterMethod (arguments, type.FullName!);

				if (registrations.Count > 0)
					nativeClass.RegisterNativeMethods (registrations.ToArray ());
			} finally {
				if (lockTaken) {
					Monitor.Exit (sharedRegistrations);
				}
			}

			return rv;
		}

		class MagicRegistrationMap {
#pragma warning disable CS0649 // Field is never assigned to;
			// assigned to in generated IL: https://github.com/dotnet/android/blob/cbfa7e20acebd37b52ec4de9d5c1a4a66ddda799/src/Xamarin.Android.Build.Tasks/Linker/MonoDroid.Tuner/MonoDroidMarkStep.cs#L204
			static Dictionary<string, int>? typesMap;
#pragma warning restore CS0649

			static void Prefill ()
			{
				// fill code added by the linker
			}

			static MagicRegistrationMap ()
			{
				Prefill ();
			}

			static public bool Filled {
				get {
					return typesMap != null && typesMap.Count > 0;
				}
			}

			internal static bool CallRegisterMethod (JniNativeMethodRegistrationArguments arguments, string typeName)
			{
				int idx = 0;

				if (typeName == null || !(typesMap?.TryGetValue (typeName, out idx) == true))
					return false;

				return CallRegisterMethodByIndex (arguments, idx);
			}

			static bool CallRegisterMethodByIndex (JniNativeMethodRegistrationArguments arguments, int? typeIdx)
			{
				// updated by the linker to register known types
				return false;
			}
		}

		[Obsolete ("Use RegisterNativeMembers(JniType, Type, ReadOnlySpan<char>) instead.")]
		public override void RegisterNativeMembers (JniType nativeClass, Type type, string? methods) =>
			RegisterNativeMembers (nativeClass, type, methods.AsSpan ());

		public override void RegisterNativeMembers (JniType nativeClass, Type type, ReadOnlySpan<char> methods)
		{
			try {
				if (methods.IsEmpty) {
					if (jniAddNativeMethodRegistrationAttributePresent) {
#pragma warning disable CS0618 // ReflectionJniTypeManager has not migrated its registration override to spans.
						base.RegisterNativeMembers (nativeClass, type, methods.ToString ());
#pragma warning restore CS0618
					}
					return;
				} else if (FastRegisterNativeMembers (nativeClass, type, methods)) {
					return;
				}

				int methodCount = CountMethods (methods);
				if (methodCount < 1) {
					if (jniAddNativeMethodRegistrationAttributePresent) {
#pragma warning disable CS0618 // ReflectionJniTypeManager has not migrated its registration override to spans.
						base.RegisterNativeMembers (nativeClass, type, methods.ToString ());
#pragma warning restore CS0618
					}
					return;
				}

				JniNativeMethodRegistration [] natives = new JniNativeMethodRegistration [methodCount];
				int nativesIndex = 0;
				MethodInfo []? typeMethods = null;

				ReadOnlySpan<char> methodsSpan = methods;
				bool needToRegisterNatives = false;

				while (!methodsSpan.IsEmpty) {
					int newLineIndex = methodsSpan.IndexOf ('\n');

					ReadOnlySpan<char> methodLine = methodsSpan.Slice (0, newLineIndex != -1 ? newLineIndex : methodsSpan.Length);
					if (!methodLine.IsEmpty) {
						SplitMethodLine (methodLine,
							out ReadOnlySpan<char> name,
							out ReadOnlySpan<char> signature,
							out ReadOnlySpan<char> callbackString,
							out ReadOnlySpan<char> callbackDeclaringTypeString);

						Delegate? callback = null;
						if (callbackString.SequenceEqual ("__export__")) {
							var mname = name.Slice (2);
							MethodInfo? minfo = null;
							typeMethods ??= type.GetMethods (BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
							foreach (var mi in typeMethods)
								if (mname.SequenceEqual (mi.Name) && signature.SequenceEqual (JavaNativeTypeManager.GetJniSignature (mi))) {
									minfo = mi;
									break;
								}

							if (minfo == null)
								throw new InvalidOperationException (FormattableString.Invariant ($"Specified managed method '{mname.ToString ()}' was not found. Signature: {signature.ToString ()}"));

							var exportAttribute = minfo.GetCustomAttribute<BaseExportAttribute> ()
								?? throw new InvalidOperationException (FormattableString.Invariant ($"Specified managed method '{mname.ToString ()}' does not have [Export] attribute. Signature: {signature.ToString ()}"));

							callback = exportAttribute.CreateDynamicCallback (minfo);
							lock (prevent_delegate_gc_lock) {
								prevent_delegate_gc.Add (callback);
							}
							needToRegisterNatives = true;
						} else {
							Type callbackDeclaringType = type;
							if (!callbackDeclaringTypeString.IsEmpty) {
								callbackDeclaringType = Type.GetType (callbackDeclaringTypeString.ToString (), throwOnError: true)!;
							}
							while (callbackDeclaringType.ContainsGenericParameters) {
								callbackDeclaringType = callbackDeclaringType.BaseType!;
							}

							GetCallbackHandler connector = (GetCallbackHandler) Delegate.CreateDelegate (typeof (GetCallbackHandler),
							                                                                             callbackDeclaringType, callbackString.ToString ());
							callback = connector ();
						}

						if (callback != null) {
							needToRegisterNatives = true;
							natives [nativesIndex++] = new JniNativeMethodRegistration (name.ToString (), signature.ToString (), callback);
						}
					}

					methodsSpan = newLineIndex != -1 ? methodsSpan.Slice (newLineIndex + 1) : default;
				}

				if (needToRegisterNatives) {
					JniEnvironment.Types.RegisterNatives (nativeClass.PeerReference, natives, nativesIndex);
				}
			} catch (Exception e) {
				JniEnvironment.Runtime.RaisePendingException (e);
			}
		}

		static int CountMethods (ReadOnlySpan<char> methodsSpan)
		{
			int count = 0;
			while (!methodsSpan.IsEmpty) {
				count++;

				int newLineIndex = methodsSpan.IndexOf ('\n');
				methodsSpan = newLineIndex != -1 ? methodsSpan.Slice (newLineIndex + 1) : default;
			}
			return count;
		}

		static void SplitMethodLine (
			ReadOnlySpan<char> methodLine,
			out ReadOnlySpan<char> name,
			out ReadOnlySpan<char> signature,
			out ReadOnlySpan<char> callback,
			out ReadOnlySpan<char> callbackDeclaringType)
		{
			int colonIndex = methodLine.IndexOf (':');
			name = methodLine.Slice (0, colonIndex);
			methodLine = methodLine.Slice (colonIndex + 1);

			colonIndex = methodLine.IndexOf (':');
			signature = methodLine.Slice (0, colonIndex);
			methodLine = methodLine.Slice (colonIndex + 1);

			colonIndex = methodLine.IndexOf (':');
			callback = methodLine.Slice (0, colonIndex != -1 ? colonIndex : methodLine.Length);

			callbackDeclaringType = colonIndex != -1 ? methodLine.Slice (colonIndex + 1) : default;
		}
	}

}
#endif // JAVA_INTEROP
