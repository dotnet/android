using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Reflection;

using Java.Interop;
using Java.Interop.Tools.TypeNameMappings;

#if JAVA_INTEROP
namespace Android.Runtime {

	class AndroidRuntime : JniRuntime {

		internal AndroidRuntime (IntPtr jnienv,
				IntPtr vm,
				bool allocNewObjectSupported,
				IntPtr classLoader,
				IntPtr classLoader_loadClass,
				bool jniAddNativeMethodRegistrationAttributePresent)
			: base (new AndroidRuntimeOptions (jnienv,
					vm,
					allocNewObjectSupported,
					classLoader,
					classLoader_loadClass,
					jniAddNativeMethodRegistrationAttributePresent))
		{
		}

		public override void FailFast (string message)
		{
			AndroidEnvironment.FailFast (message);
		}

		public override string GetCurrentManagedThreadName ()
		{
			return Thread.CurrentThread.Name;
		}

		public override string GetCurrentManagedThreadStackTrace (int skipFrames, bool fNeedFileInfo)
		{
			return new StackTrace (skipFrames, fNeedFileInfo)
				.ToString ();
		}

		public override Exception GetExceptionForThrowable (ref JniObjectReference reference, JniObjectReferenceOptions options)
		{
			if (!reference.IsValid)
				return null;
			var peeked      = JNIEnv.AndroidValueManager.PeekPeer (reference);
			var peekedExc   = peeked as Exception;
			if (peekedExc == null) {
				var throwable = Java.Lang.Object.GetObject<Java.Lang.Throwable> (reference.Handle, JniHandleOwnership.DoNotTransfer);
				JniObjectReference.Dispose (ref reference, options);
				return throwable;
			}
			JniObjectReference.Dispose (ref reference, options);
			var unwrapped = JNIEnv.AndroidValueManager.UnboxException (peeked);
			if (unwrapped != null) {
				return unwrapped;
			}
			return peekedExc;
		}

		public override void RaisePendingException (Exception pendingException)
		{
			var je  = pendingException as JavaProxyThrowable;
			if (je == null) {
				je  = new JavaProxyThrowable (pendingException);
			}
			var r = new JniObjectReference (je.Handle);
			JniEnvironment.Exceptions.Throw (r);
		}
	}

	class AndroidRuntimeOptions : JniRuntime.CreationOptions {
		public AndroidRuntimeOptions (IntPtr jnienv,
				IntPtr vm,
				bool allocNewObjectSupported,
				IntPtr classLoader,
				IntPtr classLoader_loadClass,
				bool jniAddNativeMethodRegistrationAttributePresent)
		{
			EnvironmentPointer      = jnienv;
			ClassLoader             = new JniObjectReference (classLoader, JniObjectReferenceType.Global);
			ClassLoader_LoadClass_id= classLoader_loadClass;
			InvocationPointer       = vm;
			NewObjectRequired       = !allocNewObjectSupported;
			ObjectReferenceManager  = new AndroidObjectReferenceManager ();
			TypeManager             = new AndroidTypeManager (jniAddNativeMethodRegistrationAttributePresent);
			ValueManager            = new AndroidValueManager ();
			UseMarshalMemberBuilder = false;
			JniAddNativeMethodRegistrationAttributePresent = jniAddNativeMethodRegistrationAttributePresent;
		}
	}

	class AndroidObjectReferenceManager : JniRuntime.JniObjectReferenceManager {

		[DllImport ("__Internal", CallingConvention = CallingConvention.Cdecl)]
		static extern int _monodroid_gref_get ();

		public override int GlobalReferenceCount {
			get {return _monodroid_gref_get ();}
		}

		public override int WeakGlobalReferenceCount {
			get {return -1;}
		}

		public override JniObjectReference CreateLocalReference (JniObjectReference value, ref int localReferenceCount)
		{
			var r = base.CreateLocalReference (value, ref localReferenceCount);

			if (Logger.LogLocalRef) {
				var tname = Thread.CurrentThread.Name;
				var tid   = Thread.CurrentThread.ManagedThreadId;;
				var from  = new StringBuilder (new StackTrace (true).ToString ());
				JNIEnv._monodroid_lref_log_new (localReferenceCount, r.Handle, (byte) 'L', tname, tid, from, 1);
			}

			return r;
		}

