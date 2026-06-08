// 
// AdbTaskSession.cs
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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Mono.AndroidTools.Adb;
using System.Text;

namespace Mono.AndroidTools.Internal
{
	static class AdbClientTaskExtensions
	{
		public static void MakeCancellable (this AdbClient client, CancellationToken cancellationToken)
		{
			cancellationToken.Register (() => client.Dispose (cancellationToken));
		}
		
		public static void MakeCancellable (this AdbSyncClient client, CancellationToken cancellationToken)
		{
			cancellationToken.Register (() => client.Dispose (cancellationToken));
		}
		
		public static Task ConnectAsync (this AdbClient client, TaskCreationOptions options = TaskCreationOptions.None)
		{
			return Task.Factory.FromAsync (client.BeginConnect, client.EndConnect, null, options);
		}
		
		public static Task WriteCommandAsync (this AdbClient client, string command,
			TaskCreationOptions options = TaskCreationOptions.None)
		{
			return Task.Factory.FromAsync (client.BeginWriteCommand, client.EndWriteCommand, command, null, options);
		}
		
		public static Task WriteCommandWithStatusAsync (this AdbClient client, string command,
			TaskCreationOptions options = TaskCreationOptions.None)
		{
			return Task.Factory.FromAsync<string> (client.BeginWriteCommandWithStatus,
				client.EndWriteCommandWithStatus, command, null, options);
		}
		
		public static Task<string> WriteCommandWithMessageAsync (this AdbClient client, string command,
			TaskCreationOptions options = TaskCreationOptions.None)
		{
			return Task.Factory.FromAsync<string,string> (client.BeginWriteCommandWithMessage,
				client.EndWriteCommandWithMessage, command, null, options);
		}
		
		public static Task<string> ReadStringWithLengthAsync (this AdbClient client,
			TaskCreationOptions options = TaskCreationOptions.None)
		{
			return Task.Factory.FromAsync<string> (client.BeginReadStringWithLength,
				client.EndReadStringWithLength, null, options);
		}
		
		public static Task ReadTextAsync (this AdbClient client, Action<string> output,
			TaskCreationOptions options = TaskCreationOptions.None)
		{
			return Task.Factory.FromAsync<Action<string>> (client.BeginReadText, client.EndReadText, output, null, options);
		}
		
		public static Task<string> ReadTextAsync (this AdbClient client,
			TaskCreationOptions options = TaskCreationOptions.None)
		{
			var sw = new StringWriter ();
			return Task.Factory.FromAsync<Action<string>, string> (client.BeginReadText, ar => {
				client.EndReadText (ar);
				return sw.ToString ();
			}, sw.Write, null, options);
		}
		
		public static Task ConnectTransportAsync (this AdbClient client, string deviceID,
			TaskCreationOptions options = TaskCreationOptions.None)
		{
			return Task.Factory.FromAsync (client.BeginConnectTransport, client.EndConnectTransport, deviceID, options);
		}
		
		public static Task ConnectSyncSessionAsync (this AdbSyncClient client, string deviceID,
			TaskCreationOptions options = TaskCreationOptions.None)
		{
			return Task.Factory.FromAsync (client.BeginConnectSyncSession, client.EndConnectSyncSession, deviceID, options);
		}

		[Obsolete ("Use another overload with 'AdbSyncClient.PushOptions' parameter.")]
		public static Task<long> PushSyncItemsAsync (
			this AdbSyncClient client,
			AdbSyncDirectory targetDir,
			string remoteParentDir,
			bool dryRun,
			Action<AdbSyncNotification> notifySync,
			Action<string> notifyPhase,
			AdbProgressReporter notifyProgress,
			CancellationToken cancellationToken = new CancellationToken ())
		{
			return PushSyncItemsAsync (client, targetDir, remoteParentDir, new AdbSyncClient.PushOptions () {
				DryRun = dryRun,
				NotifySync = notifySync,
				NotifyPhase = notifyPhase,
				NotifyProgress = notifyProgress
				}, cancellationToken);
		}
		
		public static Task<long> PushSyncItemsAsync (
			this AdbSyncClient client,
			AdbSyncDirectory targetDir,
			string remoteParentDir,
			AdbSyncClient.PushOptions options,
			CancellationToken cancellationToken = new CancellationToken ())
		{
			return FromAsync<AdbSyncDirectory,string,AdbSyncClient.PushOptions> (
				client.BeginPushSyncItems, client.EndPushSyncItems,
				targetDir, remoteParentDir, options);
		}

