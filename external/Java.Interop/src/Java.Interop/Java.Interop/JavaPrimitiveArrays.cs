#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Java.Interop.Expressions;
using System.Linq.Expressions;

namespace Java.Interop {

	partial class JniRuntime {
		static JniTypeSignature __BooleanTypeArraySignature;
		static JniTypeSignature __SByteTypeArraySignature;
		static JniTypeSignature __CharTypeArraySignature;
		static JniTypeSignature __Int16TypeArraySignature;
		static JniTypeSignature __Int32TypeArraySignature;
		static JniTypeSignature __Int64TypeArraySignature;
		static JniTypeSignature __SingleTypeArraySignature;
		static JniTypeSignature __DoubleTypeArraySignature;

		static bool GetBuiltInTypeArraySignature (Type type, ref JniTypeSignature signature)
		{
			if (type == typeof (JavaArray<Boolean>) || type == typeof (JavaPrimitiveArray<Boolean>)) {
				signature = GetCachedTypeSignature (ref __BooleanTypeArraySignature, "Z", arrayRank: 1, keyword: true);
				return true;
			}
			if (type == typeof (JavaArray<SByte>) || type == typeof (JavaPrimitiveArray<SByte>)) {
				signature = GetCachedTypeSignature (ref __SByteTypeArraySignature, "B", arrayRank: 1, keyword: true);
				return true;
			}
			if (type == typeof (JavaArray<Char>) || type == typeof (JavaPrimitiveArray<Char>)) {
				signature = GetCachedTypeSignature (ref __CharTypeArraySignature, "C", arrayRank: 1, keyword: true);
				return true;
			}
			if (type == typeof (JavaArray<Int16>) || type == typeof (JavaPrimitiveArray<Int16>)) {
				signature = GetCachedTypeSignature (ref __Int16TypeArraySignature, "S", arrayRank: 1, keyword: true);
				return true;
			}
			if (type == typeof (JavaArray<Int32>) || type == typeof (JavaPrimitiveArray<Int32>)) {
				signature = GetCachedTypeSignature (ref __Int32TypeArraySignature, "I", arrayRank: 1, keyword: true);
				return true;
			}
			if (type == typeof (JavaArray<Int64>) || type == typeof (JavaPrimitiveArray<Int64>)) {
				signature = GetCachedTypeSignature (ref __Int64TypeArraySignature, "J", arrayRank: 1, keyword: true);
				return true;
			}
			if (type == typeof (JavaArray<Single>) || type == typeof (JavaPrimitiveArray<Single>)) {
				signature = GetCachedTypeSignature (ref __SingleTypeArraySignature, "F", arrayRank: 1, keyword: true);
				return true;
			}
			if (type == typeof (JavaArray<Double>) || type == typeof (JavaPrimitiveArray<Double>)) {
				signature = GetCachedTypeSignature (ref __DoubleTypeArraySignature, "D", arrayRank: 1, keyword: true);
				return true;
			}
			return false;
		}

		static readonly Lazy<KeyValuePair<Type, JniValueMarshaler>[]> JniPrimitiveArrayMarshalers = new Lazy<KeyValuePair<Type, JniValueMarshaler>[]> (InitJniPrimitiveArrayMarshalers);

