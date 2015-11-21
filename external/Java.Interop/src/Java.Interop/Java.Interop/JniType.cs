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
				return new JniType (ref lref, JniObjectReferenceOptions.DisposeSourceReference);
			}
		}

		bool    registered;
		JniObjectReference  peerReference;

		public  JniObjectReference  PeerReference   {
			get {return peerReference;}
		}

		public JniType (string classname)
		{
			var peer    = JniEnvironment.Types.FindClass (classname);
			Initialize (ref peer, JniObjectReferenceOptions.DisposeSourceReference);
		}

		public JniType (ref JniObjectReference peerReference, JniObjectReferenceOptions transfer)
		{
			Initialize (ref peerReference, transfer);
		}

		void Initialize (ref JniObjectReference peerReference, JniObjectReferenceOptions transfer)
		{
			if (peerReference.Handle == IntPtr.Zero)
				throw new ArgumentException ("handle must be valid.", nameof (peerReference));
			try {
				this.peerReference  = peerReference.NewGlobalRef ();
			} finally {
				JniObjectReference.Dispose (ref peerReference, transfer);
			}
		}

		public string Name {
			get {
				AssertValid ();

				return JniEnvironment.Types.GetJniTypeNameFromClass (PeerReference);
			}
		}

#if XA_INTEGRATION
		internal
#else   // !XA_INTEGRATION
		public
#endif  // !XA_INTEGRATION
		void RegisterWithRuntime ()
		{
			AssertValid ();

			if (registered)
				return;

			JniEnvironment.Runtime.Track (this);
			registered = true;
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
			cachedType.RegisterWithRuntime ();
			return cachedType;
		}

		public void Dispose ()
		{
			if (!PeerReference.IsValid)
				return;
			if (registered)
				JniEnvironment.Runtime.UnTrack (PeerReference.Handle);
			if (methods != null)
				UnregisterNativeMethods ();
			JniObjectReference.Dispose (ref peerReference);
		}

		public JniType GetSuperclass ()
		{
			AssertValid ();

			var lref = JniEnvironment.Types.GetSuperclass (PeerReference);
			if (lref.IsValid)
				return new JniType (ref lref, JniObjectReferenceOptions.DisposeSourceReference);
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

#if !XA_INTEGRATION
			for (int i = 0; i < methods.Length; ++i) {
				methods [i].Marshaler = JniMarshalMethod.Wrap  (methods [i].Marshaler);
			}
#endif  // !XA_INTEGRATION

			JniEnvironment.Types.RegisterNatives (PeerReference, methods, checked ((int)methods.Length));
			// Prevents method delegates from being GC'd so long as this type remains
			this.methods = methods;
			RegisterWithRuntime ();
		}

		public void UnregisterNativeMethods ()
		{
			AssertValid ();

			JniEnvironment.Types.UnregisterNatives (PeerReference);
		}

		public JniMethodInfo GetConstructor (string signature)
		{
			AssertValid ();

			return JniEnvironment.InstanceMethods.GetMethodID (PeerReference, "<init>", signature);
		}

		public JniMethodInfo GetCachedConstructor (ref JniMethodInfo cachedMethod, string signature)
		{
			AssertValid ();

			return GetCachedInstanceMethod (ref cachedMethod, "<init>", signature);
		}

		public JniObjectReference AllocObject ()
		{
			AssertValid ();

			return JniEnvironment.Object.AllocObject (PeerReference);
		}

		public unsafe JniObjectReference NewObject (JniMethodInfo constructor, JniArgumentValue* @parameters)
		{
			AssertValid ();

			return JniEnvironment.Object.NewObject (PeerReference, constructor, parameters);
		}

		public JniFieldInfo GetInstanceField (string name, string signature)
		{
			AssertValid ();

			return JniEnvironment.InstanceFields.GetFieldID (PeerReference, name, signature);
		}

		public JniFieldInfo GetCachedInstanceField (ref JniFieldInfo cachedField, string name, string signature)
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

		public JniFieldInfo GetStaticField (string name, string signature)
		{
			AssertValid ();

			return JniEnvironment.StaticFields.GetStaticFieldID (PeerReference, name, signature);
		}

		public JniFieldInfo GetCachedStaticField (ref JniFieldInfo cachedField, string name, string signature)
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

		public JniMethodInfo GetInstanceMethod (string name, string signature)
		{
			AssertValid ();

			return JniEnvironment.InstanceMethods.GetMethodID (PeerReference, name, signature);
		}

		public JniMethodInfo GetCachedInstanceMethod (ref JniMethodInfo cachedMethod, string name, string signature)
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

		public JniMethodInfo GetStaticMethod (string name, string signature)
		{
			AssertValid ();

			return JniEnvironment.StaticMethods.GetStaticMethodID (PeerReference, name, signature);
		}

		public JniMethodInfo GetCachedStaticMethod (ref JniMethodInfo cachedMethod, string name, string signature)
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