		public override void DeleteLocalReference (ref JniObjectReference value, ref int localReferenceCount)
		{
			if (Logger.LogLocalRef) {
				var tname = Thread.CurrentThread.Name;
				var tid   = Thread.CurrentThread.ManagedThreadId;;
				var from  = new StringBuilder (new StackTrace (true).ToString ());
				JNIEnv._monodroid_lref_log_delete (localReferenceCount-1, value.Handle, (byte) 'L', tname, tid, from, 1);
			}
			base.DeleteLocalReference (ref value, ref localReferenceCount);
		}

		public override void CreatedLocalReference (JniObjectReference value, ref int localReferenceCount)
		{
			base.CreatedLocalReference (value, ref localReferenceCount);
			if (Logger.LogLocalRef) {
				var tname = Thread.CurrentThread.Name;
				var tid   = Thread.CurrentThread.ManagedThreadId;;
				var from  = new StringBuilder (new StackTrace (true).ToString ());
				JNIEnv._monodroid_lref_log_new (localReferenceCount, value.Handle, (byte) 'L', tname, tid, from, 1);
			}
		}

		public override IntPtr ReleaseLocalReference (ref JniObjectReference value, ref int localReferenceCount)
		{
			var r = base.ReleaseLocalReference (ref value, ref localReferenceCount);
			if (Logger.LogLocalRef) {
				var tname = Thread.CurrentThread.Name;
				var tid   = Thread.CurrentThread.ManagedThreadId;;
				var from  = new StringBuilder (new StackTrace (true).ToString ());
				JNIEnv._monodroid_lref_log_delete (localReferenceCount-1, value.Handle, (byte) 'L', tname, tid, from, 1);
			}
			return r;
		}

		public override void WriteGlobalReferenceLine (string format, params object[] args)
		{
			JNIEnv._monodroid_gref_log (string.Format (format, args));
		}

		public override JniObjectReference CreateGlobalReference (JniObjectReference value)
		{
			var r     = base.CreateGlobalReference (value);

			var log		= Logger.LogGlobalRef;
			var ctype	= log ? GetObjectRefType (value.Type) : (byte) '*';
			var ntype	= log ? GetObjectRefType (r.Type) : (byte) '*';
			var tname = log ? Thread.CurrentThread.Name : null;
			var tid   = log ? Thread.CurrentThread.ManagedThreadId : 0;
			var from  = log ? new StringBuilder (new StackTrace (true).ToString ()) : null;
			int gc 		= JNIEnv._monodroid_gref_log_new (value.Handle, ctype, r.Handle, ntype, tname, tid, from, 1);
			if (gc >= JNIEnv.gref_gc_threshold) {
				Logger.Log (LogLevel.Info, "monodroid-gc", gc + " outstanding GREFs. Performing a full GC!");
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
			var log		= Logger.LogGlobalRef;
			var ctype	= log ? GetObjectRefType (value.Type) : (byte) '*';
			var tname = log ? Thread.CurrentThread.Name : null;
			var tid   = log ? Thread.CurrentThread.ManagedThreadId : 0;
			var from  = log ? new StringBuilder (new StackTrace (true).ToString ()) : null;
			JNIEnv._monodroid_gref_log_delete (value.Handle, ctype, tname, tid, from, 1);

			base.DeleteGlobalReference (ref value);
		}

		public override JniObjectReference CreateWeakGlobalReference (JniObjectReference value)
		{
			var r = base.CreateWeakGlobalReference (value);

			var log		= Logger.LogGlobalRef;
			var ctype	= log ? GetObjectRefType (value.Type) : (byte) '*';
			var ntype	= log ? GetObjectRefType (r.Type) : (byte) '*';
			var tname = log ? Thread.CurrentThread.Name : null;
			var tid   = log ? Thread.CurrentThread.ManagedThreadId : 0;
			var from  = log ? new StringBuilder (new StackTrace (true).ToString ()) : null;
			JNIEnv._monodroid_weak_gref_new (value.Handle, ctype, r.Handle, ntype, tname, tid, from, 1);

			return r;
		}

		public override void DeleteWeakGlobalReference (ref JniObjectReference value)
		{
			var log		= Logger.LogGlobalRef;
			var ctype	= log ? GetObjectRefType (value.Type) : (byte) '*';
			var tname = log ? Thread.CurrentThread.Name : null;
			var tid   = log ? Thread.CurrentThread.ManagedThreadId : 0;
			var from  = log ? new StringBuilder (new StackTrace (true).ToString ()) : null;
			JNIEnv._monodroid_weak_gref_delete (value.Handle, ctype, tname, tid, from, 1);

			base.DeleteWeakGlobalReference (ref value);
		}
	}

