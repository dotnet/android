// 
// AggregateAsyncResult.cs
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

namespace Mono.AndroidTools.Util
{
	//used for implementing asyncresult classes for methods that internally chain several
	//other async results. it should be subclassed to hold needed state
	class AggregateAsyncResult : IAsyncResult
	{
		AsyncCallback callback;
		object state;
		
		public AggregateAsyncResult (AsyncCallback callback, object state)
		{
			this.callback = callback;
			this.state = state;
		}
		
		public void CompleteAsCallback (IAsyncResult ar)
		{
			Complete ();
		}
		
		public void Complete ()
		{
			MarkCompleted ();
			if (callback != null)
				callback (this);
		}
		
		public void CompleteWithError (Exception error)
		{
			Error = error;
			Complete ();
		}
		
		public void CheckError (CancellationToken token = new CancellationToken ())
		{
			token.ThrowIfCancellationRequested ();
			if (!IsCompleted) {
				((IAsyncResult)this).AsyncWaitHandle.WaitOne ();
			}
			if (Error != null) {
				throw Error;
			}
		}
		
		void MarkCompleted ()
		{
			lock (this) {
				IsCompleted = true;
				if (waitHandle != null)
					waitHandle.Set ();
			}
		}		
		
		public Exception Error { get; private set; }
		public bool IsCompleted { get; private set; }	
		
		object IAsyncResult.AsyncState { get { return state; } }	
		
		ManualResetEvent waitHandle;
		
		WaitHandle IAsyncResult.AsyncWaitHandle {
			get {
				lock (this) {
					if (waitHandle == null)
						waitHandle = new ManualResetEvent (IsCompleted);
				}
				return waitHandle;
			}
		}

		bool IAsyncResult.CompletedSynchronously {
			get { return false; }
		}
	}
	
	class AggregateAsyncResult<T> : AggregateAsyncResult
	{
		public AggregateAsyncResult (AsyncCallback callback, object state)
			: base (callback, state)
		{
		}
		
		public AggregateAsyncResult (T arg, AsyncCallback callback, object state)
			: base (callback, state)
		{
			this.Arg = arg;
		}
		
		public T Arg { get; set; }
	}
	
	class AggregateAsyncResult<T1,T2> : AggregateAsyncResult
	{
		public AggregateAsyncResult (AsyncCallback callback, object state)
			: base (callback, state)
		{
		}
		
		public AggregateAsyncResult (T1 arg1, T2 arg2, AsyncCallback callback, object state)
			: base (callback, state)
		{
			this.Arg1 = arg1;
			this.Arg2 = arg2;
		}
		
		public T1 Arg1 { get; set; }
		public T2 Arg2 { get; set; }
	}
}