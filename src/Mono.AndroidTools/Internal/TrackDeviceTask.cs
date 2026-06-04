// 
// TrackDeviceTask.cs
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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mono.AndroidTools.Adb;
using Mono.AndroidTools.Internal;

namespace Mono.AndroidTools
{
	class TrackDeviceTask
	{
		AdbClient client;
		TaskCompletionSource<object> tcs = new TaskCompletionSource<object> ();
		CancellationToken cancellationToken;
		Action<List<AndroidDevice>> action;

		public TrackDeviceTask (AdbClient client, Action<List<AndroidDevice>> action, CancellationToken cancellationToken)
		{
			this.client = client;
			this.cancellationToken = cancellationToken;
			this.action = action;
		}

		public Task Start ()
		{
			client.BeginConnect (Connected, this);
			return tcs.Task;
		}

		bool CheckCancelled ()
		{
			if (cancellationToken.IsCancellationRequested) {
				tcs.SetCanceled ();
				return true;
			}
			return false;
		}

		static void Connected (IAsyncResult r)
		{
			var td = (TrackDeviceTask)r.AsyncState;
			if (td.CheckCancelled ())
				return;
			try {
				td.client.EndConnect (r);
				td.client.BeginWriteCommandWithStatus ("host:track-devices", GotStatus, td);
			} catch (Exception ex) {
				if (td.CheckCancelled ())
					return;
				td.tcs.SetException (ex);
			}
		}

		static void GotStatus (IAsyncResult r)
		{
			var td = (TrackDeviceTask)r.AsyncState;
			if (td.CheckCancelled ())
				return;
			try {
				td.client.EndWriteCommandWithStatus (r);
				td.client.BeginReadStringWithLength (GotString, td);
			} catch (Exception ex) {
				if (td.CheckCancelled ())
					return;
				td.tcs.SetException (ex);
			}
		}

		static void GotString (IAsyncResult r)
		{
			var td = (TrackDeviceTask)r.AsyncState;
			if (td.CheckCancelled ())
				return;
			try {
				var str = td.client.EndReadStringWithLength (r);
				AndroidLogger.LogDebug ("TrackDeviceTask got: " + str.Trim ().Replace ("\n", ", "));
				var deviceList = AdbOutputParsing.ParseDeviceList (str);
				td.action (deviceList);
				td.client.BeginReadStringWithLength (GotString, td);
			} catch (Exception ex) {
				if (td.CheckCancelled ())
					return;
				td.tcs.SetException (ex);
			}
		}
	}
}
