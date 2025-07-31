#nullable enable

using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks.LLVMIR;

partial class LlvmIrModule
{
	/// <summary>
	/// Manages allocation and naming of buffer variables for structure members that require
	/// preallocated buffer space. This class ensures unique buffer names and tracks
	/// buffer allocations across structure instances.
	/// </summary>
	sealed class LlvmIrBufferManager
	{
		Dictionary<string, ulong> counters;
		Dictionary<object, Dictionary<string, string>> bufferVariableNames;

		/// <summary>
		/// Initializes a new instance of the <see cref="LlvmIrBufferManager"/> class.
		/// </summary>
		public LlvmIrBufferManager ()
		{
			counters = new Dictionary<string, ulong> (StringComparer.Ordinal);
			bufferVariableNames = new Dictionary<object, Dictionary<string, string>> ();
		}

		/// <summary>
		/// Allocates a new buffer with a unique name for the specified structure member.
		/// </summary>
		/// <param name="structure">The structure instance that contains the member.</param>
		/// <param name="smi">The structure member information that requires a buffer.</param>
		/// <param name="size">The size of the buffer to allocate.</param>
		/// <returns>The unique name assigned to the allocated buffer variable.</returns>
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

		/// <summary>
		/// Gets the buffer variable name for the specified structure member, if one has been allocated.
		/// </summary>
		/// <param name="structure">The structure instance containing the member.</param>
		/// <param name="smi">The structure member information to look up.</param>
		/// <returns>The buffer variable name if found; otherwise, <c>null</c>.</returns>
		public string? GetBufferVariableName (StructureInstance structure, StructureMemberInfo smi)
		{
			if (bufferVariableNames == null || bufferVariableNames.Count == 0 || structure.Obj == null) {
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

		/// <summary>
		/// Registers a buffer variable name for the specified structure member.
		/// </summary>
		/// <param name="structure">The structure instance containing the member.</param>
		/// <param name="smi">The structure member information.</param>
		/// <param name="bufferVariableName">The buffer variable name to register.</param>
		/// <returns>The registered buffer variable name.</returns>
		string Register (StructureInstance structure, StructureMemberInfo smi, string bufferVariableName)
		{
			if (bufferVariableNames == null) {
				bufferVariableNames = new Dictionary<object, Dictionary<string, string>> ();
			}

			if (structure.Obj == null) {
				throw new ArgumentException ("Structure instance object cannot be null", nameof (structure));
			}

			if (!bufferVariableNames.TryGetValue (structure.Obj, out Dictionary<string, string> members)) {
				members = new Dictionary<string, string> (StringComparer.Ordinal);
				bufferVariableNames.Add (structure.Obj, members);
			}

			members.Add (MakeUniqueMemberId (structure, smi), bufferVariableName);
			return bufferVariableName;
		}

		/// <summary>
		/// Creates a unique member identifier combining the member name and structure array index.
		/// </summary>
		/// <param name="structure">The structure instance.</param>
		/// <param name="smi">The structure member information.</param>
		/// <returns>A unique identifier for the member within the structure array.</returns>
		string MakeUniqueMemberId (StructureInstance structure, StructureMemberInfo smi) => $"{smi.Info.Name}_{structure.IndexInArray}";
	}
}
