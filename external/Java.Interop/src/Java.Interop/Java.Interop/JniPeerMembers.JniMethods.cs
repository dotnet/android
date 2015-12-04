using System;

namespace Java.Interop.GenericMarshaler {

	public static partial class JniPeerInstanceMethodsExtensions {

		public static JniObjectReference StartGenericCreateInstance<T> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T value
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateGenericArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				__vm.DestroyGenericArgumentState (value, ref arg);
			}
		}

		public static void FinishGenericCreateInstance<T> (
			this        JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T value
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value);
		}

		static unsafe void _InvokeConstructor<T> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateGenericArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, ctor, args);
			} finally {
				__vm.DestroyGenericArgumentState (value, ref arg);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2> (
			this        JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2);
		}

		static unsafe void _InvokeConstructor<T1, T2> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, ctor, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3> (
			this        JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, ctor, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4> (
			this        JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, ctor, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5> (
			this        JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, ctor, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6> (
			this        JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, ctor, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6, T7> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7> (
			this        JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6, value7);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, ctor, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8> (
			this        JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, ctor, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this        JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, ctor, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this        JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, ctor, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this        JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, ctor, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateGenericArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this        JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateGenericArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, ctor, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateGenericArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateGenericArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this        JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateGenericArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateGenericArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, ctor, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateGenericArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateGenericArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateGenericArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
				__vm14.DestroyGenericArgumentState (value14, ref arg14);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this        JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateGenericArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateGenericArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateGenericArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, ctor, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
				__vm14.DestroyGenericArgumentState (value14, ref arg14);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateGenericArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateGenericArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateGenericArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateGenericArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
				__vm14.DestroyGenericArgumentState (value14, ref arg14);
				__vm15.DestroyGenericArgumentState (value15, ref arg15);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this        JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateGenericArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateGenericArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateGenericArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateGenericArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, ctor, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
				__vm14.DestroyGenericArgumentState (value14, ref arg14);
				__vm15.DestroyGenericArgumentState (value15, ref arg15);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15, value16);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateGenericArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateGenericArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateGenericArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateGenericArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateGenericArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
				__vm14.DestroyGenericArgumentState (value14, ref arg14);
				__vm15.DestroyGenericArgumentState (value15, ref arg15);
				__vm16.DestroyGenericArgumentState (value16, ref arg16);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this        JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15, value16);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			JniPeerMembers.JniInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateGenericArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateGenericArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateGenericArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateGenericArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateGenericArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, ctor, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
				__vm14.DestroyGenericArgumentState (value14, ref arg14);
				__vm15.DestroyGenericArgumentState (value15, ref arg15);
				__vm16.DestroyGenericArgumentState (value16, ref arg16);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self
		)
		{

			var args = stackalloc JniArgumentValue [0];

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T value
		)
		{
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

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateGenericArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateGenericArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateGenericArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateGenericArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateGenericArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateGenericArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
				__vm14.DestroyGenericArgumentState (value14, ref arg14);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateGenericArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateGenericArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateGenericArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateGenericArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
				__vm14.DestroyGenericArgumentState (value14, ref arg14);
				__vm15.DestroyGenericArgumentState (value15, ref arg15);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateGenericArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateGenericArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateGenericArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateGenericArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateGenericArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
				__vm14.DestroyGenericArgumentState (value14, ref arg14);
				__vm15.DestroyGenericArgumentState (value15, ref arg15);
				__vm16.DestroyGenericArgumentState (value16, ref arg16);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self
		)
		{

			var args = stackalloc JniArgumentValue [0];

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateGenericArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				__vm.DestroyGenericArgumentState (value, ref arg);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateGenericArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateGenericArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateGenericArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateGenericArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateGenericArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateGenericArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
				__vm14.DestroyGenericArgumentState (value14, ref arg14);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateGenericArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateGenericArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateGenericArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateGenericArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
				__vm14.DestroyGenericArgumentState (value14, ref arg14);
				__vm15.DestroyGenericArgumentState (value15, ref arg15);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateGenericArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateGenericArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateGenericArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateGenericArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateGenericArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateGenericArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateGenericArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
				__vm14.DestroyGenericArgumentState (value14, ref arg14);
				__vm15.DestroyGenericArgumentState (value15, ref arg15);
				__vm16.DestroyGenericArgumentState (value16, ref arg16);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self
		)
		{

			var args = stackalloc JniArgumentValue [0];

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateGenericArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				__vm.DestroyGenericArgumentState (value, ref arg);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateGenericArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateGenericArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateGenericArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateGenericArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateGenericArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateGenericArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateGenericArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateGenericArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateGenericArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
				__vm14.DestroyGenericArgumentState (value14, ref arg14);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
				__vm14.DestroyGenericArgumentState (value14, ref arg14);
				__vm15.DestroyGenericArgumentState (value15, ref arg15);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
				__vm14.DestroyGenericArgumentState (value14, ref arg14);
				__vm15.DestroyGenericArgumentState (value15, ref arg15);
				__vm16.DestroyGenericArgumentState (value16, ref arg16);
			}
		}

		public static unsafe char InvokeGenericVirtualCharMethod (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self
		)
		{

			var args = stackalloc JniArgumentValue [0];

			try {
				return peer.InvokeVirtualCharMethod (encodedMember, self, args);
			} finally {
			}
		}

		public static unsafe char InvokeGenericVirtualCharMethod<T> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				return peer.InvokeVirtualCharMethod (encodedMember, self, args);
			} finally {
				__vm.DestroyGenericArgumentState (value, ref arg);
			}
		}

		public static unsafe char InvokeGenericVirtualCharMethod<T1, T2> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				return peer.InvokeVirtualCharMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
			}
		}

		public static unsafe char InvokeGenericVirtualCharMethod<T1, T2, T3> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				return peer.InvokeVirtualCharMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
			}
		}

		public static unsafe char InvokeGenericVirtualCharMethod<T1, T2, T3, T4> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				return peer.InvokeVirtualCharMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
			}
		}

		public static unsafe char InvokeGenericVirtualCharMethod<T1, T2, T3, T4, T5> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				return peer.InvokeVirtualCharMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
			}
		}

		public static unsafe char InvokeGenericVirtualCharMethod<T1, T2, T3, T4, T5, T6> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				return peer.InvokeVirtualCharMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
			}
		}

		public static unsafe char InvokeGenericVirtualCharMethod<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				return peer.InvokeVirtualCharMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
			}
		}

		public static unsafe char InvokeGenericVirtualCharMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				return peer.InvokeVirtualCharMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
			}
		}

		public static unsafe char InvokeGenericVirtualCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				return peer.InvokeVirtualCharMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
			}
		}

		public static unsafe char InvokeGenericVirtualCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				return peer.InvokeVirtualCharMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
			}
		}

		public static unsafe char InvokeGenericVirtualCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				return peer.InvokeVirtualCharMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
			}
		}

		public static unsafe char InvokeGenericVirtualCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				return peer.InvokeVirtualCharMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
			}
		}

		public static unsafe char InvokeGenericVirtualCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				return peer.InvokeVirtualCharMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyGenericArgumentState (value1, ref arg1);
				__vm2.DestroyGenericArgumentState (value2, ref arg2);
				__vm3.DestroyGenericArgumentState (value3, ref arg3);
				__vm4.DestroyGenericArgumentState (value4, ref arg4);
				__vm5.DestroyGenericArgumentState (value5, ref arg5);
				__vm6.DestroyGenericArgumentState (value6, ref arg6);
				__vm7.DestroyGenericArgumentState (value7, ref arg7);
				__vm8.DestroyGenericArgumentState (value8, ref arg8);
				__vm9.DestroyGenericArgumentState (value9, ref arg9);
				__vm10.DestroyGenericArgumentState (value10, ref arg10);
				__vm11.DestroyGenericArgumentState (value11, ref arg11);
				__vm12.DestroyGenericArgumentState (value12, ref arg12);
				__vm13.DestroyGenericArgumentState (value13, ref arg13);
			}
		}

		public static unsafe char InvokeGenericVirtualCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				return peer.InvokeVirtualCharMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
			}
		}

		public static unsafe char InvokeGenericVirtualCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				return peer.InvokeVirtualCharMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
			}
		}

		public static unsafe char InvokeGenericVirtualCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				return peer.InvokeVirtualCharMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
				__vm16.DestroyArgumentState (value16, ref arg16);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self
		)
		{

			var args = stackalloc JniArgumentValue [0];

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				__vm.DestroyArgumentState (value, ref arg);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
				__vm16.DestroyArgumentState (value16, ref arg16);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self
		)
		{

			var args = stackalloc JniArgumentValue [0];

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				__vm.DestroyArgumentState (value, ref arg);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
				__vm16.DestroyArgumentState (value16, ref arg16);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self
		)
		{

			var args = stackalloc JniArgumentValue [0];

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				__vm.DestroyArgumentState (value, ref arg);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
				__vm16.DestroyArgumentState (value16, ref arg16);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self
		)
		{

			var args = stackalloc JniArgumentValue [0];

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				__vm.DestroyArgumentState (value, ref arg);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
				__vm16.DestroyArgumentState (value16, ref arg16);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self
		)
		{

			var args = stackalloc JniArgumentValue [0];

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				__vm.DestroyArgumentState (value, ref arg);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
				__vm16.DestroyArgumentState (value16, ref arg16);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self
		)
		{

			var args = stackalloc JniArgumentValue [0];

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				__vm.DestroyArgumentState (value, ref arg);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerMembers.JniInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
				__vm16.DestroyArgumentState (value16, ref arg16);
			}
		}
	}

	public static partial class JniPeerStaticMethodsExtensions {


		public static unsafe void InvokeGenericVoidMethod<T> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				__vm.DestroyArgumentState (value, ref arg);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6, T7> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
				__vm16.DestroyArgumentState (value16, ref arg16);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				__vm.DestroyArgumentState (value, ref arg);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6, T7> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
				__vm16.DestroyArgumentState (value16, ref arg16);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				__vm.DestroyArgumentState (value, ref arg);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6, T7> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
				__vm16.DestroyArgumentState (value16, ref arg16);
			}
		}

		public static unsafe char InvokeGenericCharMethod<T> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				return peer.InvokeCharMethod (encodedMember, args);
			} finally {
				__vm.DestroyArgumentState (value, ref arg);
			}
		}

		public static unsafe char InvokeGenericCharMethod<T1, T2> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				return peer.InvokeCharMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
			}
		}

		public static unsafe char InvokeGenericCharMethod<T1, T2, T3> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				return peer.InvokeCharMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
			}
		}

		public static unsafe char InvokeGenericCharMethod<T1, T2, T3, T4> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				return peer.InvokeCharMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
			}
		}

		public static unsafe char InvokeGenericCharMethod<T1, T2, T3, T4, T5> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				return peer.InvokeCharMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
			}
		}

		public static unsafe char InvokeGenericCharMethod<T1, T2, T3, T4, T5, T6> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				return peer.InvokeCharMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
			}
		}

		public static unsafe char InvokeGenericCharMethod<T1, T2, T3, T4, T5, T6, T7> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				return peer.InvokeCharMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
			}
		}

		public static unsafe char InvokeGenericCharMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				return peer.InvokeCharMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
			}
		}

		public static unsafe char InvokeGenericCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				return peer.InvokeCharMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
			}
		}

		public static unsafe char InvokeGenericCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				return peer.InvokeCharMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
			}
		}

		public static unsafe char InvokeGenericCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				return peer.InvokeCharMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
			}
		}

		public static unsafe char InvokeGenericCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				return peer.InvokeCharMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
			}
		}

		public static unsafe char InvokeGenericCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				return peer.InvokeCharMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
			}
		}

		public static unsafe char InvokeGenericCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				return peer.InvokeCharMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
			}
		}

		public static unsafe char InvokeGenericCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				return peer.InvokeCharMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
			}
		}

		public static unsafe char InvokeGenericCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				return peer.InvokeCharMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
				__vm16.DestroyArgumentState (value16, ref arg16);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				__vm.DestroyArgumentState (value, ref arg);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6, T7> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6, T7, T8> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
				__vm16.DestroyArgumentState (value16, ref arg16);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				__vm.DestroyArgumentState (value, ref arg);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6, T7> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6, T7, T8> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
				__vm16.DestroyArgumentState (value16, ref arg16);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				__vm.DestroyArgumentState (value, ref arg);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6, T7> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6, T7, T8> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
				__vm16.DestroyArgumentState (value16, ref arg16);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				__vm.DestroyArgumentState (value, ref arg);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6, T7> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
				__vm16.DestroyArgumentState (value16, ref arg16);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				__vm.DestroyArgumentState (value, ref arg);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6, T7> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
				__vm16.DestroyArgumentState (value16, ref arg16);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T value
		)
		{
			var __vm = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T> ();
			var arg = __vm.CreateArgumentState (value);

			var args = stackalloc JniArgumentValue [1];
			args [0] = arg.JniArgumentValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				__vm.DestroyArgumentState (value, ref arg);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);

			var args = stackalloc JniArgumentValue [2];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);

			var args = stackalloc JniArgumentValue [3];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);

			var args = stackalloc JniArgumentValue [4];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);

			var args = stackalloc JniArgumentValue [5];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);

			var args = stackalloc JniArgumentValue [6];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6, T7> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);

			var args = stackalloc JniArgumentValue [7];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);

			var args = stackalloc JniArgumentValue [8];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);

			var args = stackalloc JniArgumentValue [9];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);

			var args = stackalloc JniArgumentValue [10];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);

			var args = stackalloc JniArgumentValue [11];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);

			var args = stackalloc JniArgumentValue [12];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);

			var args = stackalloc JniArgumentValue [13];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);

			var args = stackalloc JniArgumentValue [14];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);

			var args = stackalloc JniArgumentValue [15];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    this    JniPeerMembers.JniStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			var __vm1 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T1> ();
			var arg1 = __vm1.CreateArgumentState (value1);
			var __vm2 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T2> ();
			var arg2 = __vm2.CreateArgumentState (value2);
			var __vm3 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T3> ();
			var arg3 = __vm3.CreateArgumentState (value3);
			var __vm4 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T4> ();
			var arg4 = __vm4.CreateArgumentState (value4);
			var __vm5 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T5> ();
			var arg5 = __vm5.CreateArgumentState (value5);
			var __vm6 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T6> ();
			var arg6 = __vm6.CreateArgumentState (value6);
			var __vm7 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T7> ();
			var arg7 = __vm7.CreateArgumentState (value7);
			var __vm8 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T8> ();
			var arg8 = __vm8.CreateArgumentState (value8);
			var __vm9 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T9> ();
			var arg9 = __vm9.CreateArgumentState (value9);
			var __vm10 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T10> ();
			var arg10 = __vm10.CreateArgumentState (value10);
			var __vm11 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T11> ();
			var arg11 = __vm11.CreateArgumentState (value11);
			var __vm12 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T12> ();
			var arg12 = __vm12.CreateArgumentState (value12);
			var __vm13 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T13> ();
			var arg13 = __vm13.CreateArgumentState (value13);
			var __vm14 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T14> ();
			var arg14 = __vm14.CreateArgumentState (value14);
			var __vm15 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T15> ();
			var arg15 = __vm15.CreateArgumentState (value15);
			var __vm16 = JniEnvironment.Runtime.ValueManager.GetValueMarshaler<T16> ();
			var arg16 = __vm16.CreateArgumentState (value16);

			var args = stackalloc JniArgumentValue [16];
			args [0] = arg1.JniArgumentValue;
			args [1] = arg2.JniArgumentValue;
			args [2] = arg3.JniArgumentValue;
			args [3] = arg4.JniArgumentValue;
			args [4] = arg5.JniArgumentValue;
			args [5] = arg6.JniArgumentValue;
			args [6] = arg7.JniArgumentValue;
			args [7] = arg8.JniArgumentValue;
			args [8] = arg9.JniArgumentValue;
			args [9] = arg10.JniArgumentValue;
			args [10] = arg11.JniArgumentValue;
			args [11] = arg12.JniArgumentValue;
			args [12] = arg13.JniArgumentValue;
			args [13] = arg14.JniArgumentValue;
			args [14] = arg15.JniArgumentValue;
			args [15] = arg16.JniArgumentValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				__vm1.DestroyArgumentState (value1, ref arg1);
				__vm2.DestroyArgumentState (value2, ref arg2);
				__vm3.DestroyArgumentState (value3, ref arg3);
				__vm4.DestroyArgumentState (value4, ref arg4);
				__vm5.DestroyArgumentState (value5, ref arg5);
				__vm6.DestroyArgumentState (value6, ref arg6);
				__vm7.DestroyArgumentState (value7, ref arg7);
				__vm8.DestroyArgumentState (value8, ref arg8);
				__vm9.DestroyArgumentState (value9, ref arg9);
				__vm10.DestroyArgumentState (value10, ref arg10);
				__vm11.DestroyArgumentState (value11, ref arg11);
				__vm12.DestroyArgumentState (value12, ref arg12);
				__vm13.DestroyArgumentState (value13, ref arg13);
				__vm14.DestroyArgumentState (value14, ref arg14);
				__vm15.DestroyArgumentState (value15, ref arg15);
				__vm16.DestroyArgumentState (value16, ref arg16);
			}
		}
	}
}
