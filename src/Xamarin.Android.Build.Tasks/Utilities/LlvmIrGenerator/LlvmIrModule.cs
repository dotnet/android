using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	partial class LlvmIrModule
	{
		public IList<LlvmIrFunction>? ExternalFunctions         { get; private set; }
		public IList<LlvmIrFunctionAttributeSet>? AttributeSets { get; private set; }
		public IList<IStructureInfo>? Structures                { get; private set; }
		public IList<LlvmIrGlobalVariable>? GlobalVariables     { get; private set; }

		Dictionary<LlvmIrFunctionAttributeSet, LlvmIrFunctionAttributeSet>? attributeSets;
		Dictionary<LlvmIrFunction, LlvmIrFunction>? externalFunctions;
		Dictionary<Type, IStructureInfo>? structures;

		List<LlvmIrGlobalVariable>? globalVariables;

		/// <summary>
		/// Perform any tasks that need to be done after construction is complete.
		/// </summary>
		public void AfterConstruction ()
		{
			if (externalFunctions != null) {
				List<LlvmIrFunction> list = externalFunctions.Values.ToList ();
				list.Sort ((LlvmIrFunction a, LlvmIrFunction b) => a.Signature.Name.CompareTo (b.Signature.Name));
				ExternalFunctions = list.AsReadOnly ();
			}

			if (attributeSets != null) {
				List<LlvmIrFunctionAttributeSet> list = attributeSets.Values.ToList ();
				list.Sort ((LlvmIrFunctionAttributeSet a, LlvmIrFunctionAttributeSet b) => a.Number.CompareTo (b.Number));
				AttributeSets = list.AsReadOnly ();
			}

			if (structures != null) {
				List<IStructureInfo> list = structures.Values.ToList ();
				list.Sort ((IStructureInfo a, IStructureInfo b) => a.Name.CompareTo (b.Name));
				Structures = list.AsReadOnly ();
			}

			GlobalVariables = globalVariables?.AsReadOnly ();
		}

		public void Add (LlvmIrGlobalVariable variable)
		{
			if (globalVariables == null) {
				globalVariables = new List<LlvmIrGlobalVariable> ();
			}

			globalVariables.Add (variable);
		}

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

		/// <summary>
		/// Since LLVM IR is strongly typed, it requires each structure to be properly declared before it is
		/// used throughout the code.  This method uses reflection to scan the managed type <typeparamref name="T"/>
		/// and record the information for future use.  The returned <see cref="StructureInfo<T>"/> structure contains
		/// the description.  It is used later on not only to declare the structure in output code, but also to generate
		/// data from instances of <typeparamref name="T"/>.  This method is typically called from the <see cref="LlvmIrGenerator.MapStructures"/>
		/// method.
		/// </summary>
		public StructureInfo<T> MapStructure<T> ()
		{
			Console.WriteLine ($"Mapping structure: {typeof(T)}");
			if (structures == null) {
				structures = new Dictionary<Type, IStructureInfo> ();
			}

			Type t = typeof(T);
			if (!t.IsClass && !t.IsValueType) {
				throw new InvalidOperationException ($"{t} must be a class or a struct");
			}

			// TODO: check if already there
			if (structures.TryGetValue (t, out IStructureInfo sinfo)) {
				return (StructureInfo<T>)sinfo;
			}

			var ret = new StructureInfo<T> (this);
			structures.Add (t, ret);

			return ret;
		}

		internal IStructureInfo GetStructureInfo (Type type)
		{
			if (structures == null) {
				throw new InvalidOperationException ($"Internal error: no structures have been mapped, cannot return info for {type}");
			}

			foreach (var kvp in structures) {
				IStructureInfo si = kvp.Value;
				if (si.Type != type) {
					continue;
				}

				return si;
			}

			throw new InvalidOperationException ($"Unmapped structure {type}");
		}
	}
}
