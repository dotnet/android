#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Java.Interop {

	public static class JniMarshal {

		public static bool RecursiveEquals (object? objA, object? objB)
		{
			if (object.Equals (objA, objB))
				return true;
			var ae = objA as IEnumerable;
			var be = objB as IEnumerable;
			if (ae != null && be != null) {
				var ai = ae.GetEnumerator ();
				var bi = be.GetEnumerator ();
				try {
					bool am, bm;
					do {
						am = ai.MoveNext ();
						bm = bi.MoveNext ();
						if (!(am && bm))
							break;
						if (!RecursiveEquals (ai.Current, bi.Current))
							return false;
					} while (true);
					return (am == bm);
				} finally {
					var ad = ai as IDisposable;
					var bd = bi as IDisposable;
					if (ad != null)
						ad.Dispose ();
					if (bd != null)
						bd.Dispose ();
				}
			}
			return false;
		}
	}
}

