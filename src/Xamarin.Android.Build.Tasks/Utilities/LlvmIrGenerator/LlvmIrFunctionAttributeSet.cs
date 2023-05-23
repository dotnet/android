using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	class LlvmIrFunctionAttributeSet : IEnumerable<LlvmIrFunctionAttribute>, IEquatable<LlvmIrFunctionAttributeSet>
	{
		static readonly object counterLock = new object ();
		static uint counter = 0;

		public uint Number { get; }

		HashSet<LlvmIrFunctionAttribute> attributes;

		public LlvmIrFunctionAttributeSet ()
		{
			attributes = new HashSet<LlvmIrFunctionAttribute> ();

			lock (counterLock) {
				Number = counter++;
			}
		}

		public void Add (LlvmIrFunctionAttribute attr)
		{
			if (attr == null) {
				throw new ArgumentNullException (nameof (attr));
			}

			if (!attributes.Contains (attr)) {
				attributes.Add (attr);
			}
		}

		public void Add (LlvmIrFunctionAttributeSet sourceSet)
		{
			if (sourceSet == null) {
				throw new ArgumentNullException (nameof (sourceSet));
			}

			foreach (LlvmIrFunctionAttribute attr in sourceSet) {
				Add (attr);
			}
		}

		public string Render ()
		{
			List<LlvmIrFunctionAttribute> list = attributes.ToList ();
			list.Sort ((LlvmIrFunctionAttribute a, LlvmIrFunctionAttribute b) => a.Name.CompareTo (b.Name));

			return String.Join (" ", list.Select (a => a.Render ()));
		}

		public IEnumerator<LlvmIrFunctionAttribute> GetEnumerator () => attributes.GetEnumerator ();

		IEnumerator IEnumerable.GetEnumerator () => GetEnumerator ();

		public bool Equals (LlvmIrFunctionAttributeSet other)
		{
			if (other == null) {
				return false;
			}

			if (attributes.Count != other.attributes.Count) {
				return false;
			}

			foreach (LlvmIrFunctionAttribute attr in attributes) {
				if (!other.attributes.Contains (attr)) {
					return false;
				}
			}

			return true;
		}

		public override bool Equals (object obj)
		{
			var attrSet = obj as LlvmIrFunctionAttributeSet;
			if (attrSet == null) {
				return false;
			}

			return Equals (attrSet);
		}

		public override int GetHashCode()
		{
			int hc = 0;

			foreach (LlvmIrFunctionAttribute attr in attributes) {
				hc ^= attr?.GetHashCode () ?? 0;
			}

			return hc;
		}
    }
}
