using System;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public class Sleep : Task
	{
		public int Milliseconds { get; set; }

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Normal, $"Going to sleep for {Milliseconds}ms");
			Thread.Sleep (Milliseconds);

			return true;
		}
	}
}
