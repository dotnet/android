#nullable enable

using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Text;
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

		public static bool TryParse (string name, [NotNullWhen (true)] out JniType? type)
		{
			if (!JniEnvironment.Types.TryFindClass (name, out var peerReference)) {
				type    = null;
				return false;
			}
			type    = new JniType (ref peerReference, JniObjectReferenceOptions.CopyAndDispose);
			return true;
		}

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

		/// <safety>
		/// A non-null UTF-8 class name must be readable through its NUL terminator and unchanged
		/// until return or exception. Read-only aliases may overlap; no alias may write or
		/// free the bytes during lookup. The pointer is not retained or freed. JNI runs on
		/// the current attached thread; the owning runtime must not be disposed concurrently.
		/// </safety>
		internal unsafe JniType (byte* classname)
		{
			if (classname == null)
				throw new ArgumentNullException (nameof (classname));

			JniObjectReference peer;
			unsafe {
				// SAFETY: the caller retains bytes through the NUL; FindClass borrows them synchronously, including its fallback path.
				var name = MemoryMarshal.CreateReadOnlySpanFromNullTerminated (classname);
				peer = JniEnvironment.Types.FindClass (name);
			}
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

		/// <safety>
		/// On a cache miss, a non-null UTF-8 class name must be readable through its NUL terminator
		/// and unchanged until return or exception; no alias may write or free it during lookup.
		/// No name pointer is retained or freed. Do not race runtime or cached-type disposal
		/// with lookup; JNI uses the current attached thread. A cache hit does not read the name.
		/// </safety>
		internal static unsafe JniType GetCachedJniType ([NotNull] ref JniType? cachedType, byte* classname)
		{
			if (cachedType != null && cachedType.PeerReference.IsValid)
				return cachedType;
			JniType t;
			unsafe {
				// SAFETY: the caller retains the class name during construction; only the resulting JNI type is cached.
				t = new JniType (classname);
			}
			if (Interlocked.CompareExchange (ref cachedType, t, null) != null)
				t.Dispose ();
			cachedType.RegisterWithRuntime ();
			return cachedType;
		}

		/// <remarks>
		/// Prevent new calls to the registered native methods and wait for callbacks already
		/// running to finish before disposing this type.
		/// </remarks>
		public void Dispose ()
		{
			if (!PeerReference.IsValid)
				return;
			if (methods != null)
				UnregisterNativeMethods ();
			if (registered)
				JniEnvironment.Runtime.UnTrack (PeerReference.Handle);
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

		JniNativeMethodRegistration[]? methods;

		/// <remarks>
		/// After successful registration, retains the original array without copying it.
		/// A failed registration does not retain the supplied array, even if JNI installed
		/// some callbacks before reporting the failure. Registration exceptions propagate
		/// without attempting to roll back partial native registration.
		/// </remarks>
		/// <safety>
		/// The caller must not start another JNI registration on this instance before
		/// successful unregistration, including after a failed or partial JNI attempt.
		/// This is not checked: only the latest successfully registered array is retained,
		/// so registering again can allow earlier delegates to be collected while JNI still uses them.
		/// Do not modify <paramref name="methods"/> or its entries through any alias during
		/// this call. After successful registration, ownership of the array transfers to this
		/// instance: do not modify it until successful unregistration and completion of all
		/// callbacks. If registration fails, the caller must keep the delegates strongly
		/// reachable until successful unregistration and completion of all callbacks.
		/// The caller must ensure each Marshaler delegate in <paramref name="methods"/> has
		/// parameter and return types, calling convention, and marshaling attributes compatible
		/// with its JNI method descriptor, including the leading JNIEnv and receiver/class pointers.
		/// Managed by-ref parameters must not be used to receive JNI scalar values.
		/// This method retains delegates but does not validate their ABI compatibility.
		/// </safety>
		[RequiresDynamicCode ("Native method registration via JniNativeMethodRegistration[] requires dynamic code generation. Use the blittable RegisterNatives(JniObjectReference, ReadOnlySpan<JniNativeMethod>) overload with statically-compiled function pointers for Native AOT compatibility.")]
		public unsafe void RegisterNativeMethods (params JniNativeMethodRegistration[] methods)
		{
			AssertValid ();

			if (methods == null)
				throw new ArgumentNullException (nameof (methods));

			unsafe {
				// SAFETY: Caller guarantees callback ABI and roots delegates on failure; success retains the array.
				JniEnvironment.Types.RegisterNatives (PeerReference, methods, checked ((int)methods.Length));
			}
			this.methods = methods;
			RegisterWithRuntime ();
		}

		/// <remarks>
		/// Prevent new calls to the registered native methods and wait for callbacks already
		/// running to finish before calling this method.
		/// </remarks>
		public void UnregisterNativeMethods ()
		{
			AssertValid ();

			JniEnvironment.Types.UnregisterNatives (PeerReference);
			methods = null;
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

		internal bool TryGetInstanceMethod (string name, string signature, [NotNullWhen(true)] out JniMethodInfo? method)
		{
			AssertValid ();

			IntPtr thrown;
			method  = null;
			var env = JniEnvironment.EnvironmentPointer;
			var id  = RawGetMethodID (env, name, signature, out thrown);
			if (thrown != IntPtr.Zero) {
				JniEnvironment.Exceptions.ExceptionClear ();
				JniEnvironment.References.RawDeleteLocalRef (env, thrown);
				return false;
			}
			Debug.Assert (id != IntPtr.Zero);
			if (id == IntPtr.Zero) {
				// …huh?  Should only happen if `thrown != IntPtr.Zero`, handled above.
				return false;
			}
			method  = new JniMethodInfo (name, signature, id, isStatic: false);
			return true;
		}

		IntPtr RawGetMethodID (IntPtr env, string name, string signature, out IntPtr thrown)
		{
			var _name = Marshal.StringToCoTaskMemUTF8 (name);
			var _sig  = Marshal.StringToCoTaskMemUTF8 (signature);
			try {
				var id      = JniNativeMethods.GetMethodID (env, PeerReference.Handle, _name, _sig);
				thrown      = JniNativeMethods.ExceptionOccurred (env);
				return id;
			}
			finally {
				Marshal.ZeroFreeCoTaskMemUTF8 (_name);
				Marshal.ZeroFreeCoTaskMemUTF8 (_sig);
			}
		}

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

		internal bool TryGetStaticMethod (string name, string signature, [NotNullWhen(true)] out JniMethodInfo? method)
		{
			AssertValid ();

			IntPtr thrown;
			method  = null;
			var env = JniEnvironment.EnvironmentPointer;
			var id  = RawGetStaticMethodID (env, name, signature, out thrown);
			if (thrown != IntPtr.Zero) {
				JniEnvironment.Exceptions.ExceptionClear ();
				JniEnvironment.References.RawDeleteLocalRef (env, thrown);
				return false;
			}
			Debug.Assert (id != IntPtr.Zero);
			if (id == IntPtr.Zero) {
				// …huh?  Should only happen if `thrown != IntPtr.Zero`, handled above.
				return false;
			}
			method  = new JniMethodInfo (name, signature, id, isStatic: true);
			return true;
		}

		IntPtr RawGetStaticMethodID (IntPtr env, string name, string signature, out IntPtr thrown)
		{
			var _name = Marshal.StringToCoTaskMemUTF8 (name);
			var _sig  = Marshal.StringToCoTaskMemUTF8 (signature);
			try {
				var id      = JniNativeMethods.GetStaticMethodID (env, PeerReference.Handle, _name, _sig);
				thrown      = JniNativeMethods.ExceptionOccurred (env);
				return id;
			}
			finally {
				Marshal.ZeroFreeCoTaskMemUTF8 (_name);
				Marshal.ZeroFreeCoTaskMemUTF8 (_sig);
			}
		}

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

		/// <summary>
		/// Creates a <see cref="JniType"/> from a null-terminated UTF-8 class name span.
		/// Use with <c>"java/lang/Object"u8</c> literals to avoid string marshalling overhead.
		/// </summary>
		public JniType (ReadOnlySpan<byte> classname)
		{
			var peer = JniEnvironment.Types.FindClass (classname);
			Initialize (ref peer, JniObjectReferenceOptions.CopyAndDispose);
		}

		public static JniType GetCachedJniType ([NotNull] ref JniType? cachedType, ReadOnlySpan<byte> classname)
		{
			if (cachedType != null && cachedType.PeerReference.IsValid)
				return cachedType;
			var t = new JniType (classname);
			if (Interlocked.CompareExchange (ref cachedType, t, null) != null)
				t.Dispose ();
			cachedType.RegisterWithRuntime ();
			return cachedType;
		}

		public JniMethodInfo GetConstructor (ReadOnlySpan<byte> signature)
		{
			AssertValid ();

			return JniEnvironment.InstanceMethods.GetMethodID (PeerReference, "<init>"u8, signature);
		}

		public JniMethodInfo GetCachedConstructor ([NotNull] ref JniMethodInfo? cachedMethod, ReadOnlySpan<byte> signature)
		{
			AssertValid ();

			return GetCachedInstanceMethod (ref cachedMethod, "<init>"u8, signature);
		}

		public JniFieldInfo GetInstanceField (ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
		{
			AssertValid ();

			return JniEnvironment.InstanceFields.GetFieldID (PeerReference, name, signature);
		}

		public JniFieldInfo GetCachedInstanceField ([NotNull] ref JniFieldInfo? cachedField, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
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

		public JniFieldInfo GetStaticField (ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
		{
			AssertValid ();

			return JniEnvironment.StaticFields.GetStaticFieldID (PeerReference, name, signature);
		}

		public JniFieldInfo GetCachedStaticField ([NotNull] ref JniFieldInfo? cachedField, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
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

		public JniMethodInfo GetInstanceMethod (ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
		{
			AssertValid ();

			return JniEnvironment.InstanceMethods.GetMethodID (PeerReference, name, signature);
		}

		public JniMethodInfo GetCachedInstanceMethod ([NotNull] ref JniMethodInfo? cachedMethod, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
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

		public JniMethodInfo GetStaticMethod (ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
		{
			AssertValid ();

			return JniEnvironment.StaticMethods.GetStaticMethodID (PeerReference, name, signature);
		}

		public JniMethodInfo GetCachedStaticMethod ([NotNull] ref JniMethodInfo? cachedMethod, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
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

		internal JniMethodInfo GetConstructor (ReadOnlySpan<char> signature)
			=> GetInstanceMethod ("<init>".AsSpan (), signature);

		internal JniMethodInfo GetInstanceMethod (ReadOnlySpan<char> name, ReadOnlySpan<char> signature)
			=> CreateMethodInfo (name, signature, GetMemberID (name, signature, MemberKind.InstanceMethod), isStatic: false);

		internal JniMethodInfo GetStaticMethod (ReadOnlySpan<char> name, ReadOnlySpan<char> signature)
			=> CreateMethodInfo (name, signature, GetMemberID (name, signature, MemberKind.StaticMethod), isStatic: true);

		internal JniFieldInfo GetInstanceField (ReadOnlySpan<char> name, ReadOnlySpan<char> signature)
			=> CreateFieldInfo (name, signature, GetMemberID (name, signature, MemberKind.InstanceField), isStatic: false);

		internal JniFieldInfo GetStaticField (ReadOnlySpan<char> name, ReadOnlySpan<char> signature)
			=> CreateFieldInfo (name, signature, GetMemberID (name, signature, MemberKind.StaticField), isStatic: true);

		internal bool TryGetInstanceMethod (ReadOnlySpan<char> name, ReadOnlySpan<char> signature, [NotNullWhen (true)] out JniMethodInfo? method)
		{
			var id = GetMemberID (name, signature, MemberKind.InstanceMethod, throwOnError: false);
			method = id == IntPtr.Zero ? null : CreateMethodInfo (name, signature, id, isStatic: false);
			return method != null;
		}

		/// <safety>
		/// The UTF-8 name must be readable through its NUL terminator and immutable until lookup
		/// returns or throws, and through all later reads of returned Debug metadata. It is
		/// borrowed, not rooted or freed; no alias may write or free it while readers remain.
		/// Read-only aliases may overlap. Do not race type/runtime disposal with JNI lookup on
		/// the current attached thread. Failure transfers no ownership and retains no new metadata.
		/// </safety>
		internal unsafe bool TryGetInstanceMethod (byte* name, ReadOnlySpan<char> signature, [NotNullWhen (true)] out JniMethodInfo? method)
		{
			unsafe {
				// SAFETY: the caller retains name bytes through lookup and lazy metadata reads; the managed signature is copied in Debug.
				var id = GetMemberID (name, signature, MemberKind.InstanceMethod, throwOnError: false);
				method = id == IntPtr.Zero ? null : CreateMethodInfo (name, signature, id, isStatic: false);
			}
			return method != null;
		}

		/// <safety>
		/// Both UTF-8 buffers must be readable through their NUL terminators and immutable until lookup
		/// returns or throws, and through all later reads of returned Debug metadata. Neither is
		/// rooted or freed; read-only aliases may overlap, but no alias may write or free them
		/// while readers remain. Do not race type/runtime disposal with JNI lookup on the current
		/// attached thread. Failure transfers no ownership and retains no new metadata.
		/// </safety>
		internal unsafe bool TryGetInstanceMethod (byte* name, byte* signature, [NotNullWhen (true)] out JniMethodInfo? method)
		{
			unsafe {
				// SAFETY: both caller-owned NUL-terminated buffers remain immutable through lookup and any later Debug reads.
				var id = GetMemberID (name, signature, MemberKind.InstanceMethod, throwOnError: false);
				method = id == IntPtr.Zero ? null : new JniMethodInfo (name, signature, id, isStatic: false);
			}
			return method != null;
		}

		/// <safety>
		/// The UTF-8 signature must be readable through its NUL terminator and immutable until lookup
		/// returns or throws, and through all later reads of returned Debug metadata. It is
		/// borrowed, not rooted or freed; no alias may write or free it while readers remain.
		/// Read-only aliases may overlap. Do not race type/runtime disposal with JNI lookup on
		/// the current attached thread. Failure transfers no ownership and retains no new metadata.
		/// </safety>
		internal unsafe bool TryGetInstanceMethod (ReadOnlySpan<char> name, byte* signature, [NotNullWhen (true)] out JniMethodInfo? method)
		{
			unsafe {
				// SAFETY: the caller retains signature bytes through metadata reads; the managed name is copied in Debug.
				var id = GetMemberID (name, signature, MemberKind.InstanceMethod, throwOnError: false);
				method = id == IntPtr.Zero ? null : CreateMethodInfo (name, signature, id, isStatic: false);
			}
			return method != null;
		}

		internal bool TryGetStaticMethod (ReadOnlySpan<char> name, ReadOnlySpan<char> signature, [NotNullWhen (true)] out JniMethodInfo? method)
		{
			var id = GetMemberID (name, signature, MemberKind.StaticMethod, throwOnError: false);
			method = id == IntPtr.Zero ? null : CreateMethodInfo (name, signature, id, isStatic: true);
			return method != null;
		}

		/// <safety>
		/// The UTF-8 name must be readable through its NUL terminator and immutable until lookup
		/// returns or throws, and through all later reads of returned Debug metadata. It is
		/// borrowed, not rooted or freed; no alias may write or free it while readers remain.
		/// Read-only aliases may overlap. Do not race type/runtime disposal with JNI lookup on
		/// the current attached thread. Failure transfers no ownership and retains no new metadata.
		/// </safety>
		internal unsafe bool TryGetStaticMethod (byte* name, ReadOnlySpan<char> signature, [NotNullWhen (true)] out JniMethodInfo? method)
		{
			unsafe {
				// SAFETY: name storage survives lookup and lazy metadata reads; the managed signature is copied in Debug.
				var id = GetMemberID (name, signature, MemberKind.StaticMethod, throwOnError: false);
				method = id == IntPtr.Zero ? null : CreateMethodInfo (name, signature, id, isStatic: true);
			}
			return method != null;
		}

		/// <safety>
		/// Both UTF-8 buffers must be readable through their NUL terminators and immutable until lookup
		/// returns or throws, and through all later reads of returned Debug metadata. Neither is
		/// rooted or freed; read-only aliases may overlap, but no alias may write or free them
		/// while readers remain. Do not race type/runtime disposal with JNI lookup on the current
		/// attached thread. Failure transfers no ownership and retains no new metadata.
		/// </safety>
		internal unsafe bool TryGetStaticMethod (byte* name, byte* signature, [NotNullWhen (true)] out JniMethodInfo? method)
		{
			unsafe {
				// SAFETY: both caller-owned NUL-terminated buffers survive this lookup and subsequent Debug metadata reads.
				var id = GetMemberID (name, signature, MemberKind.StaticMethod, throwOnError: false);
				method = id == IntPtr.Zero ? null : new JniMethodInfo (name, signature, id, isStatic: true);
			}
			return method != null;
		}

		/// <safety>
		/// The UTF-8 signature must be readable through its NUL terminator and immutable until lookup
		/// returns or throws, and through all later reads of returned Debug metadata. It is
		/// borrowed, not rooted or freed; no alias may write or free it while readers remain.
		/// Read-only aliases may overlap. Do not race type/runtime disposal with JNI lookup on
		/// the current attached thread. Failure transfers no ownership and retains no new metadata.
		/// </safety>
		internal unsafe bool TryGetStaticMethod (ReadOnlySpan<char> name, byte* signature, [NotNullWhen (true)] out JniMethodInfo? method)
		{
			unsafe {
				// SAFETY: signature storage survives all later Debug reads; the managed name is copied before this method returns.
				var id = GetMemberID (name, signature, MemberKind.StaticMethod, throwOnError: false);
				method = id == IntPtr.Zero ? null : CreateMethodInfo (name, signature, id, isStatic: true);
			}
			return method != null;
		}

		internal bool TryGetInstanceField (ReadOnlySpan<char> name, ReadOnlySpan<char> signature, [NotNullWhen (true)] out JniFieldInfo? field)
		{
			var id = GetMemberID (name, signature, MemberKind.InstanceField, throwOnError: false);
			field = id == IntPtr.Zero ? null : CreateFieldInfo (name, signature, id, isStatic: false);
			return field != null;
		}

		internal bool TryGetStaticField (ReadOnlySpan<char> name, ReadOnlySpan<char> signature, [NotNullWhen (true)] out JniFieldInfo? field)
		{
			var id = GetMemberID (name, signature, MemberKind.StaticField, throwOnError: false);
			field = id == IntPtr.Zero ? null : CreateFieldInfo (name, signature, id, isStatic: true);
			return field != null;
		}

		static JniMethodInfo CreateMethodInfo (ReadOnlySpan<char> name, ReadOnlySpan<char> signature, IntPtr id, bool isStatic)
		{
#if DEBUG
			return new JniMethodInfo (name.ToString (), signature.ToString (), id, isStatic);
#else
			return new JniMethodInfo (id, isStatic);
#endif
		}

		/// <safety>
		/// A non-null UTF-8 name must remain readable through its NUL terminator and immutable for
		/// every later Debug metadata read; no alias may free or modify it during those reads.
		/// Read-only aliases may overlap. The address is neither rooted nor freed, including
		/// on failure; id is an opaque method ID, not a buffer.
		/// </safety>
		static unsafe JniMethodInfo CreateMethodInfo (byte* name, ReadOnlySpan<char> signature, IntPtr id, bool isStatic)
		{
#if DEBUG
			unsafe {
				// SAFETY: the caller retains name storage for lazy diagnostics; the managed signature is copied, never borrowed.
				return new JniMethodInfo (name, signature.ToString (), id, isStatic);
			}
#else
			return new JniMethodInfo (id, isStatic);
#endif
		}

		/// <safety>
		/// A non-null UTF-8 signature must remain readable through its NUL terminator and immutable
		/// for every later Debug metadata read; no alias may free or modify it during those reads.
		/// Read-only aliases may overlap. The address is neither rooted nor freed, including
		/// on failure; id is an opaque method ID, not a buffer.
		/// </safety>
		static unsafe JniMethodInfo CreateMethodInfo (ReadOnlySpan<char> name, byte* signature, IntPtr id, bool isStatic)
		{
#if DEBUG
			unsafe {
				// SAFETY: the caller retains signature storage for lazy reads; the managed name is copied before publication.
				return new JniMethodInfo (name.ToString (), signature, id, isStatic);
			}
#else
			return new JniMethodInfo (id, isStatic);
#endif
		}

		static JniFieldInfo CreateFieldInfo (ReadOnlySpan<char> name, ReadOnlySpan<char> signature, IntPtr id, bool isStatic)
		{
#if DEBUG
			return new JniFieldInfo (name.ToString (), signature.ToString (), id, isStatic);
#else
			return new JniFieldInfo (id, isStatic);
#endif
		}

		enum MemberKind {
			InstanceMethod,
			StaticMethod,
			InstanceField,
			StaticField,
		}

		IntPtr GetMemberID (ReadOnlySpan<char> name, ReadOnlySpan<char> signature, MemberKind kind, bool throwOnError = true)
		{
			// Match StringToCoTaskMemUTF8, including unpaired-surrogate replacement
			// and embedded-NUL termination, rather than changing to JNI modified UTF-8.
			int nameLength = checked (Encoding.UTF8.GetByteCount (name) + 1);
			byte[]? rentedName = null;
			try {
				if (nameLength > 512)
					rentedName = ArrayPool<byte>.Shared.Rent (nameLength);

				Span<byte> nameBuffer = rentedName == null
					? stackalloc byte [nameLength]
					: rentedName.AsSpan (0, nameLength);
				Encoding.UTF8.GetBytes (name, nameBuffer);
				nameBuffer [nameLength - 1] = 0;

				unsafe {
					// SAFETY: checked size reserves the NUL; fixed keeps stack/pooled bytes stable until JNI returns or throws.
					fixed (byte* nameStart = nameBuffer)
						return GetMemberID (nameStart, signature, kind, throwOnError);
				}
			} finally {
				if (rentedName != null)
					ArrayPool<byte>.Shared.Return (rentedName);
			}
		}

		/// <safety>
		/// The UTF-8 name must be readable through its NUL terminator and immutable until return or
		/// exception, without concurrent writes or frees through aliases. The bytes are not
		/// retained or freed. Do not race type/runtime disposal; JNI uses the current thread.
		/// </safety>
		unsafe IntPtr GetMemberID (byte* name, ReadOnlySpan<char> signature, MemberKind kind, bool throwOnError = true)
		{
			if (name == null)
				throw new ArgumentNullException (nameof (name));
			unsafe {
				// SAFETY: name remains borrowed from the caller; the managed signature is encoded into a private terminated buffer.
				return GetMemberID (signature, name, false, kind, throwOnError);
			}
		}

		/// <safety>
		/// Both UTF-8 buffers must be readable through their NUL terminators and immutable until
		/// return or exception. Read-only aliases may overlap; no alias may write or free them.
		/// JNI consumes them synchronously without retaining or freeing them, even on lookup
		/// failure. Do not race type/runtime disposal; the environment belongs to the current thread.
		/// </safety>
		unsafe IntPtr GetMemberID (byte* name, byte* signature, MemberKind kind, bool throwOnError = true)
		{
			AssertValid ();
			if (name == null)
				throw new ArgumentNullException (nameof (name));
			if (signature == null)
				throw new ArgumentNullException (nameof (signature));

			var env = JniEnvironment.EnvironmentPointer;
			IntPtr id;
			IntPtr thrown;
			unsafe {
				// SAFETY: caller buffers include NULs and remain stable; env is current-thread JNI and PeerReference is a live class.
				id = kind switch {
					MemberKind.InstanceMethod => JniNativeMethods.GetMethodID (env, PeerReference.Handle, (IntPtr) name, (IntPtr) signature),
					MemberKind.StaticMethod => JniNativeMethods.GetStaticMethodID (env, PeerReference.Handle, (IntPtr) name, (IntPtr) signature),
					MemberKind.InstanceField => JniNativeMethods.GetFieldID (env, PeerReference.Handle, (IntPtr) name, (IntPtr) signature),
					MemberKind.StaticField => JniNativeMethods.GetStaticFieldID (env, PeerReference.Handle, (IntPtr) name, (IntPtr) signature),
					_ => throw new ArgumentOutOfRangeException (nameof (kind)),
				};
				thrown = JniNativeMethods.ExceptionOccurred (env);
			}
			if (!throwOnError) {
				if (thrown != IntPtr.Zero) {
					JniEnvironment.Exceptions.ExceptionClear ();
					JniEnvironment.References.RawDeleteLocalRef (env, thrown);
					return IntPtr.Zero;
				}
				Debug.Assert (id != IntPtr.Zero);
				return id;
			}

			var exception = JniEnvironment.GetExceptionForLastThrowable (thrown);
			if (exception != null)
				ExceptionDispatchInfo.Capture (exception).Throw ();
			if (id == IntPtr.Zero)
				throw new InvalidOperationException ("Should not be reached; JNI member lookup should have thrown!");
			return id;
		}

		/// <safety>
		/// The UTF-8 signature must be readable through its NUL terminator and immutable until return
		/// or exception, without concurrent writes or frees through aliases. It is not retained
		/// or freed. Do not race type/runtime disposal; JNI uses the current attached thread.
		/// </safety>
		unsafe IntPtr GetMemberID (ReadOnlySpan<char> name, byte* signature, MemberKind kind, bool throwOnError = true)
		{
			if (signature == null)
				throw new ArgumentNullException (nameof (signature));
			unsafe {
				// SAFETY: the caller retains signature bytes; only the managed name is encoded into a temporary terminated buffer.
				return GetMemberID (name, signature, true, kind, throwOnError);
			}
		}

		/// <safety>
		/// The UTF-8 otherValue must be readable through its NUL terminator and immutable until return or
		/// exception; no alias may write or free it during JNI lookup. Read-only aliasing is allowed.
		/// No source bytes are retained or freed. Do not race type/runtime disposal; JNI uses the current thread.
		/// </safety>
		unsafe IntPtr GetMemberID (ReadOnlySpan<char> value, byte* otherValue, bool valueIsName, MemberKind kind, bool throwOnError)
		{
			int valueLength = checked (Encoding.UTF8.GetByteCount (value) + 1);
			byte[]? rentedValue = null;
			try {
				if (valueLength > 512)
					rentedValue = ArrayPool<byte>.Shared.Rent (valueLength);

				Span<byte> valueBuffer = rentedValue == null
					? stackalloc byte [valueLength]
					: rentedValue.AsSpan (0, valueLength);
				Encoding.UTF8.GetBytes (value, valueBuffer);
				valueBuffer [valueLength - 1] = 0;

				unsafe {
					// SAFETY: the checked buffer includes its NUL; fixed lasts through JNI, and finally returns pooled bytes on failure.
					fixed (byte* valueStart = valueBuffer) {
						return valueIsName
							? GetMemberID (valueStart, otherValue, kind, throwOnError)
							: GetMemberID (otherValue, valueStart, kind, throwOnError);
					}
				}
			} finally {
				if (rentedValue != null)
					ArrayPool<byte>.Shared.Return (rentedValue);
			}
		}
	}
}
