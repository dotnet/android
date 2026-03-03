using System;
using System.IO;
using System.Reflection;

namespace ApplicationUtility;

public class Extractor
{
	public static bool Supports<TStored> (IAspect? fromAspect) where TStored: class, IAspect
	{
		if (fromAspect == null) {
			return false;
		}

		(Type? extractorType, ConstructorInfo? ctor) = FindMatchingExtractor (typeof(TStored), fromAspect.GetType ());
		return (extractorType != null && ctor != null);
	}

	/// <summary><para>
	/// Extract aspect of type `TStored`, contained inside aspect `fromAspect` into a file pointed to by `destinationPath`.
	/// The destination file will be overwritten. If no matching extractor is found, `false` is returned, otherwise the
	/// method returns `true`. Otherwise, failures will be signaled by exceptions thrown by the specific extractor.
	/// </para><para>
	/// If the extractor throws an exception, the output file is left intact
	/// </summary>
	public static bool Extract<TStored> (IAspect? fromAspect, string destinationPath) where TStored: class, IAspect
	{
		if (!GetExtractor (typeof(TStored), fromAspect, out IExtractor? extractor) || extractor == null) {
			return false;
		}

		using var fs = File.Open (destinationPath, FileMode.Create, FileAccess.Write, FileShare.Read);
		if (!Extract (fromAspect!, extractor, fs)) {
			// TODO: would be nice to use aspect name instead of `typeof(TStored)`
			throw new InvalidOperationException ($"Extraction of aspect {typeof(TStored)} from '{fromAspect!.AspectName}' into file '{destinationPath}' failed.");
		}

		return true;
	}

	public static bool Extract<TStored> (IAspect? fromAspect, Stream destinationStream) where TStored: class, IAspect
	{
		if (!GetExtractor (typeof(TStored), fromAspect, out IExtractor? extractor) || extractor == null) {
			return false;
		}

		if (!Extract (fromAspect!, extractor, destinationStream)) {
			// TODO: would be nice to use aspect name instead of `typeof(TStored)`
			throw new InvalidOperationException ($"Extraction of aspect {typeof(TStored)} from '{fromAspect!.AspectName}' into provided stream failed.");
		}

		return true;
	}

	static bool Extract (IAspect fromAspect, IExtractor extractor, Stream destinationStream)
	{
		bool success = extractor.Extract (destinationStream);
		destinationStream.Flush ();
		destinationStream.Close ();

		return success;
	}

	static bool GetExtractor (Type storedAspectType, IAspect? fromAspect, out IExtractor? extractor)
	{
		extractor = null;
		if (fromAspect == null) {
			return false;
		}

		extractor = CreateSpecificExtractor (storedAspectType, fromAspect);
		return extractor != null;
	}

	public static bool Extract<TStored, TOptions> (IAspect? fromAspect, string destinationPath, TOptions extractorOptions) where TStored: class, IAspect
	{
		if (fromAspect == null) {
			return false;
		}

		throw new NotImplementedException ();
	}

	static (Type? extractorType, ConstructorInfo? ctor) FindMatchingExtractor (Type storedAspectType, Type containerAspectType)
	{
		Log.Debug ($"Looking for extractor type which supports {storedAspectType} inside container {containerAspectType} aspect.");

		// TODO: perhaps it would be a good idea to check all the loaded assemblies
		Assembly asm = typeof(Extractor).Assembly;
		ConstructorInfo? ctor = null;
		Type? extractorType = null;
		var ctorArgs = new Type[] { containerAspectType };

		foreach (Type type in asm.GetTypes ()) {
			(extractorType, ctor) = GetAspectExtractorRecursively (type, storedAspectType, containerAspectType, ctorArgs);
			if (extractorType != null && ctor != null) {
				return (extractorType, ctor);
			}
		}

		return (null, null);
	}

	static IExtractor? CreateSpecificExtractor (Type storedAspectType, IAspect containerAspect)
	{
		(Type? extractorType, ConstructorInfo? ctor) = FindMatchingExtractor (storedAspectType, containerAspect.GetType ());
		if (extractorType == null || ctor == null) {
			Log.Debug ($"Extractor for type '{storedAspectType}' not found.");
			return null;
		}

		try {
			return (IExtractor)ctor.Invoke (new object[] { containerAspect });
		} catch (Exception ex) {
			throw new InvalidOperationException ($"Internal error: failed to instantiate extractor type '{extractorType}'", ex);
		}
	}

	static (Type?, ConstructorInfo?) GetAspectExtractorRecursively (Type candidateType, Type storedAspectType, Type containerAspectType, Type[] ctorArgs)
	{
		if (IsExtractorForAspect (candidateType, storedAspectType, containerAspectType, ctorArgs, out ConstructorInfo? ctor)) {
			return (candidateType, ctor);
		}

		foreach (Type nestedType in candidateType.GetNestedTypes ()) {
			(Type? matching, ctor) = GetAspectExtractorRecursively (nestedType, storedAspectType, containerAspectType, ctorArgs);
			if (matching != null) {
				return (matching, ctor);
			}
		}

		return (null, null);
	}

	static bool IsExtractorForAspect (Type candidateType, Type storedAspectType, Type containerAspectType, Type[] ctorArgs, out ConstructorInfo? ctor)
	{
		ctor = null;

		// We don't support generic types, for simplicity
		if (candidateType.IsAbstract || !candidateType.IsClass || candidateType.IsGenericType) {
			return false;
		}

		bool found = false;
		foreach (AspectExtractorAttribute attr in candidateType.GetCustomAttributes<AspectExtractorAttribute> ()) {
			if (attr.StoredAspectType != storedAspectType || attr.ContainerAspectType != containerAspectType) {
				continue;
			}
			found = true;
			break;
		}

		if (!found) {
			return false;
		}

		Log.Debug ($"Found extractor '{candidateType}' for '{storedAspectType}' stored inside '{containerAspectType}'");
		ctor = candidateType.GetConstructor (ctorArgs);
		if (ctor == null) {
			throw new InvalidOperationException ($"Internal error: type '{candidateType}' claims to be an extractor for '{storedAspectType}' stored inside '{containerAspectType}' but it lacks the correct public constructor.");
		}

		foreach (Type it in candidateType.GetInterfaces ()) {
			if (it == typeof(IExtractor)) {
				return true;
			}
		}

		throw new InvalidOperationException ($"Internal error: type '{candidateType}' claims to be an exporter but it does not implement the IExtractor interface");
	}
}
