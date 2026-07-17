#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Java.Interop {

	[JniTypeSignature (JniTypeName, GenerateJavaPeer=false)]
	sealed class JavaProxyObject : JavaObject, IEquatable<JavaProxyObject>
	{
		internal const string JniTypeName = "net/dot/jni/internal/JavaProxyObject";

		static  readonly    JniPeerMembers                                  _members        = new JniPeerMembers (JniTypeName, typeof (JavaProxyObject));
		static  readonly    ConditionalWeakTable<object, JavaProxyObject>   CachedValues    = new ConditionalWeakTable<object, JavaProxyObject> ();
		static              bool                                            nativeMethodsRegistered;

		static unsafe void RegisterNativeMethods ()
		{
			using (var proxyType = new JniType ("net/dot/jni/internal/JavaProxyObject"u8)) {
				Span<JniNativeMethod> methods = stackalloc JniNativeMethod [3];
				fixed (byte* equalsName = "equals"u8, equalsSignature = "(Ljava/lang/Object;)Z"u8)
				fixed (byte* hashCodeName = "hashCode"u8, hashCodeSignature = "()I"u8)
				fixed (byte* toStringName = "toString"u8, toStringSignature = "()Ljava/lang/String;"u8) {
					methods [0] = new JniNativeMethod (equalsName, equalsSignature,
						(IntPtr) (delegate* unmanaged<IntPtr, IntPtr, IntPtr, byte>) &Equals);
					methods [1] = new JniNativeMethod (hashCodeName, hashCodeSignature,
						(IntPtr) (delegate* unmanaged<IntPtr, IntPtr, int>) &GetHashCode);
					methods [2] = new JniNativeMethod (toStringName, toStringSignature,
						(IntPtr) (delegate* unmanaged<IntPtr, IntPtr, IntPtr>) &ToString);
					JniEnvironment.Types.RegisterNatives (proxyType.PeerReference, methods);
				}
			}
			nativeMethodsRegistered = true;
		}

		public override JniPeerMembers JniPeerMembers {
			get {
				return _members;
			}
		}

		JavaProxyObject (object value)
		{
			if (value == null)
				throw new ArgumentNullException (nameof (value));
			Value = value;
		}

		public object Value {get; private set;}

		public override int GetHashCode ()
		{
			return Value.GetHashCode ();
		}

		public override bool Equals (object? obj)
		{
			if (obj is JavaProxyObject other)
				return object.Equals (Value, other.Value);
			return object.Equals (Value, obj);
		}

		public bool Equals (JavaProxyObject? other) => object.Equals (Value, other?.Value);

		public override string? ToString ()
		{
			return Value.ToString ();
		}

		[return: NotNullIfNotNull ("object")]
		public static JavaProxyObject? GetProxy (object value)
		{
			if (value == null)
				return null;

			lock (CachedValues) {
				if (CachedValues.TryGetValue (value, out var proxy))
					return proxy;
				// Register before JavaObject's constructor allocates the Java peer.
				if (!nativeMethodsRegistered)
					RegisterNativeMethods ();
				proxy = new JavaProxyObject (value);
				CachedValues.Add (value, proxy);
				return proxy;
			}
		}

		[UnmanagedCallersOnly]
		static byte Equals (IntPtr jnienv, IntPtr n_self, IntPtr n_value)
		{
			var envp = new JniTransition (jnienv);
			try {
				var self    = (JavaProxyObject?) JniEnvironment.Runtime.ValueManager.PeekPeer (new JniObjectReference (n_self));
				var r_value = new JniObjectReference (n_value);
				var value   = JniEnvironment.Runtime.ValueManager.GetValue (ref r_value, JniObjectReferenceOptions.Copy);
				return self?.Equals (value) == true ? (byte) 1 : (byte) 0;
			}
			catch (Exception e) when (JniEnvironment.Runtime.ExceptionShouldTransitionToJni (e)) {
				envp.SetPendingException (e);
				return 0;
			}
			finally {
				envp.Dispose ();
			}
		}

		[UnmanagedCallersOnly]
		static int GetHashCode (IntPtr jnienv, IntPtr n_self)
		{
			var envp = new JniTransition (jnienv);
			try {
				var self = (JavaProxyObject?) JniEnvironment.Runtime.ValueManager.PeekPeer (new JniObjectReference (n_self));
				return self?.GetHashCode () ?? 0;
			}
			catch (Exception e) when (JniEnvironment.Runtime.ExceptionShouldTransitionToJni (e)) {
				envp.SetPendingException (e);
				return 0;
			}
			finally {
				envp.Dispose ();
			}
		}

		[UnmanagedCallersOnly]
		static IntPtr ToString (IntPtr jnienv, IntPtr n_self)
		{
			var envp = new JniTransition (jnienv);
			try {
				var self    = (JavaProxyObject?) JniEnvironment.Runtime.ValueManager.PeekPeer (new JniObjectReference (n_self));
				var s       = self?.ToString ();
				var r       = JniEnvironment.Strings.NewString (s);
				try {
					return JniEnvironment.References.NewReturnToJniRef (r);
				} finally {
					JniObjectReference.Dispose (ref r);
				}
			}
			catch (Exception e) when (JniEnvironment.Runtime.ExceptionShouldTransitionToJni (e)) {
				envp.SetPendingException (e);
				return IntPtr.Zero;
			}
			finally {
				envp.Dispose ();
			}
		}
	}
}
