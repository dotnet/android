using System;

namespace Java.Interop.GenericMarshaler {

	public static partial class JniPeerInstanceMethodsExtensions {

		public static JniObjectReference StartGenericCreateInstance<T> (
			this    JniPeerInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T value
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T> (
			JniPeerInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static void FinishGenericCreateInstance<T> (
			this        JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T value
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value);
		}

		static unsafe void _InvokeConstructor<T> (
			JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.InvokeNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2> (
			this    JniPeerInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2> (
			JniPeerInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2> (
			this        JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2);
		}

		static unsafe void _InvokeConstructor<T1, T2> (
			JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.InvokeNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3> (
			this    JniPeerInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3> (
			JniPeerInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3> (
			this        JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3> (
			JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.InvokeNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4> (
			this    JniPeerInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4> (
			JniPeerInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4> (
			this        JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4> (
			JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.InvokeNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5> (
			this    JniPeerInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5> (
			JniPeerInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5> (
			this        JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5> (
			JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.InvokeNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6> (
			this    JniPeerInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6> (
			JniPeerInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6> (
			this        JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6> (
			JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.InvokeNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6, T7> (
			JniPeerInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7> (
			this        JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6, value7);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7> (
			JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.InvokeNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8> (
			JniPeerInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8> (
			this        JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8> (
			JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.InvokeNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			JniPeerInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this        JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.InvokeNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			JniPeerInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this        JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.InvokeNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			JniPeerInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this        JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.InvokeNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			JniPeerInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this        JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.InvokeNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			JniPeerInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this        JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.InvokeNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			JniPeerInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this        JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.InvokeNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			JniPeerInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this        JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.InvokeNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static JniObjectReference StartGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerInstanceMethods peer,
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (peer, constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15, value16);
			}
			return peer.AllocObject (declaringType);
		}

		static unsafe JniObjectReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			JniPeerInstanceMethods  peer,
		    string  constructorSignature,
		    Type    declaringType,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
			    return peer.NewObject (constructorSignature, declaringType, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}

		public static void FinishGenericCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this        JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (peer, constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15, value16);
		}

		static unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			JniPeerInstanceMethods  peer,
		    string      constructorSignature,
		    IJavaPeerable   self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				var methods = peer.GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.InvokeNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self
		)
		{

			var args = stackalloc JValue [0];

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static unsafe void InvokeGenericVirtualVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				peer.InvokeVirtualVoidMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self
		)
		{

			var args = stackalloc JValue [0];

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static unsafe bool InvokeGenericVirtualBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				return peer.InvokeVirtualBooleanMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self
		)
		{

			var args = stackalloc JValue [0];

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static unsafe sbyte InvokeGenericVirtualSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				return peer.InvokeVirtualSByteMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}

		public static unsafe char InvokeGenericVirtualCharacterMethod (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self
		)
		{

			var args = stackalloc JValue [0];

			try {
				return peer.InvokeVirtualCharacterMethod (encodedMember, self, args);
			} finally {
			}
		}

		public static unsafe char InvokeGenericVirtualCharacterMethod<T> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return peer.InvokeVirtualCharacterMethod (encodedMember, self, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static unsafe char InvokeGenericVirtualCharacterMethod<T1, T2> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return peer.InvokeVirtualCharacterMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static unsafe char InvokeGenericVirtualCharacterMethod<T1, T2, T3> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				return peer.InvokeVirtualCharacterMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static unsafe char InvokeGenericVirtualCharacterMethod<T1, T2, T3, T4> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				return peer.InvokeVirtualCharacterMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static unsafe char InvokeGenericVirtualCharacterMethod<T1, T2, T3, T4, T5> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				return peer.InvokeVirtualCharacterMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static unsafe char InvokeGenericVirtualCharacterMethod<T1, T2, T3, T4, T5, T6> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				return peer.InvokeVirtualCharacterMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static unsafe char InvokeGenericVirtualCharacterMethod<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				return peer.InvokeVirtualCharacterMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static unsafe char InvokeGenericVirtualCharacterMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				return peer.InvokeVirtualCharacterMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static unsafe char InvokeGenericVirtualCharacterMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				return peer.InvokeVirtualCharacterMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static unsafe char InvokeGenericVirtualCharacterMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				return peer.InvokeVirtualCharacterMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static unsafe char InvokeGenericVirtualCharacterMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				return peer.InvokeVirtualCharacterMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static unsafe char InvokeGenericVirtualCharacterMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				return peer.InvokeVirtualCharacterMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static unsafe char InvokeGenericVirtualCharacterMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				return peer.InvokeVirtualCharacterMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static unsafe char InvokeGenericVirtualCharacterMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				return peer.InvokeVirtualCharacterMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static unsafe char InvokeGenericVirtualCharacterMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				return peer.InvokeVirtualCharacterMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static unsafe char InvokeGenericVirtualCharacterMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				return peer.InvokeVirtualCharacterMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self
		)
		{

			var args = stackalloc JValue [0];

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static unsafe short InvokeGenericVirtualInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				return peer.InvokeVirtualInt16Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self
		)
		{

			var args = stackalloc JValue [0];

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static unsafe int InvokeGenericVirtualInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				return peer.InvokeVirtualInt32Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self
		)
		{

			var args = stackalloc JValue [0];

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static unsafe long InvokeGenericVirtualInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				return peer.InvokeVirtualInt64Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self
		)
		{

			var args = stackalloc JValue [0];

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static unsafe float InvokeGenericVirtualSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				return peer.InvokeVirtualSingleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self
		)
		{

			var args = stackalloc JValue [0];

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static unsafe double InvokeGenericVirtualDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				return peer.InvokeVirtualDoubleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self
		)
		{

			var args = stackalloc JValue [0];

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6, T7> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static unsafe JniObjectReference InvokeGenericVirtualObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			this    JniPeerInstanceMethods peer,
			string encodedMember,
			IJavaPeerable   self,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				return peer.InvokeVirtualObjectMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}
	}

	public static partial class JniPeerStaticMethodsExtensions {


		public static unsafe void InvokeGenericVoidMethod<T> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6, T7> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static unsafe void InvokeGenericVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				peer.InvokeVoidMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6, T7> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static unsafe bool InvokeGenericBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				return peer.InvokeBooleanMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6, T7> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static unsafe sbyte InvokeGenericSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				return peer.InvokeSByteMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}

		public static unsafe char InvokeGenericCharacterMethod<T> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return peer.InvokeCharacterMethod (encodedMember, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static unsafe char InvokeGenericCharacterMethod<T1, T2> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return peer.InvokeCharacterMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static unsafe char InvokeGenericCharacterMethod<T1, T2, T3> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				return peer.InvokeCharacterMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static unsafe char InvokeGenericCharacterMethod<T1, T2, T3, T4> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				return peer.InvokeCharacterMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static unsafe char InvokeGenericCharacterMethod<T1, T2, T3, T4, T5> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				return peer.InvokeCharacterMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static unsafe char InvokeGenericCharacterMethod<T1, T2, T3, T4, T5, T6> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				return peer.InvokeCharacterMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static unsafe char InvokeGenericCharacterMethod<T1, T2, T3, T4, T5, T6, T7> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				return peer.InvokeCharacterMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static unsafe char InvokeGenericCharacterMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				return peer.InvokeCharacterMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static unsafe char InvokeGenericCharacterMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				return peer.InvokeCharacterMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static unsafe char InvokeGenericCharacterMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				return peer.InvokeCharacterMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static unsafe char InvokeGenericCharacterMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				return peer.InvokeCharacterMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static unsafe char InvokeGenericCharacterMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				return peer.InvokeCharacterMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static unsafe char InvokeGenericCharacterMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				return peer.InvokeCharacterMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static unsafe char InvokeGenericCharacterMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				return peer.InvokeCharacterMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static unsafe char InvokeGenericCharacterMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				return peer.InvokeCharacterMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static unsafe char InvokeGenericCharacterMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				return peer.InvokeCharacterMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6, T7> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6, T7, T8> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static unsafe short InvokeGenericInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				return peer.InvokeInt16Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6, T7> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6, T7, T8> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static unsafe int InvokeGenericInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				return peer.InvokeInt32Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6, T7> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6, T7, T8> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static unsafe long InvokeGenericInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				return peer.InvokeInt64Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6, T7> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static unsafe float InvokeGenericSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				return peer.InvokeSingleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6, T7> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static unsafe double InvokeGenericDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				return peer.InvokeDoubleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);

			var args = stackalloc JValue [3];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);

			var args = stackalloc JValue [4];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);

			var args = stackalloc JValue [5];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);

			var args = stackalloc JValue [6];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6, T7> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);

			var args = stackalloc JValue [7];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);

			var args = stackalloc JValue [8];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);

			var args = stackalloc JValue [9];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);

			var args = stackalloc JValue [10];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);

			var args = stackalloc JValue [11];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);

			var args = stackalloc JValue [12];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);

			var args = stackalloc JValue [13];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);

			var args = stackalloc JValue [14];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);

			var args = stackalloc JValue [15];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
			}
		}

		public static unsafe JniObjectReference InvokeGenericObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    this    JniPeerStaticMethods    peer,
			string encodedMember,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);
			JniArgumentMarshalInfo<T3> arg3 = new JniArgumentMarshalInfo<T3>(value3);
			JniArgumentMarshalInfo<T4> arg4 = new JniArgumentMarshalInfo<T4>(value4);
			JniArgumentMarshalInfo<T5> arg5 = new JniArgumentMarshalInfo<T5>(value5);
			JniArgumentMarshalInfo<T6> arg6 = new JniArgumentMarshalInfo<T6>(value6);
			JniArgumentMarshalInfo<T7> arg7 = new JniArgumentMarshalInfo<T7>(value7);
			JniArgumentMarshalInfo<T8> arg8 = new JniArgumentMarshalInfo<T8>(value8);
			JniArgumentMarshalInfo<T9> arg9 = new JniArgumentMarshalInfo<T9>(value9);
			JniArgumentMarshalInfo<T10> arg10 = new JniArgumentMarshalInfo<T10>(value10);
			JniArgumentMarshalInfo<T11> arg11 = new JniArgumentMarshalInfo<T11>(value11);
			JniArgumentMarshalInfo<T12> arg12 = new JniArgumentMarshalInfo<T12>(value12);
			JniArgumentMarshalInfo<T13> arg13 = new JniArgumentMarshalInfo<T13>(value13);
			JniArgumentMarshalInfo<T14> arg14 = new JniArgumentMarshalInfo<T14>(value14);
			JniArgumentMarshalInfo<T15> arg15 = new JniArgumentMarshalInfo<T15>(value15);
			JniArgumentMarshalInfo<T16> arg16 = new JniArgumentMarshalInfo<T16>(value16);

			var args = stackalloc JValue [16];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;
			args [2] = arg3.JValue;
			args [3] = arg4.JValue;
			args [4] = arg5.JValue;
			args [5] = arg6.JValue;
			args [6] = arg7.JValue;
			args [7] = arg8.JValue;
			args [8] = arg9.JValue;
			args [9] = arg10.JValue;
			args [10] = arg11.JValue;
			args [11] = arg12.JValue;
			args [12] = arg13.JValue;
			args [13] = arg14.JValue;
			args [14] = arg15.JValue;
			args [15] = arg16.JValue;

			try {
				return peer.InvokeObjectMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
				arg7.Cleanup (value7);
				arg8.Cleanup (value8);
				arg9.Cleanup (value9);
				arg10.Cleanup (value10);
				arg11.Cleanup (value11);
				arg12.Cleanup (value12);
				arg13.Cleanup (value13);
				arg14.Cleanup (value14);
				arg15.Cleanup (value15);
				arg16.Cleanup (value16);
			}
		}
	}
}
