using System;
using System.Collections.Generic;

namespace Java.Interop
{
	public class JavaObjectArray<T> : JavaArray<T>
	{
		public JavaObjectArray (ref JniObjectReference handle, JniObjectReferenceOptions transfer)
			: base (ref handle, transfer)
		{
		}

		static JniObjectReference _NewArray (int length)
		{
			var info = JniEnvironment.Current.JavaVM.GetJniTypeInfoForType (typeof (T));
			if (info.SimpleReference == null)
				info = new JniTypeSignature ("java/lang/Object", info.ArrayRank);
			if (info.IsKeyword && info.ArrayRank == 0) {
				info = info.GetPrimitiveWrapper ();
			}
			using (var t = new JniType (info.Name)) {
				return JniEnvironment.Arrays.NewObjectArray (length, t.PeerReference, new JniObjectReference ());
			}
		}

		public unsafe JavaObjectArray (int length)
			: this (ref *InvalidJniObjectReference, JniObjectReferenceOptions.Invalid)
		{
			var peer    = _NewArray (CheckLength (length));
			using (SetPeerReference (ref peer, JniObjectReferenceOptions.DisposeSourceReference)) {
			}
		}

		public JavaObjectArray (IList<T> value)
			: this (CheckLength (value))
		{
			for (int i = 0; i < value.Count; ++i)
				SetElementAt (i, value [i]);
		}

		public JavaObjectArray (IEnumerable<T> value)
			: this (_ToList (value))
		{
		}

		public override T this [int index] {
			get {
				if (index < 0 || index >= Length)
					throw new ArgumentOutOfRangeException ("index", "index < 0 || index >= Length");
				return GetElementAt (index);
			}
			set {
				if (index < 0 || index >= Length)
					throw new ArgumentOutOfRangeException ("index", "index < 0 || index >= Length");
				SetElementAt (index, value);
			}
		}

		T GetElementAt (int index)
		{
			var lref = JniEnvironment.Arrays.GetObjectArrayElement (PeerReference, index);
			return JniMarshal.GetValue<T> (ref lref, JniObjectReferenceOptions.DisposeSourceReference);
		}

		void SetElementAt (int index, T value)
		{
			var h = JniMarshal.CreateLocalRef (value);
			JniEnvironment.Arrays.SetObjectArrayElement (PeerReference, index, h);
			JniEnvironment.References.Dispose (ref h, JniObjectReferenceOptions.DisposeSourceReference);
		}

		public override IEnumerator<T> GetEnumerator ()
		{
			int len = Length;
			for (int i = 0; i < len; ++i) {
				yield return GetElementAt (i);
			}
		}

		public override void Clear ()
		{
			int len = Length;
			var v = JniMarshal.CreateLocalRef (default (T));
			for (int i = 0; i < len; i++) {
				JniEnvironment.Arrays.SetObjectArrayElement (PeerReference, i, v);
			}
			JniEnvironment.References.Dispose (ref v, JniObjectReferenceOptions.DisposeSourceReference);
		}

		public override int IndexOf (T item)
		{
			int len = Length;
			for (int i = 0; i < len; i++) {
				var at = GetElementAt (i);
				try {
					if (EqualityComparer<T>.Default.Equals (item, at) || JniMarshal.RecursiveEquals (item, at))
						return i;
				} finally {
					var j = at as IJavaPeerable;
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
			CopyToList (array, arrayIndex);
		}

		internal override void CopyToList (IList<T> list, int index)
		{
			int len = Length;
			for (int i = 0; i < len; i++) {
				var item         = GetElementAt (i);
				list [index + i] = item;
				if (forMarshalCollection) {
					var d = item as IJavaPeerable;
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

		internal static object GetValue (ref JniObjectReference handle, JniObjectReferenceOptions transfer, Type targetType)
		{
			return JavaArray<T>.GetValueFromJni (ref handle, transfer, targetType, (ref JniObjectReference h, JniObjectReferenceOptions t) => new JavaObjectArray<T> (ref h, t) {
				forMarshalCollection    = true,
			});
		}

		internal static JniObjectReference CreateLocalRef (object value)
		{
			return JavaArray<T>.CreateLocalRef (value, list => new JavaObjectArray<T>(list));
		}

		internal static IJavaPeerable CreateMarshalCollection (object value)
		{
			return JavaArray<T>.CreateMarshalCollection (value, list => new JavaObjectArray<T> (list) {
				forMarshalCollection    = true,
			});
		}

		internal static void CleanupMarshalCollection (IJavaPeerable marshalObject, object value)
		{
			JavaArray<T>.CleanupMarshalCollection<JavaObjectArray<T>> (marshalObject, value);
		}
	}
}

