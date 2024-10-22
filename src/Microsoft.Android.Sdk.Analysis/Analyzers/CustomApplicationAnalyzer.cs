using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

public class CustomApplicationAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "XAA001";
    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor (
        DiagnosticId,                          // Diagnostic ID
        "Application class does not have an Activation Constructor.",      // Title
        "Application class '{0}' does not have an Activation Constructor.",  // Message format
        "Code",                          // Category
        DiagnosticSeverity.Warning,        // Default severity
        isEnabledByDefault: true  // Enabled by default
    );

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create (Rule);

	public override void Initialize (AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
    
        // Register a syntax node action to analyze method declarations
        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
	}

    private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        var classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;
        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax) as INamedTypeSymbol;
        if (classSymbol == null)
            return;

        if (!IsDerivedFrom (classSymbol, "Android.App.Application"))
            return;
        
        var constructors = classDeclarationSyntax.Members
            .OfType<ConstructorDeclarationSyntax>();

        bool foundActivationConstructor = false;
        foreach (var constructor in constructors) {
            var parameters = constructor.ParameterList.Parameters;
            if (parameters.Count != 2)
                continue;
            if (parameters[0].Type.ToString () != "IntPtr")
                continue;
            if (parameters[1].Type.ToString () != "JniHandleOwnership")
                continue;
            foundActivationConstructor = true;
        }
        if (!foundActivationConstructor) {
            var diagnostic = Diagnostic.Create(Rule, classDeclarationSyntax.Identifier.GetLocation(), classDeclarationSyntax.Identifier.Value);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsDerivedFrom(INamedTypeSymbol typeSymbol, string baseClassName)
    {
        while (typeSymbol != null)
        {
            if (typeSymbol.ToDisplayString().StartsWith(baseClassName))
            {
                return true;
            }
            typeSymbol = typeSymbol.BaseType;
        }
        return false;
    }
}