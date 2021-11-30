using System;
using System.Collections.Generic;
using System.Linq;

namespace Java.Interop.Tools.JavaTypeSystem.Models
{
	public abstract class JavaTypeModel : IJavaResolvable
	{
		/// <summary>
		/// Only the type's name, does not include declaring type name for nested type.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// Includes declaring type name(s) if type is nested (period separator). ex: Manifest.permission
		/// </summary>
		public string NestedName { get; set; }

		public string Visibility { get; }
		public bool IsAbstract { get; }
		public bool IsFinal { get; }
		public string Deprecated { get; }
		public bool IsStatic { get; }
		public string ExtendedJniSignature { get; }
		public bool IsReferencedOnly { get; internal set; }

		public JavaPackage Package { get; }
		public JavaTypeModel? DeclaringType { get; internal set; }
		public List<JavaTypeModel> NestedTypes { get; } = new List<JavaTypeModel> ();

		public JavaTypeParameters TypeParameters { get; }
		public List<JavaImplementsModel> Implements { get; } = new List<JavaImplementsModel> ();
		public List<JavaTypeReference> ImplementsModels { get; } = new List<JavaTypeReference> ();
		public List<JavaMethodModel> Methods { get; } = new List<JavaMethodModel> ();
		public List<JavaFieldModel> Fields { get; } = new List<JavaFieldModel> ();

		public Dictionary<string, string> PropertyBag { get; } = new Dictionary<string, string> ();

		protected JavaTypeModel (JavaPackage javaPackage, string javaNestedName, string javaVisibility, bool javaAbstract, bool javaFinal, string deprecated, bool javaStatic, string jniSignature)
		{
			Package = javaPackage;
			NestedName = javaNestedName.Replace ('$', '.');
			Name = NestedName.LastSubset ('.');
			Visibility = javaVisibility;
			IsAbstract = javaAbstract;
			IsFinal = javaFinal;
			Deprecated = deprecated;
			IsStatic = javaStatic;
			ExtendedJniSignature = jniSignature;

			TypeParameters = new JavaTypeParameters (this);
		}

		/// <summary>
		/// Returns string containing package name, declaring type name, and type's name. (ex: 'java.util.ArrayList.Keys')
		/// </summary>
		public string FullName {
			get {
				if (DeclaringType != null)
					return $"{DeclaringType.FullName}.{Name}";

				if (Package.Name.Length > 0)
					return $"{Package.Name}.{NestedName}";

				return Name;
			}
		}

		public bool IsNested => NestedName.Contains ('.');

		public virtual void Resolve (JavaTypeCollection types, ICollection<JavaUnresolvableModel> unresolvables)
		{
			var type_parameters = GetApplicableTypeParameters ().ToArray ();

			// Resolve any implemented interfaces
			foreach (var i in Implements) {
				try {
					var implements = types.ResolveTypeReference (TypeResolutionOptions.ResolveGenerics ? i.NameGeneric : i.Name, type_parameters);

					if (implements is null)
						throw new Exception ();

					ImplementsModels.Add (implements);
				} catch (JavaTypeResolutionException) {
					unresolvables.Add (new JavaUnresolvableModel (this, i.NameGeneric, UnresolvableType.ImplementsType));

					throw;
				}
			}

			// Resolve members
			foreach (var method in Methods)
				method.Resolve (types, unresolvables);

			foreach (var field in Fields)
				field.Resolve (types, unresolvables);

			// Resolve nested types
			foreach (var child in NestedTypes)
				child.Resolve (types, unresolvables);
		}

		// Return type's type parameters, plus type parameters for any types this is nested in.
		public IEnumerable<JavaTypeParameter> GetApplicableTypeParameters ()
		{
			foreach (var jtp in TypeParameters)
				yield return jtp;

			if (DeclaringType != null)
				foreach (var jtp in DeclaringType.GetApplicableTypeParameters ())
					yield return jtp;
		}
	}
}
