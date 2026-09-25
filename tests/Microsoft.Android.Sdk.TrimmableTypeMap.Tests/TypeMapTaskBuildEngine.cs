using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

sealed class TypeMapTaskBuildEngine : IBuildEngine
{
	public List<BuildErrorEventArgs> Errors { get; } = [];
	public bool ContinueOnError => false;
	public int LineNumberOfTaskNode => 0;
	public int ColumnNumberOfTaskNode => 0;
	public string ProjectFileOfTaskNode => "";
	public void LogErrorEvent (BuildErrorEventArgs e) => Errors.Add (e);
	public void LogWarningEvent (BuildWarningEventArgs e) { }
	public void LogMessageEvent (BuildMessageEventArgs e) { }
	public void LogCustomEvent (CustomBuildEventArgs e) { }
	public bool BuildProjectFile (string projectFileName, string [] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => throw new NotSupportedException ();
}
