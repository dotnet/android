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
			generator.WriteStructureDeclarationStart (Name);

			for (int i = 0; i < Members.Count; i++) {
				StructureMemberInfo<T> info = Members[i];
				string nativeType = LlvmIrGenerator.MapManagedTypeToNative (info.MemberType);
				if (info.Info.IsNativePointer ()) {
					nativeType += "*";
				}
				var comment = $"{nativeType} {info.Info.Name}";
				generator.WriteStructureDeclarationField (info.IRType, comment, i == Members.Count - 1);
			}

			generator.WriteStructureDeclarationEnd ();
		}

		public string? GetCommentFromProvider (StructureMemberInfo<T> smi, StructureInstance<T> instance)
		{
			if (DataProvider == null) {
				return null;
			}

			string ret = DataProvider.GetComment (instance.Obj, smi.Info.Name);
			if (ret.Length == 0) {
				return null;
			}

			return ret;
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

				if (info.MemberType == typeof (string)) {
					HasStrings = true;
				}
			}

			return size;
		}
	}
}
