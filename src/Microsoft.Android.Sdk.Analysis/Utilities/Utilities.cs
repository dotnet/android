using Microsoft.CodeAnalysis;

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
}