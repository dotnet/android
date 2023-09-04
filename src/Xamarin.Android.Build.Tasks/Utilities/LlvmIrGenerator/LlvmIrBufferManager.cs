using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks.LLVMIR;

partial class LlvmIrModule
{
	sealed class LlvmIrBufferManager
	{
		Dictionary<string, ulong> counters;
		Dictionary<object, Dictionary<string, string>> bufferVariableNames;

		public LlvmIrBufferManager ()
		{
			counters = new Dictionary<string, ulong> (StringComparer.Ordinal);
		}

		public string Allocate (StructureInstance structure, StructureMemberInfo smi, ulong size)
		{
			string baseName = $"_{structure.Info.Name}_{smi.Info.Name}";

			if (!counters.TryGetValue (baseName, out ulong count)) {
				count = 0;
				counters.Add (baseName, count);
			} else {
				count++;
				counters[baseName] = count;
			}

			return Register (structure, smi, $"{baseName}_{count:x}_{structure.IndexInArray:x}");
		}

		public string? GetBufferVariableName (StructureInstance structure, StructureMemberInfo smi)
		{
			if (bufferVariableNames == null || bufferVariableNames.Count == 0) {
				return null;
			}

			if (!bufferVariableNames.TryGetValue (structure.Obj, out Dictionary<string, string> members)) {
				return null;
			}

			if (!members.TryGetValue (MakeUniqueMemberId (structure, smi), out string bufferVariableName)) {
				return null;
			}

			return bufferVariableName;
		}

		string Register (StructureInstance structure, StructureMemberInfo smi, string bufferVariableName)
		{
			if (bufferVariableNames == null) {
				bufferVariableNames = new Dictionary<object, Dictionary<string, string>> ();
			}

			if (!bufferVariableNames.TryGetValue (structure.Obj, out Dictionary<string, string> members)) {
				members = new Dictionary<string, string> (StringComparer.Ordinal);
				bufferVariableNames.Add (structure.Obj, members);
			}

			members.Add (MakeUniqueMemberId (structure, smi), bufferVariableName);
			return bufferVariableName;
		}

		string MakeUniqueMemberId (StructureInstance structure, StructureMemberInfo smi) => $"{smi.Info.Name}_{structure.IndexInArray}";
	}
}
