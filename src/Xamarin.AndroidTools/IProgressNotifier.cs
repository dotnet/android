// 
// IProgressNotifier.cs
//  
// Authors:
//       Jonathan Pobst <jpobst@xamarin.com>
// 
// Copyright 2012 Xamarin Inc. All rights reserved.
// 

using System;

namespace Xamarin.AndroidTools
{
	public interface IProgressNotifier
	{
		void BeginStep (string step);
		void EndStep (string step);

		void ReportMessage (string message);
		void ShowErrorDialog (string title, string message);
		void ShowErrorDialog (string title, string message, Exception ex);
		void ReportProgress (long copiedBytes, long totalBytes);
	}
}
