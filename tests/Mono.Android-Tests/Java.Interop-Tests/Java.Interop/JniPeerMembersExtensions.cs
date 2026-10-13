using System;
using System.Diagnostics.CodeAnalysis;

using Java.Interop;

namespace Java.InteropTests {

	static class JniPeerMembersExtensions {
		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

		public static unsafe JniObjectReference StartGenericCreateInstance<T> (
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

		public static unsafe void FinishGenericCreateInstance<
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

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				peer.FinishCreateInstance (constructorSignature, self, args);
			} finally {
				__vm.DestroyGenericArgumentState (value, ref arg);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<
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

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				__vm.DestroyGenericArgumentState (value, ref arg);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<
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

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				__vm.DestroyGenericArgumentState (value, ref arg);
			}
		}

		public static unsafe int InvokeGenericInt32Method<
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

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				__vm.DestroyGenericArgumentState (value, ref arg);
			}
		}
	}
}
