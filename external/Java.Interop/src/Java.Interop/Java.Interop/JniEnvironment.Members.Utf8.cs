#nullable enable

#if FEATURE_JNIENVIRONMENT_JI_FUNCTION_POINTERS
using System;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace Java.Interop {

	partial class JniEnvironment {

		partial class InstanceFields {

			/// <summary>
			/// Looks up a JNI instance field ID using null-terminated UTF-8 name and signature spans.
			/// Use with <c>"fieldName"u8</c> literals to avoid string marshalling overhead.
			/// </summary>
			public static unsafe JniFieldInfo GetFieldID (JniObjectReference type, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
			{
				if (!type.IsValid)
					throw new ArgumentException ("Handle must be valid.", "type");

				IntPtr __env = JniEnvironment.EnvironmentPointer;
				fixed (byte* _name_ptr = name)
				fixed (byte* _signature_ptr = signature) {
					var tmp = JniNativeMethods.GetFieldID (__env, type.Handle, (IntPtr) _name_ptr, (IntPtr) _signature_ptr);
					IntPtr thrown = JniNativeMethods.ExceptionOccurred (__env);

					Exception? __e = JniEnvironment.GetExceptionForLastThrowable (thrown);
					if (__e != null)
						ExceptionDispatchInfo.Capture (__e).Throw ();

					if (tmp == IntPtr.Zero)
						throw new InvalidOperationException ("Should not be reached; `GetFieldID` should have thrown!");
					return new JniFieldInfo (tmp, isStatic: false);
				}
			}
		}

		partial class InstanceMethods {

			/// <summary>
			/// Looks up a JNI instance method ID using null-terminated UTF-8 name and signature spans.
			/// Use with <c>"methodName"u8</c> literals to avoid string marshalling overhead.
			/// </summary>
			public static unsafe JniMethodInfo GetMethodID (JniObjectReference type, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
			{
				if (!type.IsValid)
					throw new ArgumentException ("Handle must be valid.", "type");

				IntPtr __env = JniEnvironment.EnvironmentPointer;
				fixed (byte* _name_ptr = name)
				fixed (byte* _signature_ptr = signature) {
					var tmp = JniNativeMethods.GetMethodID (__env, type.Handle, (IntPtr) _name_ptr, (IntPtr) _signature_ptr);
					IntPtr thrown = JniNativeMethods.ExceptionOccurred (__env);

					Exception? __e = JniEnvironment.GetExceptionForLastThrowable (thrown);
					if (__e != null)
						ExceptionDispatchInfo.Capture (__e).Throw ();

					if (tmp == IntPtr.Zero)
						throw new InvalidOperationException ("Should not be reached; `GetMethodID` should have thrown!");
					return new JniMethodInfo (tmp, isStatic: false);
				}
			}
		}

		partial class StaticFields {

			/// <summary>
			/// Looks up a JNI static field ID using null-terminated UTF-8 name and signature spans.
			/// Use with <c>"fieldName"u8</c> literals to avoid string marshalling overhead.
			/// </summary>
			public static unsafe JniFieldInfo GetStaticFieldID (JniObjectReference type, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
			{
				if (!type.IsValid)
					throw new ArgumentException ("Handle must be valid.", "type");

				IntPtr __env = JniEnvironment.EnvironmentPointer;
				fixed (byte* _name_ptr = name)
				fixed (byte* _signature_ptr = signature) {
					var tmp = JniNativeMethods.GetStaticFieldID (__env, type.Handle, (IntPtr) _name_ptr, (IntPtr) _signature_ptr);
					IntPtr thrown = JniNativeMethods.ExceptionOccurred (__env);

					Exception? __e = JniEnvironment.GetExceptionForLastThrowable (thrown);
					if (__e != null)
						ExceptionDispatchInfo.Capture (__e).Throw ();

					if (tmp == IntPtr.Zero)
						throw new InvalidOperationException ("Should not be reached; `GetStaticFieldID` should have thrown!");
					return new JniFieldInfo (tmp, isStatic: true);
				}
			}
		}

		partial class StaticMethods {

			/// <summary>
			/// Looks up a JNI static method ID using null-terminated UTF-8 name and signature spans.
			/// Use with <c>"methodName"u8</c> literals to avoid string marshalling overhead.
			/// </summary>
			public static unsafe JniMethodInfo GetStaticMethodID (JniObjectReference type, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
			{
				if (!type.IsValid)
					throw new ArgumentException ("Handle must be valid.", "type");

				IntPtr __env = JniEnvironment.EnvironmentPointer;
				fixed (byte* _name_ptr = name)
				fixed (byte* _signature_ptr = signature) {
					var tmp = JniNativeMethods.GetStaticMethodID (__env, type.Handle, (IntPtr) _name_ptr, (IntPtr) _signature_ptr);
					IntPtr thrown = JniNativeMethods.ExceptionOccurred (__env);

					Exception? __e = JniEnvironment.GetExceptionForLastThrowable (thrown);
					if (__e != null)
						ExceptionDispatchInfo.Capture (__e).Throw ();

					if (tmp == IntPtr.Zero)
						throw new InvalidOperationException ("Should not be reached; `GetStaticMethodID` should have thrown!");
					return new JniMethodInfo (tmp, isStatic: true);
				}
			}
		}
	}
}
#endif // FEATURE_JNIENVIRONMENT_JI_FUNCTION_POINTERS