		public static Task<long> PushAsync (
			this AdbSyncClient client,
			string localFilePath,
			string remoteFilePath,
			AdbProgressReporter notifyProgress,
			TaskCreationOptions options = TaskCreationOptions.None)
		{
			return Task.Factory.FromAsync<string,string,AdbProgressReporter,long> (
				client.BeginPush, client.EndPush, localFilePath, remoteFilePath, notifyProgress, options
			);
		}

		public static Task<long> PushAsync (
			this AdbSyncClient client,
			Stream contents,
			string remoteFilePath,
			AdbProgressReporter notifyProgress,
			TaskCreationOptions options = TaskCreationOptions.None)
		{
			return Task.Factory.FromAsync<Stream,string,AdbProgressReporter,long> (
				client.BeginPush, client.EndPush, contents, remoteFilePath, notifyProgress, options
			);
		}

		public static Task<long> PushAsync (
			this AdbSyncClient client,
			Stream contents,
			bool leaveOpen,
			string remoteFilePath,
			AdbProgressReporter notifyProgress,
			TaskCreationOptions options = TaskCreationOptions.None)
		{

			if (leaveOpen)
				return Task.Factory.FromAsync<Stream, string,AdbProgressReporter,long> (
					client.BeginPushLeaveOpen, client.EndPushLeaveOpen, contents, remoteFilePath, notifyProgress, options
				);
			return PushAsync (client, contents, remoteFilePath, notifyProgress, options);
		}

		[Obsolete ("Use another overload with AdbSyncClient.PushOptions argument.")]
		public static Task<long> PushDirectoryAsync (
			this AdbSyncClient client,
			string localDirectoryPath,
			string remoteDirectoryPath,
			bool checkTimestamps,
			bool removeUnknown,
			bool dryRun,
			bool removeBeforeCopy,
			Action<AdbSyncNotification> notifySync,
			Action<string> notifyPhase,
			AdbProgressReporter notifyProgress,
			CancellationToken cancellationToken = new CancellationToken ())
		{
			return PushDirectoryAsync (client, new AdbSyncClient.PushOptions () {
				LocalDirectoryPath = localDirectoryPath,
				RemoteDirectoryPath = remoteDirectoryPath,
				CheckTimestamps = checkTimestamps,
				RemoveUnknown = removeUnknown,
				DryRun = dryRun,
				RemoveBeforeCopy = removeBeforeCopy,
				NotifySync = notifySync,
				NotifyPhase = notifyPhase,
				NotifyProgress = notifyProgress
				}, cancellationToken);
		}
		
		public static Task<long> PushDirectoryAsync (
			this AdbSyncClient client,
			AdbSyncClient.PushOptions options,
			CancellationToken cancellationToken = new CancellationToken ())
		{
			return FromAsync<AdbSyncClient.PushOptions> (
				client.BeginPushDirectory, client.EndPushDirectory, options);
		}

		public static Task<AdbFileInfo> StatAsync (
			this AdbSyncClient client,
			string remoteFilePath,
			TaskCreationOptions options = TaskCreationOptions.None)
		{
			return Task.Factory.FromAsync<string, AdbFileInfo> (
				client.BeginStat, client.EndStat, remoteFilePath, options
			);
		}

		static Task<long> FromAsync<T> (
							Func<T, AsyncCallback, object, IAsyncResult> beginMethod,
							Func<IAsyncResult, long> endMethod,
							T arg)
		{
			var tcs = new TaskCompletionSource<long> (null, TaskCreationOptions.None);
			beginMethod (arg, l => InnerInvoke (tcs, endMethod, l), null);

			return tcs.Task;
		}
		
		static Task<long> FromAsync<T1, T2, T3> (
							Func<T1, T2, T3, AsyncCallback, object, IAsyncResult> beginMethod,
							Func<IAsyncResult, long> endMethod,
							T1 arg1, T2 arg2, T3 arg3)
		{
			var tcs = new TaskCompletionSource<long> (null, TaskCreationOptions.None);
			beginMethod (arg1, arg2, arg3, l => InnerInvoke (tcs, endMethod, l), null);

			return tcs.Task;
		}
		
