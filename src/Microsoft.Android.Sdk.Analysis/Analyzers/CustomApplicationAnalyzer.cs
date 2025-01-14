using System.Collections.Immutable;
using System.Linq;
using Microsoft.Android.Sdk.Analysis;
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

		var constructors = classDeclarationSyntax.Members
			.OfType<ConstructorDeclarationSyntax> ();

		bool foundActivationConstructor = false;
		foreach (var constructor in constructors) {
			var parameters = constructor.ParameterList.Parameters;
			if (parameters.Count != 2)
				continue;
			if (parameters [0].Type.ToString () != "IntPtr")
				continue;
			var ns = Utilities.GetNamespaceForParameterType (parameters [1], context.SemanticModel);
			var type = parameters [1].Type switch {
				IdentifierNameSyntax identifierNameSyntax => identifierNameSyntax.Identifier.Text,
				QualifiedNameSyntax qualifiedNameSyntax => qualifiedNameSyntax.Right.Identifier.Text,
				_ => parameters [1].Type.ToString ()
			};
			var isJniHandle = (ns == "Android.Runtime") && (type == "JniHandleOwnership");
			if (!isJniHandle)
				continue;
			foundActivationConstructor = true;
		}
		if (!foundActivationConstructor) {
			var diagnostic = Diagnostic.Create (Rule, classDeclarationSyntax.Identifier.GetLocation (), classDeclarationSyntax.Identifier.Text);
			context.ReportDiagnostic (diagnostic);
		}
	}
}