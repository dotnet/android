using System;
using System.Collections.Generic;
using System.Linq;

namespace Java.Interop.Tools.JavaTypeSystem.Models
{
	public class JavaMethodModel : JavaMemberModel
	{
		public string Return { get; }
		public string ReturnGeneric { get; }
		public bool IsAbstract { get; }
		public bool IsBridge { get; }
		public string ReturnJni { get; }
		public bool IsSynthetic { get; }
		public bool IsSynchronized { get; }
		public bool IsNative { get; }
		public bool ReturnNotNull { get; }

		public JavaTypeParameters TypeParameters { get; }
		public JavaTypeReference? ReturnTypeModel { get; private set; }
		public JavaMethodModel? BaseMethod { get; set; }
		public List<JavaParameterModel> Parameters { get; } = new List<JavaParameterModel> ();
		public List<JavaExceptionModel> Exceptions { get; } = new List<JavaExceptionModel> ();

		public JavaMethodModel (string javaName, string javaVisibility, bool javaAbstract, bool javaFinal, bool javaStatic, string javaReturn, JavaTypeModel javaDeclaringType, string deprecated, string jniSignature, bool isSynthetic, bool isBridge, string returnJni, bool isNative, bool isSynchronized, bool returnNotNull, string? annotatedVisibility)
			: base (javaName, javaStatic, javaFinal, javaVisibility, javaDeclaringType, deprecated, jniSignature, annotatedVisibility)
		{
			IsAbstract = javaAbstract;
			Return = javaReturn;
			ReturnGeneric = javaReturn;
			IsBridge = isBridge;
			IsSynthetic = isSynthetic;
			ReturnJni = returnJni;
			IsNative = isNative;
			IsSynchronized = isSynchronized;
			ReturnNotNull = returnNotNull;

			TypeParameters = new JavaTypeParameters (this);
		}

		public override void Resolve (JavaTypeCollection types, ICollection<JavaUnresolvableModel> unresolvables)
		{
			if (Name.Contains ('$')) {
				unresolvables.Add (new JavaUnresolvableModel (this, "$", UnresolvableType.DollarSign));
				return;
			}

			var type_parameters = GetApplicableTypeParameters ().ToArray ();

			try {
				ReturnTypeModel = types.ResolveTypeReference (Return, type_parameters);
			} catch (JavaTypeResolutionException) {
				unresolvables.Add (new JavaUnresolvableModel (this, Return, UnresolvableType.ReturnType));

				return;
			}

			foreach (var p in Parameters.OfType<JavaParameterModel> ())
				p.Resolve (types, unresolvables);
		}

		// Return method's type parameters, plus type parameters for any declaring type(s).
		public IEnumerable<JavaTypeParameter> GetApplicableTypeParameters ()
		{
			foreach (var jtp in TypeParameters)
				yield return jtp;

			if (DeclaringType != null)
				foreach (var jtp in DeclaringType.GetApplicableTypeParameters ())
					yield return jtp;
		}

		public void FindBaseMethod (JavaClassModel? type)
		{
			if (type is null)
				return;

			var pt = (JavaClassModel)DeclaringType;

			var candidate = type.Methods.FirstOrDefault (p => p.Name == Name && IsImplementing (this, p, pt.GenericInheritanceMapping ?? throw new InvalidOperationException ($"missing {nameof (pt.GenericInheritanceMapping)}!")));

			if (candidate != null) {
				BaseMethod = candidate;

				for (var i = 0; i < candidate.Parameters.Count; i++)
					if (candidate.Parameters [i].TypeModel?.ReferencedTypeParameter != null && Parameters [i].TypeModel?.ReferencedTypeParameter == null)
						Parameters [i].InstantiatedGenericArgumentName = candidate.Parameters [i].TypeModel?.ReferencedTypeParameter?.Name;

				return;
			}

			if (type.BaseTypeReference?.ReferencedType is JavaClassModel klass)
				FindBaseMethod (klass);
		}

