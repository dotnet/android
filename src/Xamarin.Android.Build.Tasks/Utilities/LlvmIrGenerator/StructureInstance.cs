using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Xamarin.Android.Tasks.LLVMIR
{
	class StructureInstance<T>
	{
		Dictionary<StructureMemberInfo<T>, StructurePointerData>? pointees;

		public T Obj { get; }

		public StructureInstance (T instance)
		{
			Obj = instance;
		}

		public void AddPointerData (StructureMemberInfo<T> smi, string? variableName, ulong dataSize)
		{
			if (pointees == null) {
				pointees = new Dictionary<StructureMemberInfo<T>, StructurePointerData> ();
			}

			pointees.Add (smi, new StructurePointerData (variableName, dataSize));
		}

		public StructurePointerData? GetPointerData (StructureMemberInfo<T> smi)
		{
			if (pointees != null && pointees.TryGetValue (smi, out StructurePointerData ssd)) {
				return ssd;
			}

			return null;
		}
	}
}