	class AndroidTypeManager : JniRuntime.JniTypeManager {
		bool jniAddNativeMethodRegistrationAttributePresent;

		public AndroidTypeManager (bool jniAddNativeMethodRegistrationAttributePresent)
		{
			this.jniAddNativeMethodRegistrationAttributePresent = jniAddNativeMethodRegistrationAttributePresent;
		}

		protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
		{
			var t = Java.Interop.TypeManager.GetJavaToManagedType (jniSimpleReference);
			if (t == null)
				return base.GetTypesForSimpleReference (jniSimpleReference);
			return base.GetTypesForSimpleReference (jniSimpleReference)
				.Concat (Enumerable.Repeat (t, 1));
		}

		protected override string GetSimpleReference (Type type)
		{
			string j = JNIEnv.TypemapManagedToJava (type);
			if (j != null) {
				return j;
			}
			if (JNIEnv.IsRunningOnDesktop) {
				return JavaNativeTypeManager.ToJniName (type);
			}
			return null;
		}

		protected override IEnumerable<string> GetSimpleReferences (Type type)
		{
			string j = JNIEnv.TypemapManagedToJava (type);
			if (j != null) {
				yield return j;
			}
			if (JNIEnv.IsRunningOnDesktop) {
				yield return JavaNativeTypeManager.ToJniName (type);
			}
		}

		delegate Delegate GetCallbackHandler ();

		static MethodInfo dynamic_callback_gen;

		static Delegate CreateDynamicCallback (MethodInfo method)
		{
			if (dynamic_callback_gen == null) {
				var assembly = Assembly.Load ("Mono.Android.Export");
				if (assembly == null)
					throw new InvalidOperationException ("To use methods marked with ExportAttribute, Mono.Android.Export.dll needs to be referenced in the application");
				var type = assembly.GetType ("Java.Interop.DynamicCallbackCodeGenerator");
				if (type == null)
					throw new InvalidOperationException ("The referenced Mono.Android.Export.dll does not match the expected version. The required type was not found.");
				dynamic_callback_gen = type.GetMethod ("Create");
				if (dynamic_callback_gen == null)
					throw new InvalidOperationException ("The referenced Mono.Android.Export.dll does not match the expected version. The required method was not found.");
			}
			return (Delegate)dynamic_callback_gen.Invoke (null, new object [] { method });
		}

		static List<JniNativeMethodRegistration> sharedRegistrations = new List<JniNativeMethodRegistration> ();

		static bool FastRegisterNativeMembers (JniType nativeClass, Type type, string methods)
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
				JniNativeMethodRegistrationArguments arguments = new JniNativeMethodRegistrationArguments (registrations, methods);
				rv = MagicRegistrationMap.CallRegisterMethod (arguments, type.FullName);

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
#pragma warning disable CS0649 // Field is never assigned to
			static Dictionary<string, int> typesMap;
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
				int idx;

				if (typeName == null || !typesMap.TryGetValue (typeName, out idx))
					return false;

				return CallRegisterMethodByIndex (arguments, idx);
			}

			static bool CallRegisterMethodByIndex (JniNativeMethodRegistrationArguments arguments, int typeIdx)
			{
				// updated by the linker to register known types
				return false;
			}
		}

