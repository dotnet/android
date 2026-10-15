using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Android.ResourceDesigner.Benchmarks;

sealed class BenchmarkBuildEngine : IBuildEngine4
{
	readonly Dictionary<object, object> taskObjects = [];
	public bool ContinueOnError => false;
	public int LineNumberOfTaskNode => 0;
	public int ColumnNumberOfTaskNode => 0;
	public string ProjectFileOfTaskNode => "";
	public bool IsRunningMultipleNodes => false;
	public void LogErrorEvent (BuildErrorEventArgs e) => throw new InvalidOperationException (e.Message);
	public void LogWarningEvent (BuildWarningEventArgs e) => Console.Error.WriteLine (e.Message);
	public void LogMessageEvent (BuildMessageEventArgs e) { }
	public void LogCustomEvent (CustomBuildEventArgs e) { }
	public void Yield () { }
	public void Reacquire () { }
	public void RegisterTaskObject (object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection) => taskObjects.TryAdd (key, obj);
	public object? GetRegisteredTaskObject (object key, RegisteredTaskObjectLifetime lifetime) => taskObjects.TryGetValue (key, out var value) ? value : null;
	public object? UnregisterTaskObject (object key, RegisteredTaskObjectLifetime lifetime)
	{
		taskObjects.Remove (key, out var value);
		return value;
	}
	public bool BuildProjectFile (string projectFileName, string [] targetNames, IDictionary globalProperties, IDictionary targetOutputs) =>
		throw new NotSupportedException ();
	public bool BuildProjectFile (string projectFileName, string [] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion) =>
		throw new NotSupportedException ();
	public bool BuildProjectFilesInParallel (string [] projectFileNames, string [] targetNames, IDictionary [] globalProperties, IDictionary [] targetOutputsPerProject, string [] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion) =>
		throw new NotSupportedException ();
	public BuildEngineResult BuildProjectFilesInParallel (string [] projectFileNames, string [] targetNames, IDictionary [] globalProperties, IList<string> [] removeGlobalProperties, string [] toolsVersion, bool returnTargetOutputs) =>
		throw new NotSupportedException ();
}
