//
// IProgressMonitor.cs
//
// Author:
//       Vsevolod Kukol <sevoku@microsoft.com>
//
//  Copyright (c) 2017, Microsoft, Inc
//

using System;
using System.Threading;

namespace Xamarin.Installer.AndroidSDK.Manager
{
	/// <summary>
	/// The Progress monitor factory interface is used by <see cref="T:Xamarin.Installer.AndroidSDK.Manager.AndroidSDKManager"/> to
	/// create installation progress monitors on demand.
	/// </summary>
	public interface IProgressMonitorFactory
	{
		/// <summary>
		/// Creates a progress monitor to observe the SDK Manager installation progress.
		/// </summary>
		/// <returns>The progress monitor.</returns>
		/// <remarks>The SDK Manager will create a new progress monitor for each installation process.</remarks>
		IProgressMonitor CreateProgressMonitor ();
	}

	/// <summary>
	/// The cancellable progress monitor can cancel a running operation.
	/// </summary>
	/// <remarks>
	/// Implement this interface to be able tonotify the SDK Manager that the running operation should
	/// be canceled.
	/// </remarks>
	public interface ICancellableProgressMonitor : IProgressMonitor, IDisposable
	{
		/// <summary>
		/// Gets the cancellation token.
		/// </summary>
		/// <value>The cancellation token.</value>
		CancellationToken CancellationToken { get; }

		/// <summary>
		/// Gets a value indicating whether this
		/// <see cref="T:Xamarin.Installer.AndroidSDK.Manager.ICancellableProgressMonitor"/> should be disposed after the work is done.
		/// </summary>
		/// <value><c>true</c> to dispose when finished; otherwise, <c>false</c>.</value>
		bool DisposeOnFinish { get; }
	}

	/// <summary>
	/// A progress monitor with total progress will be notified by the SDK Manager with overall progress of installation steps
	/// </summary>
	public interface IProgressMonitorWithTotalProgress : ICancellableProgressMonitor
	{
		/// <summary>
		/// Reports the total progress of all the download and installation steps
		/// </summary>
		/// <param name="loadPercentage">Overall progress percentage from 0.0 to 1.0</param>
		void ReportTotalProgress (double loadPercentage,
			int mainComponentsCount, int mainComponentIndex,
			int subComponentsCount, int subComponentIndex);
	}

	/// <summary>
	/// A progress monitor will be notified by the SDK Manager of all installation steps.
	/// </summary>
	/// <remarks>
	/// Implement this interface to observe all installation steps and progress updates.
	/// </remarks>
	public interface IProgressMonitor
	{
		/// <summary>
		/// Begin a new step.
		/// </summary>
		/// <param name="step"> The translated message associated with the step to be presented to the user.</param>
		void BeginStep (string step);

		/// <summary>
		/// Begin a new step with progress notification.
		/// </summary>
		/// <param name="step"> The translated message associated with the step to be presented to the user.</param>
		/// <param name="totalWork"> The total length of the progress to be done in this step. </param>
		void BeginStep (string step, long totalWork);

		/// <summary>
		/// End the last step started with BeginStep ()
		/// </summary>
		void EndStep (AndroidSDKComponentInstallationResult result = null);

		/// <summary>
		/// Reports the progress of the currently running step.
		/// </summary>
		/// <param name="work"> The current step progress. </param>
		void ReportProgress (long work);

		/// <summary>
		/// Show a message to the user.
		/// </summary>
		/// <param name="message"> The message to be shown.</param>
		/// <remarks> Messages provide additional status information for the currently running step. </remarks>
		void ReportMessage (string message);

		/// <summary>
		/// Notify the user when an error occured.
		/// </summary>
		/// <param name="message"> The error message. </param>
		/// <param name="ex"> The exception </param>
		void ReportError (string message, Exception ex);
	}
}
