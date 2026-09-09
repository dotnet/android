using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.IntegrationTests;

/// <summary>
/// Minimal IBuildEngine implementation for use with TaskLoggingHelper in tests.
/// </summary>
sealed class MockBuildEngine (IList<BuildWarningEventArgs>? warnings = null) : IBuildEngine
{
	public bool ContinueOnError => false;
	public int LineNumberOfTaskNode => 0;
	public int ColumnNumberOfTaskNode => 0;
	public string ProjectFileOfTaskNode => "";

	public bool BuildProjectFile (string projectFileName, string [] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => true;
	public void LogCustomEvent (CustomBuildEventArgs e) { }
	public void LogErrorEvent (BuildErrorEventArgs e) { }
	public void LogMessageEvent (BuildMessageEventArgs e) { }
	public void LogWarningEvent (BuildWarningEventArgs e) => warnings?.Add (e);
}
