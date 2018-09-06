using System;
using System.IO;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Threading;
using System.Collections;

namespace Xamarin.Android.Tasks
{
	public class CancelableTask : Task, ICancelableTask {
		CancellationTokenSource tcs = new CancellationTokenSource ();

		public CancellationToken Token { get { return tcs.Token; } }

		public virtual void Cancel ()
		{
			tcs.Cancel ();
		}

		public override bool Execute ()
		{
			return !Log.HasLoggedErrors;
		}
	}
}