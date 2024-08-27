using System;
using System.Collections.Generic;
using System.Reflection;

namespace Xamarin.Android.Tasks.LLVMIR
{
	// TODO: add cache for members and data provider info
	sealed class StructureInfo
	{
		Type type;
		readonly LlvmIrTypeCache cache;

		public Type Type => type;
		public string Name                                            { get; } = String.Empty;
		public ulong Size                                             { get; }
		public IList<StructureMemberInfo> Members                     { get; } = new List<StructureMemberInfo> ();
		public NativeAssemblerStructContextDataProvider? DataProvider { get; }
		public ulong MaxFieldAlignment                                { get; private set; } = 0;
		public bool HasStrings                                        { get; private set; }
		public bool HasPreAllocatedBuffers                            { get; private set; }
		public bool HasPointers                                       { get; private set; }

		public bool IsOpaque                                          => Members.Count == 0;
		public string NativeTypeDesignator                            { get; }

		public StructureInfo (LlvmIrModule module, Type type, LlvmIrTypeCache cache)
		{
			this.type = type;
			this.cache = cache;
			Name = type.GetShortName ();
			Size = GatherMembers (type, module);
			DataProvider = type.GetDataProvider ();
			NativeTypeDesignator = type.IsNativeClass () ? "class" : "struct";
		}

		public string? GetCommentFromProvider (StructureMemberInfo smi, StructureInstance instance)
		{
			if (DataProvider == null || !smi.Info.UsesDataProvider (cache)) {
				return null;
			}

			string ret = DataProvider.GetComment (instance.Obj, smi.Info.Name);
			if (ret.Length == 0) {
				return null;
			}

			return ret;
		}

		public ulong GetBufferSizeFromProvider (StructureMemberInfo smi, StructureInstance instance)
		{
			if (DataProvider == null) {
				return 0;
			}

			return DataProvider.GetBufferSize (instance.Obj, smi.Info.Name);
		}

		ulong GatherMembers (Type type, LlvmIrModule module, bool storeMembers = true)
		{
			ulong size = 0;
			foreach (MemberInfo mi in type.GetMembers ()) {
				if (mi.ShouldBeIgnored (cache) || (!(mi is FieldInfo) && !(mi is PropertyInfo))) {
					continue;
				}

				var info = new StructureMemberInfo (mi, module, cache);
				if (info.IsNativePointer) {
					HasPointers = true;
				}

				if (storeMembers) {
					Members.Add (info);
					size += info.Size;

					if (info.Alignment > MaxFieldAlignment) {
						MaxFieldAlignment = info.Alignment;
					}
				}

				if (!HasStrings && info.MemberType == typeof (string)) {
					HasStrings = true;
				}

				if (!HasPreAllocatedBuffers && info.Info.IsNativePointerToPreallocatedBuffer (cache, out ulong _)) {
					HasPreAllocatedBuffers = true;
				}

				// If we encounter an embedded struct (as opposed to a pointer), we need to descend and check if that struct contains any strings or buffers, but we
				// do NOT want to store any members while doing that, as the struct should have been mapped by the composer previously.
				// The presence of strings/buffers is important at the generation time as it is used to decide whether we need separate stream writers for them and
				// if the owning structure does **not** have any of those, the generated code would be invalid
				if (info.IsIRStruct (cache)) {
					GatherMembers (info.MemberType, module, storeMembers: false);
				}
			}

			return size;
		}
	}
}