		static Task<long> FromAsync<T1, T2, T3, T4, T5, T6, T7, T8, T9> (
							Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, AsyncCallback, object, IAsyncResult> beginMethod,
							Func<IAsyncResult, long> endMethod,
							T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
		{
			var tcs = new TaskCompletionSource<long> (null, TaskCreationOptions.None);
			beginMethod (arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, l => InnerInvoke (tcs, endMethod, l), null);

			return tcs.Task;
		}

		static Task<long> FromAsync<T1, T2, T3, T4, T5, T6, T7> (
							Func<T1, T2, T3, T4, T5, T6, T7, AsyncCallback, object, IAsyncResult> beginMethod,
							Func<IAsyncResult, long> endMethod,
							T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
		{
			var tcs = new TaskCompletionSource<long> (null, TaskCreationOptions.None);
			beginMethod (arg1, arg2, arg3, arg4, arg5, arg6, arg7, l => InnerInvoke (tcs, endMethod, l), null);

			return tcs.Task;
		}

		static void InnerInvoke (TaskCompletionSource<long> tcs, Func<IAsyncResult, long> endMethod, IAsyncResult l)
		{
			try {
				tcs.SetResult (endMethod (l));
			} catch (OperationCanceledException) {
				tcs.SetCanceled ();
			} catch (Exception e) {
				tcs.SetException (e);
			}
		}

		public static Task Cleanup (this Task task, IDisposable client, AndroidTaskLog log, CancellationToken token)
		{
			return task.ContinueWith (t => {
				// we register the client to be disposed when the cancellation token is canceled, calling Dispos
				// here in that case causes a dispose race where, if this one wins, we get unhandled dispose / NRE exceptions
				// because the client does not know that it was canceled
				if (!token.IsCancellationRequested)
					client.Dispose ();
				
				if (log != null) {
					if (task.IsCanceled)
						AndroidLogger.LogTask (log.Complete ("Cancelled"));
					else if (task.IsFaulted)
						AndroidLogger.LogTask (log.Complete ("Faulted: " + t.Exception));
					else
						AndroidLogger.LogTask (log.Complete ("Completed"));
				}
				//swallow ObjectDisposedException if cancelling
				if (t.IsFaulted && token.IsCancellationRequested) {
					t.Exception.Handle (e => e is ObjectDisposedException);
					var tcs = new TaskCompletionSource<object> ();
					tcs.SetCanceled ();
					return tcs.Task;
				}
				return t;
			}, TaskContinuationOptions.ExecuteSynchronously).Unwrap ();
		}

		public static Task Cleanup (this Task task, IDisposable client, CancellationToken token)
		{
			return Cleanup (task, client, null, token);
		}

		public static Task<T> Cleanup<T> (this Task<T> task, IDisposable client, AndroidTaskLog log, CancellationToken token)
		{
			return task.ContinueWith (t => {
				// we register the client to be disposed when the cancellation token is canceled, calling Dispos
				// here in that case causes a dispose race where, if this one wins, we get unhandled dispose / NRE exceptions
				// because the client does not know that it was canceled
				if (!token.IsCancellationRequested)
					client.Dispose ();


				if (log != null) {
					if (task.IsCanceled)
						AndroidLogger.LogTask (log.Complete ("Cancelled"));
					else if (task.IsFaulted)
						AndroidLogger.LogTask (log.Complete ("Faulted: " + t.Exception));
					else
						AndroidLogger.LogTask (log.Complete ("Completed"));
				}
				//swallow ObjectDisposedException if cancelling
				if (t.IsFaulted && token.IsCancellationRequested) {
					t.Exception.Handle (e => e is ObjectDisposedException);
					var tcs = new TaskCompletionSource<T> ();
					tcs.SetCanceled ();
					return tcs.Task;
				}
				return t;
			}, token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap ();
		}

		public static Task<T> Cleanup<T> (this Task<T> task, IDisposable client, CancellationToken token)
		{
			return Cleanup (task, client, null, token);
		}

		public static async Task<byte []> ReadBytesAsync (this AdbClient client, int buffer = 1024, CancellationToken token = default (CancellationToken))
		{
			using (var output = new MemoryStream ()) {
				await client.Stream.CopyToAsync (output, buffer, token);
				return output.ToArray ();
			}
		}

		public static async Task<string> ReadStringAsync (this AdbClient client, int buffer = 1024, CancellationToken token = default (CancellationToken))
		{
			using (var reader = new StreamReader (client.Stream, encoding: Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: buffer, leaveOpen: true)) {
				return await reader.ReadToEndAsync ();
			}
		}

		public static Task WriteStreamAsync (this AdbClient client, Stream inputStream, int buffer = 2048)
			=> inputStream.CopyToAsync (client.Stream, buffer);
	}
}
