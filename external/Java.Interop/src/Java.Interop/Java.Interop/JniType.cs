using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

using Java.Interop;

namespace Java.Interop {

	public sealed class JniType : IDisposable {

		public static unsafe JniType DefineClass (string name, JniObjectReference loader, byte[] classFileData)
		{
			fixed (byte* buf = classFileData) {
				var lref = JniEnvironment.Types.DefineClass (name, loader, (IntPtr) buf, classFileData.Length);
				return new JniType (ref lref, JniHandleOwnership.Transfer);
			}
		}

		bool    registered;
		JniObjectReference  peer;

		public  JniObjectReference  PeerReference   {
			get {return peer;}
		}

		public JniType (string classname)
		{
			var peer    = JniEnvironment.Types.FindClass (classname);
			Initialize (ref peer, JniHandleOwnership.Transfer);
		}

		public JniType (ref JniObjectReference handle, JniHandleOwnership transfer)
		{
			Initialize (ref handle, transfer);
		}

		void Initialize (ref JniObjectReference handle, JniHandleOwnership transfer)
		{
			if (handle.Handle == IntPtr.Zero)
				throw new ArgumentException ("handle must be valid.", nameof (handle));
			try {
				peer    = handle.NewLocalRef ();
			} finally {
				JniEnvironment.References.Dispose (ref handle, transfer);
			}
		}

		public string Name {
			get {
				AssertValid ();

				return JniEnvironment.Types.GetJniTypeNameFromClass (PeerReference);
			}
		}

		public void RegisterWithVM ()
		{
			AssertValid ();

			if (registered)
				return;

			lock (this) {
				if (peer.Type != JniObjectReferenceType.Global) {
					var o           = peer;
					peer            = o.NewGlobalRef ();
					JniEnvironment.References.Dispose (ref o, JniHandleOwnership.Transfer);
				}
				JniEnvironment.Current.JavaVM.Track (this);
				registered = true;
			}
		}

		void AssertValid ()
		{
			if (PeerReference.Handle == IntPtr.Zero)
				throw new ObjectDisposedException (GetType ().FullName);
		}

		public static JniType GetCachedJniType (ref JniType cachedType, string classname)
		{
			if (cachedType != null && cachedType.PeerReference.Handle != IntPtr.Zero)
				return cachedType;
			var t = new JniType (classname);
			if (Interlocked.CompareExchange (ref cachedType, t, null) != null)
				t.Dispose ();
			cachedType.RegisterWithVM ();
			return cachedType;
		}

		public void Dispose ()
		{
			if (!PeerReference.IsValid)
				return;
			if (registered)
				JniEnvironment.Current.JavaVM.UnTrack (PeerReference.Handle);
			if (methods != null)
				UnregisterNativeMethods ();
			JniEnvironment.References.Dispose (ref peer);
		}

		public JniType GetSuperclass ()
		{
			AssertValid ();

			var lref = JniEnvironment.Types.GetSuperclass (PeerReference);
			if (lref.IsValid)
				return new JniType (ref lref, JniHandleOwnership.Transfer);
			return null;
		}

		public bool IsAssignableFrom (JniType c)
		{
			AssertValid ();

			if (c == null)
				throw new ArgumentNullException ("c");
			if (!c.PeerReference.IsValid)
				throw new ArgumentException ("'c' has an invalid handle.", "c");

			return JniEnvironment.Types.IsAssignableFrom (c.PeerReference, PeerReference);
		}

		public bool IsInstanceOfType (JniObjectReference value)
		{
			AssertValid ();

			return JniEnvironment.Types.IsInstanceOf (value, PeerReference);
		}

#pragma warning disable 0414
		// This isn't used anywhere; it's just present so that the GC won't collect the referenced delegates.
		JniNativeMethodRegistration[] methods;
#pragma warning restore 0414

