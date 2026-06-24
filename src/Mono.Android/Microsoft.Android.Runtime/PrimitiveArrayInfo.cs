#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Java.Interop;

namespace Microsoft.Android.Runtime;

static class PrimitiveArrayInfo
{
	delegate TArray PrimitiveArrayFactory<TArray> (ref JniObjectReference reference, JniObjectReferenceOptions options);

	abstract class Handler
	{
		public abstract bool TryGetArrayTypes (Type elementType, [NotNullWhen (true)] out Type[]? arrayTypes);

		public abstract bool TryGetTypeSignature (Type type, out JniTypeSignature signature);

		public abstract bool TryCreateWrapper (
			ref JniObjectReference reference,
			JniObjectReferenceOptions options,
			Type targetType,
			[NotNullWhen (true)] out object? value);

		public abstract bool TryCreateObjectReference (object value, out JniObjectReference reference);
	}

	sealed class Handler<T, TArray> : Handler
		where T : struct
		where TArray : JavaArray<T>
	{
		readonly string jniSimpleReference;
		readonly PrimitiveArrayFactory<TArray> createFromReference;
		readonly Func<IList<T>, TArray> createCopy;

		public Handler (string jniSimpleReference, PrimitiveArrayFactory<TArray> createFromReference, Func<IList<T>, TArray> createCopy)
		{
			this.jniSimpleReference = jniSimpleReference;
			this.createFromReference = createFromReference;
			this.createCopy = createCopy;
		}

		public override bool TryGetArrayTypes (Type elementType, [NotNullWhen (true)] out Type[]? arrayTypes)
		{
			if (typeof (T) != elementType) {
				arrayTypes = null;
				return false;
			}

			arrayTypes = [typeof (T[]), typeof (JavaArray<T>), typeof (JavaPrimitiveArray<T>), typeof (TArray)];
			return true;
		}

		public override bool TryGetTypeSignature (Type type, out JniTypeSignature signature)
		{
			if (IsArrayType (type)) {
				signature = new JniTypeSignature (jniSimpleReference, arrayRank: 1, keyword: true);
				return true;
			}

			signature = default;
			return false;
		}

		public override bool TryCreateWrapper (
			ref JniObjectReference reference,
			JniObjectReferenceOptions options,
			Type targetType,
			[NotNullWhen (true)] out object? value)
		{
			if (!IsTargetType (targetType)) {
				value = null;
				return false;
			}

			var array = createFromReference (ref reference, options);
			if (targetType == typeof (T[]) || IsCompatibleListType (targetType)) {
				try {
					value = array.ToArray ();
					return true;
				} finally {
					array.Dispose ();
				}
			}

			value = array;
			return true;
		}

		public override bool TryCreateObjectReference (object value, out JniObjectReference reference)
		{
			if (value is TArray array) {
				reference = array.PeerReference.IsValid
					? array.PeerReference.NewLocalRef ()
					: new JniObjectReference ();
				return true;
			}

			if (value is not IList<T> list) {
				reference = new JniObjectReference ();
				return false;
			}

			var marshaledArray = createCopy (list);
			try {
				reference = marshaledArray.PeerReference.IsValid
					? marshaledArray.PeerReference.NewLocalRef ()
					: new JniObjectReference ();
				return true;
			} finally {
				marshaledArray.Dispose ();
			}
		}

		bool IsTargetType (Type targetType)
		{
			return IsArrayType (targetType) ||
				IsCompatibleListType (targetType);
		}

		static bool IsArrayType (Type targetType)
		{
			return targetType == typeof (JavaArray<T>) ||
				targetType == typeof (JavaPrimitiveArray<T>) ||
				targetType == typeof (TArray) ||
				targetType == typeof (T[]);
		}

		static bool IsCompatibleListType (Type targetType)
		{
			return targetType.IsGenericType &&
				targetType.GetGenericTypeDefinition () == typeof (IList<>) &&
				targetType.IsAssignableFrom (typeof (IList<T>));
		}
	}

	static readonly Handler[] Handlers = [
		new Handler<bool, JavaBooleanArray> (
			"Z",
			(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaBooleanArray (ref h, o),
			list => new JavaBooleanArray (list)),
		new Handler<sbyte, JavaSByteArray> (
			"B",
			(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaSByteArray (ref h, o),
			list => new JavaSByteArray (list)),
		new Handler<char, JavaCharArray> (
			"C",
			(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaCharArray (ref h, o),
			list => new JavaCharArray (list)),
		new Handler<short, JavaInt16Array> (
			"S",
			(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaInt16Array (ref h, o),
			list => new JavaInt16Array (list)),
		new Handler<int, JavaInt32Array> (
			"I",
			(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaInt32Array (ref h, o),
			list => new JavaInt32Array (list)),
		new Handler<long, JavaInt64Array> (
			"J",
			(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaInt64Array (ref h, o),
			list => new JavaInt64Array (list)),
		new Handler<float, JavaSingleArray> (
			"F",
			(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaSingleArray (ref h, o),
			list => new JavaSingleArray (list)),
		new Handler<double, JavaDoubleArray> (
			"D",
			(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaDoubleArray (ref h, o),
			list => new JavaDoubleArray (list)),
	];

	public static bool TryGetArrayTypes (Type elementType, [NotNullWhen (true)] out Type[]? arrayTypes)
	{
		foreach (var handler in Handlers) {
			if (handler.TryGetArrayTypes (elementType, out arrayTypes)) {
				return true;
			}
		}

		arrayTypes = null;
		return false;
	}

	public static bool TryGetTypeSignature (Type type, out JniTypeSignature signature)
	{
		foreach (var handler in Handlers) {
			if (handler.TryGetTypeSignature (type, out signature)) {
				return true;
			}
		}

		signature = default;
		return false;
	}

	public static bool TryCreateWrapper (
		ref JniObjectReference reference,
		JniObjectReferenceOptions options,
		Type targetType,
		[NotNullWhen (true)] out object? value)
	{
		foreach (var handler in Handlers) {
			if (handler.TryCreateWrapper (ref reference, options, targetType, out value)) {
				return true;
			}
		}

		value = null;
		return false;
	}

	public static bool TryCreateObjectReference (object value, out JniObjectReference reference)
	{
		foreach (var handler in Handlers) {
			if (handler.TryCreateObjectReference (value, out reference)) {
				return true;
			}
		}

		reference = new JniObjectReference ();
		return false;
	}
}