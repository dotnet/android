using System;
using System.Collections.Generic;

namespace Java.Interop {

	[Obsolete ("This class is obsolete and will be removed in the next release. Use [Register] on Java peer types instead of explicit type registration.")]
	public static partial class TypeManager {
		class TypeNameComparer : IComparer<string> {
			public int Compare (string? x, string? y)
			{
				if (object.ReferenceEquals (x, y))
					return 0;
				if (x == null)
					return -1;
				if (y == null)
					return 1;

				int xe = x.IndexOf (':');
				int ye = y.IndexOf (':');

				int r  = string.CompareOrdinal (x, 0, y, 0, System.Math.Max (xe, ye));
				if (r != 0)
					return r;

				if (xe >= 0 && ye >= 0)
					return xe - ye;

				if (xe < 0)
					return x.Length - ye;

				return xe - y.Length;
			}
		}

		static readonly TypeNameComparer JavaNameComparer = new TypeNameComparer ();

		public static string? LookupTypeMapping (string[] mappings, string javaType)
		{
			int i = Array.BinarySearch (mappings, javaType, JavaNameComparer);
			if (i < 0)
				return null;
			int c = mappings [i].IndexOf (':');
			return mappings [i].Substring (c+1);
		}

		public static void RegisterType (string java_class, Type t)
		{
			throw new NotSupportedException ("Explicit Java type registration is not supported with the trimmable type map. Use [Register] on Java peer types instead.");
		}

		const string TypeRegistrationNotSupported =
			"Java package type registration is no longer supported. Java-to-managed type resolution now goes through the trimmable type map.";

		// The package-based Java-to-managed type registration fallback was removed
		// (https://github.com/dotnet/android/issues/11663); type resolution now goes
		// through the trimmable type map, and the generator no longer emits
		// the `Java.Interop.__TypeRegistrations` class that called these methods. These
		// shipped public APIs are retained for binary compatibility but now throw, as
		// they can no longer register anything.
		public static void RegisterPackage (string package, Converter<string, Type> lookup)
		{
			throw new NotSupportedException (TypeRegistrationNotSupported);
		}

		public static void RegisterPackages (string[] packages, Converter<string, Type?>[] lookups)
		{
			throw new NotSupportedException (TypeRegistrationNotSupported);
		}

	}
}
