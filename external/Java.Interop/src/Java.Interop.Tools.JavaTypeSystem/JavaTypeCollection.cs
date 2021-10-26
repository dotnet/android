using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Java.Interop.Tools.JavaTypeSystem.Models
{
	public class JavaTypeCollection
	{
		readonly Dictionary<string, JavaPackage> packages = new Dictionary<string, JavaPackage> ();
		readonly Dictionary<string, JavaTypeModel> types = new Dictionary<string, JavaTypeModel> ();
		readonly Dictionary<string, JavaTypeModel> types_flattened = new Dictionary<string, JavaTypeModel> ();
		readonly Dictionary<string, JavaTypeModel> referenced_types_flattened = new Dictionary<string, JavaTypeModel> ();
		readonly Dictionary<string, JavaTypeModel> built_in_types = new Dictionary<string, JavaTypeModel> ();

		// Expose ReadOnly versions so internal type management cannot be bypassed
		public IReadOnlyDictionary<string, JavaPackage> Packages => packages;
		public IReadOnlyDictionary<string, JavaTypeModel> Types => types;

		public IReadOnlyDictionary<string, JavaTypeModel> TypesFlattened => types_flattened;

		// We only keep a flattened version of reference types. The main issue is that the Managed nesting
		// may not match the Java nesting (ie: types nested in Java interfaces that C# originally didn't support).
		// Since we don't actually *need* this model to be nested it's simpler to keep them flattened.
		public IReadOnlyDictionary<string, JavaTypeModel> ReferencedTypesFlattened => referenced_types_flattened;

		public string? ApiSource { get; set; }
		public string? Platform { get; set; }

		public JavaTypeCollection ()
		{
			built_in_types.Add ("void", new JavaBuiltInType ("void"));
			built_in_types.Add ("boolean", new JavaBuiltInType ("boolean"));
			built_in_types.Add ("int", new JavaBuiltInType ("int"));
			built_in_types.Add ("byte", new JavaBuiltInType ("byte"));
			built_in_types.Add ("double", new JavaBuiltInType ("double"));
			built_in_types.Add ("float", new JavaBuiltInType ("float"));
			built_in_types.Add ("long", new JavaBuiltInType ("long"));
			built_in_types.Add ("short", new JavaBuiltInType ("short"));
			built_in_types.Add ("char", new JavaBuiltInType ("char"));
		}

		/// <summary>
		/// Adds a new package with the specified name.  Note if package already exists, existing package will be returned.
		/// </summary>
		public JavaPackage AddPackage (string name, string jniName, string? managedName = null)
		{
			if (packages.TryGetValue (name, out var pkg))
				return pkg;

			var new_pkg = new JavaPackage (name, jniName, managedName);

			packages.Add (new_pkg.Name, new_pkg);

			return new_pkg;
		}

		/// <summary>
		/// Adds a type to the collection.  Note declaring classes must be added before nested classes.
		/// </summary>
		/// <returns>True if type was added to collection. False if type could not be added because its declaring type was missing.</returns>
		public bool AddType (JavaTypeModel type)
		{
			var nested_name = type.NestedName;

			// Not a nested type
			if (!nested_name.Contains ('.')) {
				types [type.FullName] = type;

				types_flattened [type.FullName] = type;

				return true;
			}

			var full_name = type.FullName.ChompLast ('.');

			// Nested type, find declaring model to put it in
			if (types_flattened.TryGetValue (full_name, out var declaring)) {
				if (!declaring.NestedTypes.Contains (type))
					declaring.NestedTypes.Add (type);

				type.DeclaringType = declaring;
				types_flattened [type.FullName] = type;

				return true;
			}

			// Could not find declaring type to nest child type in
			return false;
		}

		/// <summary>
		/// Adds a reference type to the collection.
		/// </summary>
		public void AddReferencedType (JavaTypeModel type)
		{
			type.IsReferencedOnly = true;

			referenced_types_flattened [type.FullName] = type;
		}

		// This is a little trickier than we may initially think, because nested classes
		// will also need to be removed from TypesFlattened (recursively). Note this only
		// removes the type from this collection, it does not remove a nested type from
		// its declaring type model. Returns true if type(s) were removed. 
		public bool RemoveType (JavaTypeModel type)
		{
			var removed = false;

			// Remove all nested types
			foreach (var nested in type.NestedTypes)
				removed |= RemoveType (nested);

			// Remove ourselves
			removed |= types_flattened.Remove (type.FullName);
			removed |= types.Remove (type.FullName);

			return removed;
		}

		/// <summary>
		/// Ensures all types needed by the binding types can be found. Removes members or types
		/// that need types that cannot be found.
		/// </summary>
		public CollectionResolutionResults ResolveCollection (TypeResolutionOptions? options = null)
		{
			options ??= TypeResolutionOptions.Default;

			var results = new CollectionResolutionResults ();

			while (true) {
				var unresolvables = new Collection<JavaUnresolvableModel> ();

				foreach (var t in Types)
					try {
						t.Value.Resolve (this, unresolvables);
					} catch (JavaTypeResolutionException) {
					}

				foreach (var u in unresolvables) {
					if (u.Unresolvable is JavaTypeModel type) {
						u.RemovedEntireType = RemoveResolvedType (type);
					} else if (u.Unresolvable is JavaConstructorModel ctor) {
						// Remove from declaring type (must pattern check for ctor before method)
						((JavaClassModel) ctor.DeclaringType).Constructors.Remove (ctor);
					} else if (u.Unresolvable is JavaMethodModel method) {
						// Remove from declaring type
						u.RemovedEntireType = RemoveMethod (method, options);
					} else if (u.Unresolvable is JavaFieldModel field) {
						// Remove from declaring type
						field.DeclaringType.Fields.Remove (field);
					} else if (u.Unresolvable is JavaParameterModel parameter) {
						// Remove method from declaring type
						u.RemovedEntireType = RemoveMethod (parameter.DeclaringMethod, options);
					} else {
						// *Shouldn't* be possible
						throw new Exception ($"Encountered unknown IJavaResolvable: '{u.Unresolvable.GetType ().Name}'");
					}
				}

				if (unresolvables.Any ())
					results.Add (new CollectionResolutionResult (unresolvables));

				// We may have removed a type that other types/members reference, so we have
				// to keep doing this until we do not remove any types.
				if (!unresolvables.Any (u => u.RemovedEntireType))
					break;
			}

			// Once we have resolved all base classes we can resolve class members
			foreach (var klass in TypesFlattened.Values.OfType<JavaClassModel> ())
				klass.ResolveBaseMembers ();

			return results;
		}

		public JavaTypeReference ResolveTypeReference (string name, params JavaTypeParameter [] contextTypeParameters)
			=> ResolveTypeReference (JavaTypeName.Parse (name), contextTypeParameters);

		public JavaTypeReference ResolveTypeReference (JavaTypeName tn, params JavaTypeParameter [] contextTypeParameters)
		{
			var tp = contextTypeParameters.FirstOrDefault (xp => xp.Name == tn.DottedName);

			if (tp != null)
				return new JavaTypeReference (tp, tn.ArrayPart);

			if (tn.DottedName == JavaTypeReference.GenericWildcard.SpecialName)
				return new JavaTypeReference (tn.BoundsType, tn.GenericConstraints?.Select (gc => ResolveTypeReference (gc, contextTypeParameters)), tn.ArrayPart);

			var primitive = JavaTypeReference.GetSpecialType (tn.DottedName);

			if (primitive != null)
				return tn.ArrayPart == null && tn.GenericConstraints == null ? primitive : new JavaTypeReference (primitive, tn.ArrayPart, tn.BoundsType, tn.GenericConstraints?.Select (gc => ResolveTypeReference (gc, contextTypeParameters)));

			var type = FindType (tn.FullNameNonGeneric);

			if (type is null)
				throw new JavaTypeResolutionException (tn.FullNameNonGeneric);

			return new JavaTypeReference (type,
				tn.GenericArguments != null ? tn.GenericArguments.Select (_ => ResolveTypeReference (_, contextTypeParameters)).ToArray () : null,
				tn.ArrayPart);
		}

		// Returns true if a type was removed.
		bool RemoveMethod (JavaMethodModel method, TypeResolutionOptions options)
		{
			// We cannot remove a non-static, non-default method on an interface without breaking the contract.
			// If we need to do that we have to remove the entire interface instead.
			if (method.DeclaringType is JavaInterfaceModel && !method.IsStatic && method.IsAbstract && options.RemoveInterfacesWithUnresolvableMembers)
				return RemoveResolvedType (method.DeclaringType);

			if (method is JavaConstructorModel ctor && method.DeclaringType is JavaClassModel klass)
				klass.Constructors.Remove (ctor);
			else
				method.DeclaringType.Methods.Remove (method);

			return false;
		}

		bool RemoveResolvedType (JavaTypeModel type)
		{
			var removed = false;

			// Remove from declaring type
			if (type.DeclaringType != null)
				removed |= type.DeclaringType.NestedTypes.Remove (type);

			// Remove from collection
			removed |= RemoveType (type);

			// Remove from declaring package
			type.Package.Types.Remove (type);

			return removed;
		}

		public JavaTypeModel? FindType (string type)
		{
			// Prefer built-in types
			if (built_in_types.TryGetValue (type, out var builtin))
				return builtin;

			// Then binding types
			if (TypesFlattened.TryGetValue (type, out var value))
				return value;

			// Finally reference types
			if (ReferencedTypesFlattened.TryGetValue (type, out var ref_type))
				return ref_type;

			// We moved this type to "mono.android.app.IntentService" which makes this
			// type resolution fail if a user tries to reference it in Java.
			if (type == "android.app.IntentService")
				return FindType ("mono.android.app.IntentService");

			return null;
		}
	}
}
