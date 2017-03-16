using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Type = Mono.Cecil.TypeDefinition;//IKVM.Reflection.Type;

namespace Xamarin.Android.Tools.JavaDocToMdoc
{
	static class TypeUtilities
	{
		public static string FlattenName (this Type type)
		{
			return type.Name.Replace ('.', '+'); // ugh, this is ugly.
		}

		public static string FlattenFullName (this Type type)
		{
			return (type.DeclaringType != null ? FlattenFullName (type.DeclaringType) + '.' : null) + type.FullName;
		}

		internal static IEnumerable<Type> FlattenTypeHierarchy (this Type type)
		{
			yield return type;
			foreach (var nt in type.NestedTypes.SelectMany (_ => _.FlattenTypeHierarchy ()))
				yield return nt;
		}

		// It should not generate any docs for invokers and implementors.
		public static bool ShouldExcludeType (Type type, string registeredTypeName)
		{
			return type.Name.EndsWith ("Invoker", StringComparison.Ordinal) && !registeredTypeName.EndsWith ("Invoker", StringComparison.Ordinal)
				   || type.Name.EndsWith ("Implementor", StringComparison.Ordinal) && !registeredTypeName.EndsWith ("Implementor", StringComparison.Ordinal)
				   || registeredTypeName.StartsWith ("mono/", StringComparison.Ordinal) && registeredTypeName.EndsWith ("Implementor", StringComparison.Ordinal);
		}

		public static string StripGenericArgument (string signature)
		{
			int idx = signature.IndexOf ('#');
			if (idx < 0)
				return StripGenericArgument (signature, 0);
			else
				return StripGenericArgument (signature.Substring (idx), 0);
		}

		static string StripGenericArgument (string signature, int idx)
		{
			if (signature.IndexOf ('<') < 0)
				return signature;
			var sb = new StringBuilder ();
			while (idx < signature.Length) {
				var next = signature.IndexOf ('<', idx);
				if (next < 0)
					break;
				sb.Append (signature.Substring (idx, next - idx));
				int open = 0;
				for (idx = next + 1; idx < signature.Length; idx++) {
					if (signature [idx] == '<')
						open++;
					else if (signature [idx] == '>' && open-- == 0)
						break;
				}
				idx++;
			}
			if (idx < signature.Length)
				sb.Append (signature.Substring (idx));
			return sb.ToString ();
		}
	}
}
