using System;
using System.IO;

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
		throw new NotImplementedException ();
	}
}
