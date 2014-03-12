
using System;
using System.Collections.Generic;

namespace Java.Interop {

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
	}

}