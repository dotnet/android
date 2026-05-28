using System;
using System.Threading;
using Xamarin.Installer.AndroidSDK.Manager;

namespace Xamarin.Installer.AndroidSDK
{
	public class MonitorWithTotalProgress : ICancellableProgressMonitor
	{
		float currentTaskTotalWork;
		int mainComponentsCount;
		float lastLoadPercentage;
		const float minPercentageChange = 0.01f;
		float fractionPerComponent, fractionPerSubComponent;

		public bool DisposeOnFinish => monitor is ICancellableProgressMonitor
			? ((ICancellableProgressMonitor) monitor).DisposeOnFinish
			: false;

		public CancellationToken CancellationToken => monitor is ICancellableProgressMonitor
			? ((ICancellableProgressMonitor) monitor).CancellationToken
			: CancellationToken.None;

		IProgressMonitor monitor;
		private int subComponentsCount;

		public bool IsMonitorWithTotalProgress { get; private set; }
		public int MainComponentIndex { get; set; }
		public int SubComponentIndex { get; set; }
		public int SubComponentsCount { get => subComponentsCount;
			set {
				if (value <= 0)
					throw new ArgumentOutOfRangeException ();

				subComponentsCount = value;
				fractionPerSubComponent = (fractionPerComponent * 0.5f) / value;
			}
		}
		public States State { get; set; }

		public enum States
		{
			Download,
			Install
		}

		public MonitorWithTotalProgress (IProgressMonitor monitor, int mainComponentsCount)
		{
			if (mainComponentsCount <= 0)
				throw new ArgumentOutOfRangeException (nameof (mainComponentsCount));

			this.monitor = monitor;
			this.mainComponentsCount = mainComponentsCount;
			fractionPerComponent = 1f / mainComponentsCount;
			IsMonitorWithTotalProgress = monitor is IProgressMonitorWithTotalProgress;
		}

		public void BeginStep (string step)
		{
			currentTaskTotalWork = 1;
			monitor.BeginStep (step);
		}

		public void BeginStep (string step, long totalWork)
		{
			currentTaskTotalWork = totalWork;
			monitor.BeginStep (step, totalWork);
		}

		public void EndStep (AndroidSDKComponentInstallationResult result = null)
		{
			monitor.EndStep (result);
		}

		public void ReportProgress (long work)
		{
			monitor.ReportProgress (work);

			//Console.WriteLine ("MonitorWithTotalProgress.ReportProgress.State = " + State);

			if (IsMonitorWithTotalProgress) {
				var loadPercentage = ReportTotalProgress (work);

				if (lastLoadPercentage > 0.0 && Math.Abs (lastLoadPercentage - loadPercentage) <= minPercentageChange)
					return;

				lastLoadPercentage = loadPercentage;
				((IProgressMonitorWithTotalProgress) monitor).ReportTotalProgress (loadPercentage,
					mainComponentsCount, MainComponentIndex,
					subComponentsCount, SubComponentIndex);
			}
		}

		float ReportTotalProgress (long work)
		{
			// [ MainComponent1 ][ MainComponent2 ] ... [ MainComponentN ]
			// MainComponent Installation consists of archives download: [ Archive1 ][ Archive2 ] ... [ ArchiveM ]
			// and then their installation: [ Archive1 ][ Archive2 ] ... [ ArchiveM ]
			// so we map MainComponentProgress as download 0 .. 50, install 50 .. 100
			// 
			// MainComponentDownload = (Archive1DownloadBytes / Archive1DownloadTotalBytes -> 0 .. 100) (Archive2DownloadBytes / Archive2DownloadTotalBytes -> 0 .. 100) ... (ArchiveMDownloadBytes / ArchiveMDownloadTotalBytes -> 0 .. 100)
			// 
			// and then map each maincomponent to total progress as
			// 0 .. 100 -> 1/MainComponentsCount * MainComponentIndex .. 1/MainComponentsCount * (MainComponentIndex + 1)

			float totalProgress;
			float currentTaskProgress = 1f * work / currentTaskTotalWork; // 0 .. 1

			float subComponentsProgress = SubComponentIndex * fractionPerSubComponent + currentTaskProgress * fractionPerSubComponent;

			if (State == States.Download)
				totalProgress = MainComponentIndex * fractionPerComponent + subComponentsProgress; // 0 .. 50 (of local main component' progress)
			else
				totalProgress = MainComponentIndex * fractionPerComponent + fractionPerComponent / 2f + subComponentsProgress;   // 50 .. 100 (of local main component' progress)

			return totalProgress;
		}

		public void ReportMessage (string message)
		{
			monitor.ReportMessage (message);
		}

		public void ReportError (string message, Exception ex)
		{
			monitor.ReportError (message, ex);
		}

		public void Dispose ()
		{
			if (monitor is ICancellableProgressMonitor)
				((ICancellableProgressMonitor) monitor)?.Dispose ();
		}
	}
}
