// 
// AndroidDeploymentException.cs
//  
// Authors:
//       Michael Hutchinson <mhutch@xamarin.com>
// 
// Copyright 2013 Xamarin Inc. All rights reserved.
// 

using System;

namespace Xamarin.AndroidTools
{
	public class AndroidDeploymentException : Exception
	{
		AndroidDeploySession session;

		public AndroidDeploymentFailureReason Reason {
			get; private set;
		}

		public AndroidDeploymentException (AndroidDeploymentFailureReason reason) : base (reason.ToString ())
		{
			Reason = reason;
		}

		public AndroidDeploymentException (AndroidDeploymentFailureReason reason, AndroidDeploySession session)
			: base (reason.ToString ())
		{
			Reason = reason;
			this.session = session;
		}

		public AndroidDeploymentException (AndroidDeploymentFailureReason reason, Exception inner)
			: base (reason.ToString (), InnerExceptionFromAggregate (inner))
		{
			Reason = reason;
		}

		public AndroidDeploymentException (AndroidDeploymentFailureReason reason, AndroidDeploySession session, Exception inner)
			: base (reason.ToString (), InnerExceptionFromAggregate (inner))
		{
			Reason = reason;
			this.session = session;
		}

		static Exception InnerExceptionFromAggregate (Exception ex)
		{
			if (ex is AggregateException)
				return ((AggregateException)ex).Flatten ().InnerException;
			return ex;
		}

		public void GetNiceExplanation (out string title, out string detail)
		{
			switch (Reason) {
			case AndroidDeploymentFailureReason.DeviceDisconnected:
				title = "Device disconnected.";
				detail = "The device was disconnected. Please reconnect it and try again.";
				break;
			case AndroidDeploymentFailureReason.ArchitectureNotSupported:
				title = "Architecture not supported.";
				detail = string.Format (
					"The package does not support the device architecture ({0}). You can change the supported " +
					"architectures in the Android Build section of the Project Options.",
					session.Device.SupportedArchitecturesFormatted ()
				);
				break;
			case AndroidDeploymentFailureReason.FailedToGetPackageList:
				title = "Package list not found.";
				detail = "The package list could not be read from the device.";
				break;
			case AndroidDeploymentFailureReason.PackagingFailed:
				title = "Packaging failed.";
				detail = "Deployment was cancelled as the package failed to build.";
				break;
			case AndroidDeploymentFailureReason.InsufficientSpaceForRuntime:
				title = "Insufficient space on device.";
				detail =
					"Deployment failed because there was insufficient space on the device to install the shared " +
					"Mono runtime package. Please make space and try again.";
				break;
			case AndroidDeploymentFailureReason.InsufficientSpaceForPlatform:
				title = "Insufficient space on device.";
				detail =
					"Deployment failed because there was insufficient space on the device to install the shared " +
					"Mono platform package. Please make space and try again.";
				break;
			case AndroidDeploymentFailureReason.SdkNotSupportedByDevice:
				title = "Minimum Android version not supported by device.";
				detail = "Deployment failed because the device does not support the package's minimum Android version. " +
					"You can change the minimum Android version in the Android Application section of the " +
					"Project Options.";
				break;
			case AndroidDeploymentFailureReason.InsufficientSpaceForPackage:
				title = "Insufficient space on device.";
				detail =
					"Deployment failed because there was insufficient space on the device to install the package. " +
					"Please make space and try again.";
				break;
			case AndroidDeploymentFailureReason.FailedToDetermineFastDevPath:
				title = "FastDev path not found.";
				detail = "Deployment failed because the FastDev assembly installation path could not be determined.";
				break;
			case AndroidDeploymentFailureReason.FailedToSynchronizeFastDevAssemblies:
				title = "Assembly synchronization error.";
				detail = "Deployment failed due to an error in FastDev assembly synchronization.";
				break;
			case AndroidDeploymentFailureReason.FailedToSynchronizeFastDevResources:
				title = "Resource synchronization error.";
				detail = "Deployment failed due to an error in FastDev resource synchronization.";
				break;
			case AndroidDeploymentFailureReason.FastDevActivityNotFound:
				title = "FastDev activity not found.";
				detail = "Deployment failed because a FastDev activity was not found in the package.";
				break;
			case AndroidDeploymentFailureReason.FastDevFileConflict:
				title = "FastDev file conflict.";
				detail = 
					"Deployment failed because a FastDev assembly conflicted with an existing file. Try " +
					"manually removing the application from the device before redeploying.";
				break;
			case AndroidDeploymentFailureReason.FastDevDirectoryCreationFailed:
				title = "FastDev directory creation failed.";
				detail = "Deployment failed because the FastDev assembly directory could not be created.";
				break;
			case AndroidDeploymentFailureReason.ArchitectureNotSupportedBySharedRuntime:
				title = "Architecture not supported.";
				detail = string.Format (
					"The shared runtime package does not support the device architecture ({0}).",
					session.Device.SupportedArchitecturesFormatted ()
					);
				break;
			case AndroidDeploymentFailureReason.StdioRedirectionEnabled:
				title  = "Android stdio redirection is enabled.";
				detail =
					"The log.redirect-stdio system property is set to 'true'. " +
					"This is not supported; it must be set to 'false'. " +
					"See: http://developer.android.com/tools/debugging/debugging-log.html#viewingStd";
				break;
			default:
			case AndroidDeploymentFailureReason.InternalError:
				title = "Internal error.";
				if (InnerException != null) {
					detail = "Deployment failed because of an internal error: " + InnerException.Message;
				} else {
					detail = "Deployment failed because of an internal error.";
				}
				break;
			}
		}
	}

	public enum AndroidDeploymentFailureReason
	{
		DeviceDisconnected,
		InternalError,
		ArchitectureNotSupported,
		FailedToGetPackageList,
		PackagingFailed,
		InsufficientSpaceForRuntime,
		InsufficientSpaceForPlatform,
		SdkNotSupportedByDevice,
		InsufficientSpaceForPackage,
		FailedToDetermineFastDevPath,
		FailedToSynchronizeFastDevAssemblies,
		FailedToSynchronizeFastDevResources,
		FastDevActivityNotFound,
		FastDevFileConflict,
		FastDevDirectoryCreationFailed,
		ArchitectureNotSupportedBySharedRuntime,
		StdioRedirectionEnabled,
		RequiresUninstall,
	}
}
