using System;
using System.Collections.Generic;

using Mono.Cecil;

namespace Xamarin.Android.Tasks
{
	static class MarshalMethodsHelpers
	{
		// From: https://docs.microsoft.com/en-us/dotnet/framework/interop/blittable-and-non-blittable-types
		static readonly HashSet<string> blittableTypes = new HashSet<string> (StringComparer.Ordinal) {
			"System.Byte",
			"System.SByte",
			"System.Int16",
			"System.UInt16",
			"System.Int32",
			"System.UInt32",
			"System.Int64",
			"System.UInt64",
			"System.IntPtr",
			"System.UIntPtr",
			"System.Single",
			"System.Double",
		};

		public static bool IsBlittable (this TypeReference type)
		{
			if (type == null) {
				throw new ArgumentNullException (nameof (type));
			}

			return blittableTypes.Contains (type.FullName);
		}
	}
}