		public void RegisterNativeMethods (params JniNativeMethodRegistration[] methods)
		{
			AssertValid ();

			if (methods == null)
				throw new ArgumentNullException ("methods");

			for (int i = 0; i < methods.Length; ++i) {
				methods [i].Marshaler = JniMarshalMethod.Wrap  (methods [i].Marshaler);
			}

			int r = JniEnvironment.Types.RegisterNatives (PeerReference, methods, checked ((int)methods.Length));
			if (r != 0)
				throw new JavaException ("Unable to register native methods.");
			// Prevents method delegates from being GC'd so long as this type remains
			this.methods = methods;
			RegisterWithVM ();
		}

		public void UnregisterNativeMethods ()
		{
			AssertValid ();

			JniEnvironment.Types.UnregisterNatives (PeerReference);
		}

		public JniInstanceMethodID GetConstructor (string signature)
		{
			AssertValid ();

			return JniEnvironment.InstanceMethods.GetMethodID (PeerReference, "<init>", signature);
		}

		public JniInstanceMethodID GetCachedConstructor (ref JniInstanceMethodID cachedMethod, string signature)
		{
			AssertValid ();

			return GetCachedInstanceMethod (ref cachedMethod, "<init>", signature);
		}

		public JniObjectReference AllocObject ()
		{
			AssertValid ();

			return JniEnvironment.Activator.AllocObject (PeerReference);
		}

		public unsafe JniObjectReference NewObject (JniInstanceMethodID constructor, JValue* @parameters)
		{
			AssertValid ();

			return JniEnvironment.Activator.NewObject (PeerReference, constructor, parameters);
		}

		public JniInstanceFieldID GetInstanceField (string name, string signature)
		{
			AssertValid ();

			return JniEnvironment.InstanceFields.GetFieldID (PeerReference, name, signature);
		}

		public JniInstanceFieldID GetCachedInstanceField (ref JniInstanceFieldID cachedField, string name, string signature)
		{
			AssertValid ();

			if (cachedField != null && cachedField.IsValid)
				return cachedField;
			var m = GetInstanceField (name, signature);
			if (Interlocked.CompareExchange (ref cachedField, m, null) != null) {
				// No cleanup required; let the GC collect the unused instance
			}
			return cachedField;
		}

		public JniStaticFieldID GetStaticField (string name, string signature)
		{
			AssertValid ();

			return JniEnvironment.StaticFields.GetStaticFieldID (PeerReference, name, signature);
		}

		public JniStaticFieldID GetCachedStaticField (ref JniStaticFieldID cachedField, string name, string signature)
		{
			AssertValid ();

			if (cachedField != null && cachedField.IsValid)
				return cachedField;
			var m = GetStaticField (name, signature);
			if (Interlocked.CompareExchange (ref cachedField, m, null) != null) {
				// No cleanup required; let the GC collect the unused instance
			}
			return cachedField;
		}

		public JniInstanceMethodID GetInstanceMethod (string name, string signature)
		{
			AssertValid ();

			return JniEnvironment.InstanceMethods.GetMethodID (PeerReference, name, signature);
		}

		public JniInstanceMethodID GetCachedInstanceMethod (ref JniInstanceMethodID cachedMethod, string name, string signature)
		{
			AssertValid ();

			if (cachedMethod != null && cachedMethod.IsValid)
				return cachedMethod;
			var m = GetInstanceMethod (name, signature);
			if (Interlocked.CompareExchange (ref cachedMethod, m, null) != null) {
				// No cleanup required; let the GC collect the unused instance
			}
			return cachedMethod;
		}

		public JniStaticMethodID GetStaticMethod (string name, string signature)
		{
			AssertValid ();

			return JniEnvironment.StaticMethods.GetStaticMethodID (PeerReference, name, signature);
		}

		public JniStaticMethodID GetCachedStaticMethod (ref JniStaticMethodID cachedMethod, string name, string signature)
		{
			AssertValid ();

			if (cachedMethod != null && cachedMethod.IsValid)
				return cachedMethod;
			var m = GetStaticMethod (name, signature);
			if (Interlocked.CompareExchange (ref cachedMethod, m, null) != null) {
				// No cleanup required; let the GC collect the unused instance
			}
			return cachedMethod;
		}
	}
}
