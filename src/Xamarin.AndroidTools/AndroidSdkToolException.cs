//
// AndroidSdkToolException.cs
//
// Author:
//       Greg Munn <greg.munn@xamarin.com>
//
// Copyright (c) 2015 Xamarin Inc
//

using System;

namespace Xamarin.AndroidTools
{
	public sealed class AndroidSdkToolException : Exception
	{
		public AndroidSdkToolException (string message, int exitCode) : base (message)
		{
			this.ExitCode = exitCode;
		}

		public AndroidSdkToolException (string message, int exitCode, string toolErrorMessage) : base (message)
		{
			this.ExitCode = exitCode;
			this.ToolErrorMessage = toolErrorMessage;
		}

		public int ExitCode { get; private set; }

		public string ToolErrorMessage { get; private set; }
	}
}
