using Microsoft.Build.Framework;

namespace Microsoft.Android.Build.Tasks
{
	public abstract class MultithreadedAsyncTask : AsyncTask, IMultiThreadableTask
	{
		public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

		protected override string WorkingDirectory => TaskEnvironment.ProjectDirectory;

		protected MultithreadedAsyncTask () : base ("")
		{
		}
	}
}
