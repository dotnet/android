using System;
using System.Collections;
using Microsoft.Build.Framework;

namespace Microsoft.Android.Build.TypeMap.IntegrationTests;

/// <summary>
/// Minimal IBuildEngine implementation for use with TaskLoggingHelper in tests.
/// </summary>
sealed class MockBuildEngine : IBuildEngine
{
	public bool ContinueOnError => false;
	public int LineNumberOfTaskNode => 0;
	public int ColumnNumberOfTaskNode => 0;
	public string ProjectFileOfTaskNode => "";

	public bool BuildProjectFile (string projectFileName, string [] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => true;
	public void LogCustomEvent (CustomBuildEventArgs e) { }
	public void LogErrorEvent (BuildErrorEventArgs e) { }
	public void LogMessageEvent (BuildMessageEventArgs e) { }
	public void LogWarningEvent (BuildWarningEventArgs e) { }
}
