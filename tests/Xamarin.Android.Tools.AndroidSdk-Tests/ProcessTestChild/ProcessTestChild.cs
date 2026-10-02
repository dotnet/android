// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Win32.SafeHandles;

namespace Xamarin.Android.Tools.Tests
{
	public static class ProcessTestChild
	{
		public static async Task<int> Main (string[] args)
		{
			Console.OutputEncoding = new UTF8Encoding (false);
			switch (args [0]) {
				case "flood":
				case "flood-wait":
				case "flood-input":
					using (var stdout = OpenOutput (standardError: false))
					using (var stderr = OpenOutput (standardError: true)) {
						await Task.WhenAll (
							Task.Factory.StartNew (() => stdout.Write (Encoding.ASCII.GetBytes (new string ('o', 256 * 1024))),
								CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default),
							Task.Factory.StartNew (() => stderr.Write (Encoding.ASCII.GetBytes (new string ('e', 256 * 1024))),
								CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default));
					}
					if (args [0] == "flood-wait")
						await Task.Delay (TimeSpan.FromSeconds (60));
					if (args [0] == "flood-input")
						return Console.In.ReadToEnd ().Length == 256 * 1024 ? 0 : 1;
					return 0;
				case "text":
					Console.Out.Write ("first\r\nsecond\nthird\rpartial");
					Console.Error.Write ("error\r\n\npartial-error");
					return 7;
				case "encoding":
					using (var stdout = Console.OpenStandardOutput ())
					using (var stderr = Console.OpenStandardError ()) {
						await stdout.WriteAsync (Encoding.UTF8.GetPreamble ());
						await stdout.WriteAsync (Encoding.UTF8.GetBytes ("caf\u00e9\r\npartial"));
						await stderr.WriteAsync (Encoding.Unicode.GetPreamble ());
						await stderr.WriteAsync (Encoding.Unicode.GetBytes ("error\u00e9\npartial"));
					}
					return 0;
				case "input":
					Console.WriteLine ("ready");
					Console.Write ($"input:{Console.In.ReadToEnd ()}");
					return 0;
				case "environment":
					Console.Write (Environment.GetEnvironmentVariable ("PROCESS_TEST_VALUE"));
					return 0;
				case "hold-output":
				case "hold-output-running":
					var host = Environment.GetEnvironmentVariable ("PROCESS_TEST_HOST")
						?? throw new InvalidOperationException ("The test .NET host was not provided.");
					var runtimeConfig = Environment.GetEnvironmentVariable ("PROCESS_TEST_RUNTIME_CONFIG")
						?? throw new InvalidOperationException ("The test runtime configuration was not provided.");
					var psi = new ProcessStartInfo (host) { UseShellExecute = false };
					foreach (var argument in new [] { "exec", "--runtimeconfig", runtimeConfig, typeof (ProcessTestChild).Assembly.Location, "wait" })
						psi.ArgumentList.Add (argument);
					using (var child = Process.Start (psi) ?? throw new InvalidOperationException ("Could not start the pipe-holding child.")) {
						Console.WriteLine (child.Id);
						Console.Out.Flush ();
					}
					if (args [0] == "hold-output-running")
						await Task.Delay (TimeSpan.FromSeconds (60));
					return 0;
				case "wait":
					await Task.Delay (TimeSpan.FromSeconds (60));
					return 0;
				default:
					throw new ArgumentException ($"Unknown child mode '{args [0]}'.");
			}
		}

		static FileStream OpenOutput (bool standardError)
		{
			// Console's Unix streams share a lock, so use independent handles for simultaneous writes.
			var handle = OperatingSystem.IsWindows ()
				? GetStdHandle (standardError ? -12 : -11)
				: new IntPtr (standardError ? 2 : 1);
			return new FileStream (new SafeFileHandle (handle, ownsHandle: false), FileAccess.Write);
		}

		[DllImport ("kernel32.dll", SetLastError = true)]
		static extern IntPtr GetStdHandle (int standardHandle);
	}
}
