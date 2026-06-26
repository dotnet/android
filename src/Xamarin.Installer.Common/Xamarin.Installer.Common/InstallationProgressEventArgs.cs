using System;

namespace Xamarin.Installer.Common
{
	/// <summary>
	/// Passed as the argument to the the <c>AndroidSDKInstaller.InstallationProgress</c> event
	/// handler.
	/// </summary>
	public class InstallationProgressEventArgs : EventArgs
	{
		/// <summary>
		/// Installation progress message
		/// </summary>
		public string Message { get; set; }

		/// <summary>
		/// Progress is in range 0f .. 100f
		/// </summary>
		public float Progress { get; set; }

		/// <summary>
		/// This is True for the very start of a component installation
		/// </summary>
		public bool IsInitialEvent { get; set; }

		/// <summary>
		/// Definition for progress callback
		/// </summary>
		/// <param name="progress"></param>
		public delegate void InstallationProgressActionDelegate (float progress);
	}
}
