#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

using Java.Interop;

namespace Java.Interop {

	public sealed class JniType : IDisposable {

		[return: NotNullIfNotNull ("classFileData")]
		public static unsafe JniType? DefineClass (string name, JniObjectReference loader, byte[] classFileData)
		{
			if (classFileData == null)
				return null;
			fixed (byte* buf = classFileData) {
				var lref = JniEnvironment.Types.DefineClass (name, loader, (IntPtr) buf, classFileData.Length);
				return new JniType (ref lref, JniObjectReferenceOptions.CopyAndDispose);
			}
		}

#if NET
		public static bool TryParse (string name, [NotNullWhen (true)] out JniType? type)
		{
			if (!JniEnvironment.Types.TryFindClass (name, out var peerReference)) {
				type    = null;
				return false;
			}
			type    = new JniType (ref peerReference, JniObjectReferenceOptions.CopyAndDispose);
			return true;
		}
#endif  // NET

		bool    registered;
		JniObjectReference  peerReference;

		public  JniObjectReference  PeerReference   {
			get {return peerReference;}
		}

		public JniType (string classname)
		{
			var peer    = JniEnvironment.Types.FindClass (classname);
			Initialize (ref peer, JniObjectReferenceOptions.CopyAndDispose);
		}

		public JniType (ref JniObjectReference peerReference, JniObjectReferenceOptions transfer)
		{
			Initialize (ref peerReference, transfer);
		}

		void Initialize (ref JniObjectReference peerReference, JniObjectReferenceOptions transfer)
		{
			if (!peerReference.IsValid)
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

				return JniEnvironment.Types.GetJniTypeNameFromClass (PeerReference)!;
			}
		}

		public override string ToString ()
		{
			return $"JniType(Name='{Name}' PeerReference={PeerReference})";
		}

		public void RegisterWithRuntime ()
		{
			AssertValid ();

			if (registered)
				return;

			JniEnvironment.Runtime.Track (this);
			registered = true;
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		void AssertValid ()
		{
			if (!PeerReference.IsValid)
				throw new ObjectDisposedException (GetType ().FullName);
		}

		public static JniType GetCachedJniType ([NotNull] ref JniType? cachedType, string classname)
		{
			if (cachedType != null && cachedType.PeerReference.IsValid)
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

		public JniType? GetSuperclass ()
		{
			AssertValid ();

			var lref = JniEnvironment.Types.GetSuperclass (PeerReference);
			if (lref.IsValid)
				return new JniType (ref lref, JniObjectReferenceOptions.CopyAndDispose);
			return null;
		}

		public bool IsAssignableFrom (JniType c)
		{
			AssertValid ();

			if (c == null)
				throw new ArgumentNullException (nameof (c));
			if (!c.PeerReference.IsValid)
				throw new ArgumentException ("'c' has an invalid handle.", nameof (c));

			return JniEnvironment.Types.IsAssignableFrom (c.PeerReference, PeerReference);
		}

		public bool IsInstanceOfType (JniObjectReference value)
		{
			AssertValid ();

			return JniEnvironment.Types.IsInstanceOf (value, PeerReference);
		}

#pragma warning disable 0414
		// This isn't used anywhere; it's just present so that the GC won't collect the referenced delegates.
		JniNativeMethodRegistration[]? methods;
#pragma warning restore 0414

		public void RegisterNativeMethods (params JniNativeMethodRegistration[] methods)
		{
			AssertValid ();

			if (methods == null)
				throw new ArgumentNullException (nameof (methods));

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

		public JniMethodInfo GetCachedConstructor ([NotNull] ref JniMethodInfo? cachedMethod, string signature)
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

		public JniFieldInfo GetCachedInstanceField ([NotNull] ref JniFieldInfo? cachedField, string name, string signature)
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

		public JniFieldInfo GetCachedStaticField ([NotNull] ref JniFieldInfo? cachedField, string name, string signature)
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

#if NET
		internal bool TryGetInstanceMethod (string name, string signature, [NotNullWhen(true)] out JniMethodInfo? method)
		{
			AssertValid ();

			IntPtr thrown;
			method  = null;
			var id  = NativeMethods.java_interop_jnienv_get_method_id (JniEnvironment.EnvironmentPointer, out thrown, PeerReference.Handle, name, signature);
			if (thrown != IntPtr.Zero) {
				JniEnvironment.Exceptions.ExceptionClear ();
				NativeMethods.java_interop_jnienv_delete_local_ref (JniEnvironment.EnvironmentPointer, thrown);
				return false;
			}
			if (id == IntPtr.Zero) {
				// …huh?  Should only happen if `thrown != IntPtr.Zero`, handled above.
				return false;
			}
			method  = new JniMethodInfo (name, signature, id, isStatic: false);
			return true;
		}
#endif  // NET

		public JniMethodInfo GetCachedInstanceMethod ([NotNull] ref JniMethodInfo? cachedMethod, string name, string signature)
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

#if NET
		internal bool TryGetStaticMethod (string name, string signature, [NotNullWhen(true)] out JniMethodInfo? method)
		{
			AssertValid ();

			IntPtr thrown;
			method  = null;
			var id  = NativeMethods.java_interop_jnienv_get_static_method_id (JniEnvironment.EnvironmentPointer, out thrown, PeerReference.Handle, name, signature);
			if (thrown != IntPtr.Zero) {
				JniEnvironment.Exceptions.ExceptionClear ();
				NativeMethods.java_interop_jnienv_delete_local_ref (JniEnvironment.EnvironmentPointer, thrown);
				return false;
			}
			if (id == IntPtr.Zero) {
				// …huh?  Should only happen if `thrown != IntPtr.Zero`, handled above.
				return false;
			}
			method  = new JniMethodInfo (name, signature, id, isStatic: true);
			return true;
		}
#endif  // NET

		public JniMethodInfo GetCachedStaticMethod ([NotNull] ref JniMethodInfo? cachedMethod, string name, string signature)
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