		static KeyValuePair<Type, JniValueMarshaler>[] InitJniPrimitiveArrayMarshalers ()
		{
			return new [] {
				new KeyValuePair<Type, JniValueMarshaler>(typeof (Boolean[]),                   JavaBooleanArray.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaArray<Boolean>),          JavaBooleanArray.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaPrimitiveArray<Boolean>), JavaBooleanArray.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaBooleanArray),            JavaBooleanArray.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (SByte[]),                   JavaSByteArray.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaArray<SByte>),          JavaSByteArray.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaPrimitiveArray<SByte>), JavaSByteArray.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaSByteArray),            JavaSByteArray.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (Char[]),                   JavaCharArray.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaArray<Char>),          JavaCharArray.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaPrimitiveArray<Char>), JavaCharArray.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaCharArray),            JavaCharArray.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (Int16[]),                   JavaInt16Array.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaArray<Int16>),          JavaInt16Array.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaPrimitiveArray<Int16>), JavaInt16Array.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaInt16Array),            JavaInt16Array.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (Int32[]),                   JavaInt32Array.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaArray<Int32>),          JavaInt32Array.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaPrimitiveArray<Int32>), JavaInt32Array.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaInt32Array),            JavaInt32Array.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (Int64[]),                   JavaInt64Array.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaArray<Int64>),          JavaInt64Array.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaPrimitiveArray<Int64>), JavaInt64Array.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaInt64Array),            JavaInt64Array.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (Single[]),                   JavaSingleArray.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaArray<Single>),          JavaSingleArray.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaPrimitiveArray<Single>), JavaSingleArray.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaSingleArray),            JavaSingleArray.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (Double[]),                   JavaDoubleArray.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaArray<Double>),          JavaDoubleArray.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaPrimitiveArray<Double>), JavaDoubleArray.ArrayMarshaler),
				new KeyValuePair<Type, JniValueMarshaler>(typeof (JavaDoubleArray),            JavaDoubleArray.ArrayMarshaler),
			};
		}
	}

	partial class JniEnvironment {

		partial class Arrays {

			public static JavaBooleanArray? CreateMarshalBooleanArray (System.Collections.Generic.IEnumerable<Boolean>? value)
			{
				if (value == null) {
					return null;
				}
				if (value is JavaBooleanArray c) {
					return c;
				}
				return new JavaBooleanArray (value) {
					forMarshalCollection = true,
				};
			}
		}
	}

	public sealed class JniBooleanArrayElements : JniArrayElements {

		JniObjectReference      arrayHandle;

		internal unsafe JniBooleanArrayElements (JniObjectReference arrayHandle, Boolean* elements, int size)
			: base ((IntPtr) elements, size)
		{
			this.arrayHandle = arrayHandle;
		}

		public new unsafe Boolean* Elements {
			get {return (Boolean*) base.Elements;}
		}

		public ref Boolean this [int index] {
			get {
				if (IsDisposed)
					throw new ObjectDisposedException (GetType ().FullName);
				unsafe {
					return ref Elements [index];
				}
			}
		}

		protected override unsafe void Synchronize (JniReleaseArrayElementsMode releaseMode)
		{
			JniEnvironment.Arrays.ReleaseBooleanArrayElements (arrayHandle, Elements, releaseMode);
		}
	}

	[JniTypeSignature ("Z", ArrayRank=1, IsKeyword=true)]
	public sealed class JavaBooleanArray : JavaPrimitiveArray<Boolean> {

		internal    static  readonly    ValueMarshaler   ArrayMarshaler     = new ValueMarshaler ();

		public JavaBooleanArray (ref JniObjectReference handle, JniObjectReferenceOptions options)
			: base (ref handle, options)
		{
		}

		public unsafe JavaBooleanArray (int length)
			: base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			var peer    = JniEnvironment.Arrays.NewBooleanArray (CheckLength (length));
			Construct (ref peer, JniObjectReferenceOptions.CopyAndDispose);
		}

		public JavaBooleanArray (System.Collections.Generic.IList<Boolean> value)
			: this (CheckLength (value))
		{
			CopyFrom (ToArray (value), 0, 0, value.Count);
		}

		public JavaBooleanArray (System.Collections.Generic.IEnumerable<Boolean> value)
			: this (ToArray (value))
		{
		}

		protected override JniArrayElements CreateElements ()
		{
			return GetElements ();
		}

		public new unsafe JniBooleanArrayElements GetElements ()
		{
			if (!PeerReference.IsValid)
				throw new ObjectDisposedException (this.GetType ().FullName);
			var elements = JniEnvironment.Arrays.GetBooleanArrayElements (PeerReference, null);
			if (elements == null)
				throw new InvalidOperationException ("`JniEnvironment.Arrays.GetBooleanArrayElements()` returned NULL!");
			return new JniBooleanArrayElements (PeerReference, elements, Length*sizeof (Boolean));
		}

		public override unsafe int IndexOf (Boolean item)
		{
			int len = Length;
			if (len == 0)
				return -1;
			using (var e = GetElements ()) {
				Debug.Assert (e != null, "Java.Boolean.Array.GetElements() returned null! OOM?");
				if (e == null)
					return -1;      // IList<T>.IndexOf() documents no exceptions. :-/

				for (int i = 0; i < len; ++i) {
					if (e [i] == item)
						return i;
				}
			}
			return -1;
		}

		public override unsafe void Clear ()
		{
			int len = Length;
			using (var e = GetElements ()) {
				for (int i = 0; i < len; ++i) {
					e [i] = default (Boolean);
				}
			}
		}

		public override unsafe void CopyTo (int sourceIndex, Boolean[] destinationArray, int destinationIndex, int length)
		{
			if (destinationArray == null)
				throw new ArgumentNullException (nameof (destinationArray));
			CheckArrayCopy (sourceIndex, Length, destinationIndex, destinationArray.Length, length);
			if (destinationArray.Length == 0)
				return;

			fixed (Boolean* b = destinationArray)
				JniEnvironment.Arrays.GetBooleanArrayRegion (PeerReference, sourceIndex, length, (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Boolean[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException (nameof (sourceArray));
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Boolean* b = sourceArray)
				JniEnvironment.Arrays.SetBooleanArrayRegion (PeerReference, destinationIndex, length, (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type? targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Boolean>) == targetType ||
				typeof (JavaBooleanArray) == targetType;
		}

		public static object? CreateMarshaledValue (IntPtr handle, Type? targetType)
		{
			return ArrayMarshaler.CreateValue (handle, targetType);
		}

		internal sealed class ValueMarshaler : JniValueMarshaler<IList<Boolean>> {

			public override IList<Boolean> CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type? targetType)
			{
				return JavaArray<Boolean>.CreateValue (
						ref reference,
						options,
						targetType,
						(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaBooleanArray (ref h, o));
			}

			public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState ([MaybeNull] IList<Boolean> value, ParameterAttributes synchronize)
			{
				return JavaArray<Boolean>.CreateArgumentState (value, synchronize, (list, copy) => {
					var a = copy
						? new JavaBooleanArray (list)
						: new JavaBooleanArray (list.Count);
					a.forMarshalCollection = true;
					return a;
				});
			}

			public override void DestroyGenericArgumentState ([AllowNull] IList<Boolean> value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
			{
				JavaArray<Boolean>.DestroyArgumentState<JavaBooleanArray> (value, ref state, synchronize);
			}

			public override Expression CreateParameterToManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize = 0, Type? targetType = null)
			{
				Func<IntPtr, Type?, object?>  m = JavaBooleanArray.CreateMarshaledValue;

				var call    = Expression.Call (m.GetMethodInfo (), sourceValue, Expression.Constant (targetType, typeof (Type)));
				return targetType == null
					? (Expression) call
					: Expression.Convert (call, targetType);
			}
		}
	}

	partial class JniEnvironment {

		partial class Arrays {

			public static JavaSByteArray? CreateMarshalSByteArray (System.Collections.Generic.IEnumerable<SByte>? value)
			{
				if (value == null) {
					return null;
				}
				if (value is JavaSByteArray c) {
					return c;
				}
				return new JavaSByteArray (value) {
					forMarshalCollection = true,
				};
			}
		}
	}

	public sealed class JniSByteArrayElements : JniArrayElements {

		JniObjectReference      arrayHandle;

		internal unsafe JniSByteArrayElements (JniObjectReference arrayHandle, SByte* elements, int size)
			: base ((IntPtr) elements, size)
		{
			this.arrayHandle = arrayHandle;
		}

		public new unsafe SByte* Elements {
			get {return (SByte*) base.Elements;}
		}

		public ref SByte this [int index] {
			get {
				if (IsDisposed)
					throw new ObjectDisposedException (GetType ().FullName);
				unsafe {
					return ref Elements [index];
				}
			}
		}

		protected override unsafe void Synchronize (JniReleaseArrayElementsMode releaseMode)
		{
			JniEnvironment.Arrays.ReleaseByteArrayElements (arrayHandle, Elements, releaseMode);
		}
	}

	[JniTypeSignature ("B", ArrayRank=1, IsKeyword=true)]
	public sealed class JavaSByteArray : JavaPrimitiveArray<SByte> {

		internal    static  readonly    ValueMarshaler   ArrayMarshaler     = new ValueMarshaler ();

		public JavaSByteArray (ref JniObjectReference handle, JniObjectReferenceOptions options)
			: base (ref handle, options)
		{
		}

		public unsafe JavaSByteArray (int length)
			: base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			var peer    = JniEnvironment.Arrays.NewByteArray (CheckLength (length));
			Construct (ref peer, JniObjectReferenceOptions.CopyAndDispose);
		}

		public JavaSByteArray (System.Collections.Generic.IList<SByte> value)
			: this (CheckLength (value))
		{
			CopyFrom (ToArray (value), 0, 0, value.Count);
		}

		public JavaSByteArray (System.Collections.Generic.IEnumerable<SByte> value)
			: this (ToArray (value))
		{
		}

		protected override JniArrayElements CreateElements ()
		{
			return GetElements ();
		}

		public new unsafe JniSByteArrayElements GetElements ()
		{
			if (!PeerReference.IsValid)
				throw new ObjectDisposedException (this.GetType ().FullName);
			var elements = JniEnvironment.Arrays.GetByteArrayElements (PeerReference, null);
			if (elements == null)
				throw new InvalidOperationException ("`JniEnvironment.Arrays.GetByteArrayElements()` returned NULL!");
			return new JniSByteArrayElements (PeerReference, elements, Length*sizeof (SByte));
		}

		public override unsafe int IndexOf (SByte item)
		{
			int len = Length;
			if (len == 0)
				return -1;
			using (var e = GetElements ()) {
				Debug.Assert (e != null, "Java.SByte.Array.GetElements() returned null! OOM?");
				if (e == null)
					return -1;      // IList<T>.IndexOf() documents no exceptions. :-/

				for (int i = 0; i < len; ++i) {
					if (e [i] == item)
						return i;
				}
			}
			return -1;
		}

		public override unsafe void Clear ()
		{
			int len = Length;
			using (var e = GetElements ()) {
				for (int i = 0; i < len; ++i) {
					e [i] = default (SByte);
				}
			}
		}

		public override unsafe void CopyTo (int sourceIndex, SByte[] destinationArray, int destinationIndex, int length)
		{
			if (destinationArray == null)
				throw new ArgumentNullException (nameof (destinationArray));
			CheckArrayCopy (sourceIndex, Length, destinationIndex, destinationArray.Length, length);
			if (destinationArray.Length == 0)
				return;

			fixed (SByte* b = destinationArray)
				JniEnvironment.Arrays.GetByteArrayRegion (PeerReference, sourceIndex, length, (b+destinationIndex));
		}

		public override unsafe void CopyFrom (SByte[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException (nameof (sourceArray));
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (SByte* b = sourceArray)
				JniEnvironment.Arrays.SetByteArrayRegion (PeerReference, destinationIndex, length, (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type? targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<SByte>) == targetType ||
				typeof (JavaSByteArray) == targetType;
		}

		public static object? CreateMarshaledValue (IntPtr handle, Type? targetType)
		{
			return ArrayMarshaler.CreateValue (handle, targetType);
		}

		internal sealed class ValueMarshaler : JniValueMarshaler<IList<SByte>> {

			public override IList<SByte> CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type? targetType)
			{
				return JavaArray<SByte>.CreateValue (
						ref reference,
						options,
						targetType,
						(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaSByteArray (ref h, o));
			}

			public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState ([MaybeNull] IList<SByte> value, ParameterAttributes synchronize)
			{
				return JavaArray<SByte>.CreateArgumentState (value, synchronize, (list, copy) => {
					var a = copy
						? new JavaSByteArray (list)
						: new JavaSByteArray (list.Count);
					a.forMarshalCollection = true;
					return a;
				});
			}

			public override void DestroyGenericArgumentState ([AllowNull] IList<SByte> value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
			{
				JavaArray<SByte>.DestroyArgumentState<JavaSByteArray> (value, ref state, synchronize);
			}

			public override Expression CreateParameterToManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize = 0, Type? targetType = null)
			{
				Func<IntPtr, Type?, object?>  m = JavaSByteArray.CreateMarshaledValue;

				var call    = Expression.Call (m.GetMethodInfo (), sourceValue, Expression.Constant (targetType, typeof (Type)));
				return targetType == null
					? (Expression) call
					: Expression.Convert (call, targetType);
			}
		}
	}

	partial class JniEnvironment {

		partial class Arrays {

			public static JavaCharArray? CreateMarshalCharArray (System.Collections.Generic.IEnumerable<Char>? value)
			{
				if (value == null) {
					return null;
				}
				if (value is JavaCharArray c) {
					return c;
				}
				return new JavaCharArray (value) {
					forMarshalCollection = true,
				};
			}
		}
	}

	public sealed class JniCharArrayElements : JniArrayElements {

		JniObjectReference      arrayHandle;

		internal unsafe JniCharArrayElements (JniObjectReference arrayHandle, Char* elements, int size)
			: base ((IntPtr) elements, size)
		{
			this.arrayHandle = arrayHandle;
		}

		public new unsafe Char* Elements {
			get {return (Char*) base.Elements;}
		}

		public ref Char this [int index] {
			get {
				if (IsDisposed)
					throw new ObjectDisposedException (GetType ().FullName);
				unsafe {
					return ref Elements [index];
				}
			}
		}

		protected override unsafe void Synchronize (JniReleaseArrayElementsMode releaseMode)
		{
			JniEnvironment.Arrays.ReleaseCharArrayElements (arrayHandle, Elements, releaseMode);
		}
	}

	[JniTypeSignature ("C", ArrayRank=1, IsKeyword=true)]
	public sealed class JavaCharArray : JavaPrimitiveArray<Char> {

		internal    static  readonly    ValueMarshaler   ArrayMarshaler     = new ValueMarshaler ();

		public JavaCharArray (ref JniObjectReference handle, JniObjectReferenceOptions options)
			: base (ref handle, options)
		{
		}

		public unsafe JavaCharArray (int length)
			: base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			var peer    = JniEnvironment.Arrays.NewCharArray (CheckLength (length));
			Construct (ref peer, JniObjectReferenceOptions.CopyAndDispose);
		}

		public JavaCharArray (System.Collections.Generic.IList<Char> value)
			: this (CheckLength (value))
		{
			CopyFrom (ToArray (value), 0, 0, value.Count);
		}

		public JavaCharArray (System.Collections.Generic.IEnumerable<Char> value)
			: this (ToArray (value))
		{
		}

		protected override JniArrayElements CreateElements ()
		{
			return GetElements ();
		}

		public new unsafe JniCharArrayElements GetElements ()
		{
			if (!PeerReference.IsValid)
				throw new ObjectDisposedException (this.GetType ().FullName);
			var elements = JniEnvironment.Arrays.GetCharArrayElements (PeerReference, null);
			if (elements == null)
				throw new InvalidOperationException ("`JniEnvironment.Arrays.GetCharArrayElements()` returned NULL!");
			return new JniCharArrayElements (PeerReference, elements, Length*sizeof (Char));
		}

		public override unsafe int IndexOf (Char item)
		{
			int len = Length;
			if (len == 0)
				return -1;
			using (var e = GetElements ()) {
				Debug.Assert (e != null, "Java.Char.Array.GetElements() returned null! OOM?");
				if (e == null)
					return -1;      // IList<T>.IndexOf() documents no exceptions. :-/

				for (int i = 0; i < len; ++i) {
					if (e [i] == item)
						return i;
				}
			}
			return -1;
		}

		public override unsafe void Clear ()
		{
			int len = Length;
			using (var e = GetElements ()) {
				for (int i = 0; i < len; ++i) {
					e [i] = default (Char);
				}
			}
		}

		public override unsafe void CopyTo (int sourceIndex, Char[] destinationArray, int destinationIndex, int length)
		{
			if (destinationArray == null)
				throw new ArgumentNullException (nameof (destinationArray));
			CheckArrayCopy (sourceIndex, Length, destinationIndex, destinationArray.Length, length);
			if (destinationArray.Length == 0)
				return;

			fixed (Char* b = destinationArray)
				JniEnvironment.Arrays.GetCharArrayRegion (PeerReference, sourceIndex, length, (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Char[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException (nameof (sourceArray));
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Char* b = sourceArray)
				JniEnvironment.Arrays.SetCharArrayRegion (PeerReference, destinationIndex, length, (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type? targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Char>) == targetType ||
				typeof (JavaCharArray) == targetType;
		}

		public static object? CreateMarshaledValue (IntPtr handle, Type? targetType)
		{
			return ArrayMarshaler.CreateValue (handle, targetType);
		}

		internal sealed class ValueMarshaler : JniValueMarshaler<IList<Char>> {

			public override IList<Char> CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type? targetType)
			{
				return JavaArray<Char>.CreateValue (
						ref reference,
						options,
						targetType,
						(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaCharArray (ref h, o));
			}

			public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState ([MaybeNull] IList<Char> value, ParameterAttributes synchronize)
			{
				return JavaArray<Char>.CreateArgumentState (value, synchronize, (list, copy) => {
					var a = copy
						? new JavaCharArray (list)
						: new JavaCharArray (list.Count);
					a.forMarshalCollection = true;
					return a;
				});
			}

			public override void DestroyGenericArgumentState ([AllowNull] IList<Char> value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
			{
				JavaArray<Char>.DestroyArgumentState<JavaCharArray> (value, ref state, synchronize);
			}

			public override Expression CreateParameterToManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize = 0, Type? targetType = null)
			{
				Func<IntPtr, Type?, object?>  m = JavaCharArray.CreateMarshaledValue;

				var call    = Expression.Call (m.GetMethodInfo (), sourceValue, Expression.Constant (targetType, typeof (Type)));
				return targetType == null
					? (Expression) call
					: Expression.Convert (call, targetType);
			}
		}
	}

	partial class JniEnvironment {

		partial class Arrays {

			public static JavaInt16Array? CreateMarshalInt16Array (System.Collections.Generic.IEnumerable<Int16>? value)
			{
				if (value == null) {
					return null;
				}
				if (value is JavaInt16Array c) {
					return c;
				}
				return new JavaInt16Array (value) {
					forMarshalCollection = true,
				};
			}
		}
	}

	public sealed class JniInt16ArrayElements : JniArrayElements {

		JniObjectReference      arrayHandle;

		internal unsafe JniInt16ArrayElements (JniObjectReference arrayHandle, Int16* elements, int size)
			: base ((IntPtr) elements, size)
		{
			this.arrayHandle = arrayHandle;
		}

		public new unsafe Int16* Elements {
			get {return (Int16*) base.Elements;}
		}

		public ref Int16 this [int index] {
			get {
				if (IsDisposed)
					throw new ObjectDisposedException (GetType ().FullName);
				unsafe {
					return ref Elements [index];
				}
			}
		}

		protected override unsafe void Synchronize (JniReleaseArrayElementsMode releaseMode)
		{
			JniEnvironment.Arrays.ReleaseShortArrayElements (arrayHandle, Elements, releaseMode);
		}
	}

	[JniTypeSignature ("S", ArrayRank=1, IsKeyword=true)]
	public sealed class JavaInt16Array : JavaPrimitiveArray<Int16> {

		internal    static  readonly    ValueMarshaler   ArrayMarshaler     = new ValueMarshaler ();

		public JavaInt16Array (ref JniObjectReference handle, JniObjectReferenceOptions options)
			: base (ref handle, options)
		{
		}

		public unsafe JavaInt16Array (int length)
			: base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			var peer    = JniEnvironment.Arrays.NewShortArray (CheckLength (length));
			Construct (ref peer, JniObjectReferenceOptions.CopyAndDispose);
		}

		public JavaInt16Array (System.Collections.Generic.IList<Int16> value)
			: this (CheckLength (value))
		{
			CopyFrom (ToArray (value), 0, 0, value.Count);
		}

		public JavaInt16Array (System.Collections.Generic.IEnumerable<Int16> value)
			: this (ToArray (value))
		{
		}

		protected override JniArrayElements CreateElements ()
		{
			return GetElements ();
		}

		public new unsafe JniInt16ArrayElements GetElements ()
		{
			if (!PeerReference.IsValid)
				throw new ObjectDisposedException (this.GetType ().FullName);
			var elements = JniEnvironment.Arrays.GetShortArrayElements (PeerReference, null);
			if (elements == null)
				throw new InvalidOperationException ("`JniEnvironment.Arrays.GetShortArrayElements()` returned NULL!");
			return new JniInt16ArrayElements (PeerReference, elements, Length*sizeof (Int16));
		}

		public override unsafe int IndexOf (Int16 item)
		{
			int len = Length;
			if (len == 0)
				return -1;
			using (var e = GetElements ()) {
				Debug.Assert (e != null, "Java.Int16.Array.GetElements() returned null! OOM?");
				if (e == null)
					return -1;      // IList<T>.IndexOf() documents no exceptions. :-/

				for (int i = 0; i < len; ++i) {
					if (e [i] == item)
						return i;
				}
			}
			return -1;
		}

		public override unsafe void Clear ()
		{
			int len = Length;
			using (var e = GetElements ()) {
				for (int i = 0; i < len; ++i) {
					e [i] = default (Int16);
				}
			}
		}

		public override unsafe void CopyTo (int sourceIndex, Int16[] destinationArray, int destinationIndex, int length)
		{
			if (destinationArray == null)
				throw new ArgumentNullException (nameof (destinationArray));
			CheckArrayCopy (sourceIndex, Length, destinationIndex, destinationArray.Length, length);
			if (destinationArray.Length == 0)
				return;

			fixed (Int16* b = destinationArray)
				JniEnvironment.Arrays.GetShortArrayRegion (PeerReference, sourceIndex, length, (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Int16[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException (nameof (sourceArray));
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Int16* b = sourceArray)
				JniEnvironment.Arrays.SetShortArrayRegion (PeerReference, destinationIndex, length, (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type? targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Int16>) == targetType ||
				typeof (JavaInt16Array) == targetType;
		}

		public static object? CreateMarshaledValue (IntPtr handle, Type? targetType)
		{
			return ArrayMarshaler.CreateValue (handle, targetType);
		}

		internal sealed class ValueMarshaler : JniValueMarshaler<IList<Int16>> {

			public override IList<Int16> CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type? targetType)
			{
				return JavaArray<Int16>.CreateValue (
						ref reference,
						options,
						targetType,
						(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaInt16Array (ref h, o));
			}

			public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState ([MaybeNull] IList<Int16> value, ParameterAttributes synchronize)
			{
				return JavaArray<Int16>.CreateArgumentState (value, synchronize, (list, copy) => {
					var a = copy
						? new JavaInt16Array (list)
						: new JavaInt16Array (list.Count);
					a.forMarshalCollection = true;
					return a;
				});
			}

			public override void DestroyGenericArgumentState ([AllowNull] IList<Int16> value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
			{
				JavaArray<Int16>.DestroyArgumentState<JavaInt16Array> (value, ref state, synchronize);
			}

			public override Expression CreateParameterToManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize = 0, Type? targetType = null)
			{
				Func<IntPtr, Type?, object?>  m = JavaInt16Array.CreateMarshaledValue;

				var call    = Expression.Call (m.GetMethodInfo (), sourceValue, Expression.Constant (targetType, typeof (Type)));
				return targetType == null
					? (Expression) call
					: Expression.Convert (call, targetType);
			}
		}
	}

	partial class JniEnvironment {

		partial class Arrays {

			public static JavaInt32Array? CreateMarshalInt32Array (System.Collections.Generic.IEnumerable<Int32>? value)
			{
				if (value == null) {
					return null;
				}
				if (value is JavaInt32Array c) {
					return c;
				}
				return new JavaInt32Array (value) {
					forMarshalCollection = true,
				};
			}
		}
	}

	public sealed class JniInt32ArrayElements : JniArrayElements {

		JniObjectReference      arrayHandle;

		internal unsafe JniInt32ArrayElements (JniObjectReference arrayHandle, Int32* elements, int size)
			: base ((IntPtr) elements, size)
		{
			this.arrayHandle = arrayHandle;
		}

		public new unsafe Int32* Elements {
			get {return (Int32*) base.Elements;}
		}

		public ref Int32 this [int index] {
			get {
				if (IsDisposed)
					throw new ObjectDisposedException (GetType ().FullName);
				unsafe {
					return ref Elements [index];
				}
			}
		}

		protected override unsafe void Synchronize (JniReleaseArrayElementsMode releaseMode)
		{
			JniEnvironment.Arrays.ReleaseIntArrayElements (arrayHandle, Elements, releaseMode);
		}
	}

	[JniTypeSignature ("I", ArrayRank=1, IsKeyword=true)]
	public sealed class JavaInt32Array : JavaPrimitiveArray<Int32> {

		internal    static  readonly    ValueMarshaler   ArrayMarshaler     = new ValueMarshaler ();

		public JavaInt32Array (ref JniObjectReference handle, JniObjectReferenceOptions options)
			: base (ref handle, options)
		{
		}

		public unsafe JavaInt32Array (int length)
			: base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			var peer    = JniEnvironment.Arrays.NewIntArray (CheckLength (length));
			Construct (ref peer, JniObjectReferenceOptions.CopyAndDispose);
		}

		public JavaInt32Array (System.Collections.Generic.IList<Int32> value)
			: this (CheckLength (value))
		{
			CopyFrom (ToArray (value), 0, 0, value.Count);
		}

		public JavaInt32Array (System.Collections.Generic.IEnumerable<Int32> value)
			: this (ToArray (value))
		{
		}

		protected override JniArrayElements CreateElements ()
		{
			return GetElements ();
		}

		public new unsafe JniInt32ArrayElements GetElements ()
		{
			if (!PeerReference.IsValid)
				throw new ObjectDisposedException (this.GetType ().FullName);
			var elements = JniEnvironment.Arrays.GetIntArrayElements (PeerReference, null);
			if (elements == null)
				throw new InvalidOperationException ("`JniEnvironment.Arrays.GetIntArrayElements()` returned NULL!");
			return new JniInt32ArrayElements (PeerReference, elements, Length*sizeof (Int32));
		}

		public override unsafe int IndexOf (Int32 item)
		{
			int len = Length;
			if (len == 0)
				return -1;
			using (var e = GetElements ()) {
				Debug.Assert (e != null, "Java.Int32.Array.GetElements() returned null! OOM?");
				if (e == null)
					return -1;      // IList<T>.IndexOf() documents no exceptions. :-/

				for (int i = 0; i < len; ++i) {
					if (e [i] == item)
						return i;
				}
			}
			return -1;
		}

		public override unsafe void Clear ()
		{
			int len = Length;
			using (var e = GetElements ()) {
				for (int i = 0; i < len; ++i) {
					e [i] = default (Int32);
				}
			}
		}

		public override unsafe void CopyTo (int sourceIndex, Int32[] destinationArray, int destinationIndex, int length)
		{
			if (destinationArray == null)
				throw new ArgumentNullException (nameof (destinationArray));
			CheckArrayCopy (sourceIndex, Length, destinationIndex, destinationArray.Length, length);
			if (destinationArray.Length == 0)
				return;

			fixed (Int32* b = destinationArray)
				JniEnvironment.Arrays.GetIntArrayRegion (PeerReference, sourceIndex, length, (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Int32[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException (nameof (sourceArray));
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Int32* b = sourceArray)
				JniEnvironment.Arrays.SetIntArrayRegion (PeerReference, destinationIndex, length, (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type? targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Int32>) == targetType ||
				typeof (JavaInt32Array) == targetType;
		}

		public static object? CreateMarshaledValue (IntPtr handle, Type? targetType)
		{
			return ArrayMarshaler.CreateValue (handle, targetType);
		}

		internal sealed class ValueMarshaler : JniValueMarshaler<IList<Int32>> {

			public override IList<Int32> CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type? targetType)
			{
				return JavaArray<Int32>.CreateValue (
						ref reference,
						options,
						targetType,
						(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaInt32Array (ref h, o));
			}

			public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState ([MaybeNull] IList<Int32> value, ParameterAttributes synchronize)
			{
				return JavaArray<Int32>.CreateArgumentState (value, synchronize, (list, copy) => {
					var a = copy
						? new JavaInt32Array (list)
						: new JavaInt32Array (list.Count);
					a.forMarshalCollection = true;
					return a;
				});
			}

			public override void DestroyGenericArgumentState ([AllowNull] IList<Int32> value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
			{
				JavaArray<Int32>.DestroyArgumentState<JavaInt32Array> (value, ref state, synchronize);
			}

			public override Expression CreateParameterToManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize = 0, Type? targetType = null)
			{
				Func<IntPtr, Type?, object?>  m = JavaInt32Array.CreateMarshaledValue;

				var call    = Expression.Call (m.GetMethodInfo (), sourceValue, Expression.Constant (targetType, typeof (Type)));
				return targetType == null
					? (Expression) call
					: Expression.Convert (call, targetType);
			}
		}
	}

	partial class JniEnvironment {

		partial class Arrays {

			public static JavaInt64Array? CreateMarshalInt64Array (System.Collections.Generic.IEnumerable<Int64>? value)
			{
				if (value == null) {
					return null;
				}
				if (value is JavaInt64Array c) {
					return c;
				}
				return new JavaInt64Array (value) {
					forMarshalCollection = true,
				};
			}
		}
	}

	public sealed class JniInt64ArrayElements : JniArrayElements {

		JniObjectReference      arrayHandle;

		internal unsafe JniInt64ArrayElements (JniObjectReference arrayHandle, Int64* elements, int size)
			: base ((IntPtr) elements, size)
		{
			this.arrayHandle = arrayHandle;
		}

		public new unsafe Int64* Elements {
			get {return (Int64*) base.Elements;}
		}

		public ref Int64 this [int index] {
			get {
				if (IsDisposed)
					throw new ObjectDisposedException (GetType ().FullName);
				unsafe {
					return ref Elements [index];
				}
			}
		}

		protected override unsafe void Synchronize (JniReleaseArrayElementsMode releaseMode)
		{
			JniEnvironment.Arrays.ReleaseLongArrayElements (arrayHandle, Elements, releaseMode);
		}
	}

	[JniTypeSignature ("J", ArrayRank=1, IsKeyword=true)]
	public sealed class JavaInt64Array : JavaPrimitiveArray<Int64> {

		internal    static  readonly    ValueMarshaler   ArrayMarshaler     = new ValueMarshaler ();

		public JavaInt64Array (ref JniObjectReference handle, JniObjectReferenceOptions options)
			: base (ref handle, options)
		{
		}

		public unsafe JavaInt64Array (int length)
			: base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			var peer    = JniEnvironment.Arrays.NewLongArray (CheckLength (length));
			Construct (ref peer, JniObjectReferenceOptions.CopyAndDispose);
		}

		public JavaInt64Array (System.Collections.Generic.IList<Int64> value)
			: this (CheckLength (value))
		{
			CopyFrom (ToArray (value), 0, 0, value.Count);
		}

		public JavaInt64Array (System.Collections.Generic.IEnumerable<Int64> value)
			: this (ToArray (value))
		{
		}

		protected override JniArrayElements CreateElements ()
		{
			return GetElements ();
		}

		public new unsafe JniInt64ArrayElements GetElements ()
		{
			if (!PeerReference.IsValid)
				throw new ObjectDisposedException (this.GetType ().FullName);
			var elements = JniEnvironment.Arrays.GetLongArrayElements (PeerReference, null);
			if (elements == null)
				throw new InvalidOperationException ("`JniEnvironment.Arrays.GetLongArrayElements()` returned NULL!");
			return new JniInt64ArrayElements (PeerReference, elements, Length*sizeof (Int64));
		}

		public override unsafe int IndexOf (Int64 item)
		{
			int len = Length;
			if (len == 0)
				return -1;
			using (var e = GetElements ()) {
				Debug.Assert (e != null, "Java.Int64.Array.GetElements() returned null! OOM?");
				if (e == null)
					return -1;      // IList<T>.IndexOf() documents no exceptions. :-/

				for (int i = 0; i < len; ++i) {
					if (e [i] == item)
						return i;
				}
			}
			return -1;
		}

		public override unsafe void Clear ()
		{
			int len = Length;
			using (var e = GetElements ()) {
				for (int i = 0; i < len; ++i) {
					e [i] = default (Int64);
				}
			}
		}

		public override unsafe void CopyTo (int sourceIndex, Int64[] destinationArray, int destinationIndex, int length)
		{
			if (destinationArray == null)
				throw new ArgumentNullException (nameof (destinationArray));
			CheckArrayCopy (sourceIndex, Length, destinationIndex, destinationArray.Length, length);
			if (destinationArray.Length == 0)
				return;

			fixed (Int64* b = destinationArray)
				JniEnvironment.Arrays.GetLongArrayRegion (PeerReference, sourceIndex, length, (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Int64[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException (nameof (sourceArray));
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Int64* b = sourceArray)
				JniEnvironment.Arrays.SetLongArrayRegion (PeerReference, destinationIndex, length, (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type? targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Int64>) == targetType ||
				typeof (JavaInt64Array) == targetType;
		}

		public static object? CreateMarshaledValue (IntPtr handle, Type? targetType)
		{
			return ArrayMarshaler.CreateValue (handle, targetType);
		}

		internal sealed class ValueMarshaler : JniValueMarshaler<IList<Int64>> {

			public override IList<Int64> CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type? targetType)
			{
				return JavaArray<Int64>.CreateValue (
						ref reference,
						options,
						targetType,
						(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaInt64Array (ref h, o));
			}

			public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState ([MaybeNull] IList<Int64> value, ParameterAttributes synchronize)
			{
				return JavaArray<Int64>.CreateArgumentState (value, synchronize, (list, copy) => {
					var a = copy
						? new JavaInt64Array (list)
						: new JavaInt64Array (list.Count);
					a.forMarshalCollection = true;
					return a;
				});
			}

			public override void DestroyGenericArgumentState ([AllowNull] IList<Int64> value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
			{
				JavaArray<Int64>.DestroyArgumentState<JavaInt64Array> (value, ref state, synchronize);
			}

			public override Expression CreateParameterToManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize = 0, Type? targetType = null)
			{
				Func<IntPtr, Type?, object?>  m = JavaInt64Array.CreateMarshaledValue;

				var call    = Expression.Call (m.GetMethodInfo (), sourceValue, Expression.Constant (targetType, typeof (Type)));
				return targetType == null
					? (Expression) call
					: Expression.Convert (call, targetType);
			}
		}
	}

	partial class JniEnvironment {

		partial class Arrays {

			public static JavaSingleArray? CreateMarshalSingleArray (System.Collections.Generic.IEnumerable<Single>? value)
			{
				if (value == null) {
					return null;
				}
				if (value is JavaSingleArray c) {
					return c;
				}
				return new JavaSingleArray (value) {
					forMarshalCollection = true,
				};
			}
		}
	}

	public sealed class JniSingleArrayElements : JniArrayElements {

		JniObjectReference      arrayHandle;

		internal unsafe JniSingleArrayElements (JniObjectReference arrayHandle, Single* elements, int size)
			: base ((IntPtr) elements, size)
		{
			this.arrayHandle = arrayHandle;
		}

		public new unsafe Single* Elements {
			get {return (Single*) base.Elements;}
		}

		public ref Single this [int index] {
			get {
				if (IsDisposed)
					throw new ObjectDisposedException (GetType ().FullName);
				unsafe {
					return ref Elements [index];
				}
			}
		}

		protected override unsafe void Synchronize (JniReleaseArrayElementsMode releaseMode)
		{
			JniEnvironment.Arrays.ReleaseFloatArrayElements (arrayHandle, Elements, releaseMode);
		}
	}

	[JniTypeSignature ("F", ArrayRank=1, IsKeyword=true)]
	public sealed class JavaSingleArray : JavaPrimitiveArray<Single> {

		internal    static  readonly    ValueMarshaler   ArrayMarshaler     = new ValueMarshaler ();

		public JavaSingleArray (ref JniObjectReference handle, JniObjectReferenceOptions options)
			: base (ref handle, options)
		{
		}

		public unsafe JavaSingleArray (int length)
			: base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			var peer    = JniEnvironment.Arrays.NewFloatArray (CheckLength (length));
			Construct (ref peer, JniObjectReferenceOptions.CopyAndDispose);
		}

		public JavaSingleArray (System.Collections.Generic.IList<Single> value)
			: this (CheckLength (value))
		{
			CopyFrom (ToArray (value), 0, 0, value.Count);
		}

		public JavaSingleArray (System.Collections.Generic.IEnumerable<Single> value)
			: this (ToArray (value))
		{
		}

		protected override JniArrayElements CreateElements ()
		{
			return GetElements ();
		}

		public new unsafe JniSingleArrayElements GetElements ()
		{
			if (!PeerReference.IsValid)
				throw new ObjectDisposedException (this.GetType ().FullName);
			var elements = JniEnvironment.Arrays.GetFloatArrayElements (PeerReference, null);
			if (elements == null)
				throw new InvalidOperationException ("`JniEnvironment.Arrays.GetFloatArrayElements()` returned NULL!");
			return new JniSingleArrayElements (PeerReference, elements, Length*sizeof (Single));
		}

		public override unsafe int IndexOf (Single item)
		{
			int len = Length;
			if (len == 0)
				return -1;
			using (var e = GetElements ()) {
				Debug.Assert (e != null, "Java.Single.Array.GetElements() returned null! OOM?");
				if (e == null)
					return -1;      // IList<T>.IndexOf() documents no exceptions. :-/

				for (int i = 0; i < len; ++i) {
					if (e [i] == item)
						return i;
				}
			}
			return -1;
		}

		public override unsafe void Clear ()
		{
			int len = Length;
			using (var e = GetElements ()) {
				for (int i = 0; i < len; ++i) {
					e [i] = default (Single);
				}
			}
		}

		public override unsafe void CopyTo (int sourceIndex, Single[] destinationArray, int destinationIndex, int length)
		{
			if (destinationArray == null)
				throw new ArgumentNullException (nameof (destinationArray));
			CheckArrayCopy (sourceIndex, Length, destinationIndex, destinationArray.Length, length);
			if (destinationArray.Length == 0)
				return;

			fixed (Single* b = destinationArray)
				JniEnvironment.Arrays.GetFloatArrayRegion (PeerReference, sourceIndex, length, (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Single[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException (nameof (sourceArray));
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Single* b = sourceArray)
				JniEnvironment.Arrays.SetFloatArrayRegion (PeerReference, destinationIndex, length, (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type? targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Single>) == targetType ||
				typeof (JavaSingleArray) == targetType;
		}

		public static object? CreateMarshaledValue (IntPtr handle, Type? targetType)
		{
			return ArrayMarshaler.CreateValue (handle, targetType);
		}

		internal sealed class ValueMarshaler : JniValueMarshaler<IList<Single>> {

			public override IList<Single> CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type? targetType)
			{
				return JavaArray<Single>.CreateValue (
						ref reference,
						options,
						targetType,
						(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaSingleArray (ref h, o));
			}

			public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState ([MaybeNull] IList<Single> value, ParameterAttributes synchronize)
			{
				return JavaArray<Single>.CreateArgumentState (value, synchronize, (list, copy) => {
					var a = copy
						? new JavaSingleArray (list)
						: new JavaSingleArray (list.Count);
					a.forMarshalCollection = true;
					return a;
				});
			}

			public override void DestroyGenericArgumentState ([AllowNull] IList<Single> value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
			{
				JavaArray<Single>.DestroyArgumentState<JavaSingleArray> (value, ref state, synchronize);
			}

			public override Expression CreateParameterToManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize = 0, Type? targetType = null)
			{
				Func<IntPtr, Type?, object?>  m = JavaSingleArray.CreateMarshaledValue;

				var call    = Expression.Call (m.GetMethodInfo (), sourceValue, Expression.Constant (targetType, typeof (Type)));
				return targetType == null
					? (Expression) call
					: Expression.Convert (call, targetType);
			}
		}
	}

	partial class JniEnvironment {

		partial class Arrays {

			public static JavaDoubleArray? CreateMarshalDoubleArray (System.Collections.Generic.IEnumerable<Double>? value)
			{
				if (value == null) {
					return null;
				}
				if (value is JavaDoubleArray c) {
					return c;
				}
				return new JavaDoubleArray (value) {
					forMarshalCollection = true,
				};
			}
		}
	}

	public sealed class JniDoubleArrayElements : JniArrayElements {

		JniObjectReference      arrayHandle;

		internal unsafe JniDoubleArrayElements (JniObjectReference arrayHandle, Double* elements, int size)
			: base ((IntPtr) elements, size)
		{
			this.arrayHandle = arrayHandle;
		}

		public new unsafe Double* Elements {
			get {return (Double*) base.Elements;}
		}

		public ref Double this [int index] {
			get {
				if (IsDisposed)
					throw new ObjectDisposedException (GetType ().FullName);
				unsafe {
					return ref Elements [index];
				}
			}
		}

		protected override unsafe void Synchronize (JniReleaseArrayElementsMode releaseMode)
		{
			JniEnvironment.Arrays.ReleaseDoubleArrayElements (arrayHandle, Elements, releaseMode);
		}
	}

	[JniTypeSignature ("D", ArrayRank=1, IsKeyword=true)]
	public sealed class JavaDoubleArray : JavaPrimitiveArray<Double> {

		internal    static  readonly    ValueMarshaler   ArrayMarshaler     = new ValueMarshaler ();

		public JavaDoubleArray (ref JniObjectReference handle, JniObjectReferenceOptions options)
			: base (ref handle, options)
		{
		}

		public unsafe JavaDoubleArray (int length)
			: base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			var peer    = JniEnvironment.Arrays.NewDoubleArray (CheckLength (length));
			Construct (ref peer, JniObjectReferenceOptions.CopyAndDispose);
		}

		public JavaDoubleArray (System.Collections.Generic.IList<Double> value)
			: this (CheckLength (value))
		{
			CopyFrom (ToArray (value), 0, 0, value.Count);
		}

		public JavaDoubleArray (System.Collections.Generic.IEnumerable<Double> value)
			: this (ToArray (value))
		{
		}

		protected override JniArrayElements CreateElements ()
		{
			return GetElements ();
		}

		public new unsafe JniDoubleArrayElements GetElements ()
		{
			if (!PeerReference.IsValid)
				throw new ObjectDisposedException (this.GetType ().FullName);
			var elements = JniEnvironment.Arrays.GetDoubleArrayElements (PeerReference, null);
			if (elements == null)
				throw new InvalidOperationException ("`JniEnvironment.Arrays.GetDoubleArrayElements()` returned NULL!");
			return new JniDoubleArrayElements (PeerReference, elements, Length*sizeof (Double));
		}

		public override unsafe int IndexOf (Double item)
		{
			int len = Length;
			if (len == 0)
				return -1;
			using (var e = GetElements ()) {
				Debug.Assert (e != null, "Java.Double.Array.GetElements() returned null! OOM?");
				if (e == null)
					return -1;      // IList<T>.IndexOf() documents no exceptions. :-/

				for (int i = 0; i < len; ++i) {
					if (e [i] == item)
						return i;
				}
			}
			return -1;
		}

		public override unsafe void Clear ()
		{
			int len = Length;
			using (var e = GetElements ()) {
				for (int i = 0; i < len; ++i) {
					e [i] = default (Double);
				}
			}
		}

		public override unsafe void CopyTo (int sourceIndex, Double[] destinationArray, int destinationIndex, int length)
		{
			if (destinationArray == null)
				throw new ArgumentNullException (nameof (destinationArray));
			CheckArrayCopy (sourceIndex, Length, destinationIndex, destinationArray.Length, length);
			if (destinationArray.Length == 0)
				return;

			fixed (Double* b = destinationArray)
				JniEnvironment.Arrays.GetDoubleArrayRegion (PeerReference, sourceIndex, length, (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Double[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException (nameof (sourceArray));
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Double* b = sourceArray)
				JniEnvironment.Arrays.SetDoubleArrayRegion (PeerReference, destinationIndex, length, (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type? targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Double>) == targetType ||
				typeof (JavaDoubleArray) == targetType;
		}

		public static object? CreateMarshaledValue (IntPtr handle, Type? targetType)
		{
			return ArrayMarshaler.CreateValue (handle, targetType);
		}

		internal sealed class ValueMarshaler : JniValueMarshaler<IList<Double>> {

			public override IList<Double> CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type? targetType)
			{
				return JavaArray<Double>.CreateValue (
						ref reference,
						options,
						targetType,
						(ref JniObjectReference h, JniObjectReferenceOptions o) => new JavaDoubleArray (ref h, o));
			}

			public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState ([MaybeNull] IList<Double> value, ParameterAttributes synchronize)
			{
				return JavaArray<Double>.CreateArgumentState (value, synchronize, (list, copy) => {
					var a = copy
						? new JavaDoubleArray (list)
						: new JavaDoubleArray (list.Count);
					a.forMarshalCollection = true;
					return a;
				});
			}

			public override void DestroyGenericArgumentState ([AllowNull] IList<Double> value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
			{
				JavaArray<Double>.DestroyArgumentState<JavaDoubleArray> (value, ref state, synchronize);
			}

			public override Expression CreateParameterToManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize = 0, Type? targetType = null)
			{
				Func<IntPtr, Type?, object?>  m = JavaDoubleArray.CreateMarshaledValue;

				var call    = Expression.Call (m.GetMethodInfo (), sourceValue, Expression.Constant (targetType, typeof (Type)));
				return targetType == null
					? (Expression) call
					: Expression.Convert (call, targetType);
			}
		}
	}

}
