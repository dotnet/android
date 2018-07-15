using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Build.Tests
{
	public class MockBuildEngine : IBuildEngine, IBuildEngine2, IBuildEngine3, IBuildEngine4
	{
		public MockBuildEngine (TextWriter output, IList<BuildErrorEventArgs> errors = null, IList<BuildWarningEventArgs> warnings = null, IList<BuildMessageEventArgs> messages = null)
		{
			Output = output;
			Errors = errors;
			Warnings = warnings;
			Messages = messages;
		}

		private TextWriter Output { get; }

		private IList<BuildErrorEventArgs> Errors { get; }

		private IList<BuildWarningEventArgs> Warnings { get; }

		private IList<BuildMessageEventArgs> Messages { get; }

		int IBuildEngine.ColumnNumberOfTaskNode => -1;

		bool IBuildEngine.ContinueOnError => false;

		int IBuildEngine.LineNumberOfTaskNode => -1;

		string IBuildEngine.ProjectFileOfTaskNode => "this.xml";

		bool IBuildEngine2.IsRunningMultipleNodes => false;

		bool IBuildEngine.BuildProjectFile (string projectFileName, string [] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => true;

		void IBuildEngine.LogCustomEvent (CustomBuildEventArgs e)
		{
			Output.WriteLine ($"Custom: {e.Message}");
		}

		void IBuildEngine.LogErrorEvent (BuildErrorEventArgs e)
		{
			Output.WriteLine ($"Error: {e.Message}");
			if (Errors != null)
				Errors.Add (e);
		}

		void IBuildEngine.LogMessageEvent (BuildMessageEventArgs e)
		{
			Output.WriteLine ($"Message: {e.Message}");
			if (Messages != null)
				Messages.Add (e);
		}

		void IBuildEngine.LogWarningEvent (BuildWarningEventArgs e)
		{
			Output.WriteLine ($"Warning: {e.Message}");
			if (Warnings != null)
				Warnings.Add (e);
		}

		private Dictionary<object, object> Tasks = new Dictionary<object, object> ();

		void IBuildEngine4.RegisterTaskObject (object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection)
		{
			Tasks [key] = obj;
		}

		object IBuildEngine4.GetRegisteredTaskObject (object key, RegisteredTaskObjectLifetime lifetime)
		{
			Tasks.TryGetValue (key, out object value);
			return value;
		}

		object IBuildEngine4.UnregisterTaskObject (object key, RegisteredTaskObjectLifetime lifetime)
		{
			if (Tasks.TryGetValue (key, out object value)) {
				Tasks.Remove (key);
			}
			return value;
		}

		BuildEngineResult IBuildEngine3.BuildProjectFilesInParallel (string [] projectFileNames, string [] targetNames, IDictionary [] globalProperties, IList<string> [] removeGlobalProperties, string [] toolsVersion, bool returnTargetOutputs)
		{
			throw new NotImplementedException ();
		}

		void IBuildEngine3.Yield () { }

		void IBuildEngine3.Reacquire () { }

		bool IBuildEngine2.BuildProjectFile (string projectFileName, string [] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion) => true;

		bool IBuildEngine2.BuildProjectFilesInParallel (string [] projectFileNames, string [] targetNames, IDictionary [] globalProperties, IDictionary [] targetOutputsPerProject, string [] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion) => true;
	}
}
