// 
// WrapperAsyncResult.cs
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
	//for asyncresults that numply wrap an inner IAsyncResult
	// usage pattern is:
	// void BeginFoo (AsyncCallback callback, object state)
	// {
	//      var fooState = new Bar ();
	//      var wr = new WrapperResult (callback, state, blah);
	//      wr.InnerResult = BeginInner (args... foostate... args, wr.WrapperCallback, wr);
	// }
	// RetType EndFoo (IAsyncResult result)
	// {
	//      var wr = (WrapperResult) result;
	//      var fooState = (Bar) wr.WrapperState;
	//      var ret = EndInner (wr.InnerResult);
	//      return Process (ret, fooState);
	// }
	class WrapperAsyncResult : IAsyncResult
	{
		AsyncCallback callback;
		object state;
		
		public WrapperAsyncResult (AsyncCallback callback, object state, object wrapperState)
		{
			this.callback = callback;
			this.state = state;
			this.WrapperState = wrapperState;
		}
		
		public void WrapperCallback (IAsyncResult ar)
		{
			InnerResult = ar;
			callback (this);
		}
		
		public IAsyncResult InnerResult { get; set; }
		public object WrapperState { get; private set; }
		
		object IAsyncResult.AsyncState { get { return state; } }

		WaitHandle IAsyncResult.AsyncWaitHandle {
			get { return InnerResult.AsyncWaitHandle; }
		}

		bool IAsyncResult.CompletedSynchronously {
			get { return InnerResult.CompletedSynchronously; }
		}

		bool IAsyncResult.IsCompleted {
			get { return InnerResult.IsCompleted; }
		}
	}
}