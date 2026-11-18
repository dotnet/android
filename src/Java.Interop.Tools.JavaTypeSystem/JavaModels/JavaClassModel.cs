using System;
using System.Collections.Generic;
using System.Linq;

namespace Java.Interop.Tools.JavaTypeSystem.Models
{
	public class JavaClassModel : JavaTypeModel
	{
		IDictionary<JavaTypeReference, JavaTypeReference>? generic_inheritance_mapping;

		public string BaseType { get; }
		public string BaseTypeGeneric { get; }
		public string BaseTypeJni { get; }

		public JavaTypeReference? BaseTypeReference { get; private set; }
		public List<JavaConstructorModel> Constructors { get; } = new List<JavaConstructorModel> ();

		public JavaClassModel (JavaPackage javaPackage, string javaNestedName, string javaVisibility, bool javaAbstract, bool javaFinal, string javaBaseType, string javaBaseTypeGeneric, string javaDeprecated, bool javaStatic, string jniSignature, string baseTypeJni, string annotatedVisibility) :
			base (javaPackage, javaNestedName, javaVisibility, javaAbstract, javaFinal, javaDeprecated, javaStatic, jniSignature, annotatedVisibility)
		{
			BaseType = javaBaseType;
			BaseTypeGeneric = javaBaseTypeGeneric;
			BaseTypeJni = baseTypeJni;
		}

		public override void Resolve (JavaTypeCollection types, ICollection<JavaUnresolvableModel> unresolvables)
		{
			var type_parameters = GetApplicableTypeParameters ().ToArray ();

			// Resolve base class
			if (FullName != "java.lang.Object" && FullName != "java.lang.Throwable") {
				try {
					BaseTypeReference = types.ResolveTypeReference (TypeResolutionOptions.ResolveGenerics ? BaseTypeGeneric : BaseType, type_parameters);
				} catch (JavaTypeResolutionException) {
					unresolvables.Add (new JavaUnresolvableModel (this, BaseTypeGeneric, UnresolvableType.BaseType));

					throw;
				}

				// Apparently some Java obfuscator sets a class's base type to itself, which
				// results in a stack overflow. Check for this case and remove the offending type.
				if (BaseTypeReference.ReferencedType is JavaClassModel jcm && jcm == this) {
					unresolvables.Add (new JavaUnresolvableModel (this, BaseTypeGeneric, UnresolvableType.InvalidBaseType));
					throw new JavaTypeResolutionException (BaseTypeGeneric);
				}

				// We don't resolve reference-only types by default, so if our base class
				// is a reference only type, we need to force it to resolve here. This will be
				// needed later when we attempt to resolve base methods.
				try {
					if (BaseTypeReference.ReferencedType is JavaClassModel klass && klass.FullName != "java.lang.Object" && klass.BaseTypeReference is null && klass.IsReferencedOnly)
						klass.Resolve (types, unresolvables);
				} catch (JavaTypeResolutionException) {
					// Ignore
				}
			}

			// Resolve constructors
			foreach (var ctor in Constructors)
				ctor.Resolve (types, unresolvables);

			base.Resolve (types, unresolvables);
		}

		public virtual void ResolveBaseMembers ()
		{
			var klass = BaseTypeReference?.ReferencedType as JavaClassModel;

			foreach (var method in Methods)
				method.FindBaseMethod (klass);

			foreach (var nested in NestedTypes.OfType<JavaClassModel> ())
				nested.ResolveBaseMembers ();
		}

		public IDictionary<JavaTypeReference, JavaTypeReference>? GenericInheritanceMapping {
			get {
				PrepareGenericInheritanceMapping ();
				return generic_inheritance_mapping;
			}
		}

		void PrepareGenericInheritanceMapping ()
		{
			if (generic_inheritance_mapping != null)
				return; // already done.

			var bt = BaseTypeReference?.ReferencedType as JavaClassModel;

			if (BaseTypeReference is null || bt is null) {
				generic_inheritance_mapping = new Dictionary<JavaTypeReference, JavaTypeReference> ();
				return;
			}

			// begin processing from the base class.
			bt.PrepareGenericInheritanceMapping ();

			if (BaseTypeReference.TypeParameters is null) {
				generic_inheritance_mapping = new Dictionary<JavaTypeReference, JavaTypeReference> ();
				return;
			}

			if (BaseTypeReference.ReferencedType is null || BaseTypeReference.ReferencedType?.TypeParameters.Count == 0) {
				// FIXME: I guess this should not happen. But this still happens.
				//Log.LogWarning ("Warning: '{0}' is referenced as base type of '{1}' and expected to have generic type parameters, but it does not.", cls.ExtendsGeneric, cls.FullName);
				generic_inheritance_mapping = new Dictionary<JavaTypeReference, JavaTypeReference> ();
				return;
			}

			// NRT - This is checked above but compiler can't figure it out
			if (BaseTypeReference.ReferencedType!.TypeParameters.Count != BaseTypeReference.TypeParameters.Count)
				throw new Exception (string.Format ("On {0}.{1}, referenced generic arguments count do not match the base type parameters definition",
					DeclaringType?.Name, Name));

			generic_inheritance_mapping = new Dictionary<JavaTypeReference, JavaTypeReference> ();
			foreach (var kvp in BaseTypeReference.ReferencedType.TypeParameters.Zip (
					BaseTypeReference.TypeParameters,
					(def, use) => new KeyValuePair<JavaTypeParameter, JavaTypeReference> (def, use))
					.Where (p => p.Value.ReferencedTypeParameter == null || p.Key.Name != p.Value.ReferencedTypeParameter.Name))
				generic_inheritance_mapping.Add (new JavaTypeReference (kvp.Key, null), kvp.Value);
		}

		public override string ToString () => $"[Class] {FullName}";
	}
}
