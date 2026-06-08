//
// DebuggerOptions.cs
//
// Author:
//       Greg Munn <greg.munn@xamarin.com>
//
// Copyright (c) 2016 Xamarin Inc
//

using System;
using System.Net;

namespace Xamarin.AndroidTools.Debugging
{
	/// <summary>
	/// Debugger options that control how the android process connects to the debugger
	/// </summary>
	public sealed class DebuggerOptions
	{
		public DebuggerOptions()
		{
		}

		public DebuggerOptions(IPAddress address, int sdbPort, int stdoutPort, bool server)
		{
			this.Address = address;
			this.SdbPort = sdbPort;
			this.StdoutPort = stdoutPort;
			this.Server = server;
		}

		public DebuggerOptions(IPAddress address, int sdbPort, int stdoutPort, bool server, string jdwpHostName, int jdwpPort) :
			this (address, sdbPort, stdoutPort, server)
		{
			this.JdwpHostName = jdwpHostName;
			this.JdwpPort = jdwpPort;
		}

		public IPAddress Address { get; set; }
		public int SdbPort { get; set; }
		public int StdoutPort { get; set; }
		public bool Server { get; set; }
		public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds (30);
		public string JdwpHostName { get; set; } = "127.0.0.1";
		public int JdwpPort { get; set; } = 8100;
	}
}
