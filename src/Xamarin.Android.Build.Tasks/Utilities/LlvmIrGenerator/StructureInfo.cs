using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Xamarin.Android.Tasks.LLVMIR
{
	// TODO: add cache for members and data provider info
	sealed class StructureInfo<T> : IStructureInfo
	{
		public string Name { get; } = String.Empty;
		public ulong Size { get; }
		public List<StructureMemberInfo<T>> Members { get; } = new List<StructureMemberInfo<T>> ();
		public NativeAssemblerStructContextDataProvider? DataProvider { get; }
		public int MaxFieldAlignment { get; private set; } = 0;
		public bool HasStrings { get; private set; }
		public bool HasPreAllocatedBuffers { get; private set; }

		public bool IsOpaque => Members.Count == 0;

		public StructureInfo (LlvmIrGenerator generator)
		{
			Type t = typeof(T);
			Name = t.GetShortName ();
			Size = GatherMembers (t, generator);
			DataProvider = t.GetDataProvider ();
		}

		public void RenderDeclaration (LlvmIrGenerator generator)
		{
			TextWriter output = generator.Output;
			generator.WriteStructureDeclarationStart (Name, forOpaqueType: IsOpaque);

			if (IsOpaque) {
				return;
			}

			for (int i = 0; i < Members.Count; i++) {
				StructureMemberInfo<T> info = Members[i];
				string nativeType = LlvmIrGenerator.MapManagedTypeToNative (info.MemberType);
				if (info.Info.IsNativePointer ()) {
					nativeType += "*";
				}

				// TODO: nativeType can be an array, update to indicate that (and get the size)
				string arraySize;
				if (info.IsNativeArray) {
					arraySize = $"[{info.ArrayElements}]";
				} else {
					arraySize = String.Empty;
				}

				var comment = $"{nativeType} {info.Info.Name}{arraySize}";
				generator.WriteStructureDeclarationField (info.IRType, comment, i == Members.Count - 1);
			}

			generator.WriteStructureDeclarationEnd ();
		}

		public string? GetCommentFromProvider (StructureMemberInfo<T> smi, StructureInstance<T> instance)
		{
			if (DataProvider == null || !smi.Info.UsesDataProvider ()) {
				return null;
			}

			string ret = DataProvider.GetComment (instance.Obj, smi.Info.Name);
			if (ret.Length == 0) {
				return null;
			}

			return ret;
		}

		public ulong GetBufferSizeFromProvider (StructureMemberInfo<T> smi, StructureInstance<T> instance)
		{
			if (DataProvider == null) {
				return 0;
			}

			return DataProvider.GetBufferSize (instance.Obj, smi.Info.Name);
		}

		ulong GatherMembers (Type type, LlvmIrGenerator generator)
		{
			ulong size = 0;
			foreach (MemberInfo mi in type.GetMembers ()) {
				if (mi.ShouldBeIgnored () || (!(mi is FieldInfo) && !(mi is PropertyInfo))) {
					continue;
				}

				var info = new StructureMemberInfo<T> (mi, generator);
				Members.Add (info);
				size += info.Size;
				if ((int)info.Size > MaxFieldAlignment) {
					MaxFieldAlignment = (int)info.Size;
				}

				if (!HasStrings && info.MemberType == typeof (string)) {
					HasStrings = true;
				}

				if (!HasPreAllocatedBuffers && info.Info.IsNativePointerToPreallocatedBuffer (out ulong _)) {
					HasPreAllocatedBuffers = true;
				}
			}

			return size;
		}
	}
}
