// 
// AdbStartServerProcess.cs
//
// Author:
//       Michael Hutchinson <mhutch@xamarin.com>
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
using System.Threading;
using System.IO;
using Mono.AndroidTools.Util;

namespace Mono.AndroidTools.Internal
{
	interface IAdbStartServerProcess : IDisposable
	{
		bool Success { get; }
		bool Completed { get; }
		event EventHandler Exited;
		string GetOutput ();
		void Cancel ();
	}

	//HACK: using Process and a thread instead of MD process APIs because of weird stuff adb start-server does
	// When using adb start-server, .NET's StandardOutput.Read blocks or even throws a "stdout not redirected" 
	// after all data is read and the process has ended
	// This seems to be because adb forks, and even though the original process exits, the output stream somehow 
	// stays alive. It's probably been passed over to the new process somehow.
	//
	class AdbStartServerProcess : IAdbStartServerProcess
	{
		//ManualResetEvent endEventErr = new ManualResetEvent (false);
		Thread captureOutputThread; //, captureErrorThread;
		System.Diagnostics.Process proc;
		object lockObj = new object ();
		EventHandler exited;
		StringWriter output = new StringWriter ();
		bool success = false, completed = false;
		
		public bool Success { get { return success; } }
		public bool Completed { get { return completed; } }
		
		public string GetOutput ()
		{
			return output.ToString ();
		}
		
		public AdbStartServerProcess (string adbExe, EventHandler exited)
		{
			this.exited = exited;
			
			proc = new System.Diagnostics.Process ();
			proc.StartInfo = new System.Diagnostics.ProcessStartInfo (adbExe, "start-server") {
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
			};
			proc.Start ();
			
			captureOutputThread = new Thread (CaptureOutput) {
				Name = "Adb output reader",
				IsBackground = true,
			};
			captureOutputThread.Start ();
			
			/*
			captureErrorThread = new Thread (CaptureError) {
				Name = "Adb error reader",
				IsBackground = true,
			};
			captureErrorThread.Start ();*/
		}

		public event EventHandler Exited {
			add { exited += value; }
			remove { exited -= value; }
		}
		
		void CaptureOutput ()
		{
			bool hadException = false;
			try {
				//HACK: this is just long enough to contain the expected adb output string when successfully starting the server
				//if we try to read too much, we will stop responding somewhere in native code on Windows
				char[] buffer = new char [86];
				int nr;
				while ((nr = proc.StandardOutput.Read (buffer, 0, buffer.Length)) > 0) {
					var s = new string (buffer, 0, nr);
					output.Write (s);
					if (s.Contains ("daemon started successfully")) {
						success = true;
						/*
						lock (lockObj) {
							captureErrorThread.Abort ();
							captureErrorThread = null;
						}*/
						output.Dispose ();
						proc.StandardOutput.Close ();
						proc.StandardError.Close ();
						break;
					}
				}
			} catch (ThreadAbortException) {
				hadException = true;
				Thread.ResetAbort ();
			} catch (Exception ex) {
				hadException = true;
				AndroidLogger.LogError ("Unhandled exception in adb output reader", ex);
			} finally {
				//proc can be null if this class is disposed before the output reader finishes
				var p = proc;
				
				/*
				if (endEventErr != null)
					WaitHandle.WaitAll (new WaitHandle[] {endEventErr} );
				*/
				
				//HACK: if success is true at this point, then we have determined that adb is forking a new server 
				// and bailed out early to avoid the Windows native hang that happens when we read too far in the adb
				// stdout stream that gets passed over to the new process.
				// See: 
				//
				// Unfortunately, if the fork happens then the error stream thread hangs in the same way, and cannot 
				// even be aborted. We avoid this by *only* reading stderr if this condition is false. Instead we 
				// only read stderr after the output thread is done. Sadly this means we lose stderr/stdout 
				// interleaving, and the adb process could deadlock if stderr fills up too much.
				if (!success && p != null) {
					string line;
					while ((line = p.StandardError.ReadLine ()) != null)
						output.WriteLine (line);
				}

				if (!success && p != null) {
					// if we did not have an exception, wait for the proc to exit completel before checking for final success status
					// we are sometimes getting to this point and checking for exit status but because the process has not exited, we incorectly set success false
					// when in fact it did succeed. this causes XS to stop polling for device updates
					if (!hadException)
						p.WaitForExit (5000);

					if (p.HasExited && p.ExitCode <= 0)
						success = true;
				}

				completed = true;
				captureOutputThread = null;
				exited (this, EventArgs.Empty);
			}
		}
		/*
		void CaptureError ()
		{
			try {
				char[] buffer = new char [1024];
				int nr;
				while ((nr = proc.StandardError.Read (buffer, 0, buffer.Length)) > 0) {
					var s = new string (buffer, 0, nr);
					output.Write (s);
				}					
			} catch (ThreadAbortException) {
				Thread.ResetAbort ();
			} catch (Exception ex) {
				LoggingService.LogError ("Unhandled exception in adb error reader", ex);
			} finally {
				lock (lockObj) {
					if (endEventErr != null)
						endEventErr.Set ();
				}
			}
		}*/
		
		public void Dispose ()
		{
			var proc = this.proc;
			lock (lockObj) {
				if (this.proc == null)
					return;
				this.proc = null;
			}
			if (captureOutputThread != null) {
				if (captureOutputThread.IsAlive) {
					try {
						captureOutputThread.Abort ();
					} catch {}
				}
				captureOutputThread = null;
			}
			/*
			if (captureErrorThread != null) {
				if (captureErrorThread.IsAlive) {
					try {
						captureErrorThread.Abort ();
					} catch {}
				}
				captureErrorThread = null;
			}*/
			proc.Dispose ();
		}
		
		public void Cancel ()
		{
			var proc = this.proc;
			if (proc.HasExited)
				return;
			proc.Kill ();
		}
	}
}
