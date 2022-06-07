using System;
using System.Reflection;

using Xamarin.Android.Tasks;

namespace Xamarin.Android.Tasks.LLVMIR
{
	static class TypeUtilities
	{
		public static string GetShortName (this Type type)
		{
			string? fullName = type.FullName;

			if (String.IsNullOrEmpty (fullName)) {
				throw new InvalidOperationException ($"Unnamed types aren't supported ({type})");
			}

			int lastCharIdx = fullName.LastIndexOf ('.');
			string ret;
			if (lastCharIdx < 0) {
				ret = fullName;
			} else {
				ret = fullName.Substring (lastCharIdx + 1);
			}

			lastCharIdx = ret.LastIndexOf ('+');
			if (lastCharIdx >= 0) {
				ret = ret.Substring (lastCharIdx + 1);
			}

			if (String.IsNullOrEmpty (ret)) {
				throw new InvalidOperationException ($"Invalid type name ({type})");
			}

			return ret;
		}

		public static bool IsStructure (this Type type)
		{
			return type.IsValueType &&
				!type.IsEnum &&
				!type.IsPrimitive &&
				!type.IsArray &&
				type != typeof (decimal) &&
				type != typeof (DateTime) &&
				type != typeof (object);
		}

		public static bool IsIRStruct<T> (this StructureMemberInfo<T> smi)
		{
			Type type = smi.MemberType;

			// type.IsStructure() handles checks for primitive types, enums etc
			return
				type != typeof(string) &&
				!smi.Info.IsInlineArray () &&
				!smi.Info.IsNativePointer () &&
				(type.IsStructure () || type.IsClass);
		}

		public static NativeAssemblerStructContextDataProvider? GetDataProvider (this Type t)
		{
			var attr = t.GetCustomAttribute<NativeAssemblerStructContextDataProviderAttribute> ();
			if (attr == null) {
				return null;
			}

			return Activator.CreateInstance (attr.Type) as NativeAssemblerStructContextDataProvider;
		}
	}
}
