#nullable enable

using System;
using System.Runtime.ExceptionServices;

namespace Java.Interop {

	partial class JniEnvironment {

		enum Utf8MemberKind { Field, Method, StaticField, StaticMethod }

		static unsafe IntPtr GetMemberID (JniObjectReference type, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature, Utf8MemberKind kind, string apiName)
		{
			if (!type.IsValid)
				throw new ArgumentException ("Handle must be valid.", "type");
			if (name.IsEmpty || name [0] == 0)
				throw new ArgumentException ("'name' cannot be empty.", nameof (name));
			if (signature.IsEmpty || signature [0] == 0)
				throw new ArgumentException ("'signature' cannot be empty.", nameof (signature));

			const int MaxStackLength = 512;
			Span<byte> nameStack = stackalloc byte [MaxStackLength];
			Span<byte> signatureStack = stackalloc byte [MaxStackLength];
			ReadOnlySpan<byte> terminatedName = TerminateUtf8 (name, nameStack);
			ReadOnlySpan<byte> terminatedSignature = TerminateUtf8 (signature, signatureStack);

			IntPtr __env = JniEnvironment.EnvironmentPointer;
			unsafe {
				// SAFETY: Both spans are NUL-terminated within bounds and pinned through the validating JNI wrapper and exception polling on this thread's JNIEnv.
				fixed (byte* _name_ptr = terminatedName)
				fixed (byte* _signature_ptr = terminatedSignature) {
					IntPtr tmp = kind switch {
						Utf8MemberKind.Field        => JniNativeMethods.GetFieldID (__env, type.Handle, (IntPtr) _name_ptr, (IntPtr) _signature_ptr),
						Utf8MemberKind.Method       => JniNativeMethods.GetMethodID (__env, type.Handle, (IntPtr) _name_ptr, (IntPtr) _signature_ptr),
						Utf8MemberKind.StaticField  => JniNativeMethods.GetStaticFieldID (__env, type.Handle, (IntPtr) _name_ptr, (IntPtr) _signature_ptr),
						Utf8MemberKind.StaticMethod => JniNativeMethods.GetStaticMethodID (__env, type.Handle, (IntPtr) _name_ptr, (IntPtr) _signature_ptr),
						_ => throw new ArgumentOutOfRangeException (nameof (kind)),
					};
					IntPtr thrown = JniNativeMethods.ExceptionOccurred (__env);

					Exception? __e = JniEnvironment.GetExceptionForLastThrowable (thrown);
					if (__e != null)
						ExceptionDispatchInfo.Capture (__e).Throw ();

					if (tmp == IntPtr.Zero)
						throw new InvalidOperationException ($"Should not be reached; `{apiName}` should have thrown!");
					return tmp;
				}
			}

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
			public static unsafe JniFieldInfo GetFieldID (JniObjectReference type, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature) =>
				// SAFETY: Caller keeps both spans readable and unchanged through lookup.
				new JniFieldInfo (unsafe (GetMemberID (type, name, signature, Utf8MemberKind.Field, nameof (GetFieldID))), isStatic: false);
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
			public static unsafe JniMethodInfo GetMethodID (JniObjectReference type, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature) =>
				// SAFETY: Caller keeps both spans readable and unchanged through lookup.
				new JniMethodInfo (unsafe (GetMemberID (type, name, signature, Utf8MemberKind.Method, nameof (GetMethodID))), isStatic: false);
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
			public static unsafe JniFieldInfo GetStaticFieldID (JniObjectReference type, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature) =>
				// SAFETY: Caller keeps both spans readable and unchanged through lookup.
				new JniFieldInfo (unsafe (GetMemberID (type, name, signature, Utf8MemberKind.StaticField, nameof (GetStaticFieldID))), isStatic: true);
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
			public static unsafe JniMethodInfo GetStaticMethodID (JniObjectReference type, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature) =>
				// SAFETY: Caller keeps both spans readable and unchanged through lookup.
				new JniMethodInfo (unsafe (GetMemberID (type, name, signature, Utf8MemberKind.StaticMethod, nameof (GetStaticMethodID))), isStatic: true);
		}
	}
}
