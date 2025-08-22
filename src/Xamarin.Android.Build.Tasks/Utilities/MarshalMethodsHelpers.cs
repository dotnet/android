#nullable enable
using System;
using System.Collections.Generic;

using Mono.Cecil;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Provides helper methods for working with marshal methods, particularly for
	/// determining type blittability and compatibility with native interop.
	/// </summary>
	static class MarshalMethodsHelpers
	{
		/// <summary>
		/// Set of .NET types that are blittable (can be directly passed between managed and native code
		/// without marshaling). Based on Microsoft's documentation for blittable and non-blittable types.
		/// </summary>
		/// <remarks>
		/// Reference: https://docs.microsoft.com/en-us/dotnet/framework/interop/blittable-and-non-blittable-types
		/// Blittable types have the same representation in managed and unmanaged memory and can be
		/// passed directly without marshaling overhead.
		/// </remarks>
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

		/// <summary>
		/// Extension method that determines whether a type reference represents a blittable type.
		/// Blittable types can be passed directly between managed and native code without marshaling,
		/// making them ideal for use in marshal methods and [UnmanagedCallersOnly] methods.
		/// </summary>
		/// <param name="type">The type reference to check for blittability.</param>
		/// <returns>true if the type is blittable; otherwise, false.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
		/// <remarks>
		/// This method only checks the type name against a predefined set of known blittable types.
		/// It does not perform deep analysis of structs or other complex types that might also be blittable.
		/// </remarks>
		public static bool IsBlittable (this TypeReference type)
		{
			if (type == null) {
				throw new ArgumentNullException (nameof (type));
			}

			return blittableTypes.Contains (type.FullName);
		}
	}
}
