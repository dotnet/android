// 
// AdbException.cs
//  
// Author:
//       Jonathan Pobst <jpobst@xamarin.com>
// 
// Copyright (c) 2011 Xamarin Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;

namespace Mono.AndroidTools
{
	public class AdbException : Exception
	{
		public AdbException (string message)
			: base (message)
		{
			//AndroidLogger.LogError (string.Empty, message);
		}

		public AdbException (string message, Exception innerException)
			: base (message, innerException)
		{
		}

		public AdbException (Exception innerException)
			: base (innerException.Message, innerException)
		{
		}
	}

	public class DeviceNotFoundException : AdbException
	{
		public string DeviceID { get; private set ; }

		public DeviceNotFoundException (string deviceID)
				: base (string.Format ("Device '{0}' not found", deviceID))
		{
			DeviceID = deviceID;
		}
	}

	public class DeviceDisconnectedException : AdbException
	{
		public DeviceDisconnectedException () : base ("Device disconnected")
		{
		}

		public DeviceDisconnectedException (Exception innerException)
			: base ("Device disconnected", innerException)
		{
		}
	}

	public class ActivityNotFoundException : AdbException
	{
		public ActivityNotFoundException (string message)
			: base (message)
		{
		}
	}

	public class InstallFailedException : AdbException
	{
		public InstallFailedException (string message)
			: base (message)
		{
		}

		public InstallFailedException (string message, string output)
			: base (message)
		{
			Output = output;
		}

		/// <summary>
		/// The raw output from adb
		/// </summary>
		public string Output { get; private set; }
	}

	[Obsolete("Use IncompatibleCpuAbiException")]
	public class IncompatibleCpuAbiExceptiopn : IncompatibleCpuAbiException
	{
		public IncompatibleCpuAbiExceptiopn (string message)
			: base (message)
		{
		}

		public IncompatibleCpuAbiExceptiopn (string message, string output)
			: base (message, output)
		{
		}
	}

	public class IncompatibleCpuAbiException : InstallFailedException
	{
		public IncompatibleCpuAbiException (string message)
			: base (message)
		{
		}

		public IncompatibleCpuAbiException (string message, string output)
			: base (message, output)
		{
		}
	}

	public class InsufficientSpaceException : InstallFailedException
	{
		public InsufficientSpaceException (string message)
			: base (message)
		{
		}

		public InsufficientSpaceException (string message, string output)
			: base (message, output)
		{
		}
	}

	public class PackageAlreadyExistsException : InstallFailedException
	{
		public PackageAlreadyExistsException (string message)
			: base (message)
		{
		}

		public PackageAlreadyExistsException (string message, string packageFile)
			: base (message)
		{
			PackageFile = packageFile;
		}

		public PackageAlreadyExistsException (string message, string packageFile, string output)
			: base (message, output)
		{
			PackageFile = packageFile;
		}

		public string PackageFile { get; private set; }
	}

	public class SdkNotSupportedException : InstallFailedException
	{
		public SdkNotSupportedException (string message)
			: base (message)
		{
		}

		public SdkNotSupportedException (string message, string output)
			: base (message, output)
		{
		}
	}

	public class RequiresUninstallException : PackageAlreadyExistsException
	{
		public RequiresUninstallException (string message)
			: base (message)
		{
		}

		public RequiresUninstallException (string message, string packageFile)
			: base (message, packageFile)
		{
		}

		public RequiresUninstallException (string message, string packageFile, string output)
			: base (message, packageFile, output)
		{
		}
	}
}
