using System;
using System.Reflection;

namespace ApplicationUtility;

class Reporter
{
	public static void Report (IAspect aspect, bool plainTextRendering)
	{
		Type aspectType = aspect.GetType ();

		// We match the type **exactly** on purpose, we only want to dispatch on
		// the known top-level aspects here.
		Type? knownType = null;
		foreach (Type topLevelAspectType in Detector.KnownTopLevelAspects) {
			if (topLevelAspectType != aspectType) {
				continue;
			}
			knownType = topLevelAspectType;
			break;
		}

		if (knownType == null) {
			throw new InvalidOperationException ($"Internal error: cannot generate report for unsupported type '{aspectType}'");
		}

		var reportDoc = new MarkdownDocument ();
		try {
			IReporter? reporter = CreateSpecificReporter (aspectType, aspect, reportDoc);
			if (reporter == null) {
				throw new InvalidOperationException ($"Internal error: failed to instantiate reporter for type '{aspectType}'");
			}

			reporter.Report ();
		} finally {
			Console.WriteLine (reportDoc.Render (renderPlainText: plainTextRendering));
		}
	}

	static IReporter? CreateSpecificReporter (Type aspectType, IAspect aspect, MarkdownDocument reportDoc)
	{
		Log.Debug ($"Looking for type {aspectType} reporter class");
		Assembly asm = typeof(Reporter).Assembly;
		Type? reporterType = null;
		ConstructorInfo? ctor = null;
		var ctorArgs = new Type[] { aspectType, typeof (MarkdownDocument) };

		foreach (Type type in asm.GetTypes ()) {
			(reporterType, ctor) = GetAspectReporterRecursively (type, aspectType, ctorArgs);
			if (reporterType != null) {
				break;
			}
		}

		if (reporterType == null || ctor == null) {
			Log.Debug ($"Reporter for type '{aspectType}' not found.");
			return null;
		}

		try {
			return (IReporter)ctor.Invoke (new object[] { aspect, reportDoc });
		} catch (Exception ex) {
			throw new InvalidOperationException ($"Internal error: failed to instantiate reporter type '{reporterType}'", ex);
		}
	}

	static (Type?, ConstructorInfo?) GetAspectReporterRecursively (Type type, Type aspectType, Type[] ctorArgs)
	{
		if (IsReporterForAspect (type, aspectType, ctorArgs, out ConstructorInfo? ctor)) {
			return (type, ctor);
		}

		foreach (Type nestedType in type.GetNestedTypes ()) {
			(Type? matching, ctor) = GetAspectReporterRecursively (nestedType, aspectType, ctorArgs);
			if (matching != null) {
				return (matching, ctor);
			}
		}

		return (null, null);
	}

	static bool IsReporterForAspect (Type type, Type aspectType, Type[] ctorArgs, out ConstructorInfo? ctor)
	{
		ctor = null;

		// We don't support generic types, for simplicity
		if (type.IsAbstract || !type.IsClass || type.IsGenericType) {
			return false;
		}

		bool found = false;
		foreach (AspectReporterAttribute attr in type.GetCustomAttributes<AspectReporterAttribute> ()) {
			if (attr.AspectType != aspectType) {
				continue;
			}
			found = true;
			break;
		}

		if (!found) {
			return false;
		}

		Log.Debug ($"Found reporter '{type}' for '{aspectType}'");
		ctor = type.GetConstructor (ctorArgs);
		if (ctor == null) {
			throw new InvalidOperationException ($"Internal error: type '{type}' claims to be a reporter for '{aspectType}' but lacks the correct public constructor.");
		}

		foreach (Type it in type.GetInterfaces ()) {
			if (it == typeof(IReporter)) {
				return true;
			}
		}

		throw new InvalidOperationException ($"Internal error: type '{type}' claims to be a reporter but it does not implement the IReporter interface");
	}
}
