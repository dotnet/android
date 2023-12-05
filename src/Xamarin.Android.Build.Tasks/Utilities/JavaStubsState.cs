using System.Collections.Generic;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Holds state for typemap and marshal methods generators.  A single instance of this
/// class is created for each enabled target architecture.
/// </summary>
class JavaStubsState
{
	/// <summary>
	/// Target architecture for which this instance was created.
	/// </summary>
	public AndroidTargetArch TargetArch         { get; }

	/// <summary>
	/// Classifier used when scanning for Java types in the target architecture's
	/// assemblies.  Will be **null** if marshal methods are disabled.
	/// </summary>
	public MarshalMethodsClassifier? Classifier { get; }

	/// <summary>
	/// All the Java types discovered in the target architecture's assemblies.
	/// </summary>
	public List<JavaType> AllJavaTypes          { get; }

	public XAAssemblyResolverNew Resolver       { get; }

	public JavaStubsState (AndroidTargetArch arch, XAAssemblyResolverNew resolver, List<JavaType> allJavaTypes, MarshalMethodsClassifier? classifier)
	{
		TargetArch = arch;
		Resolver = resolver;
		AllJavaTypes = allJavaTypes;
		Classifier = classifier;
	}
}
