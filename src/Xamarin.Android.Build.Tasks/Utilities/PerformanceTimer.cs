#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks.Utilities;

// A class that faciliates performance timing of a task.
// Example usage:
//   var timer = PerformanceTimer.Create ("MyTask");
//   timer.StartSubTask ("Step 1");
//   <do stuff>
//   timer.StartSubTask ("Step 2");
//   <do stuff>
//   timer.Stop ();
//   timer.Log (Log);
// Also supports Disposable syntax:
//   using (timer.StartSubTask ("Step 1")) {
//     <do stuff>
//   }
class PerformanceTimer : IDisposable
{
	readonly Stopwatch sw;
	readonly List<PerformanceTimer> subtasks = [];

	PerformanceTimer? active_subtask;

	public string Name { get; }

	PerformanceTimer (string name)
	{
		Name = name;
		sw = Stopwatch.StartNew ();
	}

	public static PerformanceTimer Create (string name)
	{
		return new PerformanceTimer (name);
	}

	public void Dispose ()
	{
		Stop ();
	}

	public PerformanceTimer StartSubTask (string name)
	{
		active_subtask?.Stop ();

		active_subtask = new PerformanceTimer (name);
		subtasks.Add (active_subtask);
		return active_subtask;
	}

	public void Stop ()
	{
		active_subtask?.Stop ();
		sw.Stop ();
	}

	public void WriteLog (TaskLoggingHelper log)
	{
		var sb = new StringBuilder ();

		WriteLog (sb, 0);

		log.LogDebugMessage (sb.ToString ());
	}

	void WriteLog (StringBuilder sb, int level)
	{
		sb.AppendLine ($"{new string (' ', level * 2)}- {Name} - {sw.ElapsedMilliseconds}ms");

		level++;

		foreach (var timer in subtasks)
			timer.WriteLog (sb, level);
	}
}
