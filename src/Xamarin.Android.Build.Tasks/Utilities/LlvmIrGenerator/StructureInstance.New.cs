using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	// TODO: remove these aliases once the refactoring is done
	using StructurePointerData = LLVMIR.StructurePointerData;

	class StructureInstance
	{
		Dictionary<StructureMemberInfo, StructurePointerData>? pointees;

		public object Obj { get; }
		public Type Type  { get; }

		public StructureInstance (object instance)
		{
			Obj = instance;
			Type = instance.GetType ();
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
}
