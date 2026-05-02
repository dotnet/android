using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Linq;

namespace Android.Runtime {

	[Obsolete ("Use Java.Interop.TypeManager")]
	[RequiresDynamicCode ("Legacy TypeManager uses runtime type lookup, uninitialized object creation, and convention-based invoker type construction.")]
	[RequiresUnreferencedCode ("Legacy TypeManager uses runtime type lookup and native typemap entries that cannot be statically analyzed.")]
	public static partial class TypeManager {

		public static string? LookupTypeMapping (string[] mappings, string javaType)
		{
			return Java.Interop.TypeManager.LookupTypeMapping (mappings, javaType);
		}

		public static void RegisterType (string java_class, Type t)
		{
			Java.Interop.TypeManager.RegisterType (java_class, t);
		}

		public static void RegisterPackage (string package, Converter<string, Type> lookup)
		{
			Java.Interop.TypeManager.RegisterPackage (package, lookup);
		}

		public static void RegisterPackages (string[] packages, Converter<string, Type>[] lookups)
		{
			Java.Interop.TypeManager.RegisterPackages (packages, lookups);
		}
	}
}
