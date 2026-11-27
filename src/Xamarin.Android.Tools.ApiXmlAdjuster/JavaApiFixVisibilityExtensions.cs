using System;
using System.Linq;

namespace Xamarin.Android.Tools.ApiXmlAdjuster
{
	public static class JavaApiFixVisibilityExtensions
	{
		public static string? GetVisibleTypeName (this JavaParameter parameter)
		{
			var r = GetVisibleNonSpecialType (parameter);
			return r != null ? r.ToString () : parameter.Type;
		}

		public static string? GetVisibleReturnTypeName (this JavaMethod method)
		{
			var r = GetVisibleNonSpecialReturnType (method);
			return r != null ? r.ToString () : method.Return;
		}

		public static JavaTypeReference? GetVisibleNonSpecialType (this JavaParameter parameter)
		{
			return GetVisibleNonSpecialType (parameter.Parent, parameter.ResolvedType);
		}

		public static JavaTypeReference? GetVisibleNonSpecialReturnType (this JavaMethod method)
		{
			return GetVisibleNonSpecialType (method, method.ResolvedReturnType);
		}

		static JavaTypeReference? GetVisibleNonSpecialType (this JavaMethodBase? method, JavaTypeReference? r)
		{
			if (r == null || r.SpecialName != null || r.ReferencedTypeParameter != null || r.ArrayPart != null)
				return null;
			var requiredVisibility = method?.Visibility == "public" && method.Parent?.Visibility == "public" ? "public" : method?.Visibility;
			for (var t = r; t != null; t = (t.ReferencedType as JavaClass)?.ResolvedExtends) {
				if (t.ReferencedType == null)
					break;
				if (IsAcceptableVisibility (required: requiredVisibility, actual: t.ReferencedType.Visibility))
					return t;
			}
			return null;
		}

		static bool IsAcceptableVisibility (string? required, string? actual)
		{
			if (required == "public")
				return actual == "public";
			else
				return true;
		}
	}
}
