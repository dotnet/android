//
// AnalyticsService.cs
//
// Author:
//       Greg Munn <greg.munn@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
//

using System;
using System.Collections.Generic;

namespace Mono.AndroidTools
{
	public interface ICustomAnalytics
	{
		/// <summary>
		/// Reports the sdk versions to analytics. These are sent as a single event with properties derived from `values`
		/// </summary>
		void ReportSdkVersions(Dictionary<string, object> values);
	}

	/// <summary>
	/// Provides a way to log analytics to Xamarin Insights, or other analytics service
	/// </summary>
	public static class AnalyticsService
	{
		static ICustomAnalytics Analytics;

		public static bool IsRegistered => Analytics != null;

		public static void SetCustomAnalytics (ICustomAnalytics customAnalytics)
		{
			Analytics = customAnalytics;
		}

		/// <summary>
		/// Reports the sdk versions to analytics. These are sent as a single event with properties derived from `values`
		/// </summary>
		public static void ReportSdkVersions(Dictionary<string, object> values)
		{
			if (Analytics != null) {
				Analytics.ReportSdkVersions(values);
			}
		}

		[Obsolete("This method does nothing. The telemetry API is changing and for the time being we are only sending the minimum of events that need to be processed server side.")]
		public static void Track (string trackId, Dictionary<string, string> table = null)
		{
		}

		[Obsolete("This method does nothing. The telemetry API is changing and for the time being we are only sending the minimum of events that need to be processed server side.")]
		public static void Track (string trackId, string key, string value)
		{
		}

		[Obsolete("This method does nothing. The telemetry API is changing and for the time being we are only sending the minimum of events that need to be processed server side.")]
		public static IDisposable TrackTime (string trackId, Dictionary<string, string> table = null)
		{
			return NullTimeTracker.Default;
		}

		[Obsolete("This method does nothing. The telemetry API is changing and for the time being we are only sending the minimum of events that need to be processed server side.")]
		public static IDisposable TrackTime (string trackId, string key, string value)
		{
			return TrackTime (trackId, new Dictionary<string, string> () { {key, value} });
		}

		[Obsolete("This method does nothing. The telemetry API is changing and for the time being we are only sending the minimum of events that need to be processed server side.")]
		public static void IdentifyTrait(string trait, string value)
		{
		}

		class NullTimeTracker : IDisposable 
		{
			public static NullTimeTracker Default = new NullTimeTracker ();

			public void Dispose ()
			{
			}
		}
	}
}