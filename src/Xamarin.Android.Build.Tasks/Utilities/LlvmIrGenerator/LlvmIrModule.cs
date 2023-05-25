using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	partial class LlvmIrModule
	{
		public List<LlvmIrFunction>? ExternalFunctions => externalFunctions?.Values.ToList ();
		public List<LlvmIrFunctionAttributeSet>? AttributeSets => attributeSets?.Values.ToList ();

		Dictionary<LlvmIrFunctionAttributeSet, LlvmIrFunctionAttributeSet>? attributeSets;
		Dictionary<LlvmIrFunction, LlvmIrFunction>? externalFunctions;

		/// <summary>
		/// Add a new attribute set.  The caller MUST use the returned value to refer to the set, instead of the one passed
		/// as parameter, since this function de-duplicates sets and may return a previously added one that's identical to
		/// the new one.
		/// </summary>
		public LlvmIrFunctionAttributeSet AddAttributeSet (LlvmIrFunctionAttributeSet attrSet)
		{
			if (attributeSets == null) {
				attributeSets = new Dictionary<LlvmIrFunctionAttributeSet, LlvmIrFunctionAttributeSet> ();
			}

			if (attributeSets.TryGetValue (attrSet, out LlvmIrFunctionAttributeSet existingSet)) {
				return existingSet;
			}
			attrSet.Number = (uint)attributeSets.Count;
			attributeSets.Add (attrSet, attrSet);

			return attrSet;
		}

		/// <summary>
		/// Add a new external function declaration.  The caller MUST use the returned value to refer to the function, instead
		/// of the one passed as parameter, since this function de-duplicates function declarations and may return a previously
		/// added one that's identical to the new one.
		/// </summary>
		public LlvmIrFunction DeclareExternalFunction (LlvmIrFunction func)
		{
			if (externalFunctions == null) {
				externalFunctions = new Dictionary<LlvmIrFunction, LlvmIrFunction> ();
			}

			if (externalFunctions.TryGetValue (func, out LlvmIrFunction existingFunc)) {
				return existingFunc;
			}

			externalFunctions.Add (func, func);
			return func;
		}
	}
}
