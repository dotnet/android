using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

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

		public static NativeAssemblerStructContextDataProvider? GetDataProvider (this Type t)
		{
			var attr = t.GetCustomAttribute<NativeAssemblerStructContextDataProviderAttribute> ();
			if (attr == null) {
				return null;
			}

			return Activator.CreateInstance (attr.Type) as NativeAssemblerStructContextDataProvider;
		}

		public static bool IsNativeClass (this Type t)
		{
			var attr = t.GetCustomAttribute<NativeClassAttribute> ();
			return attr != null;
		}

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
			return (t.ImplementsInterface (typeof(ICollection<>)) && t.ImplementsInterface (typeof(ICollection))) ||
				t.ImplementsInterface (typeof(IDictionary<string, string>));
		}
	}
}
