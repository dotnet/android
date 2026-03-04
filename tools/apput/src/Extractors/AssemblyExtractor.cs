using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ApplicationUtility;

[AspectExtractor (containerAspectType: typeof (AssemblyStore),              storedAspectType: typeof (ApplicationAssembly))]
[AspectExtractor (containerAspectType: typeof (AssemblyStoreSharedLibrary), storedAspectType: typeof (ApplicationAssembly))]
[AspectExtractor (containerAspectType: typeof (PackageAAB),                 storedAspectType: typeof (ApplicationAssembly))]
[AspectExtractor (containerAspectType: typeof (PackageAPK),                 storedAspectType: typeof (ApplicationAssembly))]
[AspectExtractor (containerAspectType: typeof (PackageBase),                storedAspectType: typeof (ApplicationAssembly))]
class AssemblyExtractor : BaseExtractorWithOptions<AssemblyExtractorOptions>
{
	public AssemblyExtractor (IAspect containerAspect, AssemblyExtractorOptions options)
		: base (containerAspect, options)
	{}

	public override bool Extract (Stream destinationStream)
	{
		throw new NotSupportedException ($"Extractor handles multiple items, please use the {nameof(GetOutputStreamForPathFn)} overload.");
	}

	public override bool Extract (GetOutputStreamForPathFn getOutputStreamForPath)
	{
		var patterns = String.Join (", ", Options.AssemblyPatterns.Select (p => $"'{p}'"));
		Log.Debug ($"Assembly extractor asked to extract the following: {patterns}");

		return ContainerAspect switch {
			AssemblyStore store            => Extract (getOutputStreamForPath, store),
			AssemblyStoreSharedLibrary dso => Extract (getOutputStreamForPath, dso),
			PackageAAB aab                 => Extract (getOutputStreamForPath, aab),
			PackageAPK apk                 => Extract (getOutputStreamForPath, apk),
			PackageBase basepkg            => Extract (getOutputStreamForPath, basepkg),
			_                              => throw new InvalidOperationException ($"Internal error: unexpected container aspect type '{ContainerAspect}'")
		};

	}

	bool Extract (GetOutputStreamForPathFn getOutputStreamForPath, AssemblyStore store)
	{
		throw new NotImplementedException ();
	}

	bool Extract (GetOutputStreamForPathFn getOutputStreamForPath, AssemblyStoreSharedLibrary dso)
	{
		throw new NotImplementedException ();
	}

	bool Extract (GetOutputStreamForPathFn getOutputStreamForPath, ApplicationPackage package)
	{
		throw new NotImplementedException ();
	}

	bool Extract (GetOutputStreamForPathFn getOutputStreamForPath, List<ApplicationAssembly> assemblies)
	{
		if (assemblies.Count == 0) {
			return true;
		}

		// TODO: use Matcher from https://learn.microsoft.com/en-us/dotnet/core/extensions/file-globbing, with custom DirectoryInfoBase that
		// works with the `assemblies` list.

		throw new NotImplementedException ();
	}
}
