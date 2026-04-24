using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace ApplicationUtility;

/// <summary>
/// Provides static methods for extracting sub-aspects (e.g. assemblies, manifests) from container aspects
/// (e.g. APK/AAB packages) using reflection-based extractor discovery.
/// </summary>
public class Extractor
{
	/// <summary>
	/// Determines whether an extractor exists that can extract an aspect of type <typeparamref name="TStored"/> from <paramref name="fromAspect"/>.
	/// </summary>
	/// <typeparam name="TStored">The type of aspect to extract.</typeparam>
	/// <param name="fromAspect">The container aspect to extract from.</param>
	/// <returns><c>true</c> if extraction is supported; <c>false</c> otherwise.</returns>
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

	/// <summary>
	/// Extracts an aspect of type <typeparamref name="TStored"/> from <paramref name="fromAspect"/> into a destination stream.
	/// </summary>
	/// <typeparam name="TStored">The type of aspect to extract.</typeparam>
	/// <param name="fromAspect">The container aspect to extract from.</param>
	/// <param name="destinationStream">The stream to write the extracted data to.</param>
	/// <returns><c>true</c> if a matching extractor was found; <c>false</c> otherwise.</returns>
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

	/// <summary>
	/// Extracts an aspect of type <typeparamref name="TStored"/> from <paramref name="fromAspect"/> into a file,
	/// using extractor options of type <typeparamref name="TOptions"/>.
	/// </summary>
	/// <typeparam name="TStored">The type of aspect to extract.</typeparam>
	/// <typeparam name="TOptions">The type of extractor options.</typeparam>
	/// <param name="fromAspect">The container aspect to extract from.</param>
	/// <param name="destinationPath">Path to the output file (will be overwritten).</param>
	/// <param name="extractorOptions">Options to pass to the extractor.</param>
	/// <returns><c>true</c> if a matching extractor was found; <c>false</c> otherwise.</returns>
	public static bool Extract<TStored, TOptions> (IAspect? fromAspect, string destinationPath, TOptions extractorOptions) where TStored: class, IAspect
	{
		if (!GetExtractor<TOptions> (typeof(TStored), fromAspect, extractorOptions, out IExtractorWithOptions<TOptions>? extractor) || extractor == null) {
			return false;
		}

		using var fs = File.Open (destinationPath, FileMode.Create, FileAccess.Write, FileShare.Read);
		if (!Extract (fromAspect!, extractor, fs)) {
			// TODO: would be nice to use aspect name instead of `typeof(TStored)`
			throw new InvalidOperationException ($"Extraction of aspect {typeof(TStored)} from '{fromAspect!.AspectName}' into file '{destinationPath}' failed.");
		}

		return true;
	}

	/// <summary>
	/// Extracts multiple entries of type <typeparamref name="TStored"/> from <paramref name="fromAspect"/> into
	/// the given destination directory, using extractor options of type <typeparamref name="TOptions"/>.
	/// </summary>
	/// <typeparam name="TStored">The type of aspect to extract.</typeparam>
	/// <typeparam name="TOptions">The type of extractor options.</typeparam>
	/// <param name="fromAspect">The container aspect to extract from.</param>
	/// <param name="destinationPath">Path to the output directory.</param>
	/// <param name="extractorOptions">Options to pass to the extractor.</param>
	/// <returns><c>true</c> if extraction succeeded; <c>false</c> otherwise.</returns>
	public static bool ExtractMultiple<TStored, TOptions> (IAspect? fromAspect, string destinationPath, TOptions extractorOptions) where TStored: class, IAspect
	{
		if (!GetExtractor<TOptions> (typeof(TStored), fromAspect, extractorOptions, out IExtractorWithOptions<TOptions>? extractor) || extractor == null) {
			return false;
		}

		var streams = new List<(Stream stream, string path)> ();
		bool success = false;
		try {
			success = Extract (
				fromAspect!,
				extractor,
				(string relativePath) => {
					string outputPath = Path.Combine (destinationPath, relativePath);
					Directory.CreateDirectory (Path.GetDirectoryName (outputPath)!);
					var fs = File.Open (outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
					streams.Add ((fs, outputPath));
					return fs;
				}
			);
		} catch (Exception ex) {
			Log.Error ($"Failed to extract one or more entries.");
			Log.Error (ex.ToString ());
			success = false;
		}

		foreach ((Stream stream, string path) in streams) {
			try {
				stream.Flush ();
				stream.Close ();
			} catch (Exception ex) {
				Log.Error ($"Failed to flush and close stream for output file '{path}'");
				Log.Error (ex.ToString ());
			} finally {
				stream.Dispose ();
			}
		}

		return success;
	}

	static bool Extract (IAspect fromAspect, IExtractor extractor, GetOutputStreamForPathFn getOutputStreamForPath)
	{
		return extractor.Extract (getOutputStreamForPath);
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
			Log.Debug ($"Unable to determine extractor for '{storedAspectType}' because container aspect is not specified.");
			return false;
		}

		extractor = CreateSpecificExtractor (storedAspectType, fromAspect);
		if (extractor == null) {
			Log.Error ($"Container '{fromAspect}' doesn't support '{storedAspectType}'.");
		}

		return extractor != null;
	}

