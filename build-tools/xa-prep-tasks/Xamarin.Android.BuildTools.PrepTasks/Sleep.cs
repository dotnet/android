using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using TPLTask = System.Threading.Tasks.Task;

namespace Xamarin.Android.BuildTools.PrepTasks;

	public class XASleepInternal : AsyncTask
	{
		public override string TaskPrefix => "XASI";
		public int Milliseconds { get; set; }

		public override TPLTask RunTaskAsync ()
		{
			Log.LogMessage (MessageImportance.Normal, $"Going to sleep for {Milliseconds}ms");
			return TPLTask.Delay (Milliseconds, CancellationToken);
		}
	}
