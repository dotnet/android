using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	// TODO: remove these aliases once the refactoring is done
	using StructurePointerData = LLVMIR.StructurePointerData;

	abstract class StructureInstance
	{
		Dictionary<StructureMemberInfo, StructurePointerData>? pointees;
		StructureInfo info;

		public object? Obj { get; }
		public Type Type  => info.Type;
		public StructureInfo Info => info;

		protected StructureInstance (StructureInfo info, object? instance)
		{
			if (instance != null && !info.Type.IsAssignableFrom (instance.GetType ())) {
				throw new ArgumentException ($"must be and instance of, or derived from, the {info.Type} type, or `null`", nameof (instance));
			}

			this.info = info;
			Obj = instance;
		}

		public void AddPointerData (StructureMemberInfo smi, string? variableName, ulong dataSize)
		{
			if (pointees == null) {
				pointees = new Dictionary<StructureMemberInfo, StructurePointerData> ();
			}

			pointees.Add (smi, new StructurePointerData (variableName, dataSize));
		}

		public StructurePointerData? GetPointerData (StructureMemberInfo smi)
		{
			if (pointees != null && pointees.TryGetValue (smi, out StructurePointerData ssd)) {
				return ssd;
			}

			return null;
		}
	}

	/// <summary>
	/// Represents a typed structure instance, derived from the <see cref="StructureInstance"/> class.  The slightly weird
	/// approach is because on one hand we need to operate on a heterogenous set of structures (in which generic types would
	/// only get in the way), but on the other hand we need to be able to get the structure type (whose instance is in
	/// <see cref="Obj"/> and <see cref="Instance"/>) only by looking at the **type**.  This is needed in situations when we have
	/// an array of some structures that is empty - we wouldn't be able to gleam the structure type from any instance and we still
	/// need to output a stronly typed LLVM IR declaration of the structure array.  With this class, most of the code will use the
	/// abstract <see cref="StructureInstance"/> type, and knowing we have only one non-abstract implementation of the class allows
	/// us to use StructureInstance&lt;T&gt; in a cast, to get <c>T</c> via reflection.
	/// <summary>
	sealed class StructureInstance<T> : StructureInstance
	{
		public T? Instance => (T)Obj;

		public StructureInstance (StructureInfo info, T? instance)
			: base (info, instance)
		{}
	}
}
