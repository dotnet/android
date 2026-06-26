using System;

namespace Xamarin.Installer.AndroidSDK
{
	public class AndroidSDKComponentInstallationResult
	{
		public enum States
		{
			None,
			UserCancelled,
			DownloadError,
			InstallationError,
			UninstallationError,
			DownloadedSuccessfully,
			InstalledSuccessfully,
			UninstalledSuccessfully,
			Success,

			LicensesNotAccepted,
			LicensesAccepted,
			LicensesNotRequired,
			LicensesAcceptanceFailed
		}

		public bool Success { get; set; }
		public States State { get; set; }

		public Exception Exception { get; set; }

		public AndroidSDKComponentInstallationResult (bool success, Exception exception = null)
			: this (success ? States.Success : States.None, exception, success)
		{
		}

		public AndroidSDKComponentInstallationResult (States state, Exception exception = null, bool? success = null)
		{
			State = state;
			Exception = exception;
			Success = success.HasValue ? success.Value : state.IsSuccessful ();
		}

		/// <summary>
		/// Use <see cref="AndroidSDKComponentInstallationResultExtensions.ToReadableString (AndroidSDKComponentInstallationResult)"/> instead if you need to handle null values
		/// </summary>
		public override string ToString ()
		{
			return this.ToReadableString ();
		}
	}

	public static class AndroidSDKComponentInstallationResultExtensions
	{
		public static bool IsSuccessful(this AndroidSDKComponentInstallationResult.States state)
		{
			return state == AndroidSDKComponentInstallationResult.States.Success
				|| state == AndroidSDKComponentInstallationResult.States.DownloadedSuccessfully
				|| state == AndroidSDKComponentInstallationResult.States.InstalledSuccessfully
				|| state == AndroidSDKComponentInstallationResult.States.UninstalledSuccessfully
				|| state == AndroidSDKComponentInstallationResult.States.LicensesAccepted
				|| state == AndroidSDKComponentInstallationResult.States.LicensesNotRequired;
		}

		/// <summary>
		/// This method supports null input values
		/// </summary>
		public static string ToReadableString (this AndroidSDKComponentInstallationResult result)
		{
			return result == null ? "undefined" :
				(result.Success ? "success"
				: (
					"fail" + (!String.IsNullOrEmpty (result.Exception?.Message) ? $": \"{result.Exception.Message}\"" : String.Empty)
				)) + $" ({result.State})";
		}
	}
}
