using System;
using System.Diagnostics.CodeAnalysis;

using Java.Interop;

namespace Java.InteropTests {

	static class JniPeerMembersExtensions {
		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

		public static JniObjectReference StartGenericCreateInstance<T> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T       value)
		{
			if (peer == null)
				throw new ArgumentNullException (nameof (peer));
			_ = value;

			return peer.StartCreateInstance (constructorSignature, declaringType, null);
		}

		public static void FinishGenericCreateInstance<
				[DynamicallyAccessedMembers (Constructors)]
				T> (
			this          JniPeerMembers.JniInstanceMethods peer,
			string        constructorSignature,
			IJavaPeerable self,
			T             value)
		{
			if (peer == null)
				throw new ArgumentNullException (nameof (peer));
			if (self == null)
				throw new ArgumentNullException (nameof (self));

			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateGenericArgumentState (value);

			try {
				// SAFETY: The typed marshaler owns the single argument through this synchronous constructor call.
				unsafe {
					var args = stackalloc JniArgumentValue [1];
					args [0] = arg.JniArgumentValue;
					peer.FinishCreateInstance (constructorSignature, self, args);
				}
			} finally {
				__vm.DestroyGenericArgumentState (value, ref arg);
			}
		}

		public static void InvokeGenericVirtualVoidMethod<
				[DynamicallyAccessedMembers (Constructors)]
				T> (
			this          JniPeerMembers.JniInstanceMethods peer,
			string        encodedMember,
			IJavaPeerable self,
			T             value)
		{
			if (peer == null)
				throw new ArgumentNullException (nameof (peer));
			if (self == null)
				throw new ArgumentNullException (nameof (self));

			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateGenericArgumentState (value);

			try {
				// SAFETY: The typed marshaler owns the single argument through this synchronous method call.
				unsafe {
					var args = stackalloc JniArgumentValue [1];
					args [0] = arg.JniArgumentValue;
					peer.InvokeVirtualVoidMethod (encodedMember, self, args);
				}
			} finally {
				__vm.DestroyGenericArgumentState (value, ref arg);
			}
		}

		public static int InvokeGenericVirtualInt32Method<
				[DynamicallyAccessedMembers (Constructors)]
				T> (
			this          JniPeerMembers.JniInstanceMethods peer,
			string        encodedMember,
			IJavaPeerable self,
			T             value)
		{
			if (peer == null)
				throw new ArgumentNullException (nameof (peer));
			if (self == null)
				throw new ArgumentNullException (nameof (self));

			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateGenericArgumentState (value);

			try {
				// SAFETY: The typed marshaler owns the single argument through this synchronous method call.
				unsafe {
					var args = stackalloc JniArgumentValue [1];
					args [0] = arg.JniArgumentValue;
					return peer.InvokeVirtualInt32Method (encodedMember, self, args);
				}
			} finally {
				__vm.DestroyGenericArgumentState (value, ref arg);
			}
		}

		public static int InvokeGenericInt32Method<
				[DynamicallyAccessedMembers (Constructors)]
				T> (
			this    JniPeerMembers.JniStaticMethods peer,
			string  encodedMember,
			T       value)
		{
			if (peer == null)
				throw new ArgumentNullException (nameof (peer));

			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateGenericArgumentState (value);

			try {
				// SAFETY: The typed marshaler owns the single argument through this synchronous method call.
				unsafe {
					var args = stackalloc JniArgumentValue [1];
					args [0] = arg.JniArgumentValue;
					return peer.InvokeInt32Method (encodedMember, args);
				}
			} finally {
				__vm.DestroyGenericArgumentState (value, ref arg);
			}
		}
	}
}
