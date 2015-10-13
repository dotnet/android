using System;

namespace Java.Interop {

	partial class JniPeerInstanceMethods {

		public JniLocalReference StartCreateInstance<T> (
			string  constructorSignature,
			Type    declaringType,
			T value
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (constructorSignature, declaringType, value);
			}
			using (var lref = GetConstructorsForType (declaringType)
					.JniPeerType
					.AllocObject ())
				return lref.ToAllocObjectRef ();
		}

		unsafe JniLocalReference NewObject<T> (
		    string  constructorSignature,
		    Type    declaringType,
		    T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
			    return NewObject (constructorSignature, declaringType, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public void FinishCreateInstance<T> (
		    string      constructorSignature,
		    IJavaObject self,
		    T value
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (constructorSignature, self, value);
		}

		unsafe void _InvokeConstructor<T> (
		    string      constructorSignature,
		    IJavaObject self,
		    T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				var methods = GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.CallNonvirtualVoidMethod (self.SafeHandle, methods.JniPeerType.SafeHandle, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public JniLocalReference StartCreateInstance<T1, T2> (
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (constructorSignature, declaringType, value1, value2);
			}
			using (var lref = GetConstructorsForType (declaringType)
					.JniPeerType
					.AllocObject ())
				return lref.ToAllocObjectRef ();
		}

		unsafe JniLocalReference NewObject<T1, T2> (
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
			    return NewObject (constructorSignature, declaringType, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public void FinishCreateInstance<T1, T2> (
		    string      constructorSignature,
		    IJavaObject self,
		    T1 value1, T2 value2
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (constructorSignature, self, value1, value2);
		}

		unsafe void _InvokeConstructor<T1, T2> (
		    string      constructorSignature,
		    IJavaObject self,
		    T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				var methods = GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.CallNonvirtualVoidMethod (self.SafeHandle, methods.JniPeerType.SafeHandle, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public JniLocalReference StartCreateInstance<T1, T2, T3> (
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (constructorSignature, declaringType, value1, value2, value3);
			}
			using (var lref = GetConstructorsForType (declaringType)
					.JniPeerType
					.AllocObject ())
				return lref.ToAllocObjectRef ();
		}

		unsafe JniLocalReference NewObject<T1, T2, T3> (
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
			    return NewObject (constructorSignature, declaringType, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public void FinishCreateInstance<T1, T2, T3> (
		    string      constructorSignature,
		    IJavaObject self,
		    T1 value1, T2 value2, T3 value3
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (constructorSignature, self, value1, value2, value3);
		}

		unsafe void _InvokeConstructor<T1, T2, T3> (
		    string      constructorSignature,
		    IJavaObject self,
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
				var methods = GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.CallNonvirtualVoidMethod (self.SafeHandle, methods.JniPeerType.SafeHandle, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public JniLocalReference StartCreateInstance<T1, T2, T3, T4> (
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (constructorSignature, declaringType, value1, value2, value3, value4);
			}
			using (var lref = GetConstructorsForType (declaringType)
					.JniPeerType
					.AllocObject ())
				return lref.ToAllocObjectRef ();
		}

		unsafe JniLocalReference NewObject<T1, T2, T3, T4> (
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
			    return NewObject (constructorSignature, declaringType, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public void FinishCreateInstance<T1, T2, T3, T4> (
		    string      constructorSignature,
		    IJavaObject self,
		    T1 value1, T2 value2, T3 value3, T4 value4
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (constructorSignature, self, value1, value2, value3, value4);
		}

		unsafe void _InvokeConstructor<T1, T2, T3, T4> (
		    string      constructorSignature,
		    IJavaObject self,
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
				var methods = GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.CallNonvirtualVoidMethod (self.SafeHandle, methods.JniPeerType.SafeHandle, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public JniLocalReference StartCreateInstance<T1, T2, T3, T4, T5> (
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (constructorSignature, declaringType, value1, value2, value3, value4, value5);
			}
			using (var lref = GetConstructorsForType (declaringType)
					.JniPeerType
					.AllocObject ())
				return lref.ToAllocObjectRef ();
		}

		unsafe JniLocalReference NewObject<T1, T2, T3, T4, T5> (
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
			    return NewObject (constructorSignature, declaringType, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public void FinishCreateInstance<T1, T2, T3, T4, T5> (
		    string      constructorSignature,
		    IJavaObject self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (constructorSignature, self, value1, value2, value3, value4, value5);
		}

		unsafe void _InvokeConstructor<T1, T2, T3, T4, T5> (
		    string      constructorSignature,
		    IJavaObject self,
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
				var methods = GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.CallNonvirtualVoidMethod (self.SafeHandle, methods.JniPeerType.SafeHandle, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public JniLocalReference StartCreateInstance<T1, T2, T3, T4, T5, T6> (
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (constructorSignature, declaringType, value1, value2, value3, value4, value5, value6);
			}
			using (var lref = GetConstructorsForType (declaringType)
					.JniPeerType
					.AllocObject ())
				return lref.ToAllocObjectRef ();
		}

		unsafe JniLocalReference NewObject<T1, T2, T3, T4, T5, T6> (
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
			    return NewObject (constructorSignature, declaringType, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public void FinishCreateInstance<T1, T2, T3, T4, T5, T6> (
		    string      constructorSignature,
		    IJavaObject self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (constructorSignature, self, value1, value2, value3, value4, value5, value6);
		}

		unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6> (
		    string      constructorSignature,
		    IJavaObject self,
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
				var methods = GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.CallNonvirtualVoidMethod (self.SafeHandle, methods.JniPeerType.SafeHandle, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public JniLocalReference StartCreateInstance<T1, T2, T3, T4, T5, T6, T7> (
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7);
			}
			using (var lref = GetConstructorsForType (declaringType)
					.JniPeerType
					.AllocObject ())
				return lref.ToAllocObjectRef ();
		}

		unsafe JniLocalReference NewObject<T1, T2, T3, T4, T5, T6, T7> (
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
			    return NewObject (constructorSignature, declaringType, args);
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

		public void FinishCreateInstance<T1, T2, T3, T4, T5, T6, T7> (
		    string      constructorSignature,
		    IJavaObject self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (constructorSignature, self, value1, value2, value3, value4, value5, value6, value7);
		}

		unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7> (
		    string      constructorSignature,
		    IJavaObject self,
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
				var methods = GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.CallNonvirtualVoidMethod (self.SafeHandle, methods.JniPeerType.SafeHandle, args);
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

		public JniLocalReference StartCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8> (
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8);
			}
			using (var lref = GetConstructorsForType (declaringType)
					.JniPeerType
					.AllocObject ())
				return lref.ToAllocObjectRef ();
		}

		unsafe JniLocalReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8> (
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
			    return NewObject (constructorSignature, declaringType, args);
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

		public void FinishCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8> (
		    string      constructorSignature,
		    IJavaObject self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8);
		}

		unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8> (
		    string      constructorSignature,
		    IJavaObject self,
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
				var methods = GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.CallNonvirtualVoidMethod (self.SafeHandle, methods.JniPeerType.SafeHandle, args);
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

		public JniLocalReference StartCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9);
			}
			using (var lref = GetConstructorsForType (declaringType)
					.JniPeerType
					.AllocObject ())
				return lref.ToAllocObjectRef ();
		}

		unsafe JniLocalReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
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
			    return NewObject (constructorSignature, declaringType, args);
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

		public void FinishCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    string      constructorSignature,
		    IJavaObject self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9);
		}

		unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
		    string      constructorSignature,
		    IJavaObject self,
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
				var methods = GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.CallNonvirtualVoidMethod (self.SafeHandle, methods.JniPeerType.SafeHandle, args);
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

		public JniLocalReference StartCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
			}
			using (var lref = GetConstructorsForType (declaringType)
					.JniPeerType
					.AllocObject ())
				return lref.ToAllocObjectRef ();
		}

		unsafe JniLocalReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
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
			    return NewObject (constructorSignature, declaringType, args);
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

		public void FinishCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    string      constructorSignature,
		    IJavaObject self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
		}

		unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
		    string      constructorSignature,
		    IJavaObject self,
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
				var methods = GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.CallNonvirtualVoidMethod (self.SafeHandle, methods.JniPeerType.SafeHandle, args);
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

		public JniLocalReference StartCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
			}
			using (var lref = GetConstructorsForType (declaringType)
					.JniPeerType
					.AllocObject ())
				return lref.ToAllocObjectRef ();
		}

		unsafe JniLocalReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
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
			    return NewObject (constructorSignature, declaringType, args);
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

		public void FinishCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    string      constructorSignature,
		    IJavaObject self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
		}

		unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
		    string      constructorSignature,
		    IJavaObject self,
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
				var methods = GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.CallNonvirtualVoidMethod (self.SafeHandle, methods.JniPeerType.SafeHandle, args);
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

		public JniLocalReference StartCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
			}
			using (var lref = GetConstructorsForType (declaringType)
					.JniPeerType
					.AllocObject ())
				return lref.ToAllocObjectRef ();
		}

		unsafe JniLocalReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
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
			    return NewObject (constructorSignature, declaringType, args);
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

		public void FinishCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    string      constructorSignature,
		    IJavaObject self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
		}

		unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
		    string      constructorSignature,
		    IJavaObject self,
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
				var methods = GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.CallNonvirtualVoidMethod (self.SafeHandle, methods.JniPeerType.SafeHandle, args);
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

		public JniLocalReference StartCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
			}
			using (var lref = GetConstructorsForType (declaringType)
					.JniPeerType
					.AllocObject ())
				return lref.ToAllocObjectRef ();
		}

		unsafe JniLocalReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
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
			    return NewObject (constructorSignature, declaringType, args);
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

		public void FinishCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    string      constructorSignature,
		    IJavaObject self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
		}

		unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
		    string      constructorSignature,
		    IJavaObject self,
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
				var methods = GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.CallNonvirtualVoidMethod (self.SafeHandle, methods.JniPeerType.SafeHandle, args);
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

		public JniLocalReference StartCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
			}
			using (var lref = GetConstructorsForType (declaringType)
					.JniPeerType
					.AllocObject ())
				return lref.ToAllocObjectRef ();
		}

		unsafe JniLocalReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
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
			    return NewObject (constructorSignature, declaringType, args);
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

		public void FinishCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    string      constructorSignature,
		    IJavaObject self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
		}

		unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
		    string      constructorSignature,
		    IJavaObject self,
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
				var methods = GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.CallNonvirtualVoidMethod (self.SafeHandle, methods.JniPeerType.SafeHandle, args);
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

		public JniLocalReference StartCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
			}
			using (var lref = GetConstructorsForType (declaringType)
					.JniPeerType
					.AllocObject ())
				return lref.ToAllocObjectRef ();
		}

		unsafe JniLocalReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
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
			    return NewObject (constructorSignature, declaringType, args);
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

		public void FinishCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    string      constructorSignature,
		    IJavaObject self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
		}

		unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
		    string      constructorSignature,
		    IJavaObject self,
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
				var methods = GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.CallNonvirtualVoidMethod (self.SafeHandle, methods.JniPeerType.SafeHandle, args);
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

		public JniLocalReference StartCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			string  constructorSignature,
			Type    declaringType,
			T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return NewObject (constructorSignature, declaringType, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15, value16);
			}
			using (var lref = GetConstructorsForType (declaringType)
					.JniPeerType
					.AllocObject ())
				return lref.ToAllocObjectRef ();
		}

		unsafe JniLocalReference NewObject<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
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
			    return NewObject (constructorSignature, declaringType, args);
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

		public void FinishCreateInstance<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    string      constructorSignature,
		    IJavaObject self,
		    T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15, T16 value16
		)
		{
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			_InvokeConstructor (constructorSignature, self, value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15, value16);
		}

		unsafe void _InvokeConstructor<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
		    string      constructorSignature,
		    IJavaObject self,
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
				var methods = GetConstructorsForType (self.GetType ());
				var ctor    = methods.GetConstructor (constructorSignature);
				ctor.CallNonvirtualVoidMethod (self.SafeHandle, methods.JniPeerType.SafeHandle, args);
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
		public unsafe void CallVoidMethod (
			string encodedMember,
			IJavaObject self,
			JValue* parameters)
		{
			JniPeerMembers.AssertSelf (self);

			if (self.GetType () == DeclaringType || DeclaringType == null) {
				var m = GetMethodID (encodedMember);
				m.CallVirtualVoidMethod (self.SafeHandle, parameters);
			}
			var j = self.JniPeerMembers;
			var n = j.InstanceMethods.GetMethodID (encodedMember);
			n.CallNonvirtualVoidMethod (self.SafeHandle, j.JniPeerType.SafeHandle, parameters);
		}

		public unsafe void CallVoidMethod (
			string encodedMember,
			IJavaObject self
		)
		{

			var args = stackalloc JValue [0];

			try {
				CallVoidMethod (encodedMember, self, args);
			} finally {
			}
		}

		public unsafe void CallVoidMethod<T> (
			string encodedMember,
			IJavaObject self,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				CallVoidMethod (encodedMember, self, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public unsafe void CallVoidMethod<T1, T2> (
			string encodedMember,
			IJavaObject self,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				CallVoidMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public unsafe void CallVoidMethod<T1, T2, T3> (
			string encodedMember,
			IJavaObject self,
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
				CallVoidMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public unsafe void CallVoidMethod<T1, T2, T3, T4> (
			string encodedMember,
			IJavaObject self,
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
				CallVoidMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5> (
			string encodedMember,
			IJavaObject self,
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
				CallVoidMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6> (
			string encodedMember,
			IJavaObject self,
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
				CallVoidMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6, T7> (
			string encodedMember,
			IJavaObject self,
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
				CallVoidMethod (encodedMember, self, args);
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

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			string encodedMember,
			IJavaObject self,
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
				CallVoidMethod (encodedMember, self, args);
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

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			string encodedMember,
			IJavaObject self,
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
				CallVoidMethod (encodedMember, self, args);
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

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			string encodedMember,
			IJavaObject self,
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
				CallVoidMethod (encodedMember, self, args);
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

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			string encodedMember,
			IJavaObject self,
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
				CallVoidMethod (encodedMember, self, args);
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

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			string encodedMember,
			IJavaObject self,
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
				CallVoidMethod (encodedMember, self, args);
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

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			string encodedMember,
			IJavaObject self,
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
				CallVoidMethod (encodedMember, self, args);
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

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			string encodedMember,
			IJavaObject self,
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
				CallVoidMethod (encodedMember, self, args);
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

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			string encodedMember,
			IJavaObject self,
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
				CallVoidMethod (encodedMember, self, args);
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

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			string encodedMember,
			IJavaObject self,
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
				CallVoidMethod (encodedMember, self, args);
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
		public unsafe bool CallBooleanMethod (
			string encodedMember,
			IJavaObject self,
			JValue* parameters)
		{
			JniPeerMembers.AssertSelf (self);

			if (self.GetType () == DeclaringType || DeclaringType == null) {
				var m = GetMethodID (encodedMember);
				return m.CallVirtualBooleanMethod (self.SafeHandle, parameters);
			}
			var j = self.JniPeerMembers;
			var n = j.InstanceMethods.GetMethodID (encodedMember);
			return n.CallNonvirtualBooleanMethod (self.SafeHandle, j.JniPeerType.SafeHandle, parameters);
		}

		public unsafe bool CallBooleanMethod (
			string encodedMember,
			IJavaObject self
		)
		{

			var args = stackalloc JValue [0];

			try {
				return CallBooleanMethod (encodedMember, self, args);
			} finally {
			}
		}

		public unsafe bool CallBooleanMethod<T> (
			string encodedMember,
			IJavaObject self,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return CallBooleanMethod (encodedMember, self, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public unsafe bool CallBooleanMethod<T1, T2> (
			string encodedMember,
			IJavaObject self,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return CallBooleanMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public unsafe bool CallBooleanMethod<T1, T2, T3> (
			string encodedMember,
			IJavaObject self,
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
				return CallBooleanMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4> (
			string encodedMember,
			IJavaObject self,
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
				return CallBooleanMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5> (
			string encodedMember,
			IJavaObject self,
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
				return CallBooleanMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6> (
			string encodedMember,
			IJavaObject self,
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
				return CallBooleanMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6, T7> (
			string encodedMember,
			IJavaObject self,
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
				return CallBooleanMethod (encodedMember, self, args);
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

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			string encodedMember,
			IJavaObject self,
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
				return CallBooleanMethod (encodedMember, self, args);
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

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			string encodedMember,
			IJavaObject self,
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
				return CallBooleanMethod (encodedMember, self, args);
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

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			string encodedMember,
			IJavaObject self,
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
				return CallBooleanMethod (encodedMember, self, args);
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

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			string encodedMember,
			IJavaObject self,
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
				return CallBooleanMethod (encodedMember, self, args);
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

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			string encodedMember,
			IJavaObject self,
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
				return CallBooleanMethod (encodedMember, self, args);
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

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			string encodedMember,
			IJavaObject self,
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
				return CallBooleanMethod (encodedMember, self, args);
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

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			string encodedMember,
			IJavaObject self,
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
				return CallBooleanMethod (encodedMember, self, args);
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

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			string encodedMember,
			IJavaObject self,
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
				return CallBooleanMethod (encodedMember, self, args);
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

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			string encodedMember,
			IJavaObject self,
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
				return CallBooleanMethod (encodedMember, self, args);
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
		public unsafe sbyte CallSByteMethod (
			string encodedMember,
			IJavaObject self,
			JValue* parameters)
		{
			JniPeerMembers.AssertSelf (self);

			if (self.GetType () == DeclaringType || DeclaringType == null) {
				var m = GetMethodID (encodedMember);
				return m.CallVirtualSByteMethod (self.SafeHandle, parameters);
			}
			var j = self.JniPeerMembers;
			var n = j.InstanceMethods.GetMethodID (encodedMember);
			return n.CallNonvirtualSByteMethod (self.SafeHandle, j.JniPeerType.SafeHandle, parameters);
		}

		public unsafe sbyte CallSByteMethod (
			string encodedMember,
			IJavaObject self
		)
		{

			var args = stackalloc JValue [0];

			try {
				return CallSByteMethod (encodedMember, self, args);
			} finally {
			}
		}

		public unsafe sbyte CallSByteMethod<T> (
			string encodedMember,
			IJavaObject self,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return CallSByteMethod (encodedMember, self, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public unsafe sbyte CallSByteMethod<T1, T2> (
			string encodedMember,
			IJavaObject self,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return CallSByteMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public unsafe sbyte CallSByteMethod<T1, T2, T3> (
			string encodedMember,
			IJavaObject self,
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
				return CallSByteMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4> (
			string encodedMember,
			IJavaObject self,
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
				return CallSByteMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5> (
			string encodedMember,
			IJavaObject self,
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
				return CallSByteMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6> (
			string encodedMember,
			IJavaObject self,
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
				return CallSByteMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6, T7> (
			string encodedMember,
			IJavaObject self,
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
				return CallSByteMethod (encodedMember, self, args);
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

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			string encodedMember,
			IJavaObject self,
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
				return CallSByteMethod (encodedMember, self, args);
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

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			string encodedMember,
			IJavaObject self,
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
				return CallSByteMethod (encodedMember, self, args);
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

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			string encodedMember,
			IJavaObject self,
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
				return CallSByteMethod (encodedMember, self, args);
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

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			string encodedMember,
			IJavaObject self,
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
				return CallSByteMethod (encodedMember, self, args);
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

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			string encodedMember,
			IJavaObject self,
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
				return CallSByteMethod (encodedMember, self, args);
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

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			string encodedMember,
			IJavaObject self,
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
				return CallSByteMethod (encodedMember, self, args);
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

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			string encodedMember,
			IJavaObject self,
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
				return CallSByteMethod (encodedMember, self, args);
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

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			string encodedMember,
			IJavaObject self,
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
				return CallSByteMethod (encodedMember, self, args);
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

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			string encodedMember,
			IJavaObject self,
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
				return CallSByteMethod (encodedMember, self, args);
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
		public unsafe char CallCharMethod (
			string encodedMember,
			IJavaObject self,
			JValue* parameters)
		{
			JniPeerMembers.AssertSelf (self);

			if (self.GetType () == DeclaringType || DeclaringType == null) {
				var m = GetMethodID (encodedMember);
				return m.CallVirtualCharMethod (self.SafeHandle, parameters);
			}
			var j = self.JniPeerMembers;
			var n = j.InstanceMethods.GetMethodID (encodedMember);
			return n.CallNonvirtualCharMethod (self.SafeHandle, j.JniPeerType.SafeHandle, parameters);
		}

		public unsafe char CallCharMethod (
			string encodedMember,
			IJavaObject self
		)
		{

			var args = stackalloc JValue [0];

			try {
				return CallCharMethod (encodedMember, self, args);
			} finally {
			}
		}

		public unsafe char CallCharMethod<T> (
			string encodedMember,
			IJavaObject self,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return CallCharMethod (encodedMember, self, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public unsafe char CallCharMethod<T1, T2> (
			string encodedMember,
			IJavaObject self,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return CallCharMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public unsafe char CallCharMethod<T1, T2, T3> (
			string encodedMember,
			IJavaObject self,
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
				return CallCharMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public unsafe char CallCharMethod<T1, T2, T3, T4> (
			string encodedMember,
			IJavaObject self,
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
				return CallCharMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5> (
			string encodedMember,
			IJavaObject self,
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
				return CallCharMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6> (
			string encodedMember,
			IJavaObject self,
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
				return CallCharMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6, T7> (
			string encodedMember,
			IJavaObject self,
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
				return CallCharMethod (encodedMember, self, args);
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

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			string encodedMember,
			IJavaObject self,
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
				return CallCharMethod (encodedMember, self, args);
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

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			string encodedMember,
			IJavaObject self,
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
				return CallCharMethod (encodedMember, self, args);
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

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			string encodedMember,
			IJavaObject self,
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
				return CallCharMethod (encodedMember, self, args);
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

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			string encodedMember,
			IJavaObject self,
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
				return CallCharMethod (encodedMember, self, args);
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

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			string encodedMember,
			IJavaObject self,
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
				return CallCharMethod (encodedMember, self, args);
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

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			string encodedMember,
			IJavaObject self,
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
				return CallCharMethod (encodedMember, self, args);
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

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			string encodedMember,
			IJavaObject self,
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
				return CallCharMethod (encodedMember, self, args);
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

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			string encodedMember,
			IJavaObject self,
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
				return CallCharMethod (encodedMember, self, args);
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

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			string encodedMember,
			IJavaObject self,
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
				return CallCharMethod (encodedMember, self, args);
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
		public unsafe short CallInt16Method (
			string encodedMember,
			IJavaObject self,
			JValue* parameters)
		{
			JniPeerMembers.AssertSelf (self);

			if (self.GetType () == DeclaringType || DeclaringType == null) {
				var m = GetMethodID (encodedMember);
				return m.CallVirtualInt16Method (self.SafeHandle, parameters);
			}
			var j = self.JniPeerMembers;
			var n = j.InstanceMethods.GetMethodID (encodedMember);
			return n.CallNonvirtualInt16Method (self.SafeHandle, j.JniPeerType.SafeHandle, parameters);
		}

		public unsafe short CallInt16Method (
			string encodedMember,
			IJavaObject self
		)
		{

			var args = stackalloc JValue [0];

			try {
				return CallInt16Method (encodedMember, self, args);
			} finally {
			}
		}

		public unsafe short CallInt16Method<T> (
			string encodedMember,
			IJavaObject self,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return CallInt16Method (encodedMember, self, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public unsafe short CallInt16Method<T1, T2> (
			string encodedMember,
			IJavaObject self,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return CallInt16Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public unsafe short CallInt16Method<T1, T2, T3> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt16Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public unsafe short CallInt16Method<T1, T2, T3, T4> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt16Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt16Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt16Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6, T7> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt16Method (encodedMember, self, args);
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

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6, T7, T8> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt16Method (encodedMember, self, args);
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

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt16Method (encodedMember, self, args);
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

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt16Method (encodedMember, self, args);
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

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt16Method (encodedMember, self, args);
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

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt16Method (encodedMember, self, args);
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

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt16Method (encodedMember, self, args);
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

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt16Method (encodedMember, self, args);
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

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt16Method (encodedMember, self, args);
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

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt16Method (encodedMember, self, args);
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
		public unsafe int CallInt32Method (
			string encodedMember,
			IJavaObject self,
			JValue* parameters)
		{
			JniPeerMembers.AssertSelf (self);

			if (self.GetType () == DeclaringType || DeclaringType == null) {
				var m = GetMethodID (encodedMember);
				return m.CallVirtualInt32Method (self.SafeHandle, parameters);
			}
			var j = self.JniPeerMembers;
			var n = j.InstanceMethods.GetMethodID (encodedMember);
			return n.CallNonvirtualInt32Method (self.SafeHandle, j.JniPeerType.SafeHandle, parameters);
		}

		public unsafe int CallInt32Method (
			string encodedMember,
			IJavaObject self
		)
		{

			var args = stackalloc JValue [0];

			try {
				return CallInt32Method (encodedMember, self, args);
			} finally {
			}
		}

		public unsafe int CallInt32Method<T> (
			string encodedMember,
			IJavaObject self,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return CallInt32Method (encodedMember, self, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public unsafe int CallInt32Method<T1, T2> (
			string encodedMember,
			IJavaObject self,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return CallInt32Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public unsafe int CallInt32Method<T1, T2, T3> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt32Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public unsafe int CallInt32Method<T1, T2, T3, T4> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt32Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt32Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt32Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6, T7> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt32Method (encodedMember, self, args);
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

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6, T7, T8> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt32Method (encodedMember, self, args);
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

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt32Method (encodedMember, self, args);
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

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt32Method (encodedMember, self, args);
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

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt32Method (encodedMember, self, args);
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

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt32Method (encodedMember, self, args);
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

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt32Method (encodedMember, self, args);
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

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt32Method (encodedMember, self, args);
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

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt32Method (encodedMember, self, args);
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

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt32Method (encodedMember, self, args);
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
		public unsafe long CallInt64Method (
			string encodedMember,
			IJavaObject self,
			JValue* parameters)
		{
			JniPeerMembers.AssertSelf (self);

			if (self.GetType () == DeclaringType || DeclaringType == null) {
				var m = GetMethodID (encodedMember);
				return m.CallVirtualInt64Method (self.SafeHandle, parameters);
			}
			var j = self.JniPeerMembers;
			var n = j.InstanceMethods.GetMethodID (encodedMember);
			return n.CallNonvirtualInt64Method (self.SafeHandle, j.JniPeerType.SafeHandle, parameters);
		}

		public unsafe long CallInt64Method (
			string encodedMember,
			IJavaObject self
		)
		{

			var args = stackalloc JValue [0];

			try {
				return CallInt64Method (encodedMember, self, args);
			} finally {
			}
		}

		public unsafe long CallInt64Method<T> (
			string encodedMember,
			IJavaObject self,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return CallInt64Method (encodedMember, self, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public unsafe long CallInt64Method<T1, T2> (
			string encodedMember,
			IJavaObject self,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return CallInt64Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public unsafe long CallInt64Method<T1, T2, T3> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt64Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public unsafe long CallInt64Method<T1, T2, T3, T4> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt64Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt64Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt64Method (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6, T7> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt64Method (encodedMember, self, args);
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

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6, T7, T8> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt64Method (encodedMember, self, args);
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

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt64Method (encodedMember, self, args);
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

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt64Method (encodedMember, self, args);
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

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt64Method (encodedMember, self, args);
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

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt64Method (encodedMember, self, args);
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

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt64Method (encodedMember, self, args);
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

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt64Method (encodedMember, self, args);
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

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt64Method (encodedMember, self, args);
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

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			string encodedMember,
			IJavaObject self,
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
				return CallInt64Method (encodedMember, self, args);
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
		public unsafe float CallSingleMethod (
			string encodedMember,
			IJavaObject self,
			JValue* parameters)
		{
			JniPeerMembers.AssertSelf (self);

			if (self.GetType () == DeclaringType || DeclaringType == null) {
				var m = GetMethodID (encodedMember);
				return m.CallVirtualSingleMethod (self.SafeHandle, parameters);
			}
			var j = self.JniPeerMembers;
			var n = j.InstanceMethods.GetMethodID (encodedMember);
			return n.CallNonvirtualSingleMethod (self.SafeHandle, j.JniPeerType.SafeHandle, parameters);
		}

		public unsafe float CallSingleMethod (
			string encodedMember,
			IJavaObject self
		)
		{

			var args = stackalloc JValue [0];

			try {
				return CallSingleMethod (encodedMember, self, args);
			} finally {
			}
		}

		public unsafe float CallSingleMethod<T> (
			string encodedMember,
			IJavaObject self,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return CallSingleMethod (encodedMember, self, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public unsafe float CallSingleMethod<T1, T2> (
			string encodedMember,
			IJavaObject self,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return CallSingleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public unsafe float CallSingleMethod<T1, T2, T3> (
			string encodedMember,
			IJavaObject self,
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
				return CallSingleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public unsafe float CallSingleMethod<T1, T2, T3, T4> (
			string encodedMember,
			IJavaObject self,
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
				return CallSingleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5> (
			string encodedMember,
			IJavaObject self,
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
				return CallSingleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6> (
			string encodedMember,
			IJavaObject self,
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
				return CallSingleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6, T7> (
			string encodedMember,
			IJavaObject self,
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
				return CallSingleMethod (encodedMember, self, args);
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

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			string encodedMember,
			IJavaObject self,
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
				return CallSingleMethod (encodedMember, self, args);
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

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			string encodedMember,
			IJavaObject self,
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
				return CallSingleMethod (encodedMember, self, args);
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

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			string encodedMember,
			IJavaObject self,
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
				return CallSingleMethod (encodedMember, self, args);
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

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			string encodedMember,
			IJavaObject self,
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
				return CallSingleMethod (encodedMember, self, args);
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

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			string encodedMember,
			IJavaObject self,
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
				return CallSingleMethod (encodedMember, self, args);
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

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			string encodedMember,
			IJavaObject self,
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
				return CallSingleMethod (encodedMember, self, args);
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

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			string encodedMember,
			IJavaObject self,
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
				return CallSingleMethod (encodedMember, self, args);
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

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			string encodedMember,
			IJavaObject self,
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
				return CallSingleMethod (encodedMember, self, args);
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

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			string encodedMember,
			IJavaObject self,
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
				return CallSingleMethod (encodedMember, self, args);
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
		public unsafe double CallDoubleMethod (
			string encodedMember,
			IJavaObject self,
			JValue* parameters)
		{
			JniPeerMembers.AssertSelf (self);

			if (self.GetType () == DeclaringType || DeclaringType == null) {
				var m = GetMethodID (encodedMember);
				return m.CallVirtualDoubleMethod (self.SafeHandle, parameters);
			}
			var j = self.JniPeerMembers;
			var n = j.InstanceMethods.GetMethodID (encodedMember);
			return n.CallNonvirtualDoubleMethod (self.SafeHandle, j.JniPeerType.SafeHandle, parameters);
		}

		public unsafe double CallDoubleMethod (
			string encodedMember,
			IJavaObject self
		)
		{

			var args = stackalloc JValue [0];

			try {
				return CallDoubleMethod (encodedMember, self, args);
			} finally {
			}
		}

		public unsafe double CallDoubleMethod<T> (
			string encodedMember,
			IJavaObject self,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return CallDoubleMethod (encodedMember, self, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public unsafe double CallDoubleMethod<T1, T2> (
			string encodedMember,
			IJavaObject self,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return CallDoubleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public unsafe double CallDoubleMethod<T1, T2, T3> (
			string encodedMember,
			IJavaObject self,
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
				return CallDoubleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public unsafe double CallDoubleMethod<T1, T2, T3, T4> (
			string encodedMember,
			IJavaObject self,
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
				return CallDoubleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5> (
			string encodedMember,
			IJavaObject self,
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
				return CallDoubleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6> (
			string encodedMember,
			IJavaObject self,
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
				return CallDoubleMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6, T7> (
			string encodedMember,
			IJavaObject self,
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
				return CallDoubleMethod (encodedMember, self, args);
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

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			string encodedMember,
			IJavaObject self,
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
				return CallDoubleMethod (encodedMember, self, args);
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

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			string encodedMember,
			IJavaObject self,
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
				return CallDoubleMethod (encodedMember, self, args);
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

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			string encodedMember,
			IJavaObject self,
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
				return CallDoubleMethod (encodedMember, self, args);
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

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			string encodedMember,
			IJavaObject self,
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
				return CallDoubleMethod (encodedMember, self, args);
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

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			string encodedMember,
			IJavaObject self,
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
				return CallDoubleMethod (encodedMember, self, args);
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

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			string encodedMember,
			IJavaObject self,
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
				return CallDoubleMethod (encodedMember, self, args);
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

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			string encodedMember,
			IJavaObject self,
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
				return CallDoubleMethod (encodedMember, self, args);
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

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			string encodedMember,
			IJavaObject self,
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
				return CallDoubleMethod (encodedMember, self, args);
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

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			string encodedMember,
			IJavaObject self,
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
				return CallDoubleMethod (encodedMember, self, args);
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
		public unsafe JniLocalReference CallObjectMethod (
			string encodedMember,
			IJavaObject self,
			JValue* parameters)
		{
			JniPeerMembers.AssertSelf (self);

			if (self.GetType () == DeclaringType || DeclaringType == null) {
				var m = GetMethodID (encodedMember);
				return m.CallVirtualObjectMethod (self.SafeHandle, parameters);
			}
			var j = self.JniPeerMembers;
			var n = j.InstanceMethods.GetMethodID (encodedMember);
			return n.CallNonvirtualObjectMethod (self.SafeHandle, j.JniPeerType.SafeHandle, parameters);
		}

		public unsafe JniLocalReference CallObjectMethod (
			string encodedMember,
			IJavaObject self
		)
		{

			var args = stackalloc JValue [0];

			try {
				return CallObjectMethod (encodedMember, self, args);
			} finally {
			}
		}

		public unsafe JniLocalReference CallObjectMethod<T> (
			string encodedMember,
			IJavaObject self,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return CallObjectMethod (encodedMember, self, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public unsafe JniLocalReference CallObjectMethod<T1, T2> (
			string encodedMember,
			IJavaObject self,
			T1 value1, T2 value2
		)
		{
			JniArgumentMarshalInfo<T1> arg1 = new JniArgumentMarshalInfo<T1>(value1);
			JniArgumentMarshalInfo<T2> arg2 = new JniArgumentMarshalInfo<T2>(value2);

			var args = stackalloc JValue [2];
			args [0] = arg1.JValue;
			args [1] = arg2.JValue;

			try {
				return CallObjectMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3> (
			string encodedMember,
			IJavaObject self,
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
				return CallObjectMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4> (
			string encodedMember,
			IJavaObject self,
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
				return CallObjectMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5> (
			string encodedMember,
			IJavaObject self,
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
				return CallObjectMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6> (
			string encodedMember,
			IJavaObject self,
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
				return CallObjectMethod (encodedMember, self, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6, T7> (
			string encodedMember,
			IJavaObject self,
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
				return CallObjectMethod (encodedMember, self, args);
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

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
			string encodedMember,
			IJavaObject self,
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
				return CallObjectMethod (encodedMember, self, args);
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

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
			string encodedMember,
			IJavaObject self,
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
				return CallObjectMethod (encodedMember, self, args);
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

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
			string encodedMember,
			IJavaObject self,
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
				return CallObjectMethod (encodedMember, self, args);
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

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
			string encodedMember,
			IJavaObject self,
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
				return CallObjectMethod (encodedMember, self, args);
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

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
			string encodedMember,
			IJavaObject self,
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
				return CallObjectMethod (encodedMember, self, args);
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

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
			string encodedMember,
			IJavaObject self,
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
				return CallObjectMethod (encodedMember, self, args);
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

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
			string encodedMember,
			IJavaObject self,
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
				return CallObjectMethod (encodedMember, self, args);
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

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
			string encodedMember,
			IJavaObject self,
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
				return CallObjectMethod (encodedMember, self, args);
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

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
			string encodedMember,
			IJavaObject self,
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
				return CallObjectMethod (encodedMember, self, args);
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

	partial class JniPeerStaticMethods {

		public unsafe void CallVoidMethod (
			string encodedMember,
			JValue* parameters)
		{
			var m = GetMethodID (encodedMember);
			m.CallVoidMethod (Members.JniPeerType.SafeHandle, parameters);
		}

		public unsafe void CallVoidMethod<T> (
			string encodedMember,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				CallVoidMethod (encodedMember, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public unsafe void CallVoidMethod<T1, T2> (
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
				CallVoidMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public unsafe void CallVoidMethod<T1, T2, T3> (
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
				CallVoidMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public unsafe void CallVoidMethod<T1, T2, T3, T4> (
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
				CallVoidMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5> (
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
				CallVoidMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6> (
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
				CallVoidMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6, T7> (
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
				CallVoidMethod (encodedMember, args);
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

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
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
				CallVoidMethod (encodedMember, args);
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

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
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
				CallVoidMethod (encodedMember, args);
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

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
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
				CallVoidMethod (encodedMember, args);
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

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
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
				CallVoidMethod (encodedMember, args);
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

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
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
				CallVoidMethod (encodedMember, args);
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

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
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
				CallVoidMethod (encodedMember, args);
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

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
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
				CallVoidMethod (encodedMember, args);
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

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
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
				CallVoidMethod (encodedMember, args);
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

		public unsafe void CallVoidMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
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
				CallVoidMethod (encodedMember, args);
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
		public unsafe bool CallBooleanMethod (
			string encodedMember,
			JValue* parameters)
		{
			var m = GetMethodID (encodedMember);
			return m.CallBooleanMethod (Members.JniPeerType.SafeHandle, parameters);
		}

		public unsafe bool CallBooleanMethod<T> (
			string encodedMember,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return CallBooleanMethod (encodedMember, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public unsafe bool CallBooleanMethod<T1, T2> (
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
				return CallBooleanMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public unsafe bool CallBooleanMethod<T1, T2, T3> (
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
				return CallBooleanMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4> (
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
				return CallBooleanMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5> (
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
				return CallBooleanMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6> (
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
				return CallBooleanMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6, T7> (
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
				return CallBooleanMethod (encodedMember, args);
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

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
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
				return CallBooleanMethod (encodedMember, args);
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

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
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
				return CallBooleanMethod (encodedMember, args);
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

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
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
				return CallBooleanMethod (encodedMember, args);
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

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
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
				return CallBooleanMethod (encodedMember, args);
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

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
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
				return CallBooleanMethod (encodedMember, args);
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

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
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
				return CallBooleanMethod (encodedMember, args);
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

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
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
				return CallBooleanMethod (encodedMember, args);
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

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
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
				return CallBooleanMethod (encodedMember, args);
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

		public unsafe bool CallBooleanMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
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
				return CallBooleanMethod (encodedMember, args);
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
		public unsafe sbyte CallSByteMethod (
			string encodedMember,
			JValue* parameters)
		{
			var m = GetMethodID (encodedMember);
			return m.CallSByteMethod (Members.JniPeerType.SafeHandle, parameters);
		}

		public unsafe sbyte CallSByteMethod<T> (
			string encodedMember,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return CallSByteMethod (encodedMember, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public unsafe sbyte CallSByteMethod<T1, T2> (
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
				return CallSByteMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public unsafe sbyte CallSByteMethod<T1, T2, T3> (
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
				return CallSByteMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4> (
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
				return CallSByteMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5> (
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
				return CallSByteMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6> (
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
				return CallSByteMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6, T7> (
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
				return CallSByteMethod (encodedMember, args);
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

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
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
				return CallSByteMethod (encodedMember, args);
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

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
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
				return CallSByteMethod (encodedMember, args);
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

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
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
				return CallSByteMethod (encodedMember, args);
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

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
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
				return CallSByteMethod (encodedMember, args);
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

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
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
				return CallSByteMethod (encodedMember, args);
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

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
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
				return CallSByteMethod (encodedMember, args);
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

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
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
				return CallSByteMethod (encodedMember, args);
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

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
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
				return CallSByteMethod (encodedMember, args);
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

		public unsafe sbyte CallSByteMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
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
				return CallSByteMethod (encodedMember, args);
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
		public unsafe char CallCharMethod (
			string encodedMember,
			JValue* parameters)
		{
			var m = GetMethodID (encodedMember);
			return m.CallCharMethod (Members.JniPeerType.SafeHandle, parameters);
		}

		public unsafe char CallCharMethod<T> (
			string encodedMember,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return CallCharMethod (encodedMember, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public unsafe char CallCharMethod<T1, T2> (
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
				return CallCharMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public unsafe char CallCharMethod<T1, T2, T3> (
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
				return CallCharMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public unsafe char CallCharMethod<T1, T2, T3, T4> (
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
				return CallCharMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5> (
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
				return CallCharMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6> (
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
				return CallCharMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6, T7> (
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
				return CallCharMethod (encodedMember, args);
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

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
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
				return CallCharMethod (encodedMember, args);
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

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
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
				return CallCharMethod (encodedMember, args);
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

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
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
				return CallCharMethod (encodedMember, args);
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

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
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
				return CallCharMethod (encodedMember, args);
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

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
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
				return CallCharMethod (encodedMember, args);
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

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
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
				return CallCharMethod (encodedMember, args);
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

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
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
				return CallCharMethod (encodedMember, args);
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

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
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
				return CallCharMethod (encodedMember, args);
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

		public unsafe char CallCharMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
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
				return CallCharMethod (encodedMember, args);
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
		public unsafe short CallInt16Method (
			string encodedMember,
			JValue* parameters)
		{
			var m = GetMethodID (encodedMember);
			return m.CallInt16Method (Members.JniPeerType.SafeHandle, parameters);
		}

		public unsafe short CallInt16Method<T> (
			string encodedMember,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return CallInt16Method (encodedMember, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public unsafe short CallInt16Method<T1, T2> (
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
				return CallInt16Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public unsafe short CallInt16Method<T1, T2, T3> (
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
				return CallInt16Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public unsafe short CallInt16Method<T1, T2, T3, T4> (
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
				return CallInt16Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5> (
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
				return CallInt16Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6> (
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
				return CallInt16Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6, T7> (
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
				return CallInt16Method (encodedMember, args);
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

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6, T7, T8> (
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
				return CallInt16Method (encodedMember, args);
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

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
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
				return CallInt16Method (encodedMember, args);
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

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
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
				return CallInt16Method (encodedMember, args);
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

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
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
				return CallInt16Method (encodedMember, args);
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

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
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
				return CallInt16Method (encodedMember, args);
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

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
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
				return CallInt16Method (encodedMember, args);
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

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
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
				return CallInt16Method (encodedMember, args);
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

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
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
				return CallInt16Method (encodedMember, args);
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

		public unsafe short CallInt16Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
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
				return CallInt16Method (encodedMember, args);
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
		public unsafe int CallInt32Method (
			string encodedMember,
			JValue* parameters)
		{
			var m = GetMethodID (encodedMember);
			return m.CallInt32Method (Members.JniPeerType.SafeHandle, parameters);
		}

		public unsafe int CallInt32Method<T> (
			string encodedMember,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return CallInt32Method (encodedMember, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public unsafe int CallInt32Method<T1, T2> (
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
				return CallInt32Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public unsafe int CallInt32Method<T1, T2, T3> (
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
				return CallInt32Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public unsafe int CallInt32Method<T1, T2, T3, T4> (
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
				return CallInt32Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5> (
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
				return CallInt32Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6> (
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
				return CallInt32Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6, T7> (
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
				return CallInt32Method (encodedMember, args);
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

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6, T7, T8> (
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
				return CallInt32Method (encodedMember, args);
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

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
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
				return CallInt32Method (encodedMember, args);
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

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
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
				return CallInt32Method (encodedMember, args);
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

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
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
				return CallInt32Method (encodedMember, args);
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

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
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
				return CallInt32Method (encodedMember, args);
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

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
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
				return CallInt32Method (encodedMember, args);
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

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
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
				return CallInt32Method (encodedMember, args);
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

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
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
				return CallInt32Method (encodedMember, args);
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

		public unsafe int CallInt32Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
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
				return CallInt32Method (encodedMember, args);
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
		public unsafe long CallInt64Method (
			string encodedMember,
			JValue* parameters)
		{
			var m = GetMethodID (encodedMember);
			return m.CallInt64Method (Members.JniPeerType.SafeHandle, parameters);
		}

		public unsafe long CallInt64Method<T> (
			string encodedMember,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return CallInt64Method (encodedMember, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public unsafe long CallInt64Method<T1, T2> (
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
				return CallInt64Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public unsafe long CallInt64Method<T1, T2, T3> (
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
				return CallInt64Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public unsafe long CallInt64Method<T1, T2, T3, T4> (
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
				return CallInt64Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5> (
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
				return CallInt64Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6> (
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
				return CallInt64Method (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6, T7> (
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
				return CallInt64Method (encodedMember, args);
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

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6, T7, T8> (
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
				return CallInt64Method (encodedMember, args);
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

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
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
				return CallInt64Method (encodedMember, args);
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

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
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
				return CallInt64Method (encodedMember, args);
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

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
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
				return CallInt64Method (encodedMember, args);
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

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
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
				return CallInt64Method (encodedMember, args);
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

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
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
				return CallInt64Method (encodedMember, args);
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

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
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
				return CallInt64Method (encodedMember, args);
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

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
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
				return CallInt64Method (encodedMember, args);
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

		public unsafe long CallInt64Method<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
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
				return CallInt64Method (encodedMember, args);
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
		public unsafe float CallSingleMethod (
			string encodedMember,
			JValue* parameters)
		{
			var m = GetMethodID (encodedMember);
			return m.CallSingleMethod (Members.JniPeerType.SafeHandle, parameters);
		}

		public unsafe float CallSingleMethod<T> (
			string encodedMember,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return CallSingleMethod (encodedMember, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public unsafe float CallSingleMethod<T1, T2> (
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
				return CallSingleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public unsafe float CallSingleMethod<T1, T2, T3> (
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
				return CallSingleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public unsafe float CallSingleMethod<T1, T2, T3, T4> (
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
				return CallSingleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5> (
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
				return CallSingleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6> (
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
				return CallSingleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6, T7> (
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
				return CallSingleMethod (encodedMember, args);
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

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
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
				return CallSingleMethod (encodedMember, args);
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

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
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
				return CallSingleMethod (encodedMember, args);
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

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
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
				return CallSingleMethod (encodedMember, args);
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

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
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
				return CallSingleMethod (encodedMember, args);
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

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
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
				return CallSingleMethod (encodedMember, args);
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

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
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
				return CallSingleMethod (encodedMember, args);
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

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
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
				return CallSingleMethod (encodedMember, args);
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

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
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
				return CallSingleMethod (encodedMember, args);
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

		public unsafe float CallSingleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
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
				return CallSingleMethod (encodedMember, args);
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
		public unsafe double CallDoubleMethod (
			string encodedMember,
			JValue* parameters)
		{
			var m = GetMethodID (encodedMember);
			return m.CallDoubleMethod (Members.JniPeerType.SafeHandle, parameters);
		}

		public unsafe double CallDoubleMethod<T> (
			string encodedMember,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return CallDoubleMethod (encodedMember, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public unsafe double CallDoubleMethod<T1, T2> (
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
				return CallDoubleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public unsafe double CallDoubleMethod<T1, T2, T3> (
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
				return CallDoubleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public unsafe double CallDoubleMethod<T1, T2, T3, T4> (
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
				return CallDoubleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5> (
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
				return CallDoubleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6> (
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
				return CallDoubleMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6, T7> (
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
				return CallDoubleMethod (encodedMember, args);
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

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
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
				return CallDoubleMethod (encodedMember, args);
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

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
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
				return CallDoubleMethod (encodedMember, args);
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

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
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
				return CallDoubleMethod (encodedMember, args);
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

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
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
				return CallDoubleMethod (encodedMember, args);
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

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
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
				return CallDoubleMethod (encodedMember, args);
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

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
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
				return CallDoubleMethod (encodedMember, args);
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

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
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
				return CallDoubleMethod (encodedMember, args);
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

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
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
				return CallDoubleMethod (encodedMember, args);
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

		public unsafe double CallDoubleMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
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
				return CallDoubleMethod (encodedMember, args);
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
		public unsafe JniLocalReference CallObjectMethod (
			string encodedMember,
			JValue* parameters)
		{
			var m = GetMethodID (encodedMember);
			return m.CallObjectMethod (Members.JniPeerType.SafeHandle, parameters);
		}

		public unsafe JniLocalReference CallObjectMethod<T> (
			string encodedMember,
			T value
		)
		{
			JniArgumentMarshalInfo<T> arg = new JniArgumentMarshalInfo<T>(value);

			var args = stackalloc JValue [1];
			args [0] = arg.JValue;

			try {
				return CallObjectMethod (encodedMember, args);
			} finally {
				arg.Cleanup (value);
			}
		}

		public unsafe JniLocalReference CallObjectMethod<T1, T2> (
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
				return CallObjectMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
			}
		}

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3> (
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
				return CallObjectMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
			}
		}

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4> (
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
				return CallObjectMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
			}
		}

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5> (
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
				return CallObjectMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
			}
		}

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6> (
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
				return CallObjectMethod (encodedMember, args);
			} finally {
				arg1.Cleanup (value1);
				arg2.Cleanup (value2);
				arg3.Cleanup (value3);
				arg4.Cleanup (value4);
				arg5.Cleanup (value5);
				arg6.Cleanup (value6);
			}
		}

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6, T7> (
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
				return CallObjectMethod (encodedMember, args);
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

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8> (
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
				return CallObjectMethod (encodedMember, args);
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

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
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
				return CallObjectMethod (encodedMember, args);
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

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> (
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
				return CallObjectMethod (encodedMember, args);
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

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> (
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
				return CallObjectMethod (encodedMember, args);
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

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> (
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
				return CallObjectMethod (encodedMember, args);
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

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> (
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
				return CallObjectMethod (encodedMember, args);
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

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> (
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
				return CallObjectMethod (encodedMember, args);
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

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> (
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
				return CallObjectMethod (encodedMember, args);
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

		public unsafe JniLocalReference CallObjectMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> (
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
				return CallObjectMethod (encodedMember, args);
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
