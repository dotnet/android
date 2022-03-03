using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR
{
	sealed class StructureMemberInfo<T>
	{
		public string IRType   { get; }
		public MemberInfo Info { get; }
		public Type MemberType { get; }

		public StructureMemberInfo (MemberInfo mi)
		{
			Info = mi;

			// TODO: check member custom attributes to see if the native member should be a pointer (except for string,
			// since MapManagedType already takes care of that)
			MemberType = mi switch {
				FieldInfo fi => fi.FieldType,
				PropertyInfo pi => pi.PropertyType,
				_ => throw new InvalidOperationException ($"Unsupported member type {mi}")
			};

			if (MemberType != typeof(string) && (MemberType.IsStructure () || MemberType.IsClass)) {
				IRType = $"%struct.{MemberType.GetShortName ()}";
			} else {
				IRType = LlvmIrGenerator.MapManagedTypeToIR (MemberType);
			}

			if (mi.IsNativePointer ()) {
				IRType += "*";
			}
		}

		public object? GetValue (T instance)
		{
			if (Info is FieldInfo fi) {
				return fi.GetValue (instance);
			}

			var pi = Info as PropertyInfo;
			return pi.GetValue (instance);
		}
	}
}