		public override void RegisterNativeMembers (JniType nativeClass, Type type, string methods)
		{
			if (FastRegisterNativeMembers (nativeClass, type, methods))
				return;

			if (string.IsNullOrEmpty (methods)) {
				if (jniAddNativeMethodRegistrationAttributePresent)
					base.RegisterNativeMembers (nativeClass, type, methods);
				return;
			}

			string[] members = methods.Split ('\n');
			if (members.Length < 2) {
				if (jniAddNativeMethodRegistrationAttributePresent)
					base.RegisterNativeMembers (nativeClass, type, methods);
				return;
			}

			JniNativeMethodRegistration[] natives = new JniNativeMethodRegistration [members.Length-1];
			for (int i = 0; i < members.Length; ++i) {
				string method = members [i];
				if (string.IsNullOrEmpty (method))
					continue;
				string[] toks = members [i].Split (new[]{':'}, 4);
				Delegate callback;
				if (toks [2] == "__export__") {
					var mname = toks [0].Substring (2);
					var minfo = type.GetMethods (BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Where (m => m.Name == mname && JavaNativeTypeManager.GetJniSignature (m) == toks [1]).FirstOrDefault ();
					if (minfo == null)
						throw new InvalidOperationException (String.Format ("Specified managed method '{0}' was not found. Signature: {1}", mname, toks [1]));
					callback = CreateDynamicCallback (minfo);
				} else {
					Type callbackDeclaringType = type;
					if (toks.Length == 4) {
						callbackDeclaringType = Type.GetType (toks [3], throwOnError: true);
					}
					while (callbackDeclaringType.ContainsGenericParameters) {
						callbackDeclaringType = callbackDeclaringType.BaseType;
					}
					GetCallbackHandler connector = (GetCallbackHandler) Delegate.CreateDelegate (typeof (GetCallbackHandler),
						callbackDeclaringType, toks [2]);
					callback = connector ();
				}
				natives [i] = new JniNativeMethodRegistration (toks [0], toks [1], callback);
			}

			JniEnvironment.Types.RegisterNatives (nativeClass.PeerReference, natives, natives.Length);
		}
	}

	class AndroidValueManager : JniRuntime.JniValueManager {

		Dictionary<IntPtr, IdentityHashTargets>         instances       = new Dictionary<IntPtr, IdentityHashTargets> ();

		public override void WaitForGCBridgeProcessing ()
		{
			JNIEnv.WaitForBridgeProcessing ();
		}

		public override IJavaPeerable CreatePeer (ref JniObjectReference reference, JniObjectReferenceOptions options, Type targetType)
		{
			if (!reference.IsValid)
				return null;

			var peer        = Java.Interop.TypeManager.CreateInstance (reference.Handle, JniHandleOwnership.DoNotTransfer, targetType) as IJavaPeerable;
			JniObjectReference.Dispose (ref reference, options);
			return peer;
		}

		public override void AddPeer (IJavaPeerable value)
		{
			if (value == null)
				throw new ArgumentNullException (nameof (value));
			if (!value.PeerReference.IsValid)
				throw new ArgumentException ("Must have a valid JNI object reference!", nameof (value));

			var reference       = value.PeerReference;
			var hash            = JNIEnv.IdentityHash (reference.Handle);

			AddPeer (value, reference, hash);
		}

		internal void AddPeer (IJavaPeerable value, JniObjectReference reference, IntPtr hash)
		{
			lock (instances) {
				IdentityHashTargets targets;
				if (!instances.TryGetValue (hash, out targets)) {
					targets = new IdentityHashTargets (value);
					instances.Add (hash, targets);
					return;
				}
				bool found = false;
				for (int i = 0; i < targets.Count; ++i) {
					IJavaPeerable target;
					var wref = targets [i];
					if (ShouldReplaceMapping (wref, reference, out target)) {
						found = true;
						targets [i] = IdentityHashTargets.CreateWeakReference (value);
						break;
					}
					if (JniEnvironment.Types.IsSameObject (value.PeerReference, target.PeerReference)) {
						found = true;
						if (Logger.LogGlobalRef) {
							Logger.Log (LogLevel.Info, "monodroid-gref",
									string.Format ("warning: not replacing previous registered handle {0} with handle {1} for key_handle 0x{2}",
										target.PeerReference.ToString (), reference.ToString (), hash.ToString ("x")));
						}
					}
				}
				if (!found) {
					targets.Add (value);
				}
			}
		}

