using System;
using System.Collections.Generic;

namespace Java.Interop
{
	public class JavaObjectArray<T> : JavaArray<T>
	{
		public JavaObjectArray (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		static JniLocalReference _NewArray (int length)
		{
			var info = JniEnvironment.Current.JavaVM.GetJniTypeInfoForType (typeof (T));
			if (info.JniTypeName == null)
				info.JniTypeName = "java/lang/Object";
			if (info.TypeIsKeyword && info.ArrayRank == 0) {
				if (info.JniTypeName == "I")
					info.JniTypeName = JniInteger.JniTypeName;
			}
			using (var t = new JniType (info.ToString ())) {
				return JniEnvironment.Arrays.NewObjectArray (length, t.SafeHandle, JniReferenceSafeHandle.Null);
			}
		}

		public JavaObjectArray (int length)
			: this (_NewArray (CheckLength (length)), JniHandleOwnership.Transfer)
		{
		}

		public JavaObjectArray (IList<T> value)
			: this (CheckLength (value))
		{
			for (int i = 0; i < value.Count; ++i)
				this [i] = value [i];
		}

		public JavaObjectArray (IEnumerable<T> value)
			: this (_ToList (value))
		{
		}

		public override T this [int index] {
			get {
				if (index < 0 || index >= Length)
					throw new ArgumentOutOfRangeException ("index", "index < 0 || index >= Length");
				var lref = JniEnvironment.Arrays.GetObjectArrayElement (SafeHandle, index);
				return JniMarshal.GetValue<T> (lref, JniHandleOwnership.Transfer);
			}
			set {
				if (index < 0 || index >= Length)
					throw new ArgumentOutOfRangeException ("index", "index < 0 || index >= Length");
				using (var h = JniMarshal.CreateLocalRef (value))
					JniEnvironment.Arrays.SetObjectArrayElement (SafeHandle, index, h);
			}
		}

		public override void Clear ()
		{
			int len = Length;
			for (int i = 0; i < len; i++)
				this [i] = default (T);
		}

		public override int IndexOf (T item)
		{
			int len = Length;
			for (int i = 0; i < len; i++) {
				var at = this [i];
				try {
					if (EqualityComparer<T>.Default.Equals (item, at) || JniMarshal.RecursiveEquals (item, at))
						return i;
				} finally {
					var j = at as IJavaObject;
					if (j != null)
						j.DisposeUnlessRegistered ();
				}
			}
			return -1;
		}

		public override void CopyTo (T[] array, int arrayIndex)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			CheckArrayCopy (0, Length, arrayIndex, array.Length, Length);
			CopyTo ((IList<T>) array, arrayIndex);
		}

		void CopyTo (IList<T> list, int index)
		{
			int len = Length;
			for (int i = 0; i < len; i++) {
				var item = this [i];
				list [index + i] = item;
				if (forMarshalCollection) {
					var d = item as IJavaObject;
					if (d != null)
						d.DisposeUnlessRegistered ();
				}
			}
		}

		internal override bool TargetTypeIsCurrentType (Type targetType)
		{
			return base.TargetTypeIsCurrentType (targetType) ||
				targetType == typeof (JavaObjectArray<T>);
		}

		internal static object GetValue (JniReferenceSafeHandle handle, JniHandleOwnership transfer, Type targetType)
		{
			var value = JniEnvironment.Current.JavaVM.PeekObject (handle);
			var array = value as JavaObjectArray<T>;
			if (array != null) {
				JniEnvironment.Handles.Dispose (handle, transfer);
				return array.ToTargetType (targetType, dispose: false);
			}
			return new JavaObjectArray<T> (handle, transfer) {
				forMarshalCollection = true,
			}.ToTargetType (targetType, dispose: true);
		}

		internal static JniLocalReference CreateLocalRef (object value)
		{
			if (value == null)
				return new JniLocalReference ();
			var v = value as JavaObjectArray<T>;
			if (v != null)
				return v.SafeHandle.NewLocalRef ();
			var list = value as IList<T>;
			if (list == null)
				throw CreateMarshalNotSupportedException (value.GetType (), typeof (JavaObjectArray<T>));
			using (var temp = new JavaObjectArray<T> (list)) {
				return temp.SafeHandle.NewLocalRef ();
			}
		}

		internal static IJavaObject CreateMarshalCollection (object value)
		{
			if (value == null)
				return null;
			var v = value as JavaObjectArray<T>;
			if (v != null)
				return v;
			var list = value as IList<T>;
			if (list == null)
				throw CreateMarshalNotSupportedException (value.GetType (), typeof (JavaObjectArray<T>));
			return new JavaObjectArray<T> (list) {
				forMarshalCollection = true,
			};
		}

		internal static void CleanupMarshalCollection (IJavaObject marshalObject, object value)
		{
			var source = (JavaObjectArray<T>) marshalObject;
			if (source == null)
				return;

			var listDest  = value as IList<T>;
			if (listDest != null) {
				source.CopyTo (listDest, 0);
			}
			if (source.forMarshalCollection) {
				source.Dispose ();
			}
		}
	}
}