		// It should detect implementation method for:
		//	- equivalent implementation
		//	- generic instantiation
		//	- TODO: variance
		//	- TODO?: array indicator fixup ("T..." should match "T[]")
		public static bool IsImplementing (JavaMethodModel derived, JavaMethodModel basis, IDictionary<JavaTypeReference, JavaTypeReference> genericInstantiation)
		{
			if (genericInstantiation == null)
				throw new ArgumentNullException ("genericInstantiation");

			if (basis.Name != derived.Name)
				return false;

			if (basis.Parameters.Count != derived.Parameters.Count)
				return false;

			if (basis.Parameters.Zip (derived.Parameters, (bp, dp) => IsParameterAssignableTo (dp, bp, derived, basis, genericInstantiation)).All (v => v))
				return true;
			return false;
		}

		static bool IsParameterAssignableTo (JavaParameterModel dp, JavaParameterModel bp, JavaMethodModel derived, JavaMethodModel basis, IDictionary<JavaTypeReference, JavaTypeReference> genericInstantiation)
		{
			// If type names are equivalent, they simply match... except that the generic type parameter names match.
			// Generic type arguments need more check, so do not examine them just by name.
			//
			// FIXME: It is likely that this check should NOT result in "this method is not an override",
			// but rather like "this method is an override, but it should be still generated in the resulting XML".
			// For example, this results in that java.util.EnumMap#put() is NOT an override of
			// java.util.AbstractMap#put(), it is an override, not just that it is still generated in the XML.
			if (bp.TypeModel?.ReferencedTypeParameter != null && dp.TypeModel?.ReferencedTypeParameter != null &&
			    bp.TypeModel.ReferencedTypeParameter.ToString () != dp.TypeModel.ReferencedTypeParameter.ToString ())
				return false;
			if (bp.GenericType == dp.GenericType)
				return true;

			if (bp.TypeModel?.ArrayPart != bp.TypeModel?.ArrayPart)
				return false;

			// if base is type with generic type parameters and derived is without any generic args, that's OK.
			// java.lang.Class should match java.lang.Class<T>.
			if (bp.TypeModel?.ReferencedType != null && dp.TypeModel?.ReferencedType != null &&
			    bp.TypeModel?.ReferencedType.FullName == dp.TypeModel?.ReferencedType.FullName &&
			    dp.TypeModel?.TypeParameters == null)
				return true;

			// generic instantiation check.
			var baseGTP = bp.TypeModel?.ReferencedTypeParameter;
			if (baseGTP != null) {
				if (baseGTP.Parent?.DeclaringMethod != null && IsConformantType (baseGTP, dp.TypeModel))
					return true;
				var k = genericInstantiation.Keys.FirstOrDefault (tr => bp.TypeModel?.Equals (tr) ?? false);
				if (k == null)
					// the specified generic type parameter is not part of
					// the mappings e.g. non-instantiated ones.
					return false;
				if (genericInstantiation [k].Equals (dp.TypeModel))
					// the specified generic type parameter exactly matches
					// whatever specified at the derived method.
					return true;
			}

			// FIXME: implement variance check.

			return false;
		}

		static bool IsConformantType (JavaTypeParameter typeParameter, JavaTypeReference? examinedType)
		{
			if (!typeParameter.GenericConstraints.Any ())
				return true;
			// FIXME: implement correct generic constraint conformance check.
			//Log.LogDebug ("NOTICE: generic constraint conformance check is not implemented, so the type might be actually compatible. Type parameter: {0}{1}, examined type: {2}",
			//		typeParameter.Name, typeParameter.Parent?.ParentMethod?.Name ?? typeParameter.Parent?.ParentType?.Name, examinedType);
			return false;
		}

		public override string ToString () => "[Method] " + ToStringHelper (Return, Name, TypeParameters);

		// Content of this value is not stable.
		public string ToStringHelper (string? returnType, string? name, JavaTypeParameters? typeParameters)
		{
			return string.Format ("{0}{1}{2}{3}{4}{5}({6})",
				returnType,
				returnType == null ? null : " ",
				name,
				typeParameters?.Any () == true ? "<" : null,
				typeParameters?.Any () == true ? string.Join (", ", typeParameters) : null,
				typeParameters?.Any () == true ? ">" : null,
				string.Join (", ", Parameters));
		}
	}
}