		internal void AddPeer (IJavaPeerable value, IntPtr handle, JniHandleOwnership transfer, out IntPtr handleField)
		{
			if (handle == IntPtr.Zero) {
				handleField = handle;
				return;
			}

			var transferType = transfer & (JniHandleOwnership.DoNotTransfer | JniHandleOwnership.TransferLocalRef | JniHandleOwnership.TransferGlobalRef);
			switch (transferType) {
				case JniHandleOwnership.DoNotTransfer:
					handleField = JNIEnv.NewGlobalRef (handle);
					break;
				case JniHandleOwnership.TransferLocalRef:
					handleField = JNIEnv.NewGlobalRef (handle);
					JNIEnv.DeleteLocalRef (handle);
					break;
				case JniHandleOwnership.TransferGlobalRef:
					handleField = handle;
					break;
				default:
					throw new ArgumentOutOfRangeException ("transfer", transfer,
							"Invalid `transfer` value: " + transfer + " on type " + value.GetType ());
			}
			if (handleField == IntPtr.Zero)
				throw new InvalidOperationException ("Unable to allocate Global Reference for object '" + value.ToString () + "'!");

			IntPtr hash = JNIEnv.IdentityHash (handleField);
			value.SetJniIdentityHashCode ((int) hash);
			if ((transfer & JniHandleOwnership.DoNotRegister) == 0) {
				AddPeer (value, new JniObjectReference (handleField, JniObjectReferenceType.Global), hash);
			}

			if (Logger.LogGlobalRef) {
				JNIEnv._monodroid_gref_log ("handle 0x" + handleField.ToString ("x") +
						"; key_handle 0x" + hash.ToString ("x") +
						": Java Type: `" + JNIEnv.GetClassNameFromInstance (handleField) + "`; " +
						"MCW type: `" + value.GetType ().FullName + "`\n");
			}
		}

		bool ShouldReplaceMapping (WeakReference<IJavaPeerable> current, JniObjectReference reference, out IJavaPeerable target)
		{
			target      = null;

			if (current == null)
				return true;

			// Target has been GC'd; see also FIXME, above, in finalizer
			if (!current.TryGetTarget (out target) || target == null)
				return true;

			// It's possible that the instance was GC'd, but the finalizer
			// hasn't executed yet, so the `instances` entry is stale.
			if (!target.PeerReference.IsValid)
				return true;

			if (!JniEnvironment.Types.IsSameObject (target.PeerReference, reference))
				return false;

			// JNIEnv.NewObject/JNIEnv.CreateInstance() compatibility.
			// When two MCW's are created for one Java instance [0],
			// we want the 2nd MCW to replace the 1st, as the 2nd is
			// the one the dev created; the 1st is an implicit intermediary.
			//
			// [0]: If Java ctor invokes overridden virtual method, we'll
			// transition into managed code w/o a registered instance, and
			// thus will create an "intermediary" via
			// (IntPtr, JniHandleOwnership) .ctor.
			if ((target.JniManagedPeerState & JniManagedPeerStates.Replaceable) == JniManagedPeerStates.Replaceable)
				return true;

			return false;
		}

		public override void RemovePeer (IJavaPeerable value)
		{
			if (value == null)
				throw new ArgumentNullException (nameof (value));
			if (!value.PeerReference.IsValid)
				throw new ArgumentException ("Must have a valid JNI object reference!", nameof (value));

			var reference       = value.PeerReference;
			var hash            = JNIEnv.IdentityHash (reference.Handle);

			RemovePeer (value, hash);
		}

		internal void RemovePeer (IJavaPeerable value, IntPtr hash)
		{
			lock (instances) {
				IdentityHashTargets targets;
				if (!instances.TryGetValue (hash, out targets)) {
					return;
				}
				for (int i = targets.Count - 1; i >= 0; i--) {
					var wref = targets [i];
					if (!wref.TryGetTarget (out IJavaPeerable target)) {
						// wref is invalidated; remove it.
						targets.RemoveAt (i);
						continue;
					}
					if (!object.ReferenceEquals (target, value)) {
						continue;
					}
					targets.RemoveAt (i);
				}
				if (targets.Count == 0) {
					instances.Remove (hash);
				}
			}
		}

