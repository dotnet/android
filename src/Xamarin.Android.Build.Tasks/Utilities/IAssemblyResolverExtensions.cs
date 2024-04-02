using Mono.Cecil;

namespace Xamarin.Android.Tasks;

static class AssebmlyResolverExtensions
{
	public static AssemblyDefinition? Resolve (this IAssemblyResolver resolver, string fullName, ReaderParameters? parameters = null)
	{
		return resolver?.Resolve (AssemblyNameReference.Parse (fullName), parameters);
	}
}
