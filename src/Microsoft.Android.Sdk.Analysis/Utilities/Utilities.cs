using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class AssemblyLoader : IAnalyzerAssemblyLoader
{
	private readonly HashSet<string> _loadedAssemblies = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

	public static AssemblyLoader Instance = new AssemblyLoader ();

	public void AddDependencyLocation (string fullPath)
	{
		_loadedAssemblies.Add (fullPath);
	}

	public Assembly LoadFromPath (string fullPath)
	{
		if (_loadedAssemblies.Contains (fullPath)) {
			return Assembly.LoadFrom (fullPath);
		}

		throw new InvalidOperationException ($"Assembly at path '{fullPath}' was not added as a dependency location.");
	}
}
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
		var typeSymbol = semanticModel.GetTypeInfo (parameterSyntax.Type).Type;

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
