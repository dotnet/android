using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Java.Interop;

namespace Microsoft.Android.Runtime;

sealed partial class TrimmableTypeMapValueManager
{
	delegate TArray PrimitiveArrayFactory<TArray> (ref JniObjectReference reference, JniObjectReferenceOptions options);

	readonly struct PrimitiveArrayArgumentState
	{
		public readonly bool DisposeArray;

		public PrimitiveArrayArgumentState (bool disposeArray)
		{
			DisposeArray = disposeArray;
		}
	}

	abstract class PrimitiveArrayHandler
	{
		public abstract bool TryCreateWrapper (
			ref JniObjectReference reference,
			JniObjectReferenceOptions options,
			[DynamicallyAccessedMembers (Constructors)]
			Type targetType,
			[NotNullWhen (true)] out object? value);

		public abstract bool TryCreateArgumentState (object value, ParameterAttributes synchronize, out JniValueMarshalerState state);

		public abstract bool TryDestroyArgumentState (object? value, ref JniValueMarshalerState state, ParameterAttributes synchronize);

		public abstract bool IsTargetType (Type targetType);
	}

	sealed class PrimitiveArrayHandler<T, TArray> : PrimitiveArrayHandler
		where TArray : global::Java.Interop.JavaArray<T>
	{
		readonly PrimitiveArrayFactory<TArray> createFromReference;
		readonly Func<int, TArray> create;
		readonly Func<IList<T>, TArray> createCopy;

		public PrimitiveArrayHandler (PrimitiveArrayFactory<TArray> createFromReference, Func<int, TArray> create, Func<IList<T>, TArray> createCopy)
		{
			this.createFromReference = createFromReference;
			this.create = create;
			this.createCopy = createCopy;
		}

		public override bool TryCreateWrapper (
			ref JniObjectReference reference,
			JniObjectReferenceOptions options,
			[DynamicallyAccessedMembers (Constructors)]
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

		public override bool TryCreateArgumentState (object value, ParameterAttributes synchronize, out JniValueMarshalerState state)
		{
			if (value is TArray array) {
				state = new JniValueMarshalerState (array);
				return true;
			}

			if (value is not IList<T> list) {
				state = new JniValueMarshalerState ();
				return false;
			}

			synchronize = GetCopyDirection (synchronize);
			var copy = (synchronize & ParameterAttributes.In) == ParameterAttributes.In;
			var marshaledArray = copy ? createCopy (list) : create (list.Count);
			state = new JniValueMarshalerState (marshaledArray, new PrimitiveArrayArgumentState (disposeArray: true));
			return true;
		}

		public override bool TryDestroyArgumentState (object? value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
		{
			if (state.PeerableValue is not TArray source) {
				return false;
			}

			synchronize = GetCopyDirection (synchronize);
			if ((synchronize & ParameterAttributes.Out) == ParameterAttributes.Out && value is IList<T> destination) {
				for (int i = 0; i < source.Length; i++) {
					destination [i] = source [i];
				}
			}

			if (state.Extra is PrimitiveArrayArgumentState { DisposeArray: true }) {
				source.Dispose ();
			}

			state = new JniValueMarshalerState ();
			return true;
		}

		public override bool IsTargetType (Type targetType)
		{
			return targetType == typeof (global::Java.Interop.JavaArray<T>) ||
				targetType == typeof (global::Java.Interop.JavaPrimitiveArray<T>) ||
				targetType == typeof (TArray) ||
				targetType == typeof (T[]) ||
				IsCompatibleListType (targetType);
		}

		static bool IsCompatibleListType (Type targetType)
		{
			return targetType.IsGenericType &&
				targetType.GetGenericTypeDefinition () == typeof (IList<>) &&
				targetType.IsAssignableFrom (typeof (IList<T>));
		}
	}

	static readonly PrimitiveArrayHandler[] PrimitiveArrayHandlers = new PrimitiveArrayHandler [] {
		new PrimitiveArrayHandler<bool, JavaBooleanArray> (
			(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaBooleanArray (ref h, o),
			length => new JavaBooleanArray (length),
			list => new JavaBooleanArray (list)),
		new PrimitiveArrayHandler<sbyte, JavaSByteArray> (
			(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaSByteArray (ref h, o),
			length => new JavaSByteArray (length),
			list => new JavaSByteArray (list)),
		new PrimitiveArrayHandler<char, JavaCharArray> (
			(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaCharArray (ref h, o),
			length => new JavaCharArray (length),
			list => new JavaCharArray (list)),
		new PrimitiveArrayHandler<short, JavaInt16Array> (
			(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaInt16Array (ref h, o),
			length => new JavaInt16Array (length),
			list => new JavaInt16Array (list)),
		new PrimitiveArrayHandler<int, JavaInt32Array> (
			(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaInt32Array (ref h, o),
			length => new JavaInt32Array (length),
			list => new JavaInt32Array (list)),
		new PrimitiveArrayHandler<long, JavaInt64Array> (
			(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaInt64Array (ref h, o),
			length => new JavaInt64Array (length),
			list => new JavaInt64Array (list)),
		new PrimitiveArrayHandler<float, JavaSingleArray> (
			(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaSingleArray (ref h, o),
			length => new JavaSingleArray (length),
			list => new JavaSingleArray (list)),
		new PrimitiveArrayHandler<double, JavaDoubleArray> (
			(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaDoubleArray (ref h, o),
			length => new JavaDoubleArray (length),
			list => new JavaDoubleArray (list)),
	};

	static bool TryCreatePrimitiveArrayWrapper (
		ref JniObjectReference reference,
		JniObjectReferenceOptions options,
		[DynamicallyAccessedMembers (Constructors)]
		Type targetType,
		[NotNullWhen (true)] out object? value)
	{
		foreach (var handler in PrimitiveArrayHandlers) {
			if (handler.TryCreateWrapper (ref reference, options, targetType, out value)) {
				return true;
			}
		}

		value = null;
		return false;
	}

	static bool TryCreatePrimitiveArrayArgumentState (object value, ParameterAttributes synchronize, out JniValueMarshalerState state)
	{
		foreach (var handler in PrimitiveArrayHandlers) {
			if (handler.TryCreateArgumentState (value, synchronize, out state)) {
				return true;
			}
		}

		state = new JniValueMarshalerState ();
		return false;
	}

	static bool TryDestroyPrimitiveArrayArgumentState (object? value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
	{
		foreach (var handler in PrimitiveArrayHandlers) {
			if (handler.TryDestroyArgumentState (value, ref state, synchronize)) {
				return true;
			}
		}

		return false;
	}

	static bool IsPrimitiveArrayTargetType (Type targetType)
	{
		foreach (var handler in PrimitiveArrayHandlers) {
			if (handler.IsTargetType (targetType)) {
				return true;
			}
		}

		return false;
	}

	static ParameterAttributes GetCopyDirection (ParameterAttributes value)
	{
		const ParameterAttributes inout = ParameterAttributes.In | ParameterAttributes.Out;
		if ((value & inout) != 0)
			return value & inout;
		return inout;
	}
}
