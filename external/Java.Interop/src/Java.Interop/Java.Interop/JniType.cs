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

	/// <remarks>
	/// For overloads accepting UTF-8 spans, the caller must ensure each span has readable
	/// NUL-terminated backing storage for the duration of the call. Complete C# UTF-8 literals
	/// provide a terminator beyond their span length; copies and slices do not carry that guarantee.
	/// </remarks>
	public sealed class JniType : IDisposable {

		[return: NotNullIfNotNull ("classFileData")]
		public static JniType? DefineClass (string name, JniObjectReference loader, byte[] classFileData)
		{
			if (classFileData == null)
				return null;
			unsafe {
				// SAFETY: Pin the complete managed class-file array through JNI's synchronous read of its exact length.
				fixed (byte* buf = classFileData) {
					var lref = JniEnvironment.Types.DefineClass (name, loader, (IntPtr) buf, classFileData.Length);
					return new JniType (ref lref, JniObjectReferenceOptions.CopyAndDispose);
				}
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
		/// Keep non-null UTF-8 bytes readable through their NUL and unchanged until return or
		/// exception; no alias may write/free them, and runtime disposal must not race lookup.
		/// JNI uses the current attached thread. No source storage is retained or freed.
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
		/// On a cache miss, the name has <see cref="JniType(byte*)"/>'s call-duration contract.
		/// Do not race cached-type disposal with lookup. No name pointer is retained.
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

		/// <safety>
		/// constructor must describe a constructor for this type. parameters must address readable,
		/// initialized JNI slots matching its descriptor, or be null only for no arguments.
		/// Keep the storage and referenced Java objects live until return or failure; local references
		/// must belong to the invoking thread. Exclude concurrent writes or release through aliases.
		/// The argument buffer is not retained.
		/// </safety>
		public unsafe JniObjectReference NewObject (JniMethodInfo constructor, JniArgumentValue* @parameters)
		{
			AssertValid ();

			unsafe {
				// SAFETY: The caller keeps constructor-compatible argument slots and their Java references live.
				return JniEnvironment.Object.NewObject (PeerReference, constructor, parameters);
			}
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
				unsafe {
					// SAFETY: Both terminated buffers are allocated above and remain owned until the matching finally.
					var id      = JniNativeMethods.GetMethodID (env, PeerReference.Handle, _name, _sig);
					thrown      = JniNativeMethods.ExceptionOccurred (env);
					return id;
				}
			}
			finally {
				unsafe {
					// SAFETY: Free the two CoTaskMemUTF8 allocations once, after native lookup and exception capture.
					Marshal.ZeroFreeCoTaskMemUTF8 (_name);
					Marshal.ZeroFreeCoTaskMemUTF8 (_sig);
				}
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
				unsafe {
					// SAFETY: This call owns both terminated CoTaskMem buffers through static lookup and exception capture.
					var id      = JniNativeMethods.GetStaticMethodID (env, PeerReference.Handle, _name, _sig);
					thrown      = JniNativeMethods.ExceptionOccurred (env);
					return id;
				}
			}
			finally {
				unsafe {
					// SAFETY: Release only the CoTaskMemUTF8 allocations created above, in their existing failure-safe order.
					Marshal.ZeroFreeCoTaskMemUTF8 (_name);
					Marshal.ZeroFreeCoTaskMemUTF8 (_sig);
				}
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
		/// <safety>
		/// The caller must ensure the backing storage remains readable and NUL-terminated
		/// for the duration of the call. C# UTF-8 literals provide a
		/// terminator immediately beyond the span length; copies and slices do not carry that guarantee.
		/// Keep the bytes unchanged through failed or successful lookup; aliases must not
		/// mutate or reclaim them concurrently. The lookup pins managed backing storage.
		/// </safety>
		public unsafe JniType (ReadOnlySpan<byte> classname)
		{
			JniObjectReference peer;
			unsafe {
				// SAFETY: The caller supplies a readable terminated name; FindClass pins it for JNI and returns an owned local reference for Initialize.
				peer = JniEnvironment.Types.FindClass (classname);
			}
			Initialize (ref peer, JniObjectReferenceOptions.CopyAndDispose);
		}

		/// <safety>
		/// The caller must ensure <paramref name="classname"/> has readable NUL-terminated
		/// backing storage for the duration of the call.
		/// Keep its bytes unchanged until completion, including failure, without concurrent
		/// reclamation or mutation; the lookup pins managed storage.
		/// </safety>
		public static unsafe JniType GetCachedJniType ([NotNull] ref JniType? cachedType, ReadOnlySpan<byte> classname)
		{
			if (cachedType != null && cachedType.PeerReference.IsValid)
				return cachedType;
			JniType t;
			unsafe {
				// SAFETY: On a cache miss the caller's terminated bytes remain readable through construction; the constructor owns JNI pinning.
				t = new JniType (classname);
			}
			if (Interlocked.CompareExchange (ref cachedType, t, null) != null)
				t.Dispose ();
			cachedType.RegisterWithRuntime ();
			return cachedType;
		}

		/// <safety>
		/// The caller must ensure <paramref name="signature"/> has readable NUL-terminated
		/// backing storage for the duration of the call.
		/// Keep its bytes unchanged until completion, including failure, without concurrent
		/// reclamation or mutation; the lookup pins managed storage.
		/// </safety>
		public unsafe JniMethodInfo GetConstructor (ReadOnlySpan<byte> signature)
		{
			AssertValid ();

			unsafe {
				// SAFETY: The <init> literal has static NUL-terminated storage; the caller supplies the signature's terminator and lookup pins both spans.
				return JniEnvironment.InstanceMethods.GetMethodID (PeerReference, "<init>"u8, signature);
			}
		}

		/// <safety>
		/// The caller must ensure <paramref name="signature"/> has readable NUL-terminated
		/// backing storage for the duration of the call.
		/// Keep its bytes unchanged until completion, including failure, without concurrent
		/// reclamation or mutation; the lookup pins managed storage.
		/// </safety>
		public unsafe JniMethodInfo GetCachedConstructor ([NotNull] ref JniMethodInfo? cachedMethod, ReadOnlySpan<byte> signature)
		{
			AssertValid ();

			unsafe {
				// SAFETY: The constructor name is a complete UTF8 literal; the caller keeps the signature terminated and unchanged for a possible cache miss.
				return GetCachedInstanceMethod (ref cachedMethod, "<init>"u8, signature);
			}
		}

		/// <safety>
		/// The caller must ensure <paramref name="name"/> and <paramref name="signature"/> have
		/// readable NUL-terminated backing storage for the duration of the call.
		/// Prevent concurrent mutation/reclamation of either sequence through success or
		/// failure; this lookup pins managed backing storage.
		/// </safety>
		public unsafe JniFieldInfo GetInstanceField (ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
		{
			AssertValid ();

			unsafe {
				// SAFETY: Both caller-owned spans satisfy termination and lifetime requirements; GetFieldID pins them for this class's JNI lookup.
				return JniEnvironment.InstanceFields.GetFieldID (PeerReference, name, signature);
			}
		}

		/// <safety>
		/// The caller must ensure <paramref name="name"/> and <paramref name="signature"/> have
		/// readable NUL-terminated backing storage for the duration of the call.
		/// Prevent concurrent mutation/reclamation of either sequence through success or
		/// failure; a cache-miss lookup pins managed backing storage.
		/// </safety>
		public unsafe JniFieldInfo GetCachedInstanceField ([NotNull] ref JniFieldInfo? cachedField, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
		{
			AssertValid ();

			if (cachedField != null && cachedField.IsValid)
				return cachedField;
			JniFieldInfo m;
			unsafe {
				// SAFETY: On a cache miss the same terminated caller bytes are consumed synchronously; only field metadata, not their addresses, is cached.
				m = GetInstanceField (name, signature);
			}
			if (Interlocked.CompareExchange (ref cachedField, m, null) != null) {
				// No cleanup required; let the GC collect the unused instance
			}
			return cachedField;
		}

		/// <safety>
		/// The caller must ensure <paramref name="name"/> and <paramref name="signature"/> have
		/// readable NUL-terminated backing storage for the duration of the call.
		/// Prevent concurrent mutation/reclamation of either sequence through success or
		/// failure; this lookup pins managed backing storage.
		/// </safety>
		public unsafe JniFieldInfo GetStaticField (ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
		{
			AssertValid ();

			unsafe {
				// SAFETY: GetStaticFieldID pins both caller-guaranteed terminated spans for the current class's synchronous JNI read.
				return JniEnvironment.StaticFields.GetStaticFieldID (PeerReference, name, signature);
			}
		}

		/// <safety>
		/// The caller must ensure <paramref name="name"/> and <paramref name="signature"/> have
		/// readable NUL-terminated backing storage for the duration of the call.
		/// Prevent concurrent mutation/reclamation of either sequence through success or
		/// failure; a cache-miss lookup pins managed backing storage.
		/// </safety>
		public unsafe JniFieldInfo GetCachedStaticField ([NotNull] ref JniFieldInfo? cachedField, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
		{
			AssertValid ();

			if (cachedField != null && cachedField.IsValid)
				return cachedField;
			JniFieldInfo m;
			unsafe {
				// SAFETY: The caller maintains both terminated byte sequences until the cache-miss lookup completes, including JNI exception handling.
				m = GetStaticField (name, signature);
			}
			if (Interlocked.CompareExchange (ref cachedField, m, null) != null) {
				// No cleanup required; let the GC collect the unused instance
			}
			return cachedField;
		}

		/// <safety>
		/// The caller must ensure <paramref name="name"/> and <paramref name="signature"/> have
		/// readable NUL-terminated backing storage for the duration of the call.
		/// Prevent concurrent mutation/reclamation of either sequence through success or
		/// failure; this lookup pins managed backing storage.
		/// </safety>
		public unsafe JniMethodInfo GetInstanceMethod (ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
		{
			AssertValid ();

			unsafe {
				// SAFETY: The caller supplies termination and byte lifetime; GetMethodID pins the spans and does not retain their addresses.
				return JniEnvironment.InstanceMethods.GetMethodID (PeerReference, name, signature);
			}
		}

		/// <safety>
		/// The caller must ensure <paramref name="name"/> and <paramref name="signature"/> have
		/// readable NUL-terminated backing storage for the duration of the call.
		/// Prevent concurrent mutation/reclamation of either sequence through success or
		/// failure; a cache-miss lookup pins managed backing storage.
		/// </safety>
		public unsafe JniMethodInfo GetCachedInstanceMethod ([NotNull] ref JniMethodInfo? cachedMethod, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
		{
			AssertValid ();

			if (cachedMethod != null && cachedMethod.IsValid)
				return cachedMethod;
			JniMethodInfo m;
			unsafe {
				// SAFETY: The cache miss forwards the caller's still-live terminated spans; the result caches an opaque method ID rather than borrowed bytes.
				m = GetInstanceMethod (name, signature);
			}
			if (Interlocked.CompareExchange (ref cachedMethod, m, null) != null) {
				// No cleanup required; let the GC collect the unused instance
			}
			return cachedMethod;
		}

		/// <safety>
		/// The caller must ensure <paramref name="name"/> and <paramref name="signature"/> have
		/// readable NUL-terminated backing storage for the duration of the call.
		/// Prevent concurrent mutation/reclamation of either sequence through success or
		/// failure; this lookup pins managed backing storage.
		/// </safety>
		public unsafe JniMethodInfo GetStaticMethod (ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
		{
			AssertValid ();

			unsafe {
				// SAFETY: Both terminated spans are caller-valid for this call; GetStaticMethodID pins them before the current-thread JNI read.
				return JniEnvironment.StaticMethods.GetStaticMethodID (PeerReference, name, signature);
			}
		}

		/// <safety>
		/// The caller must ensure <paramref name="name"/> and <paramref name="signature"/> have
		/// readable NUL-terminated backing storage for the duration of the call.
		/// Prevent concurrent mutation/reclamation of either sequence through success or
		/// failure; a cache-miss lookup pins managed backing storage.
		/// </safety>
		public unsafe JniMethodInfo GetCachedStaticMethod ([NotNull] ref JniMethodInfo? cachedMethod, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
		{
			AssertValid ();

			if (cachedMethod != null && cachedMethod.IsValid)
				return cachedMethod;
			JniMethodInfo m;
			unsafe {
				// SAFETY: A cache miss consumes the caller's terminated bytes only through lookup completion; published metadata retains no byte-buffer alias.
				m = GetStaticMethod (name, signature);
			}
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
		/// Keep the UTF-8 name readable through its NUL and unchanged through lookup and later
		/// Debug metadata reads; no alias may write/free it. No ownership transfers, even on
		/// failure. JNI uses the current thread; do not race type/runtime disposal.
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
		/// Keep both UTF-8 buffers readable through their NULs and unchanged through lookup and
		/// later Debug reads. Read-only aliases may overlap; no writes/frees or type/runtime
		/// disposal may race readers. JNI uses the current thread; failure takes no ownership.
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
		/// Keep the UTF-8 signature readable through its NUL and unchanged through lookup and
		/// later Debug reads; no alias may write/free it. No ownership transfers, even on failure.
		/// JNI uses the current thread; do not race type/runtime disposal.
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
		/// Keep the UTF-8 name readable through its NUL and unchanged through lookup and later
		/// Debug metadata reads; no alias may write/free it. No ownership transfers, even on
		/// failure. JNI uses the current thread; do not race type/runtime disposal.
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
		/// Keep both UTF-8 buffers readable through their NULs and unchanged through lookup and
		/// later Debug reads. Read-only aliases may overlap; no writes/frees or type/runtime
		/// disposal may race readers. JNI uses the current thread; failure takes no ownership.
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
		/// Keep the UTF-8 signature readable through its NUL and unchanged through lookup and
		/// later Debug reads; no alias may write/free it. No ownership transfers, even on failure.
		/// JNI uses the current thread; do not race type/runtime disposal.
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
		/// The name has the borrowed lifetime of <see cref="JniMethodInfo(byte*, string, IntPtr, bool)"/>.
		/// The ID remains opaque; construction takes no buffer ownership, including on failure.
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
		/// The signature has the borrowed lifetime of <see cref="JniMethodInfo(string, byte*, IntPtr, bool)"/>.
		/// The ID remains opaque; construction takes no buffer ownership, including on failure.
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
		/// Keep UTF-8 name bytes readable through their NUL and unchanged until return or throw;
		/// no alias may write/free them. JNI uses the current thread; do not race type/runtime
		/// disposal. The bytes are neither retained nor freed.
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
		/// Keep both UTF-8 buffers readable through their NULs and unchanged until return or throw;
		/// read-only aliases may overlap, but no alias may write/free them. JNI uses the current
		/// thread; do not race type/runtime disposal. The bytes are neither retained nor freed.
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
		/// Keep UTF-8 signature bytes readable through their NUL and unchanged until return or
		/// throw; no alias may write/free them. JNI uses the current thread; do not race type/runtime
		/// disposal. The bytes are neither retained nor freed.
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
		/// OtherValue has the same call-duration buffer and current-thread JNI contract as
		/// <see cref="GetMemberID(byte*, byte*, MemberKind, bool)"/>. No source bytes are retained.
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
