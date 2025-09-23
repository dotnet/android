using System;
using System.Reflection;

namespace ApplicationUtility;

class Reporter
{
	public static void Report (IAspect aspect)
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
			throw new InvalidOperationException ($"Internal error: cannot generate report for type '{aspectType}'");
		}

		IReporter? reporter = CreateSpecificReporter (aspectType, aspect);
		if (reporter == null) {
			throw new InvalidOperationException ($"Internal error: failed to instantiate reporter for type '{aspectType}'");
		}

		reporter.Report ();
	}

	static IReporter? CreateSpecificReporter (Type aspectType, IAspect aspect)
	{
		Log.Debug ($"Looking for type {aspectType} reporter class");
		Assembly asm = typeof(Reporter).Assembly;
		Type? reporterType = null;
		ConstructorInfo? ctor = null;
		var ctorArgs = new Type[] { aspectType };

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
			return (IReporter)ctor.Invoke (new object[] { aspect });
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

		var attr = type.GetCustomAttribute<AspectReporterAttribute> ();
		if (attr == null || attr.AspectType != aspectType) {
			return false;
		}

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
