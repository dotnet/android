using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer (LanguageNames.CSharp)]
public class CustomApplicationAnalyzer : DiagnosticAnalyzer
{
    private const string AndroidApplication = "Android.App.Application";
    public const string DiagnosticId = "XAA001";
    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor (
        id: DiagnosticId,
        title: "Application class does not have an Activation Constructor",
        messageFormat: "Application class '{0}' does not have an Activation Constructor",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create (Rule);

    public override void Initialize (AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis (GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution ();

        // Register a syntax node action to analyze method declarations
        context.RegisterSyntaxNodeAction (AnalyzeClass, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClass (SyntaxNodeAnalysisContext context)
    {
        var classDeclarationSyntax = context.Node as ClassDeclarationSyntax;
        if (classDeclarationSyntax == null)
            return;

        var classSymbol = context.SemanticModel.GetDeclaredSymbol (classDeclarationSyntax) as INamedTypeSymbol;
        if (classSymbol == null)
            return;

        if (!Utilities.IsDerivedFrom (classSymbol, AndroidApplication))
            return;

        var constructors = classDeclarationSyntax.Members
            .OfType<ConstructorDeclarationSyntax> ();

        bool foundActivationConstructor = false;
        foreach (var constructor in constructors) {
            var parameters = constructor.ParameterList.Parameters;
            if (parameters.Count != 2)
                continue;
            if (parameters [0].Type.ToString () != "IntPtr")
                continue;
            if (parameters [1].Type.ToString () != "JniHandleOwnership")
                continue;
            foundActivationConstructor = true;
        }
        if (!foundActivationConstructor) {
            var diagnostic = Diagnostic.Create (Rule, classDeclarationSyntax.Identifier.GetLocation (), classDeclarationSyntax.Identifier.Text);
            context.ReportDiagnostic (diagnostic);
        }
    }
}