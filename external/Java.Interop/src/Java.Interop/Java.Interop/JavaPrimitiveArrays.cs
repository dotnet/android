
using System;
using System.Collections.Generic;

namespace Java.Interop {

	partial class JavaVM {
		static readonly KeyValuePair<Type, JniMarshalInfo>[] JniPrimitiveArrayMarshalers = new []{
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Byte[]), new JniMarshalInfo {
				GetValueFromJni             = JavaBooleanArray.GetValueFromJni,
				CreateLocalRef              = JavaBooleanArray.CreateLocalRef,
				CreateMarshalCollection     = JavaBooleanArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaBooleanArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaArray<Byte>), new JniMarshalInfo {
				GetValueFromJni             = JavaBooleanArray.GetValueFromJni,
				CreateLocalRef              = JavaBooleanArray.CreateLocalRef,
				CreateMarshalCollection     = JavaBooleanArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaBooleanArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaPrimitiveArray<Byte>), new JniMarshalInfo {
				GetValueFromJni             = JavaBooleanArray.GetValueFromJni,
				CreateLocalRef              = JavaBooleanArray.CreateLocalRef,
				CreateMarshalCollection     = JavaBooleanArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaBooleanArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaBooleanArray), new JniMarshalInfo {
				GetValueFromJni             = JavaBooleanArray.GetValueFromJni,
				CreateLocalRef              = JavaBooleanArray.CreateLocalRef,
				CreateMarshalCollection     = JavaBooleanArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaBooleanArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (SByte[]), new JniMarshalInfo {
				GetValueFromJni             = JavaSByteArray.GetValueFromJni,
				CreateLocalRef              = JavaSByteArray.CreateLocalRef,
				CreateMarshalCollection     = JavaSByteArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaSByteArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaArray<SByte>), new JniMarshalInfo {
				GetValueFromJni             = JavaSByteArray.GetValueFromJni,
				CreateLocalRef              = JavaSByteArray.CreateLocalRef,
				CreateMarshalCollection     = JavaSByteArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaSByteArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaPrimitiveArray<SByte>), new JniMarshalInfo {
				GetValueFromJni             = JavaSByteArray.GetValueFromJni,
				CreateLocalRef              = JavaSByteArray.CreateLocalRef,
				CreateMarshalCollection     = JavaSByteArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaSByteArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaSByteArray), new JniMarshalInfo {
				GetValueFromJni             = JavaSByteArray.GetValueFromJni,
				CreateLocalRef              = JavaSByteArray.CreateLocalRef,
				CreateMarshalCollection     = JavaSByteArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaSByteArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Char[]), new JniMarshalInfo {
				GetValueFromJni             = JavaCharArray.GetValueFromJni,
				CreateLocalRef              = JavaCharArray.CreateLocalRef,
				CreateMarshalCollection     = JavaCharArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaCharArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaArray<Char>), new JniMarshalInfo {
				GetValueFromJni             = JavaCharArray.GetValueFromJni,
				CreateLocalRef              = JavaCharArray.CreateLocalRef,
				CreateMarshalCollection     = JavaCharArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaCharArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaPrimitiveArray<Char>), new JniMarshalInfo {
				GetValueFromJni             = JavaCharArray.GetValueFromJni,
				CreateLocalRef              = JavaCharArray.CreateLocalRef,
				CreateMarshalCollection     = JavaCharArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaCharArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaCharArray), new JniMarshalInfo {
				GetValueFromJni             = JavaCharArray.GetValueFromJni,
				CreateLocalRef              = JavaCharArray.CreateLocalRef,
				CreateMarshalCollection     = JavaCharArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaCharArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Int16[]), new JniMarshalInfo {
				GetValueFromJni             = JavaInt16Array.GetValueFromJni,
				CreateLocalRef              = JavaInt16Array.CreateLocalRef,
				CreateMarshalCollection     = JavaInt16Array.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaInt16Array.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaArray<Int16>), new JniMarshalInfo {
				GetValueFromJni             = JavaInt16Array.GetValueFromJni,
				CreateLocalRef              = JavaInt16Array.CreateLocalRef,
				CreateMarshalCollection     = JavaInt16Array.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaInt16Array.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaPrimitiveArray<Int16>), new JniMarshalInfo {
				GetValueFromJni             = JavaInt16Array.GetValueFromJni,
				CreateLocalRef              = JavaInt16Array.CreateLocalRef,
				CreateMarshalCollection     = JavaInt16Array.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaInt16Array.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaInt16Array), new JniMarshalInfo {
				GetValueFromJni             = JavaInt16Array.GetValueFromJni,
				CreateLocalRef              = JavaInt16Array.CreateLocalRef,
				CreateMarshalCollection     = JavaInt16Array.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaInt16Array.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Int32[]), new JniMarshalInfo {
				GetValueFromJni             = JavaInt32Array.GetValueFromJni,
				CreateLocalRef              = JavaInt32Array.CreateLocalRef,
				CreateMarshalCollection     = JavaInt32Array.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaInt32Array.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaArray<Int32>), new JniMarshalInfo {
				GetValueFromJni             = JavaInt32Array.GetValueFromJni,
				CreateLocalRef              = JavaInt32Array.CreateLocalRef,
				CreateMarshalCollection     = JavaInt32Array.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaInt32Array.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaPrimitiveArray<Int32>), new JniMarshalInfo {
				GetValueFromJni             = JavaInt32Array.GetValueFromJni,
				CreateLocalRef              = JavaInt32Array.CreateLocalRef,
				CreateMarshalCollection     = JavaInt32Array.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaInt32Array.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaInt32Array), new JniMarshalInfo {
				GetValueFromJni             = JavaInt32Array.GetValueFromJni,
				CreateLocalRef              = JavaInt32Array.CreateLocalRef,
				CreateMarshalCollection     = JavaInt32Array.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaInt32Array.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Int64[]), new JniMarshalInfo {
				GetValueFromJni             = JavaInt64Array.GetValueFromJni,
				CreateLocalRef              = JavaInt64Array.CreateLocalRef,
				CreateMarshalCollection     = JavaInt64Array.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaInt64Array.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaArray<Int64>), new JniMarshalInfo {
				GetValueFromJni             = JavaInt64Array.GetValueFromJni,
				CreateLocalRef              = JavaInt64Array.CreateLocalRef,
				CreateMarshalCollection     = JavaInt64Array.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaInt64Array.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaPrimitiveArray<Int64>), new JniMarshalInfo {
				GetValueFromJni             = JavaInt64Array.GetValueFromJni,
				CreateLocalRef              = JavaInt64Array.CreateLocalRef,
				CreateMarshalCollection     = JavaInt64Array.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaInt64Array.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaInt64Array), new JniMarshalInfo {
				GetValueFromJni             = JavaInt64Array.GetValueFromJni,
				CreateLocalRef              = JavaInt64Array.CreateLocalRef,
				CreateMarshalCollection     = JavaInt64Array.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaInt64Array.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Single[]), new JniMarshalInfo {
				GetValueFromJni             = JavaSingleArray.GetValueFromJni,
				CreateLocalRef              = JavaSingleArray.CreateLocalRef,
				CreateMarshalCollection     = JavaSingleArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaSingleArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaArray<Single>), new JniMarshalInfo {
				GetValueFromJni             = JavaSingleArray.GetValueFromJni,
				CreateLocalRef              = JavaSingleArray.CreateLocalRef,
				CreateMarshalCollection     = JavaSingleArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaSingleArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaPrimitiveArray<Single>), new JniMarshalInfo {
				GetValueFromJni             = JavaSingleArray.GetValueFromJni,
				CreateLocalRef              = JavaSingleArray.CreateLocalRef,
				CreateMarshalCollection     = JavaSingleArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaSingleArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaSingleArray), new JniMarshalInfo {
				GetValueFromJni             = JavaSingleArray.GetValueFromJni,
				CreateLocalRef              = JavaSingleArray.CreateLocalRef,
				CreateMarshalCollection     = JavaSingleArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaSingleArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Double[]), new JniMarshalInfo {
				GetValueFromJni             = JavaDoubleArray.GetValueFromJni,
				CreateLocalRef              = JavaDoubleArray.CreateLocalRef,
				CreateMarshalCollection     = JavaDoubleArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaDoubleArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaArray<Double>), new JniMarshalInfo {
				GetValueFromJni             = JavaDoubleArray.GetValueFromJni,
				CreateLocalRef              = JavaDoubleArray.CreateLocalRef,
				CreateMarshalCollection     = JavaDoubleArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaDoubleArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaPrimitiveArray<Double>), new JniMarshalInfo {
				GetValueFromJni             = JavaDoubleArray.GetValueFromJni,
				CreateLocalRef              = JavaDoubleArray.CreateLocalRef,
				CreateMarshalCollection     = JavaDoubleArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaDoubleArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaDoubleArray), new JniMarshalInfo {
				GetValueFromJni             = JavaDoubleArray.GetValueFromJni,
				CreateLocalRef              = JavaDoubleArray.CreateLocalRef,
				CreateMarshalCollection     = JavaDoubleArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaDoubleArray.CleanupMarshalCollection,
			}),
		};
	}
	public sealed class JniBooleanArrayElements : JniArrayElements {

		JniReferenceSafeHandle arrayHandle;

		internal JniBooleanArrayElements (JniReferenceSafeHandle arrayHandle, IntPtr elements)
			: base (elements)
		{
			this.arrayHandle = arrayHandle;
		}

		public new unsafe Byte* Elements {
			get {return (Byte*) base.Elements;}
		}

		protected override void Synchronize (JniArrayElementsReleaseMode releaseMode)
		{
			JniEnvironment.Arrays.ReleaseBooleanArrayElements (arrayHandle, base.Elements, (int) releaseMode);
		}
	}

	[JniTypeInfo ("Z", ArrayRank=1, TypeIsKeyword=true)]
	public sealed partial class JavaBooleanArray : JavaPrimitiveArray<Byte> {

		public JavaBooleanArray (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public JavaBooleanArray (int length)
			: base (JniEnvironment.Arrays.NewBooleanArray (CheckLength (length)), JniHandleOwnership.Transfer)
		{
		}

		public JavaBooleanArray (System.Collections.Generic.IList<Byte> value)
			: this (CheckLength (value))
		{
			CopyFrom (_ToArray (value), 0, 0, value.Count);
		}

		public JavaBooleanArray (System.Collections.Generic.IEnumerable<Byte> value)
			: this (_ToArray (value))
		{
		}

		protected override JniArrayElements CreateElements ()
		{
			return GetElements ();
		}

		public new JniBooleanArrayElements GetElements ()
		{
			IntPtr elements = JniEnvironment.Arrays.GetBooleanArrayElements (SafeHandle, IntPtr.Zero);
			return new JniBooleanArrayElements (SafeHandle, elements);
		}

		public override unsafe int IndexOf (Byte item)
		{
			int len = Length;
			using (var e = GetElements ()) {
				for (int i = 0; i < len; ++i) {
					if (e.Elements [i] == item)
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
					e.Elements [i] = default (Byte);
				}
			}
		}

		public override unsafe void CopyTo (int sourceIndex, Byte[] destinationArray, int destinationIndex, int length)
		{
			if (destinationArray == null)
				throw new ArgumentNullException ("destinationArray");
			CheckArrayCopy (sourceIndex, Length, destinationIndex, destinationArray.Length, length);
			if (destinationArray.Length == 0)
				return;

			fixed (Byte* b = destinationArray)
				JniEnvironment.Arrays.GetBooleanArrayRegion (SafeHandle, sourceIndex, length, (IntPtr) (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Byte[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException ("sourceArray");
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Byte* b = sourceArray)
				JniEnvironment.Arrays.SetBooleanArrayRegion (SafeHandle, destinationIndex, length, (IntPtr) (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Byte>) == targetType ||
				typeof (JavaBooleanArray) == targetType;
		}

		internal static JniLocalReference CreateLocalRef (object value)
		{
		    return JavaArray<Byte>.CreateLocalRef<JavaBooleanArray> (
		            value,
		            list => new JavaBooleanArray (list));
		}

		internal static IList<Byte> GetValueFromJni (JniReferenceSafeHandle handle, JniHandleOwnership transfer, Type targetType)
		{
		    return JavaArray<Byte>.GetValueFromJni (
		            handle,
		            transfer,
		            targetType,
		            (h, t) => new JavaBooleanArray (h, t));
		}

		internal static IJavaObject CreateMarshalCollection (object value)
		{
		    return JavaArray<Byte>.CreateMarshalCollection (value, list => new JavaBooleanArray (list) {
		        forMarshalCollection = true,
		    });
		}

		internal static void CleanupMarshalCollection (IJavaObject marshalObject, object value)
		{
		    JavaArray<Byte>.CleanupMarshalCollection<JavaBooleanArray> (marshalObject, value);
		}
	}

	public sealed class JniSByteArrayElements : JniArrayElements {

		JniReferenceSafeHandle arrayHandle;

		internal JniSByteArrayElements (JniReferenceSafeHandle arrayHandle, IntPtr elements)
			: base (elements)
		{
			this.arrayHandle = arrayHandle;
		}

		public new unsafe SByte* Elements {
			get {return (SByte*) base.Elements;}
		}

		protected override void Synchronize (JniArrayElementsReleaseMode releaseMode)
		{
			JniEnvironment.Arrays.ReleaseByteArrayElements (arrayHandle, base.Elements, (int) releaseMode);
		}
	}

	[JniTypeInfo ("B", ArrayRank=1, TypeIsKeyword=true)]
	public sealed partial class JavaSByteArray : JavaPrimitiveArray<SByte> {

		public JavaSByteArray (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public JavaSByteArray (int length)
			: base (JniEnvironment.Arrays.NewByteArray (CheckLength (length)), JniHandleOwnership.Transfer)
		{
		}

		public JavaSByteArray (System.Collections.Generic.IList<SByte> value)
			: this (CheckLength (value))
		{
			CopyFrom (_ToArray (value), 0, 0, value.Count);
		}

		public JavaSByteArray (System.Collections.Generic.IEnumerable<SByte> value)
			: this (_ToArray (value))
		{
		}

		protected override JniArrayElements CreateElements ()
		{
			return GetElements ();
		}

		public new JniSByteArrayElements GetElements ()
		{
			IntPtr elements = JniEnvironment.Arrays.GetByteArrayElements (SafeHandle, IntPtr.Zero);
			return new JniSByteArrayElements (SafeHandle, elements);
		}

		public override unsafe int IndexOf (SByte item)
		{
			int len = Length;
			using (var e = GetElements ()) {
				for (int i = 0; i < len; ++i) {
					if (e.Elements [i] == item)
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
					e.Elements [i] = default (SByte);
				}
			}
		}

		public override unsafe void CopyTo (int sourceIndex, SByte[] destinationArray, int destinationIndex, int length)
		{
			if (destinationArray == null)
				throw new ArgumentNullException ("destinationArray");
			CheckArrayCopy (sourceIndex, Length, destinationIndex, destinationArray.Length, length);
			if (destinationArray.Length == 0)
				return;

			fixed (SByte* b = destinationArray)
				JniEnvironment.Arrays.GetByteArrayRegion (SafeHandle, sourceIndex, length, (IntPtr) (b+destinationIndex));
		}

		public override unsafe void CopyFrom (SByte[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException ("sourceArray");
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (SByte* b = sourceArray)
				JniEnvironment.Arrays.SetByteArrayRegion (SafeHandle, destinationIndex, length, (IntPtr) (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<SByte>) == targetType ||
				typeof (JavaSByteArray) == targetType;
		}

		internal static JniLocalReference CreateLocalRef (object value)
		{
		    return JavaArray<SByte>.CreateLocalRef<JavaSByteArray> (
		            value,
		            list => new JavaSByteArray (list));
		}

		internal static IList<SByte> GetValueFromJni (JniReferenceSafeHandle handle, JniHandleOwnership transfer, Type targetType)
		{
		    return JavaArray<SByte>.GetValueFromJni (
		            handle,
		            transfer,
		            targetType,
		            (h, t) => new JavaSByteArray (h, t));
		}

		internal static IJavaObject CreateMarshalCollection (object value)
		{
		    return JavaArray<SByte>.CreateMarshalCollection (value, list => new JavaSByteArray (list) {
		        forMarshalCollection = true,
		    });
		}

		internal static void CleanupMarshalCollection (IJavaObject marshalObject, object value)
		{
		    JavaArray<SByte>.CleanupMarshalCollection<JavaSByteArray> (marshalObject, value);
		}
	}

	public sealed class JniCharArrayElements : JniArrayElements {

		JniReferenceSafeHandle arrayHandle;

		internal JniCharArrayElements (JniReferenceSafeHandle arrayHandle, IntPtr elements)
			: base (elements)
		{
			this.arrayHandle = arrayHandle;
		}

		public new unsafe Char* Elements {
			get {return (Char*) base.Elements;}
		}

		protected override void Synchronize (JniArrayElementsReleaseMode releaseMode)
		{
			JniEnvironment.Arrays.ReleaseCharArrayElements (arrayHandle, base.Elements, (int) releaseMode);
		}
	}

	[JniTypeInfo ("C", ArrayRank=1, TypeIsKeyword=true)]
	public sealed partial class JavaCharArray : JavaPrimitiveArray<Char> {

		public JavaCharArray (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public JavaCharArray (int length)
			: base (JniEnvironment.Arrays.NewCharArray (CheckLength (length)), JniHandleOwnership.Transfer)
		{
		}

		public JavaCharArray (System.Collections.Generic.IList<Char> value)
			: this (CheckLength (value))
		{
			CopyFrom (_ToArray (value), 0, 0, value.Count);
		}

		public JavaCharArray (System.Collections.Generic.IEnumerable<Char> value)
			: this (_ToArray (value))
		{
		}

		protected override JniArrayElements CreateElements ()
		{
			return GetElements ();
		}

		public new JniCharArrayElements GetElements ()
		{
			IntPtr elements = JniEnvironment.Arrays.GetCharArrayElements (SafeHandle, IntPtr.Zero);
			return new JniCharArrayElements (SafeHandle, elements);
		}

		public override unsafe int IndexOf (Char item)
		{
			int len = Length;
			using (var e = GetElements ()) {
				for (int i = 0; i < len; ++i) {
					if (e.Elements [i] == item)
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
					e.Elements [i] = default (Char);
				}
			}
		}

		public override unsafe void CopyTo (int sourceIndex, Char[] destinationArray, int destinationIndex, int length)
		{
			if (destinationArray == null)
				throw new ArgumentNullException ("destinationArray");
			CheckArrayCopy (sourceIndex, Length, destinationIndex, destinationArray.Length, length);
			if (destinationArray.Length == 0)
				return;

			fixed (Char* b = destinationArray)
				JniEnvironment.Arrays.GetCharArrayRegion (SafeHandle, sourceIndex, length, (IntPtr) (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Char[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException ("sourceArray");
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Char* b = sourceArray)
				JniEnvironment.Arrays.SetCharArrayRegion (SafeHandle, destinationIndex, length, (IntPtr) (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Char>) == targetType ||
				typeof (JavaCharArray) == targetType;
		}

		internal static JniLocalReference CreateLocalRef (object value)
		{
		    return JavaArray<Char>.CreateLocalRef<JavaCharArray> (
		            value,
		            list => new JavaCharArray (list));
		}

		internal static IList<Char> GetValueFromJni (JniReferenceSafeHandle handle, JniHandleOwnership transfer, Type targetType)
		{
		    return JavaArray<Char>.GetValueFromJni (
		            handle,
		            transfer,
		            targetType,
		            (h, t) => new JavaCharArray (h, t));
		}

		internal static IJavaObject CreateMarshalCollection (object value)
		{
		    return JavaArray<Char>.CreateMarshalCollection (value, list => new JavaCharArray (list) {
		        forMarshalCollection = true,
		    });
		}

		internal static void CleanupMarshalCollection (IJavaObject marshalObject, object value)
		{
		    JavaArray<Char>.CleanupMarshalCollection<JavaCharArray> (marshalObject, value);
		}
	}

	public sealed class JniInt16ArrayElements : JniArrayElements {

		JniReferenceSafeHandle arrayHandle;

		internal JniInt16ArrayElements (JniReferenceSafeHandle arrayHandle, IntPtr elements)
			: base (elements)
		{
			this.arrayHandle = arrayHandle;
		}

		public new unsafe Int16* Elements {
			get {return (Int16*) base.Elements;}
		}

		protected override void Synchronize (JniArrayElementsReleaseMode releaseMode)
		{
			JniEnvironment.Arrays.ReleaseShortArrayElements (arrayHandle, base.Elements, (int) releaseMode);
		}
	}

	[JniTypeInfo ("S", ArrayRank=1, TypeIsKeyword=true)]
	public sealed partial class JavaInt16Array : JavaPrimitiveArray<Int16> {

		public JavaInt16Array (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public JavaInt16Array (int length)
			: base (JniEnvironment.Arrays.NewShortArray (CheckLength (length)), JniHandleOwnership.Transfer)
		{
		}

		public JavaInt16Array (System.Collections.Generic.IList<Int16> value)
			: this (CheckLength (value))
		{
			CopyFrom (_ToArray (value), 0, 0, value.Count);
		}

		public JavaInt16Array (System.Collections.Generic.IEnumerable<Int16> value)
			: this (_ToArray (value))
		{
		}

		protected override JniArrayElements CreateElements ()
		{
			return GetElements ();
		}

		public new JniInt16ArrayElements GetElements ()
		{
			IntPtr elements = JniEnvironment.Arrays.GetShortArrayElements (SafeHandle, IntPtr.Zero);
			return new JniInt16ArrayElements (SafeHandle, elements);
		}

		public override unsafe int IndexOf (Int16 item)
		{
			int len = Length;
			using (var e = GetElements ()) {
				for (int i = 0; i < len; ++i) {
					if (e.Elements [i] == item)
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
					e.Elements [i] = default (Int16);
				}
			}
		}

		public override unsafe void CopyTo (int sourceIndex, Int16[] destinationArray, int destinationIndex, int length)
		{
			if (destinationArray == null)
				throw new ArgumentNullException ("destinationArray");
			CheckArrayCopy (sourceIndex, Length, destinationIndex, destinationArray.Length, length);
			if (destinationArray.Length == 0)
				return;

			fixed (Int16* b = destinationArray)
				JniEnvironment.Arrays.GetShortArrayRegion (SafeHandle, sourceIndex, length, (IntPtr) (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Int16[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException ("sourceArray");
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Int16* b = sourceArray)
				JniEnvironment.Arrays.SetShortArrayRegion (SafeHandle, destinationIndex, length, (IntPtr) (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Int16>) == targetType ||
				typeof (JavaInt16Array) == targetType;
		}

		internal static JniLocalReference CreateLocalRef (object value)
		{
		    return JavaArray<Int16>.CreateLocalRef<JavaInt16Array> (
		            value,
		            list => new JavaInt16Array (list));
		}

		internal static IList<Int16> GetValueFromJni (JniReferenceSafeHandle handle, JniHandleOwnership transfer, Type targetType)
		{
		    return JavaArray<Int16>.GetValueFromJni (
		            handle,
		            transfer,
		            targetType,
		            (h, t) => new JavaInt16Array (h, t));
		}

		internal static IJavaObject CreateMarshalCollection (object value)
		{
		    return JavaArray<Int16>.CreateMarshalCollection (value, list => new JavaInt16Array (list) {
		        forMarshalCollection = true,
		    });
		}

		internal static void CleanupMarshalCollection (IJavaObject marshalObject, object value)
		{
		    JavaArray<Int16>.CleanupMarshalCollection<JavaInt16Array> (marshalObject, value);
		}
	}

	public sealed class JniInt32ArrayElements : JniArrayElements {

		JniReferenceSafeHandle arrayHandle;

		internal JniInt32ArrayElements (JniReferenceSafeHandle arrayHandle, IntPtr elements)
			: base (elements)
		{
			this.arrayHandle = arrayHandle;
		}

		public new unsafe Int32* Elements {
			get {return (Int32*) base.Elements;}
		}

		protected override void Synchronize (JniArrayElementsReleaseMode releaseMode)
		{
			JniEnvironment.Arrays.ReleaseIntArrayElements (arrayHandle, base.Elements, (int) releaseMode);
		}
	}

	[JniTypeInfo ("I", ArrayRank=1, TypeIsKeyword=true)]
	public sealed partial class JavaInt32Array : JavaPrimitiveArray<Int32> {

		public JavaInt32Array (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public JavaInt32Array (int length)
			: base (JniEnvironment.Arrays.NewIntArray (CheckLength (length)), JniHandleOwnership.Transfer)
		{
		}

		public JavaInt32Array (System.Collections.Generic.IList<Int32> value)
			: this (CheckLength (value))
		{
			CopyFrom (_ToArray (value), 0, 0, value.Count);
		}

		public JavaInt32Array (System.Collections.Generic.IEnumerable<Int32> value)
			: this (_ToArray (value))
		{
		}

		protected override JniArrayElements CreateElements ()
		{
			return GetElements ();
		}

		public new JniInt32ArrayElements GetElements ()
		{
			IntPtr elements = JniEnvironment.Arrays.GetIntArrayElements (SafeHandle, IntPtr.Zero);
			return new JniInt32ArrayElements (SafeHandle, elements);
		}

		public override unsafe int IndexOf (Int32 item)
		{
			int len = Length;
			using (var e = GetElements ()) {
				for (int i = 0; i < len; ++i) {
					if (e.Elements [i] == item)
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
					e.Elements [i] = default (Int32);
				}
			}
		}

		public override unsafe void CopyTo (int sourceIndex, Int32[] destinationArray, int destinationIndex, int length)
		{
			if (destinationArray == null)
				throw new ArgumentNullException ("destinationArray");
			CheckArrayCopy (sourceIndex, Length, destinationIndex, destinationArray.Length, length);
			if (destinationArray.Length == 0)
				return;

			fixed (Int32* b = destinationArray)
				JniEnvironment.Arrays.GetIntArrayRegion (SafeHandle, sourceIndex, length, (IntPtr) (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Int32[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException ("sourceArray");
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Int32* b = sourceArray)
				JniEnvironment.Arrays.SetIntArrayRegion (SafeHandle, destinationIndex, length, (IntPtr) (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Int32>) == targetType ||
				typeof (JavaInt32Array) == targetType;
		}

		internal static JniLocalReference CreateLocalRef (object value)
		{
		    return JavaArray<Int32>.CreateLocalRef<JavaInt32Array> (
		            value,
		            list => new JavaInt32Array (list));
		}

		internal static IList<Int32> GetValueFromJni (JniReferenceSafeHandle handle, JniHandleOwnership transfer, Type targetType)
		{
		    return JavaArray<Int32>.GetValueFromJni (
		            handle,
		            transfer,
		            targetType,
		            (h, t) => new JavaInt32Array (h, t));
		}

		internal static IJavaObject CreateMarshalCollection (object value)
		{
		    return JavaArray<Int32>.CreateMarshalCollection (value, list => new JavaInt32Array (list) {
		        forMarshalCollection = true,
		    });
		}

		internal static void CleanupMarshalCollection (IJavaObject marshalObject, object value)
		{
		    JavaArray<Int32>.CleanupMarshalCollection<JavaInt32Array> (marshalObject, value);
		}
	}

	public sealed class JniInt64ArrayElements : JniArrayElements {

		JniReferenceSafeHandle arrayHandle;

		internal JniInt64ArrayElements (JniReferenceSafeHandle arrayHandle, IntPtr elements)
			: base (elements)
		{
			this.arrayHandle = arrayHandle;
		}

		public new unsafe Int64* Elements {
			get {return (Int64*) base.Elements;}
		}

		protected override void Synchronize (JniArrayElementsReleaseMode releaseMode)
		{
			JniEnvironment.Arrays.ReleaseLongArrayElements (arrayHandle, base.Elements, (int) releaseMode);
		}
	}

	[JniTypeInfo ("J", ArrayRank=1, TypeIsKeyword=true)]
	public sealed partial class JavaInt64Array : JavaPrimitiveArray<Int64> {

		public JavaInt64Array (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public JavaInt64Array (int length)
			: base (JniEnvironment.Arrays.NewLongArray (CheckLength (length)), JniHandleOwnership.Transfer)
		{
		}

		public JavaInt64Array (System.Collections.Generic.IList<Int64> value)
			: this (CheckLength (value))
		{
			CopyFrom (_ToArray (value), 0, 0, value.Count);
		}

		public JavaInt64Array (System.Collections.Generic.IEnumerable<Int64> value)
			: this (_ToArray (value))
		{
		}

		protected override JniArrayElements CreateElements ()
		{
			return GetElements ();
		}

		public new JniInt64ArrayElements GetElements ()
		{
			IntPtr elements = JniEnvironment.Arrays.GetLongArrayElements (SafeHandle, IntPtr.Zero);
			return new JniInt64ArrayElements (SafeHandle, elements);
		}

		public override unsafe int IndexOf (Int64 item)
		{
			int len = Length;
			using (var e = GetElements ()) {
				for (int i = 0; i < len; ++i) {
					if (e.Elements [i] == item)
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
					e.Elements [i] = default (Int64);
				}
			}
		}

		public override unsafe void CopyTo (int sourceIndex, Int64[] destinationArray, int destinationIndex, int length)
		{
			if (destinationArray == null)
				throw new ArgumentNullException ("destinationArray");
			CheckArrayCopy (sourceIndex, Length, destinationIndex, destinationArray.Length, length);
			if (destinationArray.Length == 0)
				return;

			fixed (Int64* b = destinationArray)
				JniEnvironment.Arrays.GetLongArrayRegion (SafeHandle, sourceIndex, length, (IntPtr) (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Int64[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException ("sourceArray");
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Int64* b = sourceArray)
				JniEnvironment.Arrays.SetLongArrayRegion (SafeHandle, destinationIndex, length, (IntPtr) (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Int64>) == targetType ||
				typeof (JavaInt64Array) == targetType;
		}

		internal static JniLocalReference CreateLocalRef (object value)
		{
		    return JavaArray<Int64>.CreateLocalRef<JavaInt64Array> (
		            value,
		            list => new JavaInt64Array (list));
		}

		internal static IList<Int64> GetValueFromJni (JniReferenceSafeHandle handle, JniHandleOwnership transfer, Type targetType)
		{
		    return JavaArray<Int64>.GetValueFromJni (
		            handle,
		            transfer,
		            targetType,
		            (h, t) => new JavaInt64Array (h, t));
		}

		internal static IJavaObject CreateMarshalCollection (object value)
		{
		    return JavaArray<Int64>.CreateMarshalCollection (value, list => new JavaInt64Array (list) {
		        forMarshalCollection = true,
		    });
		}

		internal static void CleanupMarshalCollection (IJavaObject marshalObject, object value)
		{
		    JavaArray<Int64>.CleanupMarshalCollection<JavaInt64Array> (marshalObject, value);
		}
	}

	public sealed class JniSingleArrayElements : JniArrayElements {

		JniReferenceSafeHandle arrayHandle;

		internal JniSingleArrayElements (JniReferenceSafeHandle arrayHandle, IntPtr elements)
			: base (elements)
		{
			this.arrayHandle = arrayHandle;
		}

		public new unsafe Single* Elements {
			get {return (Single*) base.Elements;}
		}

		protected override void Synchronize (JniArrayElementsReleaseMode releaseMode)
		{
			JniEnvironment.Arrays.ReleaseFloatArrayElements (arrayHandle, base.Elements, (int) releaseMode);
		}
	}

	[JniTypeInfo ("F", ArrayRank=1, TypeIsKeyword=true)]
	public sealed partial class JavaSingleArray : JavaPrimitiveArray<Single> {

		public JavaSingleArray (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public JavaSingleArray (int length)
			: base (JniEnvironment.Arrays.NewFloatArray (CheckLength (length)), JniHandleOwnership.Transfer)
		{
		}

		public JavaSingleArray (System.Collections.Generic.IList<Single> value)
			: this (CheckLength (value))
		{
			CopyFrom (_ToArray (value), 0, 0, value.Count);
		}

		public JavaSingleArray (System.Collections.Generic.IEnumerable<Single> value)
			: this (_ToArray (value))
		{
		}

		protected override JniArrayElements CreateElements ()
		{
			return GetElements ();
		}

		public new JniSingleArrayElements GetElements ()
		{
			IntPtr elements = JniEnvironment.Arrays.GetFloatArrayElements (SafeHandle, IntPtr.Zero);
			return new JniSingleArrayElements (SafeHandle, elements);
		}

		public override unsafe int IndexOf (Single item)
		{
			int len = Length;
			using (var e = GetElements ()) {
				for (int i = 0; i < len; ++i) {
					if (e.Elements [i] == item)
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
					e.Elements [i] = default (Single);
				}
			}
		}

		public override unsafe void CopyTo (int sourceIndex, Single[] destinationArray, int destinationIndex, int length)
		{
			if (destinationArray == null)
				throw new ArgumentNullException ("destinationArray");
			CheckArrayCopy (sourceIndex, Length, destinationIndex, destinationArray.Length, length);
			if (destinationArray.Length == 0)
				return;

			fixed (Single* b = destinationArray)
				JniEnvironment.Arrays.GetFloatArrayRegion (SafeHandle, sourceIndex, length, (IntPtr) (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Single[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException ("sourceArray");
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Single* b = sourceArray)
				JniEnvironment.Arrays.SetFloatArrayRegion (SafeHandle, destinationIndex, length, (IntPtr) (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Single>) == targetType ||
				typeof (JavaSingleArray) == targetType;
		}

		internal static JniLocalReference CreateLocalRef (object value)
		{
		    return JavaArray<Single>.CreateLocalRef<JavaSingleArray> (
		            value,
		            list => new JavaSingleArray (list));
		}

		internal static IList<Single> GetValueFromJni (JniReferenceSafeHandle handle, JniHandleOwnership transfer, Type targetType)
		{
		    return JavaArray<Single>.GetValueFromJni (
		            handle,
		            transfer,
		            targetType,
		            (h, t) => new JavaSingleArray (h, t));
		}

		internal static IJavaObject CreateMarshalCollection (object value)
		{
		    return JavaArray<Single>.CreateMarshalCollection (value, list => new JavaSingleArray (list) {
		        forMarshalCollection = true,
		    });
		}

		internal static void CleanupMarshalCollection (IJavaObject marshalObject, object value)
		{
		    JavaArray<Single>.CleanupMarshalCollection<JavaSingleArray> (marshalObject, value);
		}
	}

	public sealed class JniDoubleArrayElements : JniArrayElements {

		JniReferenceSafeHandle arrayHandle;

		internal JniDoubleArrayElements (JniReferenceSafeHandle arrayHandle, IntPtr elements)
			: base (elements)
		{
			this.arrayHandle = arrayHandle;
		}

		public new unsafe Double* Elements {
			get {return (Double*) base.Elements;}
		}

		protected override void Synchronize (JniArrayElementsReleaseMode releaseMode)
		{
			JniEnvironment.Arrays.ReleaseDoubleArrayElements (arrayHandle, base.Elements, (int) releaseMode);
		}
	}

	[JniTypeInfo ("D", ArrayRank=1, TypeIsKeyword=true)]
	public sealed partial class JavaDoubleArray : JavaPrimitiveArray<Double> {

		public JavaDoubleArray (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public JavaDoubleArray (int length)
			: base (JniEnvironment.Arrays.NewDoubleArray (CheckLength (length)), JniHandleOwnership.Transfer)
		{
		}

		public JavaDoubleArray (System.Collections.Generic.IList<Double> value)
			: this (CheckLength (value))
		{
			CopyFrom (_ToArray (value), 0, 0, value.Count);
		}

		public JavaDoubleArray (System.Collections.Generic.IEnumerable<Double> value)
			: this (_ToArray (value))
		{
		}

		protected override JniArrayElements CreateElements ()
		{
			return GetElements ();
		}

		public new JniDoubleArrayElements GetElements ()
		{
			IntPtr elements = JniEnvironment.Arrays.GetDoubleArrayElements (SafeHandle, IntPtr.Zero);
			return new JniDoubleArrayElements (SafeHandle, elements);
		}

		public override unsafe int IndexOf (Double item)
		{
			int len = Length;
			using (var e = GetElements ()) {
				for (int i = 0; i < len; ++i) {
					if (e.Elements [i] == item)
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
					e.Elements [i] = default (Double);
				}
			}
		}

		public override unsafe void CopyTo (int sourceIndex, Double[] destinationArray, int destinationIndex, int length)
		{
			if (destinationArray == null)
				throw new ArgumentNullException ("destinationArray");
			CheckArrayCopy (sourceIndex, Length, destinationIndex, destinationArray.Length, length);
			if (destinationArray.Length == 0)
				return;

			fixed (Double* b = destinationArray)
				JniEnvironment.Arrays.GetDoubleArrayRegion (SafeHandle, sourceIndex, length, (IntPtr) (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Double[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException ("sourceArray");
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Double* b = sourceArray)
				JniEnvironment.Arrays.SetDoubleArrayRegion (SafeHandle, destinationIndex, length, (IntPtr) (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Double>) == targetType ||
				typeof (JavaDoubleArray) == targetType;
		}

		internal static JniLocalReference CreateLocalRef (object value)
		{
		    return JavaArray<Double>.CreateLocalRef<JavaDoubleArray> (
		            value,
		            list => new JavaDoubleArray (list));
		}

		internal static IList<Double> GetValueFromJni (JniReferenceSafeHandle handle, JniHandleOwnership transfer, Type targetType)
		{
		    return JavaArray<Double>.GetValueFromJni (
		            handle,
		            transfer,
		            targetType,
		            (h, t) => new JavaDoubleArray (h, t));
		}

		internal static IJavaObject CreateMarshalCollection (object value)
		{
		    return JavaArray<Double>.CreateMarshalCollection (value, list => new JavaDoubleArray (list) {
		        forMarshalCollection = true,
		    });
		}

		internal static void CleanupMarshalCollection (IJavaObject marshalObject, object value)
		{
		    JavaArray<Double>.CleanupMarshalCollection<JavaDoubleArray> (marshalObject, value);
		}
	}

}