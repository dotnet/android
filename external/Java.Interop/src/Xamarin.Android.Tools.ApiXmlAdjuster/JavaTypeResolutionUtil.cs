using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tools.ApiXmlAdjuster
{
	public static class JavaTypeResolutionUtil
	{

		static V Get<K,V> (this IDictionary<K,V> dic, K key)
		{
			V v;
			return dic.TryGetValue (key, out v) ? v : default (V);
		}

		// It should detect implementation method for:
		//	- equivalent implementation
		//	- generic instantiation
		//	- TODO: variance
		//	- TODO?: array indicator fixup ("T..." should match "T[]")
		public static bool IsImplementing (this JavaMethod derived, JavaMethod basis, IDictionary<JavaTypeReference,JavaTypeReference> genericInstantiation)
		{
			if (genericInstantiation == null)
				throw new ArgumentNullException ("genericInstantiation");

			if (basis.Name != derived.Name)
				return false;

			if (basis.Parameters.Count != derived.Parameters.Count)
				return false;

			if (basis.Parameters.Zip (derived.Parameters, (bp, dp) => dp.IsParameterAssignableTo (bp, derived, basis, genericInstantiation)).All (v => v))
				return true;
			return false;
		}

		static bool IsParameterAssignableTo (this JavaParameter dp, JavaParameter bp, JavaMethod derived, JavaMethod basis, IDictionary<JavaTypeReference,JavaTypeReference> genericInstantiation)
		{
			// If type names are equivalent, they simply match... except that the generic type parameter names match.
			// Generic type arguments need more check, so do not examine them just by name.
			//
			// FIXME: It is likely that this check should NOT result in "this method is not an override",
			// but rather like "this method is an override, but it should be still generated in the resulting XML".
			// For example, this results in that java.util.EnumMap#put() is NOT an override of
			// java.util.AbstractMap#put(), it is an override, not just that it is still generated in the XML.
			if (bp.ResolvedType.ReferencedTypeParameter != null && dp.ResolvedType.ReferencedTypeParameter != null &&
			    bp.ResolvedType.ReferencedTypeParameter.ToString () != dp.ResolvedType.ReferencedTypeParameter.ToString ())
				return false;
			if (bp.Type == dp.Type)
				return true;

			if (bp.ResolvedType.ArrayPart != bp.ResolvedType.ArrayPart)
				return false;
			
			// if base is type with generic type parameters and derived is without any generic args, that's OK.
			// java.lang.Class should match java.lang.Class<T>.
			if (bp.ResolvedType.ReferencedType != null && dp.ResolvedType.ReferencedType != null &&
			    bp.ResolvedType.ReferencedType.FullName == dp.ResolvedType.ReferencedType.FullName &&
			    dp.ResolvedType.TypeParameters == null)
				return true;
				
			// generic instantiation check.
			var baseGTP = bp.ResolvedType.ReferencedTypeParameter;
			if (baseGTP != null) {
				if (baseGTP.Parent.ParentMethod != null && baseGTP.IsConformantType (dp.ResolvedType))
					return true;
				var k = genericInstantiation.Keys.FirstOrDefault (tr => bp.ResolvedType.Equals (tr));
				if (k == null)
					// the specified generic type parameter is not part of
					// the mappings e.g. non-instantiated ones.
					return false;
				if (genericInstantiation [k].Equals (dp.ResolvedType))
					// the specified generic type parameter exactly matches
					// whatever specified at the derived method.
					return true;
			}

			// FIXME: implement variance check.

			return false;
		}
		
		static bool IsConformantType (this JavaTypeParameter typeParameter, JavaTypeReference examinedType)
		{
			if (typeParameter.GenericConstraints == null)
				return true;
			// FIXME: implement correct generic constraint conformance check.
			Log.LogDebug ("NOTICE: generic constraint conformance check is not implemented, so the type might be actually compatible. Type parameter: {0}{1}, examined type: {2}",
			                               typeParameter.Name, typeParameter.Parent.ParentMethod?.Name ?? typeParameter.Parent.ParentType?.Name, examinedType);
			return false;
		}
	}
}

