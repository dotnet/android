using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Linker;

namespace Xamarin.Android.Tasks.Utilities;

class AssemblyRewriterPipeline
{
	public LinkContext PipelineContext { get; }
	public List<IAssemblyRewriterTask> Tasks { get; } = [];

	public AssemblyRewriterPipeline (LinkContext pipelineContext)
	{
		PipelineContext = pipelineContext;
	}

	public AssemblyRewriterResult ProcessAssembly (AssemblyDefinition assembly, string sourceAssembly, string destinationAssembly)
	{
		var context = new AssemblyRewriterContext (sourceAssembly, destinationAssembly);
		var result = new AssemblyRewriterResult ();

		foreach (var task in Tasks) {
			var sw = Stopwatch.StartNew ();
			task.ProcessAssembly (assembly, context);
			sw.Stop ();

			result.TaskResults.Add (new AssemblyRewriterTaskResult (task.GetType ().Name, context.AssemblyModified, sw.Elapsed));
		}

		return result;
	}
}

interface IAssemblyRewriterTask
{
	void ProcessAssembly (AssemblyDefinition assembly, AssemblyRewriterContext context);
}

public class AssemblyRewriterContext
{
	public string SourceAssembly { get; }
	public string DestinationAssembly { get; }
	public bool AssemblyModified { get; set; }

	public AssemblyRewriterContext (string sourceAssembly, string destinationAssembly)
	{
		SourceAssembly = sourceAssembly;
		DestinationAssembly = destinationAssembly;
	}
}

class AssemblyRewriterResult
{
	public bool AssemblyModified => TaskResults.Any (x => x.AssemblyModified);
	public List<AssemblyRewriterTaskResult> TaskResults { get; } = [];

	public string GetSummary ()
	{
		var sb = $"({(AssemblyModified ? "" : "not ")}modified)";

		foreach (var taskResult in TaskResults)
			sb += $" {taskResult.TaskName}{(taskResult.AssemblyModified ? "*" : "")}: {(int)taskResult.Duration.TotalMilliseconds} ms";

		return sb;
	}
}

class AssemblyRewriterTaskResult
{
	public string TaskName { get; set; }
	public bool AssemblyModified { get; set; }
	public TimeSpan Duration { get; set; }

	public AssemblyRewriterTaskResult (string taskName, bool assemblyModified, TimeSpan duration)
	{
		TaskName = taskName;
		AssemblyModified = assemblyModified;
		Duration = duration;
	}
}
