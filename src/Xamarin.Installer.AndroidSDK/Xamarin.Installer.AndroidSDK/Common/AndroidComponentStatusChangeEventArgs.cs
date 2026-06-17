//
//  Authors:
//    Marek Habersack <grendel@twistedcode.net>
//
//  Copyright (c) 2017, Microsoft, Inc (http://microsoft.com)
//
//  All rights reserved.
//
using System;

namespace Xamarin.Installer.AndroidSDK.Common
{
	/// <summary>
	/// Android component status change event arguments.
	/// </summary>
	public class AndroidComponentStatusChangeEventArgs : EventArgs
	{
		/// <summary>
		/// Gets the new component status.
		/// </summary>
		/// <value>The status.</value>
		public AndroidComponentStatus Status { get; }

		/// <summary>
		/// Gets the component whose status changed
		/// </summary>
		/// <value>The component.</value>
		public IAndroidComponent Component { get; }

		internal AndroidComponentStatusChangeEventArgs (AndroidComponentStatus status, IAndroidComponent component)
		{
			Component = component ?? throw new ArgumentNullException (nameof (component));
			Status = status;
		}
	}
}
