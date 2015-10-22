
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Java.Interop {

	partial class JavaVM {
		static readonly KeyValuePair<Type, JniTypeInfo>[] JniBuiltinArrayMappings = new[]{
			new KeyValuePair<Type, JniTypeInfo>(typeof (JavaPrimitiveArray<Boolean>),   new JniTypeInfo ("Z", true, 1)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (JavaArray<Boolean>),            new JniTypeInfo ("Z", true, 1)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (JavaPrimitiveArray<SByte>),   new JniTypeInfo ("B", true, 1)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (JavaArray<SByte>),            new JniTypeInfo ("B", true, 1)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (JavaPrimitiveArray<Char>),   new JniTypeInfo ("C", true, 1)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (JavaArray<Char>),            new JniTypeInfo ("C", true, 1)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (JavaPrimitiveArray<Int16>),   new JniTypeInfo ("S", true, 1)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (JavaArray<Int16>),            new JniTypeInfo ("S", true, 1)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (JavaPrimitiveArray<Int32>),   new JniTypeInfo ("I", true, 1)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (JavaArray<Int32>),            new JniTypeInfo ("I", true, 1)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (JavaPrimitiveArray<Int64>),   new JniTypeInfo ("J", true, 1)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (JavaArray<Int64>),            new JniTypeInfo ("J", true, 1)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (JavaPrimitiveArray<Single>),   new JniTypeInfo ("F", true, 1)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (JavaArray<Single>),            new JniTypeInfo ("F", true, 1)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (JavaPrimitiveArray<Double>),   new JniTypeInfo ("D", true, 1)),
			new KeyValuePair<Type, JniTypeInfo>(typeof (JavaArray<Double>),            new JniTypeInfo ("D", true, 1)),
		};

		static readonly KeyValuePair<Type, JniMarshalInfo>[] JniPrimitiveArrayMarshalers = new []{
			new KeyValuePair<Type, JniMarshalInfo>(typeof (Boolean[]), new JniMarshalInfo {
				GetValueFromJni             = JavaBooleanArray.GetValueFromJni,
				CreateLocalRef              = JavaBooleanArray.CreateLocalRef,
				CreateMarshalCollection     = JavaBooleanArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaBooleanArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaArray<Boolean>), new JniMarshalInfo {
				GetValueFromJni             = JavaBooleanArray.GetValueFromJni,
				CreateLocalRef              = JavaBooleanArray.CreateLocalRef,
				CreateMarshalCollection     = JavaBooleanArray.CreateMarshalCollection,
				CleanupMarshalCollection    = JavaBooleanArray.CleanupMarshalCollection,
			}),
			new KeyValuePair<Type, JniMarshalInfo>(typeof (JavaPrimitiveArray<Boolean>), new JniMarshalInfo {
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

		JniObjectReference      arrayHandle;

		internal JniBooleanArrayElements (JniObjectReference arrayHandle, IntPtr elements)
			: base (elements)
		{
			this.arrayHandle = arrayHandle;
		}

		public new unsafe Boolean* Elements {
			get {return (Boolean*) base.Elements;}
		}

		protected override void Synchronize (JniArrayElementsReleaseMode releaseMode)
		{
			JniEnvironment.Arrays.ReleaseBooleanArrayElements (arrayHandle, base.Elements, (int) releaseMode);
		}
	}

	[JniTypeInfo ("Z", ArrayRank=1, TypeIsKeyword=true)]
	public sealed partial class JavaBooleanArray : JavaPrimitiveArray<Boolean> {

		public JavaBooleanArray (ref JniObjectReference handle, JniHandleOwnership transfer)
			: base (ref handle, transfer)
		{
		}

		public unsafe JavaBooleanArray (int length)
			: base (ref *InvalidJniObjectReference, JniHandleOwnership.Invalid)
		{
		    var peer    = JniEnvironment.Arrays.NewBooleanArray (CheckLength (length));
		    using (SetPeerReference (ref peer, JniHandleOwnership.Transfer)) {
		    }
		}

		public JavaBooleanArray (System.Collections.Generic.IList<Boolean> value)
			: this (CheckLength (value))
		{
			CopyFrom (_ToArray (value), 0, 0, value.Count);
		}

		public JavaBooleanArray (System.Collections.Generic.IEnumerable<Boolean> value)
			: this (_ToArray (value))
		{
		}

		protected override JniArrayElements CreateElements ()
		{
			return GetElements ();
		}

		public new JniBooleanArrayElements GetElements ()
		{
			IntPtr elements = JniEnvironment.Arrays.GetBooleanArrayElements (PeerReference, IntPtr.Zero);
			return elements == IntPtr.Zero ? null : new JniBooleanArrayElements (PeerReference, elements);
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
					e.Elements [i] = default (Boolean);
				}
			}
		}

		public override unsafe void CopyTo (int sourceIndex, Boolean[] destinationArray, int destinationIndex, int length)
		{
			if (destinationArray == null)
				throw new ArgumentNullException ("destinationArray");
			CheckArrayCopy (sourceIndex, Length, destinationIndex, destinationArray.Length, length);
			if (destinationArray.Length == 0)
				return;

			fixed (Boolean* b = destinationArray)
				JniEnvironment.Arrays.GetBooleanArrayRegion (PeerReference, sourceIndex, length, (IntPtr) (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Boolean[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException ("sourceArray");
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Boolean* b = sourceArray)
				JniEnvironment.Arrays.SetBooleanArrayRegion (PeerReference, destinationIndex, length, (IntPtr) (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Boolean>) == targetType ||
				typeof (JavaBooleanArray) == targetType;
		}

		internal static JniObjectReference CreateLocalRef (object value)
		{
		    return JavaArray<Boolean>.CreateLocalRef<JavaBooleanArray> (
		            value,
		            list => new JavaBooleanArray (list));
		}

		internal static IList<Boolean> GetValueFromJni (ref JniObjectReference reference, JniHandleOwnership transfer, Type targetType)
		{
		    return JavaArray<Boolean>.GetValueFromJni (
		            ref reference,
		            transfer,
		            targetType,
		            (ref JniObjectReference h, JniHandleOwnership t) => new JavaBooleanArray (ref h, t));
		}

		internal static IJavaPeerable CreateMarshalCollection (object value)
		{
		    return JavaArray<Boolean>.CreateMarshalCollection (value, list => new JavaBooleanArray (list) {
		        forMarshalCollection = true,
		    });
		}

		internal static void CleanupMarshalCollection (IJavaPeerable marshalObject, object value)
		{
		    JavaArray<Boolean>.CleanupMarshalCollection<JavaBooleanArray> (marshalObject, value);
		}
	}

	public sealed class JniSByteArrayElements : JniArrayElements {

		JniObjectReference      arrayHandle;

		internal JniSByteArrayElements (JniObjectReference arrayHandle, IntPtr elements)
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

		public JavaSByteArray (ref JniObjectReference handle, JniHandleOwnership transfer)
			: base (ref handle, transfer)
		{
		}

		public unsafe JavaSByteArray (int length)
			: base (ref *InvalidJniObjectReference, JniHandleOwnership.Invalid)
		{
		    var peer    = JniEnvironment.Arrays.NewByteArray (CheckLength (length));
		    using (SetPeerReference (ref peer, JniHandleOwnership.Transfer)) {
		    }
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
			IntPtr elements = JniEnvironment.Arrays.GetByteArrayElements (PeerReference, IntPtr.Zero);
			return elements == IntPtr.Zero ? null : new JniSByteArrayElements (PeerReference, elements);
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
				JniEnvironment.Arrays.GetByteArrayRegion (PeerReference, sourceIndex, length, (IntPtr) (b+destinationIndex));
		}

		public override unsafe void CopyFrom (SByte[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException ("sourceArray");
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (SByte* b = sourceArray)
				JniEnvironment.Arrays.SetByteArrayRegion (PeerReference, destinationIndex, length, (IntPtr) (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<SByte>) == targetType ||
				typeof (JavaSByteArray) == targetType;
		}

		internal static JniObjectReference CreateLocalRef (object value)
		{
		    return JavaArray<SByte>.CreateLocalRef<JavaSByteArray> (
		            value,
		            list => new JavaSByteArray (list));
		}

		internal static IList<SByte> GetValueFromJni (ref JniObjectReference reference, JniHandleOwnership transfer, Type targetType)
		{
		    return JavaArray<SByte>.GetValueFromJni (
		            ref reference,
		            transfer,
		            targetType,
		            (ref JniObjectReference h, JniHandleOwnership t) => new JavaSByteArray (ref h, t));
		}

		internal static IJavaPeerable CreateMarshalCollection (object value)
		{
		    return JavaArray<SByte>.CreateMarshalCollection (value, list => new JavaSByteArray (list) {
		        forMarshalCollection = true,
		    });
		}

		internal static void CleanupMarshalCollection (IJavaPeerable marshalObject, object value)
		{
		    JavaArray<SByte>.CleanupMarshalCollection<JavaSByteArray> (marshalObject, value);
		}
	}

	public sealed class JniCharArrayElements : JniArrayElements {

		JniObjectReference      arrayHandle;

		internal JniCharArrayElements (JniObjectReference arrayHandle, IntPtr elements)
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

		public JavaCharArray (ref JniObjectReference handle, JniHandleOwnership transfer)
			: base (ref handle, transfer)
		{
		}

		public unsafe JavaCharArray (int length)
			: base (ref *InvalidJniObjectReference, JniHandleOwnership.Invalid)
		{
		    var peer    = JniEnvironment.Arrays.NewCharArray (CheckLength (length));
		    using (SetPeerReference (ref peer, JniHandleOwnership.Transfer)) {
		    }
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
			IntPtr elements = JniEnvironment.Arrays.GetCharArrayElements (PeerReference, IntPtr.Zero);
			return elements == IntPtr.Zero ? null : new JniCharArrayElements (PeerReference, elements);
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
				JniEnvironment.Arrays.GetCharArrayRegion (PeerReference, sourceIndex, length, (IntPtr) (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Char[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException ("sourceArray");
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Char* b = sourceArray)
				JniEnvironment.Arrays.SetCharArrayRegion (PeerReference, destinationIndex, length, (IntPtr) (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Char>) == targetType ||
				typeof (JavaCharArray) == targetType;
		}

		internal static JniObjectReference CreateLocalRef (object value)
		{
		    return JavaArray<Char>.CreateLocalRef<JavaCharArray> (
		            value,
		            list => new JavaCharArray (list));
		}

		internal static IList<Char> GetValueFromJni (ref JniObjectReference reference, JniHandleOwnership transfer, Type targetType)
		{
		    return JavaArray<Char>.GetValueFromJni (
		            ref reference,
		            transfer,
		            targetType,
		            (ref JniObjectReference h, JniHandleOwnership t) => new JavaCharArray (ref h, t));
		}

		internal static IJavaPeerable CreateMarshalCollection (object value)
		{
		    return JavaArray<Char>.CreateMarshalCollection (value, list => new JavaCharArray (list) {
		        forMarshalCollection = true,
		    });
		}

		internal static void CleanupMarshalCollection (IJavaPeerable marshalObject, object value)
		{
		    JavaArray<Char>.CleanupMarshalCollection<JavaCharArray> (marshalObject, value);
		}
	}

	public sealed class JniInt16ArrayElements : JniArrayElements {

		JniObjectReference      arrayHandle;

		internal JniInt16ArrayElements (JniObjectReference arrayHandle, IntPtr elements)
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

		public JavaInt16Array (ref JniObjectReference handle, JniHandleOwnership transfer)
			: base (ref handle, transfer)
		{
		}

		public unsafe JavaInt16Array (int length)
			: base (ref *InvalidJniObjectReference, JniHandleOwnership.Invalid)
		{
		    var peer    = JniEnvironment.Arrays.NewShortArray (CheckLength (length));
		    using (SetPeerReference (ref peer, JniHandleOwnership.Transfer)) {
		    }
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
			IntPtr elements = JniEnvironment.Arrays.GetShortArrayElements (PeerReference, IntPtr.Zero);
			return elements == IntPtr.Zero ? null : new JniInt16ArrayElements (PeerReference, elements);
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
				JniEnvironment.Arrays.GetShortArrayRegion (PeerReference, sourceIndex, length, (IntPtr) (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Int16[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException ("sourceArray");
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Int16* b = sourceArray)
				JniEnvironment.Arrays.SetShortArrayRegion (PeerReference, destinationIndex, length, (IntPtr) (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Int16>) == targetType ||
				typeof (JavaInt16Array) == targetType;
		}

		internal static JniObjectReference CreateLocalRef (object value)
		{
		    return JavaArray<Int16>.CreateLocalRef<JavaInt16Array> (
		            value,
		            list => new JavaInt16Array (list));
		}

		internal static IList<Int16> GetValueFromJni (ref JniObjectReference reference, JniHandleOwnership transfer, Type targetType)
		{
		    return JavaArray<Int16>.GetValueFromJni (
		            ref reference,
		            transfer,
		            targetType,
		            (ref JniObjectReference h, JniHandleOwnership t) => new JavaInt16Array (ref h, t));
		}

		internal static IJavaPeerable CreateMarshalCollection (object value)
		{
		    return JavaArray<Int16>.CreateMarshalCollection (value, list => new JavaInt16Array (list) {
		        forMarshalCollection = true,
		    });
		}

		internal static void CleanupMarshalCollection (IJavaPeerable marshalObject, object value)
		{
		    JavaArray<Int16>.CleanupMarshalCollection<JavaInt16Array> (marshalObject, value);
		}
	}

	public sealed class JniInt32ArrayElements : JniArrayElements {

		JniObjectReference      arrayHandle;

		internal JniInt32ArrayElements (JniObjectReference arrayHandle, IntPtr elements)
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

		public JavaInt32Array (ref JniObjectReference handle, JniHandleOwnership transfer)
			: base (ref handle, transfer)
		{
		}

		public unsafe JavaInt32Array (int length)
			: base (ref *InvalidJniObjectReference, JniHandleOwnership.Invalid)
		{
		    var peer    = JniEnvironment.Arrays.NewIntArray (CheckLength (length));
		    using (SetPeerReference (ref peer, JniHandleOwnership.Transfer)) {
		    }
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
			IntPtr elements = JniEnvironment.Arrays.GetIntArrayElements (PeerReference, IntPtr.Zero);
			return elements == IntPtr.Zero ? null : new JniInt32ArrayElements (PeerReference, elements);
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
				JniEnvironment.Arrays.GetIntArrayRegion (PeerReference, sourceIndex, length, (IntPtr) (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Int32[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException ("sourceArray");
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Int32* b = sourceArray)
				JniEnvironment.Arrays.SetIntArrayRegion (PeerReference, destinationIndex, length, (IntPtr) (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Int32>) == targetType ||
				typeof (JavaInt32Array) == targetType;
		}

		internal static JniObjectReference CreateLocalRef (object value)
		{
		    return JavaArray<Int32>.CreateLocalRef<JavaInt32Array> (
		            value,
		            list => new JavaInt32Array (list));
		}

		internal static IList<Int32> GetValueFromJni (ref JniObjectReference reference, JniHandleOwnership transfer, Type targetType)
		{
		    return JavaArray<Int32>.GetValueFromJni (
		            ref reference,
		            transfer,
		            targetType,
		            (ref JniObjectReference h, JniHandleOwnership t) => new JavaInt32Array (ref h, t));
		}

		internal static IJavaPeerable CreateMarshalCollection (object value)
		{
		    return JavaArray<Int32>.CreateMarshalCollection (value, list => new JavaInt32Array (list) {
		        forMarshalCollection = true,
		    });
		}

		internal static void CleanupMarshalCollection (IJavaPeerable marshalObject, object value)
		{
		    JavaArray<Int32>.CleanupMarshalCollection<JavaInt32Array> (marshalObject, value);
		}
	}

	public sealed class JniInt64ArrayElements : JniArrayElements {

		JniObjectReference      arrayHandle;

		internal JniInt64ArrayElements (JniObjectReference arrayHandle, IntPtr elements)
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

		public JavaInt64Array (ref JniObjectReference handle, JniHandleOwnership transfer)
			: base (ref handle, transfer)
		{
		}

		public unsafe JavaInt64Array (int length)
			: base (ref *InvalidJniObjectReference, JniHandleOwnership.Invalid)
		{
		    var peer    = JniEnvironment.Arrays.NewLongArray (CheckLength (length));
		    using (SetPeerReference (ref peer, JniHandleOwnership.Transfer)) {
		    }
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
			IntPtr elements = JniEnvironment.Arrays.GetLongArrayElements (PeerReference, IntPtr.Zero);
			return elements == IntPtr.Zero ? null : new JniInt64ArrayElements (PeerReference, elements);
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
				JniEnvironment.Arrays.GetLongArrayRegion (PeerReference, sourceIndex, length, (IntPtr) (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Int64[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException ("sourceArray");
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Int64* b = sourceArray)
				JniEnvironment.Arrays.SetLongArrayRegion (PeerReference, destinationIndex, length, (IntPtr) (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Int64>) == targetType ||
				typeof (JavaInt64Array) == targetType;
		}

		internal static JniObjectReference CreateLocalRef (object value)
		{
		    return JavaArray<Int64>.CreateLocalRef<JavaInt64Array> (
		            value,
		            list => new JavaInt64Array (list));
		}

		internal static IList<Int64> GetValueFromJni (ref JniObjectReference reference, JniHandleOwnership transfer, Type targetType)
		{
		    return JavaArray<Int64>.GetValueFromJni (
		            ref reference,
		            transfer,
		            targetType,
		            (ref JniObjectReference h, JniHandleOwnership t) => new JavaInt64Array (ref h, t));
		}

		internal static IJavaPeerable CreateMarshalCollection (object value)
		{
		    return JavaArray<Int64>.CreateMarshalCollection (value, list => new JavaInt64Array (list) {
		        forMarshalCollection = true,
		    });
		}

		internal static void CleanupMarshalCollection (IJavaPeerable marshalObject, object value)
		{
		    JavaArray<Int64>.CleanupMarshalCollection<JavaInt64Array> (marshalObject, value);
		}
	}

	public sealed class JniSingleArrayElements : JniArrayElements {

		JniObjectReference      arrayHandle;

		internal JniSingleArrayElements (JniObjectReference arrayHandle, IntPtr elements)
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

		public JavaSingleArray (ref JniObjectReference handle, JniHandleOwnership transfer)
			: base (ref handle, transfer)
		{
		}

		public unsafe JavaSingleArray (int length)
			: base (ref *InvalidJniObjectReference, JniHandleOwnership.Invalid)
		{
		    var peer    = JniEnvironment.Arrays.NewFloatArray (CheckLength (length));
		    using (SetPeerReference (ref peer, JniHandleOwnership.Transfer)) {
		    }
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
			IntPtr elements = JniEnvironment.Arrays.GetFloatArrayElements (PeerReference, IntPtr.Zero);
			return elements == IntPtr.Zero ? null : new JniSingleArrayElements (PeerReference, elements);
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
				JniEnvironment.Arrays.GetFloatArrayRegion (PeerReference, sourceIndex, length, (IntPtr) (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Single[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException ("sourceArray");
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Single* b = sourceArray)
				JniEnvironment.Arrays.SetFloatArrayRegion (PeerReference, destinationIndex, length, (IntPtr) (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Single>) == targetType ||
				typeof (JavaSingleArray) == targetType;
		}

		internal static JniObjectReference CreateLocalRef (object value)
		{
		    return JavaArray<Single>.CreateLocalRef<JavaSingleArray> (
		            value,
		            list => new JavaSingleArray (list));
		}

		internal static IList<Single> GetValueFromJni (ref JniObjectReference reference, JniHandleOwnership transfer, Type targetType)
		{
		    return JavaArray<Single>.GetValueFromJni (
		            ref reference,
		            transfer,
		            targetType,
		            (ref JniObjectReference h, JniHandleOwnership t) => new JavaSingleArray (ref h, t));
		}

		internal static IJavaPeerable CreateMarshalCollection (object value)
		{
		    return JavaArray<Single>.CreateMarshalCollection (value, list => new JavaSingleArray (list) {
		        forMarshalCollection = true,
		    });
		}

		internal static void CleanupMarshalCollection (IJavaPeerable marshalObject, object value)
		{
		    JavaArray<Single>.CleanupMarshalCollection<JavaSingleArray> (marshalObject, value);
		}
	}

	public sealed class JniDoubleArrayElements : JniArrayElements {

		JniObjectReference      arrayHandle;

		internal JniDoubleArrayElements (JniObjectReference arrayHandle, IntPtr elements)
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

		public JavaDoubleArray (ref JniObjectReference handle, JniHandleOwnership transfer)
			: base (ref handle, transfer)
		{
		}

		public unsafe JavaDoubleArray (int length)
			: base (ref *InvalidJniObjectReference, JniHandleOwnership.Invalid)
		{
		    var peer    = JniEnvironment.Arrays.NewDoubleArray (CheckLength (length));
		    using (SetPeerReference (ref peer, JniHandleOwnership.Transfer)) {
		    }
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
			IntPtr elements = JniEnvironment.Arrays.GetDoubleArrayElements (PeerReference, IntPtr.Zero);
			return elements == IntPtr.Zero ? null : new JniDoubleArrayElements (PeerReference, elements);
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
				JniEnvironment.Arrays.GetDoubleArrayRegion (PeerReference, sourceIndex, length, (IntPtr) (b+destinationIndex));
		}

		public override unsafe void CopyFrom (Double[] sourceArray, int sourceIndex, int destinationIndex, int length)
		{
			if (sourceArray == null)
				throw new ArgumentNullException ("sourceArray");
			CheckArrayCopy (sourceIndex, sourceArray.Length, destinationIndex, Length, length);
			if (sourceArray.Length == 0)
				return;

			fixed (Double* b = sourceArray)
				JniEnvironment.Arrays.SetDoubleArrayRegion (PeerReference, destinationIndex, length, (IntPtr) (b+sourceIndex));
		}

		internal override bool TargetTypeIsCurrentType (Type targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				typeof (JavaPrimitiveArray<Double>) == targetType ||
				typeof (JavaDoubleArray) == targetType;
		}

		internal static JniObjectReference CreateLocalRef (object value)
		{
		    return JavaArray<Double>.CreateLocalRef<JavaDoubleArray> (
		            value,
		            list => new JavaDoubleArray (list));
		}

		internal static IList<Double> GetValueFromJni (ref JniObjectReference reference, JniHandleOwnership transfer, Type targetType)
		{
		    return JavaArray<Double>.GetValueFromJni (
		            ref reference,
		            transfer,
		            targetType,
		            (ref JniObjectReference h, JniHandleOwnership t) => new JavaDoubleArray (ref h, t));
		}

		internal static IJavaPeerable CreateMarshalCollection (object value)
		{
		    return JavaArray<Double>.CreateMarshalCollection (value, list => new JavaDoubleArray (list) {
		        forMarshalCollection = true,
		    });
		}

		internal static void CleanupMarshalCollection (IJavaPeerable marshalObject, object value)
		{
		    JavaArray<Double>.CleanupMarshalCollection<JavaDoubleArray> (marshalObject, value);
		}
	}

}