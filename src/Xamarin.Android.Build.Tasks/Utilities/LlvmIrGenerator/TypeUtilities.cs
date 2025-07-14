using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Provides utility methods for working with types in the context of LLVM IR generation.
	/// </summary>
	static class TypeUtilities
	{
		/// <summary>
		/// Gets the short name of a type (without namespace or containing type information).
		/// </summary>
		/// <param name="type">The type to get the short name for.</param>
		/// <returns>The short name of the type.</returns>
		/// <exception cref="InvalidOperationException">Thrown when the type is unnamed or has an invalid name.</exception>
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

		/// <summary>
		/// Determines whether a type represents a structure (value type that is not an enum, primitive, or array).
		/// </summary>
		/// <param name="type">The type to check.</param>
		/// <returns>true if the type is a structure; otherwise, false.</returns>
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

		/// <summary>
		/// Determines whether a structure member represents an LLVM IR structure.
		/// </summary>
		/// <param name="smi">The structure member info to check.</param>
		/// <param name="cache">The LLVM IR type cache.</param>
		/// <returns>true if the member represents an IR structure; otherwise, false.</returns>
		public static bool IsIRStruct (this StructureMemberInfo smi, LlvmIrTypeCache cache)
		{
			Type type = smi.MemberType;

			// type.IsStructure() handles checks for primitive types, enums etc
			return
				type != typeof(string) &&
				!smi.Info.IsInlineArray (cache) &&
				!smi.Info.IsNativePointer (cache) &&
				(type.IsStructure () || type.IsClass);
		}

		/// <summary>
		/// Gets the data provider for a type if it has a NativeAssemblerStructContextDataProviderAttribute.
		/// </summary>
		/// <param name="t">The type to get the data provider for.</param>
		/// <returns>The data provider instance, or null if the type doesn't have a data provider attribute.</returns>
		public static NativeAssemblerStructContextDataProvider? GetDataProvider (this Type t)
		{
			var attr = t.GetCustomAttribute<NativeAssemblerStructContextDataProviderAttribute> ();
			if (attr == null) {
				return null;
			}

			return Activator.CreateInstance (attr.Type) as NativeAssemblerStructContextDataProvider;
		}

		/// <summary>
		/// Determines whether a type is marked with the NativeClassAttribute.
		/// </summary>
		/// <param name="t">The type to check.</param>
		/// <returns>true if the type has the NativeClassAttribute; otherwise, false.</returns>
		public static bool IsNativeClass (this Type t)
		{
			var attr = t.GetCustomAttribute<NativeClassAttribute> ();
			return attr != null;
		}

		/// <summary>
		/// Determines whether a type implements a specific interface.
		/// </summary>
		/// <param name="type">The type to check.</param>
		/// <param name="requiredIfaceType">The interface type to check for.</param>
		/// <returns>true if the type implements the interface; otherwise, false.</returns>
		public static bool ImplementsInterface (this Type type, Type requiredIfaceType)
		{
			if (type == null || requiredIfaceType == null) {
				return false;
			}

			if (type == requiredIfaceType) {
				return true;
			}

			bool generic = requiredIfaceType.IsGenericType;
			foreach (Type iface in type.GetInterfaces ()) {
				if (iface == requiredIfaceType) {
					return true;
				}

				if (generic) {
					if (!iface.IsGenericType) {
						continue;
					}

					if (iface.GetGenericTypeDefinition () == requiredIfaceType.GetGenericTypeDefinition ()) {
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Determines whether a type is a StructureInstance generic type and gets the structure type.
		/// </summary>
		/// <param name="type">The type to check.</param>
		/// <param name="structureType">When this method returns, contains the structure type if the type is a StructureInstance; otherwise, null.</param>
		/// <returns>true if the type is a StructureInstance; otherwise, false.</returns>
		public static bool IsStructureInstance (this Type type, out Type? structureType)
		{
			structureType = null;
			if (!type.IsGenericType) {
				return false;
			}

			if (type.GetGenericTypeDefinition () != typeof(StructureInstance<>)) {
				return false;
			}

			structureType = type.GetGenericArguments ()[0];
			return true;
		}

		/// <summary>
		/// Return element type of a single-dimensional (with one exception, see below) array.  Parameter <paramref name="type"/> **MUST**
		/// correspond to one of the following array types: T[], ICollection&lt;T&gt; or IDictionary&lt;string, string&gt;.  The latter is
		/// used to comfortably represent name:value arrays, which are output as single dimensional arrays in the native code.
		/// </summary>
		/// <param name="type">The array type to get the element type for.</param>
		/// <returns>The element type of the array.</returns>
		/// <exception cref="System.InvalidOperationException">
		/// Thrown when <paramref name="type"/> is not one of the array types listed above.
		/// </exception>
		public static Type GetArrayElementType (this Type type)
		{
			if (type.IsArray) {
				return type.GetElementType ();
			}

			if (!type.IsGenericType) {
				throw WrongTypeException ();
			}

			Type genericType = type.GetGenericTypeDefinition ();
			if (genericType.ImplementsInterface (typeof(ICollection<>))) {
				Type[] genericArgs = type.GetGenericArguments ();
				return genericArgs[0];
			}

			if (!genericType.ImplementsInterface (typeof(IDictionary<string, string>))) {
				throw WrongTypeException ();
			}

			return typeof(string);

			// Dictionary
			Exception WrongTypeException () => new InvalidOperationException ($"Internal error: type '{type}' is not an array, ICollection<T> or IDictionary<string, string>");
		}

		/// <summary>
		/// Determine whether type represents an array, in our understanding.  That means the type has to be
		/// a standard single-dimensional language array (i.e. <c>T[]</c>), implement ICollection&lt;T&gt; together with ICollection or,
		/// as a special case for name:value pair collections, implement IDictionary&lt;string, string&gt;
		/// </summary>
		/// <param name="t">The type to check.</param>
		/// <returns>true if the type represents an array; otherwise, false.</returns>
		/// <exception cref="NotSupportedException">Thrown when multi-dimensional arrays are encountered.</exception>
		public static bool IsArray (this Type t)
		{
			if (t.IsPrimitive) {
				return false;
			}

			if (t == typeof(string)) {
				return false;
			}

			if (t.IsArray) {
				if (t.GetArrayRank () > 1) {
					throw new NotSupportedException ("Internal error: multi-dimensional arrays aren't supported");
				}

				return true;
			}

			// TODO: cache results here
			// IDictionary<string, string> is a special case for name:value string arrays which we use for some constructs.
			return (t.ImplementsInterface (typeof(ICollection<>)) || t.ImplementsInterface (typeof(ICollection))) ||
				t.ImplementsInterface (typeof(IDictionary<string, string>));
		}
	}
}
