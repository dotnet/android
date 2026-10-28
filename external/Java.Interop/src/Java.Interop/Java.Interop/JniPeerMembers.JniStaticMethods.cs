#nullable enable

using System;
using System.Collections.Concurrent;

namespace Java.Interop
{
	partial class JniPeerMembers {
	public sealed partial class JniStaticMethods {

		internal JniStaticMethods (JniPeerMembers members)
		{
			Members = members;
		}

		internal    readonly    JniPeerMembers              Members;

		ConcurrentDictionary<string, JniMethodInfo>? staticMethods;

		ConcurrentDictionary<string, JniMethodInfo> StaticMethods => GetOrCreate (ref staticMethods, 3);

		internal void Dispose ()
		{
			Clear (ref staticMethods, static method => method.StaticRedirect?.Dispose ());
		}

		public JniMethodInfo GetMethodInfo (string encodedMember)
		{
			return GetOrAdd (
				StaticMethods,
				encodedMember,
				static (member, methods) => {
					ReadOnlySpan<char> method, signature;
					JniPeerMembers.GetNameAndSignature (member, out method, out signature);
					return methods.GetMethodInfo (method, signature);
				},
				this,
				static method => method.StaticRedirect?.Dispose ());
		}

		JniMethodInfo GetMethodInfo (ReadOnlySpan<char> method, ReadOnlySpan<char> signature)
		{
			var m              = (JniMethodInfo?) null;
			var newMethod      = Members.GetReplacementMethodInfo (method, signature);
			if (newMethod.HasValue) {
				var info = newMethod.Value;
				JniType? t = CreateTargetType (info, Members);
				try {
					if (TryGetStaticMethod (t, info, method, signature, out m)) {
						m.StaticRedirect = t;
						t = null;
						return m;
					}
				} finally {
					t?.Dispose ();
				}
			}
			if (Members.JniPeerType.TryGetStaticMethod (method, signature, out m)) {
				return m;
			}
			newMethod = JniPeerMembers.GetBaseReplacementMethodInfo (Members.ManagedPeerType, method, signature);
			if (newMethod.HasValue) {
				var info = newMethod.Value;
				JniType? t = CreateTargetType (info, Members);
				try {
					if (TryGetStaticMethod (t, info, method, signature, out m)) {
						m.StaticRedirect = t;
						t = null;
						return m;
					}
				} finally {
					t?.Dispose ();
				}
			}
			m   = FindInFallbackTypes (method, signature);
			if (m != null) {
				return m;
			}
			return Members.JniPeerType.GetStaticMethod (method, signature);
		}

#pragma warning disable CA1801
		JniType GetMethodDeclaringType (JniMethodInfo method)
		{
			if (method.StaticRedirect != null) {
				return method.StaticRedirect;
			}
			return Members.JniPeerType;
		}
#pragma warning restore CA1801

		JniMethodInfo? FindInFallbackTypes (ReadOnlySpan<char> method, ReadOnlySpan<char> signature)
		{
			var fallbackTypes  = JniEnvironment.Runtime.TypeManager.GetStaticMethodFallbackTypes (Members.JniPeerTypeName);
			if (fallbackTypes == null) {
				return null;
			}
			foreach (var ft in fallbackTypes) {
				JniType? t = null;
				try {
					if (!JniType.TryParse (ft, out t)) {
						continue;
					}
					if (t.TryGetStaticMethod (method, signature, out var m)) {
						m.StaticRedirect    = t;
						t                   = null;
						return m;
					}
				}
				finally {
					t?.Dispose ();
				}
			}
			return null;
		}

		/// <safety>
		/// The resolved method must return void. parameters must supply readable, initialized JNI slots
		/// matching its descriptor (null only for no arguments), with live references on this thread
		/// until return or failure. Exclude writes/frees through aliases; the buffer is not retained.
		/// </safety>
		public unsafe void InvokeVoidMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			unsafe {
				// SAFETY: Caller-provided slots match the resolved void method and remain readable until JNI returns.
				JniEnvironment.StaticMethods.CallStaticVoidMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
			}
		}

		/// <safety>
		/// The resolved method must return JNI boolean. parameters must supply readable, initialized slots
		/// matching its descriptor (null only for no arguments), with live references on this thread
		/// until return or failure. Exclude writes/frees through aliases; the buffer is not retained.
		/// </safety>
		public unsafe bool InvokeBooleanMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			unsafe {
				// SAFETY: The caller supplies the resolved boolean method's complete argument buffer for this call.
				return JniEnvironment.StaticMethods.CallStaticBooleanMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
			}
		}