		public override IJavaPeerable PeekPeer (JniObjectReference reference)
		{
			if (!reference.IsValid)
				return null;

			var hash    = JNIEnv.IdentityHash (reference.Handle);
			lock (instances) {
				IdentityHashTargets targets;
				if (instances.TryGetValue (hash, out targets)) {
					for (int i = targets.Count - 1; i >= 0; i--) {
						var wref    = targets [i];
						if (!wref.TryGetTarget (out var result) || !result.PeerReference.IsValid) {
							targets.RemoveAt (i);
							continue;
						}
						if (!JniEnvironment.Types.IsSameObject (reference, result.PeerReference))
							continue;
						return result;
					}
				}
			}
			return null;
		}

		protected override bool TryUnboxPeerObject (IJavaPeerable value, out object result)
		{
			var proxy = value as JavaProxyThrowable;
			if (proxy != null) {
				result  = proxy.InnerException;
				return true;
			}
			return base.TryUnboxPeerObject (value, out result);
		}

		internal Exception UnboxException (IJavaPeerable value)
		{
			object r;
			if (TryUnboxPeerObject (value, out r) && r is Exception e) {
				return e;
			}
			return null;
		}

		public override void CollectPeers ()
		{
			GC.Collect ();
		}

		public override void FinalizePeer (IJavaPeerable value)
		{
			if (value == null)
				throw new ArgumentNullException (nameof (value));

			if (Logger.LogGlobalRef) {
				JNIEnv._monodroid_gref_log ($"Finalizing handle {value.PeerReference}\n");
			}

			// FIXME: need hash cleanup mechanism.
			// Finalization occurs after a test of java persistence.  If the
			// handle still contains a java reference, we can't finalize the
			// object and should "resurrect" it.
			if (value.PeerReference.IsValid) {
				GC.ReRegisterForFinalize (value);
			} else {
				RemovePeer (value, (IntPtr) value.JniIdentityHashCode);
				value.SetPeerReference (new JniObjectReference ());
				value.Finalized ();
			}
		}

		public override List<JniSurfacedPeerInfo> GetSurfacedPeers ()
		{
			lock (instances) {
				var surfacedPeers = new List<JniSurfacedPeerInfo> (instances.Count);
				foreach (var e in instances) {
					for (int i = 0; i < e.Value.Count; i++) {
						var value = e.Value [i];
						surfacedPeers.Add (new JniSurfacedPeerInfo (e.Key.ToInt32 (), value));
					}
				}
				return surfacedPeers;
			}
		}
	}

	class InstancesKeyComparer : IEqualityComparer<IntPtr>
	{

		public bool Equals (IntPtr x, IntPtr y)
		{
			return x == y;
		}

		public int GetHashCode (IntPtr value)
		{
			return value.GetHashCode ();
		}
	}

	class IdentityHashTargets {
		WeakReference<IJavaPeerable>            first;
		List<WeakReference<IJavaPeerable>>      rest;

		public static WeakReference<IJavaPeerable> CreateWeakReference (IJavaPeerable value)
		{
			return new WeakReference<IJavaPeerable> (value, trackResurrection: true);
		}

		public IdentityHashTargets (IJavaPeerable value)
		{
			first   = CreateWeakReference (value);
		}

		public int Count => (first != null ? 1 : 0) + (rest != null ? rest.Count : 0);

		public WeakReference<IJavaPeerable> this [int index] {
			get {
				if (index == 0)
					return first;
				index -= 1;
				if (rest == null || index >= rest.Count)
					return null;
				return rest [index];
			}
			set {
				if (index == 0) {
					first = value;
					return;
				}
				index -= 1;
				rest [index] = value;
			}
		}

		public void Add (IJavaPeerable value)
		{
			if (first == null) {
				first   = CreateWeakReference (value);
				return;
			}
			if (rest == null)
				rest    = new List<WeakReference<IJavaPeerable>> ();
			rest.Add (CreateWeakReference (value));
		}

		public void RemoveAt (int index)
		{
			if (index == 0) {
				first   = null;
				if (rest?.Count > 0) {
					first   = rest [0];
					rest.RemoveAt (0);
				}
				return;
			}
			index -= 1;
			rest.RemoveAt (index);
		}
	}
}
#endif // JAVA_INTEROP