	static bool GetExtractor<TOptions> (Type storedAspectType, IAspect? fromAspect, TOptions options, out IExtractorWithOptions<TOptions>? extractor)
	{
		extractor = null;
		if (fromAspect == null) {
			Log.Debug ($"Unable to determine extractor for '{storedAspectType}' because container aspect is not specified.");
			return false;
		}

		extractor = CreateSpecificExtractor<TOptions> (storedAspectType, fromAspect, options);
		if (extractor == null) {
			Log.Error ($"Container '{fromAspect}' doesn't support '{storedAspectType}'.");
		}
		return extractor != null;
	}

	static (Type? extractorType, ConstructorInfo? ctor) FindMatchingExtractor (Type storedAspectType, Type containerAspectType, Type[] ctorArgs)
	{
		Log.Debug ($"Looking for extractor type which supports {storedAspectType} inside container {containerAspectType} aspect.");

		// TODO: perhaps it would be a good idea to check all the loaded assemblies
		Assembly asm = typeof(Extractor).Assembly;
		ConstructorInfo? ctor = null;
		Type? extractorType = null;

		foreach (Type type in asm.GetTypes ()) {
			(extractorType, ctor) = GetAspectExtractorRecursively (type, storedAspectType, containerAspectType, ctorArgs);
			if (extractorType != null && ctor != null) {
				return (extractorType, ctor);
			}
		}

		return (null, null);
	}

	static (Type? extractorType, ConstructorInfo? ctor) FindMatchingExtractor (Type storedAspectType, Type containerAspectType)
	{
		var ctorArgs = new Type[] { containerAspectType };
		return FindMatchingExtractor (storedAspectType, containerAspectType, ctorArgs);
	}

	static (Type? extractorType, ConstructorInfo? ctor) FindMatchingExtractor<TOptions> (Type storedAspectType, Type containerAspectType)
	{
		var ctorArgs = new Type[] { containerAspectType, typeof(TOptions) };
		return FindMatchingExtractor (storedAspectType, containerAspectType, ctorArgs);
	}

	static IExtractor? CreateSpecificExtractor (Type storedAspectType, IAspect containerAspect)
	{
		(Type? extractorType, ConstructorInfo? ctor) = FindMatchingExtractor (storedAspectType, containerAspect.GetType ());
		if (extractorType == null || ctor == null) {
			Log.Debug ($"Extractor for type '{storedAspectType}' contained in '{containerAspect.GetType ()}' not found.");
			return null;
		}

		try {
			return (IExtractor)ctor.Invoke (new object[] { containerAspect });
		} catch (Exception ex) {
			throw new InvalidOperationException ($"Internal error: failed to instantiate extractor type '{extractorType}'", ex);
		}
	}

	static IExtractorWithOptions<TOptions>? CreateSpecificExtractor<TOptions> (Type storedAspectType, IAspect containerAspect, TOptions options)
	{
		(Type? extractorType, ConstructorInfo? ctor) = FindMatchingExtractor<TOptions> (storedAspectType, containerAspect.GetType ());
		if (extractorType == null || ctor == null) {
			Log.Debug ($"Extractor for type '{storedAspectType}' contained in '{containerAspect.GetType ()}' not found.");
			return null;
		}

		try {
			return (IExtractorWithOptions<TOptions>)ctor.Invoke (new object[] { containerAspect, options! });
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

	static bool IsExtractorForAspect (Type expectedInterfaceType, Type candidateType, Type storedAspectType, Type containerAspectType, Type[] ctorArgs, out ConstructorInfo? ctor)
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
			if (it == expectedInterfaceType) {
				return true;
			}
		}

		throw new InvalidOperationException ($"Internal error: type '{candidateType}' claims to be an exporter but it does not implement the {expectedInterfaceType} interface");
	}

	static bool IsExtractorForAspect (Type candidateType, Type storedAspectType, Type containerAspectType, Type[] ctorArgs, out ConstructorInfo? ctor)
	{
		return IsExtractorForAspect (typeof(IExtractor), candidateType, storedAspectType, containerAspectType, ctorArgs, out ctor);
	}

	static bool IsExtractorForAspect<TOptions> (Type candidateType, Type storedAspectType, Type containerAspectType, Type[] ctorArgs, out ConstructorInfo? ctor)
	{
		return IsExtractorForAspect (typeof(IExtractorWithOptions<TOptions>), candidateType, storedAspectType, containerAspectType, ctorArgs, out ctor);
	}
}
