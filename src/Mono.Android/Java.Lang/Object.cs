using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

using Java.Interop;

using Android.Runtime;

namespace Java.Lang {

	[DataContract]
	public partial class Object : IDisposable, IJavaObject, IJavaObjectEx
#if JAVA_INTEROP
		, IJavaPeerable
#endif  // JAVA_INTEROP
	{

		static  Dictionary<IntPtr, List<WeakReference>>   instances   = new Dictionary<IntPtr, List<WeakReference>> (new InstancesKeyComparer ());

		IntPtr key_handle;
		IntPtr weak_handle;
		JObjectRefType handle_type;
		IntPtr handle;
		int refs_added;
		bool             needsActivation;
		bool             isProxy;

		IntPtr IJavaObjectEx.KeyHandle {
			get {return key_handle;}
			set {key_handle = value;}
		}

		bool IJavaObjectEx.IsProxy {
			get {return isProxy;}
			set {isProxy = value;}
		}

		bool IJavaObjectEx.NeedsActivation {
			get {return needsActivation;}
			set {needsActivation = true;}
		}

		IntPtr IJavaObjectEx.ToLocalJniHandle ()
		{
			lock (this) {
				if (handle == IntPtr.Zero)
					return handle;
				return JNIEnv.NewLocalRef (handle);
			}
		}

		~Object ()
		{
			if (Logger.LogGlobalRef) {
				JNIEnv._monodroid_gref_log (
						string.Format ("Finalizing handle 0x{0}\n", handle.ToString ("x")));
			}
			// FIXME: need hash cleanup mechanism.
			// Finalization occurs after a test of java persistence.  If the
			// handle still contains a java reference, we can't finalize the
			// object and should "resurrect" it.
			refs_added = 0;
			if (handle != IntPtr.Zero)
				GC.ReRegisterForFinalize (this);
			else {
				Dispose (false);
				DeregisterInstance (this, key_handle);
			}
		}

		public Object (IntPtr handle, JniHandleOwnership transfer)
		{
			// Check if handle was preset by our java activation mechanism
			if (this.handle != IntPtr.Zero) {
				needsActivation = true;
				handle = this.handle;
				if (handle_type != 0)
					return;
				transfer  = JniHandleOwnership.DoNotTransfer;
			}

			SetHandle (handle, transfer);
		}

		// Note: must be internal so that e.g. DataContractJsonSerializer will find it
		[OnDeserialized]
		[Preserve]
		internal void SetHandleOnDeserialized (StreamingContext context)
		{
			if (Handle != IntPtr.Zero)
				return;

			SetHandle (
					JNIEnv.StartCreateInstance (GetType (), "()V"),
					JniHandleOwnership.TransferLocalRef);
			JNIEnv.FinishCreateInstance (Handle, "()V");
		}

#if JAVA_INTEROP
		public int JniIdentityHashCode {
			get {return (int) key_handle;}
		}

		public JniObjectReference PeerReference {
			get {
				return new JniObjectReference (handle, (JniObjectReferenceType) handle_type);
			}
		}

		public virtual JniPeerMembers JniPeerMembers {
			get { return _members; }
		}
#endif  // JAVA_INTEROP

		public IntPtr Handle {
			get {
				if (weak_handle != IntPtr.Zero)
					Logger.Log (LogLevel.Warn, "Mono.Android.dll", "Accessing object which is out for collection via original handle");

				return handle;
			}
		}

		protected virtual IntPtr ThresholdClass {
			get { return Class.Object; }
		}

		protected virtual System.Type ThresholdType {
			get { return typeof (Java.Lang.Object); }
		}

		internal IntPtr GetThresholdClass ()
		{
			return ThresholdClass;
		}

		internal System.Type GetThresholdType ()
		{
			return ThresholdType;
		}

#if JAVA_INTEROP
		JniManagedPeerStates IJavaPeerable.JniManagedPeerState {
			get {
				var e = (IJavaObjectEx) this;
				var s = JniManagedPeerStates.None;
				if (e.IsProxy)
					s |= JniManagedPeerStates.Replaceable;
				if (e.NeedsActivation)
					s |= JniManagedPeerStates.Activatable;
				return s;
			}
		}

		void IJavaPeerable.DisposeUnlessReferenced ()
		{
			var p = PeekObject (handle);
			if (p == null) {
				Dispose ();
			}
		}

		public void UnregisterFromRuntime ()
		{
			DeregisterInstance (this, key_handle);
		}

		void IJavaPeerable.Disposed ()
		{
			throw new NotSupportedException ();
		}

		void IJavaPeerable.Finalized ()
		{
			throw new NotSupportedException ();
		}

		void IJavaPeerable.SetJniIdentityHashCode (int value)
		{
			key_handle  = (IntPtr) value;
		}

		void IJavaPeerable.SetJniManagedPeerState (JniManagedPeerStates value)
		{
			var e = (IJavaObjectEx) this;
			if ((value & JniManagedPeerStates.Replaceable) == JniManagedPeerStates.Replaceable)
				e.IsProxy = true;
			if ((value & JniManagedPeerStates.Activatable) == JniManagedPeerStates.Activatable)
				e.NeedsActivation = true;
		}

		void IJavaPeerable.SetPeerReference (JniObjectReference reference)
		{
			this.handle         = reference.Handle;
			this.handle_type    = (JObjectRefType) reference.Type;
		}
#endif  // JAVA_INTEROP


		public void Dispose ()
		{
			Dispose (true);
			Dispose (this, ref handle, key_handle, handle_type);
			GC.SuppressFinalize (this);
		}

		protected virtual void Dispose (bool disposing)
		{
		}

		internal static void Dispose (object instance, ref IntPtr handle, IntPtr key_handle, JObjectRefType handle_type)
		{
			if (handle == IntPtr.Zero)
				return;

			if (Logger.LogGlobalRef) {
				JNIEnv._monodroid_gref_log (
						string.Format ("Disposing handle 0x{0}\n", handle.ToString ("x")));
			}

			DeregisterInstance (instance, key_handle);

			switch (handle_type) {
				case JObjectRefType.Global:
					lock (instance) {
						JNIEnv.DeleteGlobalRef (handle);
						handle = IntPtr.Zero;
					}
					break;
				case JObjectRefType.WeakGlobal:
					lock (instance) {
						JNIEnv.DeleteWeakGlobalRef (handle);
						handle = IntPtr.Zero;
					}
					break;
				default:
					throw new InvalidOperationException ("Trying to dispose handle of type '" +
							handle_type + "' which is not supported.");
			}
		}

		protected void SetHandle (IntPtr value, JniHandleOwnership transfer)
		{
			RegisterInstance (this, value, transfer, out handle);
			handle_type = JObjectRefType.Global;
		}

		internal static void RegisterInstance (IJavaObject instance, IntPtr value, JniHandleOwnership transfer, out IntPtr handle)
		{
			if (value == IntPtr.Zero) {
				handle = value;
				return;
			}

			var transferType  = transfer & (JniHandleOwnership.DoNotTransfer | JniHandleOwnership.TransferLocalRef | JniHandleOwnership.TransferGlobalRef);
			switch (transferType) {
			case JniHandleOwnership.DoNotTransfer:
				handle = JNIEnv.NewGlobalRef (value);
				break;
			case JniHandleOwnership.TransferLocalRef:
				handle = JNIEnv.NewGlobalRef (value);
				JNIEnv.DeleteLocalRef (value);
				break;
			case JniHandleOwnership.TransferGlobalRef:
				handle = value;
				break;
			default:
				throw new ArgumentOutOfRangeException ("transfer", transfer, 
						"Invalid `transfer` value: " + transfer + " on type " + instance.GetType ());
			}
			if (handle == IntPtr.Zero)
				throw new InvalidOperationException ("Unable to allocate Global Reference for object '" + instance.ToString () + "'!");

			IntPtr key = JNIEnv.IdentityHash (handle);
			if ((transfer & JniHandleOwnership.DoNotRegister) == 0) {
				_RegisterInstance (instance, key, handle);
			}
			var ex = instance as IJavaObjectEx;
			if (ex != null)
				ex.KeyHandle = key;

			if (Logger.LogGlobalRef) {
				JNIEnv._monodroid_gref_log ("handle 0x" + handle.ToString ("x") +
						"; key_handle 0x" + key.ToString ("x") +
						": Java Type: `" + JNIEnv.GetClassNameFromInstance (handle) + "`; " +
						"MCW type: `" + instance.GetType ().FullName + "`\n");
			}
		}

		static void _RegisterInstance (IJavaObject instance, IntPtr key, IntPtr handle)
		{
			lock (instances) {
				List<WeakReference> wrefs;
				if (!instances.TryGetValue (key, out wrefs)) {
					wrefs = new List<WeakReference> (1) {
						new WeakReference (instance, true),
					};
					instances.Add (key, wrefs);
				}
				else {
					bool found = false;
					for (int i  = 0; i < wrefs.Count; ++i) {
						var wref  = wrefs [i];
						if (ShouldReplaceMapping (wref, handle)) {
							found = true;
							wrefs.Remove (wref);
							wrefs.Add (new WeakReference (instance, true));
							break;
						}
						var cur = wref == null ?  null        : (IJavaObject) wref.Target;
						var _c  = cur  == null ?  IntPtr.Zero : cur.Handle;
						if (_c != IntPtr.Zero && JNIEnv.IsSameObject (handle, _c)) {
							found = true;
							if (Logger.LogGlobalRef) {
								Logger.Log (LogLevel.Info, "monodroid-gref",
										string.Format ("warning: not replacing previous registered handle 0x{0} with handle 0x{1} for key_handle 0x{2}",
											_c.ToString ("x"), handle.ToString ("x"), key.ToString ("x")));
							}
							break;
						}
					}
					if (!found) {
						wrefs.Add (new WeakReference (instance, true));
					}
				}
			}
		}

		static bool ShouldReplaceMapping (WeakReference current, IntPtr handle)
		{
			if (current == null)
				return true;

			// Target has been GC'd; see also FIXME, above, in finalizer
			object target = current.Target;
			if (target == null)
				return true;

			// It's possible that the instance was GC'd, but the finalizer
			// hasn't executed yet, so the `instances` entry is stale.
			var ijo = (IJavaObject) target;
			if (ijo.Handle == IntPtr.Zero)
				return true;

			if (!JNIEnv.IsSameObject (ijo.Handle, handle))
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
			var ex = target as IJavaObjectEx;
			if (ex != null && ex.IsProxy)
				return true;

			return false;
		}

		internal static void DeregisterInstance (object instance, IntPtr key_handle)
		{
			lock (instances) {
				List<WeakReference> wrefs;
				if (instances.TryGetValue (key_handle, out wrefs)) {
					for (int i  = wrefs.Count-1; i >= 0; --i) {
						var wref  = wrefs [i];
						if (wref.Target == null || wref.Target == instance) {
							wrefs.RemoveAt (i);
						}
					}
					if (wrefs.Count == 0)
						instances.Remove (key_handle);
				}
			}
		}

		internal static List<WeakReference> GetSurfacedObjects_ForDiagnosticsOnly ()
		{
			lock (instances) {
				var surfaced = new List<WeakReference> (instances.Count);
				foreach (var e in instances) {
					surfaced.AddRange (e.Value);
				}
				return surfaced;
			}
		}

		internal static IJavaObject PeekObject (IntPtr handle)
		{
			if (handle == IntPtr.Zero)
				return null;

			lock (instances) {
				List<WeakReference> wrefs;
				if (instances.TryGetValue (JNIEnv.IdentityHash (handle), out wrefs)) {
					for (int i  = 0; i < wrefs.Count; ++i) {
						var wref  = wrefs [i];
						IJavaObject res = wref.Target as IJavaObject;
						if (res != null && res.Handle != IntPtr.Zero && JNIEnv.IsSameObject (handle, res.Handle))
							return res;
					}
				}
			}
			return null;
		}

		public static T GetObject<T> (IntPtr jnienv, IntPtr handle, JniHandleOwnership transfer)
			where T : class, IJavaObject
		{
			JNIEnv.CheckHandle (jnienv);
			return GetObject<T> (handle, transfer);
		}

		public static T GetObject<T> (IntPtr handle, JniHandleOwnership transfer)
			where T : class, IJavaObject
		{
			return _GetObject<T>(handle, transfer);
		}

		internal static T _GetObject<T> (IntPtr handle, JniHandleOwnership transfer)
		{
			if (handle == IntPtr.Zero)
				return default (T);

			return (T) GetObject (handle, transfer, typeof (T));
		}

		internal static IJavaObject GetObject (IntPtr handle, JniHandleOwnership transfer, Type type = null)
		{
			if (handle == IntPtr.Zero)
				return null;

			lock (instances) {
				List<WeakReference> wrefs;
				if (instances.TryGetValue (JNIEnv.IdentityHash (handle), out wrefs)) {
					for (int i = 0; i < wrefs.Count; ++i) {
						var wref    = wrefs [i];
						var result  = wref.Target as IJavaObject;
						var exists  = result != null && result.Handle != IntPtr.Zero && JNIEnv.IsSameObject (handle, result.Handle);
						if (exists) {
							if (type == null ? true : type.IsAssignableFrom (result.GetType ())) {
								JNIEnv.DeleteRef (handle, transfer);
								return result;
							}
							/*
							Logger.Log (LogLevel.Warn, "*jonp*", "# jonp: Object.GetObject: handle=0x" + handle.ToString ("x") + " found but is of type '" + result.GetType ().FullName +
									"' and not the required targetType of '" + type + "'.");
							 */
						}
					}
				}
			}

			return Java.Interop.TypeManager.CreateInstance (handle, transfer, type);
		}

		public T[] ToArray<T>()
		{
			return JNIEnv.GetArray<T>(Handle);
		}

		public static Java.Lang.Object FromArray<T>(T[] value)
		{
			if (value == null)
				return null;
			return new Java.Lang.Object (JNIEnv.NewArray (value), JniHandleOwnership.TransferLocalRef);
		}

		public static implicit operator Java.Lang.Object (bool value)
		{
			return new Java.Lang.Boolean (value);
		}

		public static implicit operator Java.Lang.Object (sbyte value)
		{
			return new Java.Lang.Byte (value);
		}

		public static implicit operator Java.Lang.Object (char value)
		{
			return new Java.Lang.Character (value);
		}

		public static implicit operator Java.Lang.Object (int value)
		{
			return new Java.Lang.Integer (value);
		}

		public static implicit operator Java.Lang.Object (long value)
		{
			return new Java.Lang.Long (value);
		}

		public static implicit operator Java.Lang.Object (float value)
		{
			return new Java.Lang.Float (value);
		}

		public static implicit operator Java.Lang.Object (double value)
		{
			return new Java.Lang.Double (value);
		}

		public static implicit operator Java.Lang.Object (string value)
		{
			if (value == null)
				return null;
			return new Java.Lang.ICharSequenceInvoker (JNIEnv.NewString (value), JniHandleOwnership.TransferLocalRef);
		}

		public static explicit operator bool (Java.Lang.Object value)
		{
			return Convert.ToBoolean (value);
		}

		public static explicit operator sbyte (Java.Lang.Object value)
		{
			return Convert.ToSByte (value);
		}

		public static explicit operator char (Java.Lang.Object value)
		{
			return Convert.ToChar (value);
		}

		public static explicit operator int (Java.Lang.Object value)
		{
			return Convert.ToInt32 (value);
		}

		public static explicit operator long (Java.Lang.Object value)
		{
			return Convert.ToInt64 (value);
		}

		public static explicit operator float (Java.Lang.Object value)
		{
			return Convert.ToSingle (value);
		}

		public static explicit operator double (Java.Lang.Object value)
		{
			return Convert.ToDouble (value);
		}

		public static explicit operator string (Java.Lang.Object value)
		{
			if (value == null)
				return null;
			return Convert.ToString (value);
		}

		public static implicit operator Java.Lang.Object (Java.Lang.Object[] value)
		{
			if (value == null)
				return null;
			return new Java.Lang.Object (JNIEnv.NewArray (value), JniHandleOwnership.TransferLocalRef);
		}

		public static implicit operator Java.Lang.Object (bool[] value)
		{
			if (value == null)
				return null;
			return new Java.Lang.Object (JNIEnv.NewArray (value), JniHandleOwnership.TransferLocalRef);
		}

		public static implicit operator Java.Lang.Object (byte[] value)
		{
			if (value == null)
				return null;
			return new Java.Lang.Object (JNIEnv.NewArray (value), JniHandleOwnership.TransferLocalRef);
		}

		public static implicit operator Java.Lang.Object (char[] value)
		{
			if (value == null)
				return null;
			return new Java.Lang.Object (JNIEnv.NewArray (value), JniHandleOwnership.TransferLocalRef);
		}

		public static implicit operator Java.Lang.Object (int[] value)
		{
			if (value == null)
				return null;
			return new Java.Lang.Object (JNIEnv.NewArray (value), JniHandleOwnership.TransferLocalRef);
		}

		public static implicit operator Java.Lang.Object (long[] value)
		{
			if (value == null)
				return null;
			return new Java.Lang.Object (JNIEnv.NewArray (value), JniHandleOwnership.TransferLocalRef);
		}

		public static implicit operator Java.Lang.Object (float[] value)
		{
			if (value == null)
				return null;
			return new Java.Lang.Object (JNIEnv.NewArray (value), JniHandleOwnership.TransferLocalRef);
		}

		public static implicit operator Java.Lang.Object (double[] value)
		{
			if (value == null)
				return null;
			return new Java.Lang.Object (JNIEnv.NewArray (value), JniHandleOwnership.TransferLocalRef);
		}

		public static implicit operator Java.Lang.Object (string[] value)
		{
			if (value == null)
				return null;
			return new Java.Lang.Object (JNIEnv.NewArray (value), JniHandleOwnership.TransferLocalRef);
		}

		public static explicit operator Java.Lang.Object[] (Java.Lang.Object value)
		{
			if (value == null)
				return null;
			return value.ToArray<Java.Lang.Object>();
		}

		public static explicit operator bool[] (Java.Lang.Object value)
		{
			if (value == null)
				return null;
			return value.ToArray<bool>();
		}

		public static explicit operator byte[] (Java.Lang.Object value)
		{
			if (value == null)
				return null;
			return value.ToArray<byte>();
		}

		public static explicit operator char[] (Java.Lang.Object value)
		{
			if (value == null)
				return null;
			return value.ToArray<char>();
		}

		public static explicit operator int[] (Java.Lang.Object value)
		{
			if (value == null)
				return null;
			return value.ToArray<int>();
		}

		public static explicit operator long[] (Java.Lang.Object value)
		{
			if (value == null)
				return null;
			return value.ToArray<long>();
		}

		public static explicit operator float[] (Java.Lang.Object value)
		{
			if (value == null)
				return null;
			return value.ToArray<float>();
		}

		public static explicit operator double[] (Java.Lang.Object value)
		{
			if (value == null)
				return null;
			return value.ToArray<double>();
		}

		public static explicit operator string[] (Java.Lang.Object value)
		{
			if (value == null)
				return null;
			return value.ToArray<string>();
		}
	}

	class InstancesKeyComparer : IEqualityComparer<IntPtr> {

		public bool Equals (IntPtr x, IntPtr y)
		{
			return x == y;
		}

		public int GetHashCode (IntPtr value)
		{
			return value.GetHashCode ();
		}
	}
}
