using System;
using System.Collections;
using System.Collections.Generic;

namespace Java.Lang {

	public sealed partial class String : global::Java.Lang.Object, Java.Lang.ICharSequence, IEnumerable
	{
		public unsafe String (string value)
#if JAVA_INTEROP1
			: base (ref *InvalidJniObjectReference, Java.Interop.JniObjectReferenceOptions.None)
#endif  // JAVA_INTEROP1
		{
		}

		public char CharAt (int index)
		{
			throw new NotImplementedException ();
		}

		public int Length ()
		{
			throw new NotImplementedException ();
		}

		public Java.Lang.ICharSequence SubSequenceFormatted (int start, int end)
		{
			throw new NotImplementedException ();
		}

		public override string ToString ()
		{
			throw new NotImplementedException ();
		}

		public IEnumerator<char> GetEnumerator ()
		{
			throw new NotImplementedException ();
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			throw new NotImplementedException ();
		}
	}
}
