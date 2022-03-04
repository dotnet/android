using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR
{
	// TODO: add cache for members and data provider info
	sealed class StructureInfo<T> : IStructureInfo
	{
		public string Name { get; } = String.Empty;
		public ulong Size { get; }
		public List<StructureMemberInfo<T>> Members { get; } = new List<StructureMemberInfo<T>> ();
		public NativeAssemblerStructContextDataProvider? DataProvider { get; }

		public StructureInfo ()
		{
			Type t = typeof(T);
			Name = t.GetShortName ();
			Size = GatherMembers (t);
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

		ulong GatherMembers (Type type)
		{
			foreach (MemberInfo mi in type.GetMembers ()) {
				if (mi.ShouldBeIgnored () || (!(mi is FieldInfo) && !(mi is PropertyInfo))) {
					continue;
				}

				Members.Add (new StructureMemberInfo<T> (mi));
			}

			return 0;
		}
	}
}
