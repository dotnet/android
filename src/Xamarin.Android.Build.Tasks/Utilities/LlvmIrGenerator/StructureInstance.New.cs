using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	// TODO: remove these aliases once the refactoring is done
	using StructurePointerData = LLVMIR.StructurePointerData;

	class StructureInstance
	{
		Dictionary<StructureMemberInfo, StructurePointerData>? pointees;
		StructureInfo info;

		public object Obj { get; }
		public Type Type  => info.Type;
		public StructureInfo Info => info;

		public StructureInstance (StructureInfo info, object instance)
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
}