		/// <safety>
		/// The resolved method must return JNI byte. parameters must supply readable, initialized slots
		/// matching its descriptor (null only for no arguments), with live references on this thread
		/// until return or failure. Exclude writes/frees through aliases; the buffer is not retained.
		/// </safety>
		public unsafe sbyte InvokeSByteMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			unsafe {
				// SAFETY: The caller supplies the resolved byte method's readable slots and live Java references.
				return JniEnvironment.StaticMethods.CallStaticByteMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
			}
		}

		/// <safety>
		/// The resolved method must return JNI char. parameters must supply readable, initialized slots
		/// matching its descriptor (null only for no arguments), with live references on this thread
		/// until return or failure. Exclude writes/frees through aliases; the buffer is not retained.
		/// </safety>
		public unsafe char InvokeCharMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			unsafe {
				// SAFETY: The caller supplies descriptor-compatible slots for the resolved UTF-16 character result.
				return JniEnvironment.StaticMethods.CallStaticCharMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
			}
		}

		/// <safety>
		/// The resolved method must return JNI short. parameters must supply readable, initialized slots
		/// matching its descriptor (null only for no arguments), with live references on this thread
		/// until return or failure. Exclude writes/frees through aliases; the buffer is not retained.
		/// </safety>
		public unsafe short InvokeInt16Method (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			unsafe {
				// SAFETY: The caller keeps every resolved short method argument readable and correctly represented.
				return JniEnvironment.StaticMethods.CallStaticShortMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
			}
		}

		/// <safety>
		/// The resolved method must return JNI int. parameters must supply readable, initialized slots
		/// matching its descriptor (null only for no arguments), with live references on this thread
		/// until return or failure. Exclude writes/frees through aliases; the buffer is not retained.
		/// </safety>
		public unsafe int InvokeInt32Method (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			unsafe {
				// SAFETY: The caller supplies the resolved int method's complete descriptor-matched argument buffer.
				return JniEnvironment.StaticMethods.CallStaticIntMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
			}
		}

		/// <safety>
		/// The resolved method must return JNI long. parameters must supply readable, initialized slots
		/// matching its descriptor (null only for no arguments), with live references on this thread
		/// until return or failure. Exclude writes/frees through aliases; the buffer is not retained.
		/// </safety>
		public unsafe long InvokeInt64Method (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			unsafe {
				// SAFETY: The caller keeps the resolved long method's slots and Java references live during JNI.
				return JniEnvironment.StaticMethods.CallStaticLongMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
			}
		}

		/// <safety>
		/// The resolved method must return JNI float. parameters must supply readable, initialized slots
		/// matching its descriptor (null only for no arguments), with live references on this thread
		/// until return or failure. Exclude writes/frees through aliases; the buffer is not retained.
		/// </safety>
		public unsafe float InvokeSingleMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			unsafe {
				// SAFETY: The caller supplies the resolved float method's complete readable argument buffer.
				return JniEnvironment.StaticMethods.CallStaticFloatMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
			}
		}

		/// <safety>
		/// The resolved method must return JNI double. parameters must supply readable, initialized slots
		/// matching its descriptor (null only for no arguments), with live references on this thread
		/// until return or failure. Exclude writes/frees through aliases; the buffer is not retained.
		/// </safety>
		public unsafe double InvokeDoubleMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			unsafe {
				// SAFETY: The caller provides descriptor-compatible slots for the resolved double-returning method.
				return JniEnvironment.StaticMethods.CallStaticDoubleMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
			}
		}

		/// <safety>
		/// The resolved method must return a JNI reference. parameters must supply readable, initialized
		/// descriptor-matched slots (null only for no arguments), with live references on this thread
		/// until return or failure. Exclude writes/frees through aliases; the buffer is not retained.
		/// </safety>
		public unsafe JniObjectReference InvokeObjectMethod (string encodedMember, JniArgumentValue* parameters)
		{
			var m = GetMethodInfo (encodedMember);
			unsafe {
				// SAFETY: The caller owns readable descriptor-matched slots; the JNI result is a local reference.
				return JniEnvironment.StaticMethods.CallStaticObjectMethod (GetMethodDeclaringType (m).PeerReference, m, parameters);
			}
		}
	}}
}
