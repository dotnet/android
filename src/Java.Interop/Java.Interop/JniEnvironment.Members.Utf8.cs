#nullable enable

using System;
using System.Runtime.ExceptionServices;

namespace Java.Interop {

	partial class JniEnvironment {

		const int MaxStackTerminatedUtf8Length = 512;

		static ReadOnlySpan<byte> TerminateUtf8 (ReadOnlySpan<byte> value, Span<byte> stackBuffer)
		{
			if (value.IndexOf ((byte) 0) >= 0)
				return value;
			int length = checked (value.Length + 1);
			Span<byte> buffer = length <= stackBuffer.Length
				? stackBuffer.Slice (0, length)
				: new byte [length];
			value.CopyTo (buffer);
			buffer [value.Length] = 0;
			return buffer;
		}

		partial class InstanceFields {

			/// <summary>
			/// Looks up a JNI instance field ID using JNI modified UTF-8 name and signature spans.
			/// Use with <c>"fieldName"u8</c> literals to avoid string marshalling overhead.
			/// Each span ends at its length or at an earlier NUL within it.
			/// Invalid or 4-byte standard UTF-8 throws <see cref="ArgumentException"/> before JNI reads it.
			/// </summary>
			/// <safety>
			/// The caller must keep <paramref name="name"/> and <paramref name="signature"/> readable
			/// and unchanged through success or failure, and prevent concurrent mutation/reclamation
			/// through aliases. This wrapper pins managed backing storage during JNI access and does
			/// not retain the spans.
			/// </safety>
			public static unsafe JniFieldInfo GetFieldID (JniObjectReference type, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
			{
				if (!type.IsValid)
					throw new ArgumentException ("Handle must be valid.", "type");
				if (name.IsEmpty || name [0] == 0)
					throw new ArgumentException ("'name' cannot be empty.", nameof (name));
				if (signature.IsEmpty || signature [0] == 0)
					throw new ArgumentException ("'signature' cannot be empty.", nameof (signature));

				JniModifiedUtf8.Validate (name, nameof (name));
				JniModifiedUtf8.Validate (signature, nameof (signature));

				Span<byte> nameStack = stackalloc byte [MaxStackTerminatedUtf8Length];
				Span<byte> signatureStack = stackalloc byte [MaxStackTerminatedUtf8Length];
				ReadOnlySpan<byte> terminatedName = TerminateUtf8 (name, nameStack);
				ReadOnlySpan<byte> terminatedSignature = TerminateUtf8 (signature, signatureStack);

				IntPtr __env = JniEnvironment.EnvironmentPointer;
				unsafe {
					// SAFETY: Both spans are validated, NUL-terminated within bounds, and pinned through GetFieldID and exception polling on this thread's JNIEnv.
					fixed (byte* _name_ptr = terminatedName)
					fixed (byte* _signature_ptr = terminatedSignature) {
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
		}

		partial class InstanceMethods {

			/// <summary>
			/// Looks up a JNI instance method ID using JNI modified UTF-8 name and signature spans.
			/// Use with <c>"methodName"u8</c> literals to avoid string marshalling overhead.
			/// Each span ends at its length or at an earlier NUL within it.
			/// Invalid or 4-byte standard UTF-8 throws <see cref="ArgumentException"/> before JNI reads it.
			/// </summary>
			/// <safety>
			/// The caller must keep <paramref name="name"/> and <paramref name="signature"/> readable
			/// and unchanged through success or failure, and prevent concurrent mutation/reclamation
			/// through aliases. This wrapper pins managed backing storage during JNI access and does
			/// not retain the spans.
			/// </safety>
			public static unsafe JniMethodInfo GetMethodID (JniObjectReference type, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
			{
				if (!type.IsValid)
					throw new ArgumentException ("Handle must be valid.", "type");
				if (name.IsEmpty || name [0] == 0)
					throw new ArgumentException ("'name' cannot be empty.", nameof (name));
				if (signature.IsEmpty || signature [0] == 0)
					throw new ArgumentException ("'signature' cannot be empty.", nameof (signature));

				JniModifiedUtf8.Validate (name, nameof (name));
				JniModifiedUtf8.Validate (signature, nameof (signature));

				Span<byte> nameStack = stackalloc byte [MaxStackTerminatedUtf8Length];
				Span<byte> signatureStack = stackalloc byte [MaxStackTerminatedUtf8Length];
				ReadOnlySpan<byte> terminatedName = TerminateUtf8 (name, nameStack);
				ReadOnlySpan<byte> terminatedSignature = TerminateUtf8 (signature, signatureStack);

				IntPtr __env = JniEnvironment.EnvironmentPointer;
				unsafe {
					// SAFETY: Both spans are validated, NUL-terminated within bounds, and pinned through GetMethodID and exception polling on this thread's JNIEnv.
					fixed (byte* _name_ptr = terminatedName)
					fixed (byte* _signature_ptr = terminatedSignature) {
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
		}

		partial class StaticFields {

			/// <summary>
			/// Looks up a JNI static field ID using JNI modified UTF-8 name and signature spans.
			/// Use with <c>"fieldName"u8</c> literals to avoid string marshalling overhead.
			/// Each span ends at its length or at an earlier NUL within it.
			/// Invalid or 4-byte standard UTF-8 throws <see cref="ArgumentException"/> before JNI reads it.
			/// </summary>
			/// <safety>
			/// The caller must keep <paramref name="name"/> and <paramref name="signature"/> readable
			/// and unchanged through success or failure, and prevent concurrent mutation/reclamation
			/// through aliases. This wrapper pins managed backing storage during JNI access and does
			/// not retain the spans.
			/// </safety>
			public static unsafe JniFieldInfo GetStaticFieldID (JniObjectReference type, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
			{
				if (!type.IsValid)
					throw new ArgumentException ("Handle must be valid.", "type");
				if (name.IsEmpty || name [0] == 0)
					throw new ArgumentException ("'name' cannot be empty.", nameof (name));
				if (signature.IsEmpty || signature [0] == 0)
					throw new ArgumentException ("'signature' cannot be empty.", nameof (signature));

				JniModifiedUtf8.Validate (name, nameof (name));
				JniModifiedUtf8.Validate (signature, nameof (signature));

				Span<byte> nameStack = stackalloc byte [MaxStackTerminatedUtf8Length];
				Span<byte> signatureStack = stackalloc byte [MaxStackTerminatedUtf8Length];
				ReadOnlySpan<byte> terminatedName = TerminateUtf8 (name, nameStack);
				ReadOnlySpan<byte> terminatedSignature = TerminateUtf8 (signature, signatureStack);

				IntPtr __env = JniEnvironment.EnvironmentPointer;
				unsafe {
					// SAFETY: Both spans are validated, NUL-terminated within bounds, and pinned through GetStaticFieldID and exception polling on this thread's JNIEnv.
					fixed (byte* _name_ptr = terminatedName)
					fixed (byte* _signature_ptr = terminatedSignature) {
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
		}

		partial class StaticMethods {

			/// <summary>
			/// Looks up a JNI static method ID using JNI modified UTF-8 name and signature spans.
			/// Use with <c>"methodName"u8</c> literals to avoid string marshalling overhead.
			/// Each span ends at its length or at an earlier NUL within it.
			/// Invalid or 4-byte standard UTF-8 throws <see cref="ArgumentException"/> before JNI reads it.
			/// </summary>
			/// <safety>
			/// The caller must keep <paramref name="name"/> and <paramref name="signature"/> readable
			/// and unchanged through success or failure, and prevent concurrent mutation/reclamation
			/// through aliases. This wrapper pins managed backing storage during JNI access and does
			/// not retain the spans.
			/// </safety>
			public static unsafe JniMethodInfo GetStaticMethodID (JniObjectReference type, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
			{
				if (!type.IsValid)
					throw new ArgumentException ("Handle must be valid.", "type");
				if (name.IsEmpty || name [0] == 0)
					throw new ArgumentException ("'name' cannot be empty.", nameof (name));
				if (signature.IsEmpty || signature [0] == 0)
					throw new ArgumentException ("'signature' cannot be empty.", nameof (signature));

				JniModifiedUtf8.Validate (name, nameof (name));
				JniModifiedUtf8.Validate (signature, nameof (signature));

				Span<byte> nameStack = stackalloc byte [MaxStackTerminatedUtf8Length];
				Span<byte> signatureStack = stackalloc byte [MaxStackTerminatedUtf8Length];
				ReadOnlySpan<byte> terminatedName = TerminateUtf8 (name, nameStack);
				ReadOnlySpan<byte> terminatedSignature = TerminateUtf8 (signature, signatureStack);

				IntPtr __env = JniEnvironment.EnvironmentPointer;
				unsafe {
					// SAFETY: Both spans are validated, NUL-terminated within bounds, and pinned through GetStaticMethodID and exception polling on this thread's JNIEnv.
					fixed (byte* _name_ptr = terminatedName)
					fixed (byte* _signature_ptr = terminatedSignature) {
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
}
