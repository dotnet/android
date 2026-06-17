using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Installer.AndroidSDK.Common
{
	public interface ILicensesStorage
	{
		/// <summary>
		/// Returns if the license was accepted in this Android SDK
		/// </summary>
		/// <param name="androidSdkPath">Android Sdk path</param>
		/// <param name="license">license to check</param>
		/// <returns>true if the license is accepted, false otherwise</returns>
		bool IsLicenseAccepted(string androidSdkPath, License license);

		/// <summary>
		/// Accept licenses
		/// </summary>
		/// <param name="androidSdkPath">Android Sdk path</param>
		/// <param name="javaSdkPath">Java Sdk path</param>
		/// <param name="cmdLineToolsPath">specific cmdLine-tools path to use</param>
		/// <param name="licenses">list of licenses to accept</param>
		/// <param name="token">cancellation token</param>
		/// <param name="logPath">log file path</param>
		/// <param name="throwsErrorIfValidationFailed">throw exception with arguments validation error</param>
		Task AcceptLicensesAsync(
			string androidSdkPath,
			string javaSdkPath,
			string cmdLineToolsPath,
			IEnumerable<License> licenses,
			CancellationToken token,
			string logPath = null,
			bool throwsErrorIfValidationFailed = false);
	}
}
