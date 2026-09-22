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

		internal unsafe JniType (IntPtr classname)
		{
			if (classname == IntPtr.Zero)
				throw new ArgumentNullException (nameof (classname));

			var name = MemoryMarshal.CreateReadOnlySpanFromNullTerminated ((byte*)classname);
			var peer = JniEnvironment.Types.FindClass (name);
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

		internal static JniType GetCachedJniType ([NotNull] ref JniType? cachedType, IntPtr classname)
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

		[RequiresDynamicCode ("Native method registration via JniNativeMethodRegistration[] requires dynamic code generation. Use the blittable RegisterNatives(JniObjectReference, ReadOnlySpan<JniNativeMethod>) overload with statically-compiled function pointers for Native AOT compatibility.")]
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

		internal bool TryGetInstanceMethod (IntPtr name, ReadOnlySpan<char> signature, [NotNullWhen (true)] out JniMethodInfo? method)
		{
			var id = GetMemberID (name, signature, MemberKind.InstanceMethod, throwOnError: false);
			method = id == IntPtr.Zero ? null : CreateMethodInfo (name, signature, id, isStatic: false);
			return method != null;
		}

		internal bool TryGetInstanceMethod (IntPtr name, IntPtr signature, [NotNullWhen (true)] out JniMethodInfo? method)
		{
			var id = GetMemberID (name, signature, MemberKind.InstanceMethod, throwOnError: false);
			method = id == IntPtr.Zero ? null : new JniMethodInfo (name, signature, id, isStatic: false);
			return method != null;
		}

		internal bool TryGetInstanceMethod (ReadOnlySpan<char> name, IntPtr signature, [NotNullWhen (true)] out JniMethodInfo? method)
		{
			var id = GetMemberID (name, signature, MemberKind.InstanceMethod, throwOnError: false);
			method = id == IntPtr.Zero ? null : CreateMethodInfo (name, signature, id, isStatic: false);
			return method != null;
		}

		internal bool TryGetStaticMethod (ReadOnlySpan<char> name, ReadOnlySpan<char> signature, [NotNullWhen (true)] out JniMethodInfo? method)
		{
			var id = GetMemberID (name, signature, MemberKind.StaticMethod, throwOnError: false);
			method = id == IntPtr.Zero ? null : CreateMethodInfo (name, signature, id, isStatic: true);
			return method != null;
		}

		internal bool TryGetStaticMethod (IntPtr name, ReadOnlySpan<char> signature, [NotNullWhen (true)] out JniMethodInfo? method)
		{
			var id = GetMemberID (name, signature, MemberKind.StaticMethod, throwOnError: false);
			method = id == IntPtr.Zero ? null : CreateMethodInfo (name, signature, id, isStatic: true);
			return method != null;
		}

		internal bool TryGetStaticMethod (IntPtr name, IntPtr signature, [NotNullWhen (true)] out JniMethodInfo? method)
		{
			var id = GetMemberID (name, signature, MemberKind.StaticMethod, throwOnError: false);
			method = id == IntPtr.Zero ? null : new JniMethodInfo (name, signature, id, isStatic: true);
			return method != null;
		}

		internal bool TryGetStaticMethod (ReadOnlySpan<char> name, IntPtr signature, [NotNullWhen (true)] out JniMethodInfo? method)
		{
			var id = GetMemberID (name, signature, MemberKind.StaticMethod, throwOnError: false);
			method = id == IntPtr.Zero ? null : CreateMethodInfo (name, signature, id, isStatic: true);
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

		static JniMethodInfo CreateMethodInfo (IntPtr name, ReadOnlySpan<char> signature, IntPtr id, bool isStatic)
		{
#if DEBUG
			return new JniMethodInfo (name, signature.ToString (), id, isStatic);
#else
			return new JniMethodInfo (id, isStatic);
#endif
		}

		static JniMethodInfo CreateMethodInfo (ReadOnlySpan<char> name, IntPtr signature, IntPtr id, bool isStatic)
		{
#if DEBUG
			return new JniMethodInfo (name.ToString (), signature, id, isStatic);
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

		unsafe IntPtr GetMemberID (ReadOnlySpan<char> name, ReadOnlySpan<char> signature, MemberKind kind, bool throwOnError = true)
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

				fixed (byte* nameStart = nameBuffer)
					return GetMemberID ((IntPtr)nameStart, signature, kind, throwOnError);
			} finally {
				if (rentedName != null)
					ArrayPool<byte>.Shared.Return (rentedName);
			}
		}

		unsafe IntPtr GetMemberID (IntPtr name, ReadOnlySpan<char> signature, MemberKind kind, bool throwOnError = true)
		{
			if (name == IntPtr.Zero)
				throw new ArgumentNullException (nameof (name));
			return GetMemberID (signature, name, false, kind, throwOnError);
		}

		unsafe IntPtr GetMemberID (IntPtr name, IntPtr signature, MemberKind kind, bool throwOnError = true)
		{
			AssertValid ();
			if (name == IntPtr.Zero)
				throw new ArgumentNullException (nameof (name));
			if (signature == IntPtr.Zero)
				throw new ArgumentNullException (nameof (signature));

			var env = JniEnvironment.EnvironmentPointer;
			IntPtr id = kind switch {
				MemberKind.InstanceMethod => JniNativeMethods.GetMethodID (env, PeerReference.Handle, name, signature),
				MemberKind.StaticMethod => JniNativeMethods.GetStaticMethodID (env, PeerReference.Handle, name, signature),
				MemberKind.InstanceField => JniNativeMethods.GetFieldID (env, PeerReference.Handle, name, signature),
				MemberKind.StaticField => JniNativeMethods.GetStaticFieldID (env, PeerReference.Handle, name, signature),
				_ => throw new ArgumentOutOfRangeException (nameof (kind)),
			};
			var thrown = JniNativeMethods.ExceptionOccurred (env);
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

		unsafe IntPtr GetMemberID (ReadOnlySpan<char> name, IntPtr signature, MemberKind kind, bool throwOnError = true)
		{
			if (signature == IntPtr.Zero)
				throw new ArgumentNullException (nameof (signature));
			return GetMemberID (name, signature, true, kind, throwOnError);
		}

		unsafe IntPtr GetMemberID (ReadOnlySpan<char> value, IntPtr otherValue, bool valueIsName, MemberKind kind, bool throwOnError)
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

				fixed (byte* valueStart = valueBuffer) {
					var valuePointer = (IntPtr)valueStart;
					return valueIsName
						? GetMemberID (valuePointer, otherValue, kind, throwOnError)
						: GetMemberID (otherValue, valuePointer, kind, throwOnError);
				}
			} finally {
				if (rentedValue != null)
					ArrayPool<byte>.Shared.Return (rentedValue);
			}
		}
	}
}
