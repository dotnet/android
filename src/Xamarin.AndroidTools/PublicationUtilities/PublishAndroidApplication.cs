using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Mono.AndroidTools;
using Xamarin.AndroidTools.PublicationUtilities;

namespace Xamarin.AndroidTools
{
	/// <summary>
	/// Legacy packaging tasks
	/// </summary>
	public static class PublishAndroidApplication
	{
		/// <summary>
		/// Signs the .APK asynchronously but does not return a faulted task
		/// </summary>
		public static Task<bool> SignPackage (AndroidSigningOptions options, string unsignedApk, string signedApk, StringWriter output, CancellationToken token)
		{
			if (token.CanBeCanceled && token.IsCancellationRequested)
				throw new OperationCanceledException ();

			return PackageSigningTasks.SignPackageAsync (options, unsignedApk, signedApk, token).ContinueWith (res => {
				return HandleAsyncTaskAndReturnBool (res, "SignPackageAsync faulted", output);
			});
		}

		/// <summary>
		/// Aligns the .APK asynchronously but does not return a faulted task
		/// </summary>
		public static Task<bool> AlignPackage (string srcApk, string destApk, StringWriter output, CancellationToken token)
		{
			if (token.CanBeCanceled && token.IsCancellationRequested)
				throw new OperationCanceledException ();

			return PackageSigningTasks.AlignPackageAsync (srcApk, destApk, token).ContinueWith (res => {
				return HandleAsyncTaskAndReturnBool (res, "AlignPackageAsync faulted", output);
			});
		}

		/// <summary>
		/// Generates a key-pair asynchronously but does not return a faulted task
		/// </summary>
		public static Task<bool> GenerateKeyPair (AndroidSigningOptions options, string dname, int validity, StringWriter output, CancellationToken token)
		{
			if (token.CanBeCanceled && token.IsCancellationRequested)
				throw new OperationCanceledException ();

			return PackageSigningTasks.GenerateKeyPairAsync (options, dname, validity, token).ContinueWith (res => {
				return HandleAsyncTaskAndReturnBool (res, "GenerateKeyPairAsync faulted", output);
			});
		}

		/// <summary>
		/// Verifies a key-pair asynchronously but does not return a faulted task
		/// </summary>
		public static Task<bool> VerifyKeyPair (AndroidSigningOptions options, StringWriter output, CancellationToken token)
		{
			if (token.CanBeCanceled && token.IsCancellationRequested)
				throw new OperationCanceledException ();

			if (output != null)
				output.WriteLine ("Keystore verification failed:");

			return PackageSigningTasks.VerifyKeyPairAsync (options, token).ContinueWith (res => {
				return HandleAsyncTaskAndReturnBool (res, "VerifyKeyPairAsync faulted", output);
			});
		}

		/// <summary>
		/// Handles the task and returns true or false, logs any exceptions and returns false for cancelled tasks.
		/// Preserves API semantics for the methods not suffixed with 'Async'
		/// </summary>
		private static bool HandleAsyncTaskAndReturnBool (Task<bool> task, string faultMesage, StringWriter output)
		{
			if (task.IsFaulted) {
				// observe the exception
				AndroidLogger.LogError (faultMesage, task.Exception);
				if (output != null) {
					var toolException = task.Exception.InnerException as AndroidSdkToolException;
					if (toolException != null) {
						output.Write (toolException.ToolErrorMessage);
					}
				}
				return false;
			}

			if (task.IsCanceled) {
				return false;
			}

			return task.Result;
		}
	}
}
