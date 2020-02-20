using System.Collections.Generic;
using Mono.Cecil;

namespace Java.Interop.Tools.Cecil
{
	/// <summary>
	/// A class for caching lookups from TypeReference -> TypeDefinition.
	/// Generally its lifetime should match an AssemblyResolver instance.
	/// </summary>
	public class TypeDefinitionCache
	{
		readonly Dictionary<TypeReference, TypeDefinition> cache = new Dictionary<TypeReference, TypeDefinition> ();

		public virtual TypeDefinition Resolve (TypeReference typeReference)
		{
			if (cache.TryGetValue (typeReference, out var typeDefinition))
				return typeDefinition;
			return cache [typeReference] = typeReference.Resolve ();
		}
	}
}
