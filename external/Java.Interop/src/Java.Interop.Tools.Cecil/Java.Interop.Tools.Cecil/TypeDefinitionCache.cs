using System.Collections.Generic;
using Mono.Cecil;

using Java.Interop.Tools.Diagnostics;

namespace Java.Interop.Tools.Cecil
{
	/// <summary>
	/// A class for caching lookups from TypeReference -> TypeDefinition.
	/// Generally its lifetime should match an AssemblyResolver instance.
	/// </summary>
	public class TypeDefinitionCache : IMetadataResolver
	{
		readonly    Dictionary<TypeReference, TypeDefinition?>      types   = new Dictionary<TypeReference, TypeDefinition?> ();
		readonly    Dictionary<FieldReference, FieldDefinition?>    fields  = new Dictionary<FieldReference, FieldDefinition?> ();
		readonly    Dictionary<MethodReference, MethodDefinition?>  methods = new Dictionary<MethodReference, MethodDefinition?> ();

		public virtual TypeDefinition? Resolve (TypeReference typeReference)
		{
			if (types.TryGetValue (typeReference, out var typeDefinition))
				return typeDefinition;
			return types [typeReference] = typeReference.Resolve ();
		}

		public virtual FieldDefinition? Resolve (FieldReference field)
		{
			if (fields.TryGetValue (field, out var fieldDefinition))
				return fieldDefinition;
			return fields [field] = field.Resolve ();
		}

		public virtual MethodDefinition? Resolve (MethodReference method)
		{
			if (methods.TryGetValue (method, out var methodDefinition))
				return methodDefinition;
			return methods [method] = method.Resolve ();
		}
	}
}
