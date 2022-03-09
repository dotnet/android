using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Xamarin.Android.Tasks.LLVMIR
{
	class StructureInstance<T>
	{
		Dictionary<StructureMemberInfo<T>, StructureStringData>? strings;

		public T Obj { get; }

		public StructureInstance (T instance)
		{
			Obj = instance;
		}

		public void AddStringData (StructureMemberInfo<T> smi, string? variableName, ulong stringSize)
		{
			if (strings == null) {
				strings = new Dictionary<StructureMemberInfo<T>, StructureStringData> ();
			}

			strings.Add (smi, new StructureStringData (variableName, stringSize));
		}

		public StructureStringData? GetStringData (StructureMemberInfo<T> smi)
		{
			if (strings != null && strings.TryGetValue (smi, out StructureStringData ssd)) {
				return ssd;
			}

			return null;
		}
	}
}
