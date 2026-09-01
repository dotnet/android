using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class DexUtilsTests
{
	const string Annotation = "Landroid/webkit/JavascriptInterface;";
	const string ClassName = "Lexample/Target;";
	const string Method = "report";

	[Test]
	public void RuntimeMethodAnnotationMatchesExactClassAndMethod ()
	{
		const string output = """
			Class #0 annotations:
			Annotations on field #0 'report'
			VISIBILITY_RUNTIME Landroid/webkit/JavascriptInterface;
			Annotations on method #1 'other'
			VISIBILITY_RUNTIME Landroid/webkit/JavascriptInterface;
			Class #0            -
			  Class descriptor  : 'Lexample/Other;'
			Class #1 annotations:
			Annotations on method #2 'report' parameters
			VISIBILITY_RUNTIME Landroid/webkit/JavascriptInterface;
			Annotations on method #2 'report'
			  VISIBILITY_RUNTIME Landroid/webkit/JavascriptInterface;
			Class #1            -
			  Class descriptor  : 'Lexample/Target;'
			""";

		Assert.IsTrue (ContainsAnnotation ("\t" + output.Replace ("\n", "  \r\n\t")));
	}

	[Test]
	public void RuntimeMethodAnnotationRejectsFieldAndParameterAnnotations ()
	{
		const string output = """
			Class #0 annotations:
			Annotations on field #0 'report'
			VISIBILITY_RUNTIME Landroid/webkit/JavascriptInterface;
			Annotations on method #1 'report' parameters
			VISIBILITY_RUNTIME Landroid/webkit/JavascriptInterface;
			Class #0            -
			  Class descriptor  : 'Lexample/Target;'
			""";

		Assert.IsFalse (ContainsAnnotation (output));
	}

	[Test]
	public void RuntimeMethodAnnotationRejectsOtherClass ()
	{
		const string output = """
			Class #0 annotations:
			Annotations on method #1 'report'
			VISIBILITY_RUNTIME Landroid/webkit/JavascriptInterface;
			Class #0            -
			  Class descriptor  : 'Lexample/Other;'
			Class #1            -
			  Class descriptor  : 'Lexample/Target;'
			""";

		Assert.IsFalse (ContainsAnnotation (output));
	}

	[Test]
	public void RuntimeMethodAnnotationRejectsOtherVisibility ()
	{
		const string output = """
			Class #0 annotations:
			Annotations on method #1 'report'
			VISIBILITY_BUILD Landroid/webkit/JavascriptInterface;
			VISIBILITY_SYSTEM Landroid/webkit/JavascriptInterface;
			Class #0            -
			  Class descriptor  : 'Lexample/Target;'
			""";

		Assert.IsFalse (ContainsAnnotation (output));
	}

	[Test]
	public void RuntimeMethodAnnotationRejectsZeroAnnotations ()
	{
		const string output = """
			Class #0            -
			  Class descriptor  : 'Lexample/Target;'
			""";

		Assert.IsFalse (ContainsAnnotation (output));
	}

	[Test]
	public void RuntimeMethodAnnotationRejectsTruncatedBlock ()
	{
		const string output = """
			Class #0 annotations:
			Annotations on method #1 'report'
			VISIBILITY_RUNTIME Landroid/webkit/JavascriptInterface;
			""";

		Assert.IsFalse (ContainsAnnotation (output));
	}

	[Test]
	public void RuntimeMethodAnnotationDoesNotTransferAcrossMalformedClasses ()
	{
		const string output = """
			Class #0 annotations:
			Annotations on method #1 'report'
			VISIBILITY_RUNTIME Landroid/webkit/JavascriptInterface;
			Class #1            -
			  Class descriptor  : 'Lexample/Target;'
			""";

		Assert.IsFalse (ContainsAnnotation (output));
	}

	[Test]
	public void RuntimeMethodAnnotationMatchesDescriptorBeforeAnnotation ()
	{
		const string output = """
			Class #0            -
			  Class descriptor  : 'Lexample/Target;'
			Class #0 annotations:
			Annotations on method #1 'report'
			VISIBILITY_RUNTIME Landroid/webkit/JavascriptInterface;
			""";

		Assert.IsTrue (ContainsAnnotation (output));
	}

	[Test]
	public void RuntimeMethodAnnotationMatchesDuplicateMethodName ()
	{
		const string output = """
			Class #0 annotations:
			Annotations on method #1 'report'
			VISIBILITY_BUILD Landroid/webkit/JavascriptInterface;
			Annotations on method #2 'report'
			VISIBILITY_RUNTIME Landroid/webkit/JavascriptInterface;
			Class #0            -
			  Class descriptor  : 'Lexample/Target;'
			""";

		Assert.IsTrue (ContainsAnnotation (output));
	}

	static bool ContainsAnnotation (string output)
	{
		return DexUtils.ContainsRuntimeMethodAnnotation (output.Split ('\n'), ClassName, Method, Annotation);
	}
}
