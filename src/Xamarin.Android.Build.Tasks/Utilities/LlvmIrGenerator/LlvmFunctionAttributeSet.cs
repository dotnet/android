using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tasks.LLVMIR
{
	class LlvmFunctionAttributeSet : IEnumerable<LLVMFunctionAttribute>, IEquatable<LlvmFunctionAttributeSet>
	{
		static readonly object counterLock = new object ();
		static uint counter = 0;

		public uint Number { get; }

		HashSet<LLVMFunctionAttribute> attributes;

		public LlvmFunctionAttributeSet ()
		{
			attributes = new HashSet<LLVMFunctionAttribute> ();

			lock (counterLock) {
				Number = counter++;
			}
		}

		public void Add (LLVMFunctionAttribute attr)
		{
			if (attr == null) {
				throw new ArgumentNullException (nameof (attr));
			}

			if (!attributes.Contains (attr)) {
				attributes.Add (attr);
			}
		}

		public void Add (LlvmFunctionAttributeSet sourceSet)
		{
			if (sourceSet == null) {
				throw new ArgumentNullException (nameof (sourceSet));
			}

			foreach (LLVMFunctionAttribute attr in sourceSet) {
				Add (attr);
			}
		}

		public string Render ()
		{
			List<LLVMFunctionAttribute> list = attributes.ToList ();
			list.Sort ((LLVMFunctionAttribute a, LLVMFunctionAttribute b) => a.Name.CompareTo (b.Name));

			return String.Join (" ", list.Select (a => a.Render ()));
		}

		public IEnumerator<LLVMFunctionAttribute> GetEnumerator () => attributes.GetEnumerator ();

		IEnumerator IEnumerable.GetEnumerator () => GetEnumerator ();

		public bool Equals (LlvmFunctionAttributeSet other)
		{
			if (other == null) {
				return false;
			}

			if (attributes.Count != other.attributes.Count) {
				return false;
			}

			foreach (LLVMFunctionAttribute attr in attributes) {
				if (!other.attributes.Contains (attr)) {
					return false;
				}
			}

			return true;
		}

		public override bool Equals (object obj)
		{
			var attrSet = obj as LlvmFunctionAttributeSet;
			if (attrSet == null) {
				return false;
			}

			return Equals (attrSet);
		}

		public override int GetHashCode()
		{
			int hc = 0;

			foreach (LLVMFunctionAttribute attr in attributes) {
				hc ^= attr?.GetHashCode () ?? 0;
			}

			return hc;
		}
    }
}
