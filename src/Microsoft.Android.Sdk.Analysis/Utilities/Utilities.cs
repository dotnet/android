using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class Utilities
{
	internal static bool IsDerivedFrom (INamedTypeSymbol typeSymbol, string baseClassName)
	{
		while (typeSymbol != null) {
			if (typeSymbol.ToDisplayString ().StartsWith (baseClassName)) {
				return true;
			}
			typeSymbol = typeSymbol.BaseType;
		}
		return false;
	}

	internal static string GetNamespaceForParameterType (ParameterSyntax parameterSyntax, SemanticModel semanticModel)
	{
		// Ensure the parameter has a type specified
		if (parameterSyntax.Type == null) {
			return null; // No type specified
		}

		// Get the symbol for the type of the parameter
		var typeSymbol = semanticModel.GetSymbolInfo (parameterSyntax.Type).Symbol as ITypeSymbol;

		if (typeSymbol == null) {
			return null; // Unable to resolve the symbol
		}

		// Traverse the containing namespaces to build the full namespace
		var namespaceParts = typeSymbol.ContainingNamespace;
		return namespaceParts.IsGlobalNamespace
			? string.Empty // The type is in the global namespace
			: namespaceParts.ToDisplayString (); // Full namespace as a string
	}
}
