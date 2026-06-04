//
// IAndroidDevice.cs
//
// Author:
//       Greg Munn <greg.munn@xamarin.com>
//
// Copyright (c) 2015 Xamarin Inc
//

using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

namespace Mono.AndroidTools
{
	/// <summary>
	/// Represents a connected android device. 
	/// TODO: add additional methods to IAndroidDevice as needed
	/// </summary>
	public interface IAndroidDevice
	{
		/// <summary>
		/// Gets the device ID
		/// </summary>
		string ID { get; }

		/// <summary>
		/// Gets the id of a process running on the device with the given package name.
		/// Returns 0 if no process was found.
		/// </summary>
		Task<int> GetProcessId (string packageName);

		/// <summary>
		/// Gets the id of a process running on the device with the given package name
		/// Returns 0 if no process was found.
		/// </summary>
		Task<int> GetProcessId (string packageName, CancellationToken cancellationToken);

		/// <summary>
		/// Reads logcat continuously until cancelled. Invokes callback for each entry and excludes entries with a Tag in excludedTags
		/// </summary>
		Task GetLogCat (Action<AndroidLogCatEntry> callback, CancellationToken cancellationToken, string[] excludedLogTags = null);

		/// <summary>
		/// Sets the property with the given value
		/// </summary>
		Task SetProperty (string property, string value);

		Task<Dictionary<string,string>> GetProperties ();

		AndroidDeviceProperties Properties { get; }
	}
}
