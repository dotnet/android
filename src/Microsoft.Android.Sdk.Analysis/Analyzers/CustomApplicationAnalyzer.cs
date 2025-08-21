using System.Collections.Immutable;
using System.Linq;
using Microsoft.Android.Sdk.Analysis.Properties;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer (LanguageNames.CSharp)]
public class CustomApplicationAnalyzer : DiagnosticAnalyzer
{
	private const string AndroidApplication = "Android.App.Application";
	public const string DiagnosticId = "DNAA0001";
	private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor (
		id: DiagnosticId,
		title: Resources.DNAA0001_Title,
		messageFormat: Resources.DNAA0001_MessageFormat,
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

		bool foundActivationConstructor = false;
		
		// Check all constructors (including primary constructors) using symbol information
		foreach (var constructor in classSymbol.Constructors) {
			if (HasActivationConstructorSignature (constructor)) {
				foundActivationConstructor = true;
				break;
			}
		}
		
		if (!foundActivationConstructor) {
			var diagnostic = Diagnostic.Create (Rule, classDeclarationSyntax.Identifier.GetLocation (), classDeclarationSyntax.Identifier.Text);
			context.ReportDiagnostic (diagnostic);
		}
	}

	private static bool HasActivationConstructorSignature (IMethodSymbol constructor)
	{
		if (constructor.Parameters.Length != 2)
			return false;

		var firstParam = constructor.Parameters [0];
		var secondParam = constructor.Parameters [1];

		// Check first parameter: IntPtr or nint
		var firstParamType = firstParam.Type.ToDisplayString ();
		bool isValidFirstParam = firstParamType == "System.IntPtr" || firstParamType == "nint";

		// Check second parameter: Android.Runtime.JniHandleOwnership
		var secondParamType = secondParam.Type;
		bool isValidSecondParam = secondParamType.ContainingNamespace?.ToDisplayString () == "Android.Runtime" && 
		                         secondParamType.Name == "JniHandleOwnership";

		return isValidFirstParam && isValidSecondParam;
	}
}