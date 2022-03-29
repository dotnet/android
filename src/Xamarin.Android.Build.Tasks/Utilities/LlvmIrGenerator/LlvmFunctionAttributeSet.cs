using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tasks.LLVMIR
{
	class LlvmFunctionAttributeSet : IEnumerable<LLVMFunctionAttribute>
	{
		HashSet<LLVMFunctionAttribute> attributes;

		public LlvmFunctionAttributeSet ()
		{
			attributes = new HashSet<LLVMFunctionAttribute> ();
		}

		public void Add (LLVMFunctionAttribute attr)
		{
			if (attr == null) {
				throw new ArgumentNullException (nameof (attr));
			}

			// TODO: implement uniqueness checks
			attributes.Add (attr);
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
	}
}
