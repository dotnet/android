using System;
using System.Globalization;

using Android.App;
using Android.Content;

using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests
{
	[TestFixture]
	public class TimeZoneTest
	{
		[Test]
		public void TestDaylightSavingsTime ()
		{
			using (var jtz = Java.Util.TimeZone.Default) {
				CompareTimeZoneData (TimeZone.CurrentTimeZone, TimeZoneInfo.Local, jtz);
			}
		}

		static void CompareTimeZoneData (TimeZone tz, TimeZoneInfo tzi, Java.Util.TimeZone jtz)
		{
			Console.WriteLine ("## Comparing TimeZone Data:");
			Console.WriteLine ("#      TimeZone: StandardName={0}; DaylightName={1}",
					tz.StandardName, tz.DaylightName);
			Console.WriteLine ("#  TimeZoneInfo: StandardName={0}; DaylightName={1}; DisplayName={2}; Id={3}",
				tzi.StandardName, tzi.DaylightName, tzi.DisplayName, tzi.Id);
			Console.WriteLine ("# Java TimeZone: DisplayName={0}; ID={1}",
					jtz.DisplayName, jtz.ID);
			bool found_errors = false;
			for (int year = 2012; year < 2015; ++year) {
				if (tz != null) {
					var dst = tz.GetDaylightChanges (year);
					Console.WriteLine ("Year: {0}; DST: {1} -> {2}", year, dst.Start.ToString ("g"), dst.End.ToString ("g"));
				}
				for (int month = 1; month <= 12; month++) {
					int lastDayInMonth = DateTime.DaysInMonth (year, month);
					for (int day = 1; day <= lastDayInMonth; day++) {
						var localtime   = new DateTime (year, month, day, 10, 00, 00, DateTimeKind.Local);
						var utctime     = localtime.ToUniversalTime ();

						var tz_offset   = tz.GetUtcOffset (localtime);
						var tzi_offset  = tzi.GetUtcOffset (localtime);

						var tz_dst      = tz.IsDaylightSavingTime (localtime);
						var tzi_dst     = tzi.IsDaylightSavingTime (localtime);

						using (var jd = ToDate (localtime)) {
							var jtz_dst     = jtz.InDaylightTime (jd);
							var ms          = (int)(localtime - new DateTime (year, month, day, 0, 0, 0, DateTimeKind.Local)).TotalMilliseconds;
							var jtz_offseti = jtz.GetOffset (1, year, month - 1, day, ((int) localtime.DayOfWeek)+1, ms);
							var jtz_offset  = TimeSpan.FromMilliseconds (jtz_offseti);

							if (tz_offset != tzi_offset || tz_offset != jtz_offset || tzi_offset != jtz_offset ||
									tz_dst != tzi_dst || tz_dst != jtz_dst || tzi_dst != jtz_dst) {
								found_errors = true;
								Console.WriteLine ("MISMATCH! @ {0} [{1}]", localtime, jd.ToLocaleString ());
								Console.WriteLine ("\t     TimeZone Offset: {0}", tz_offset);
								Console.WriteLine ("\t TimeZoneInfo Offset: {0}", tzi_offset);
								Console.WriteLine ("\tJava TimeZone Offset: {0}", jtz_offset);
								Console.WriteLine ("\t        TimeZone DST: {0}", tz_dst);
								Console.WriteLine ("\t    TimeZoneInfo DST: {0}", tzi_dst);
								Console.WriteLine ("\t   Java TimeZone DST: {0}", jtz_dst);
							}
						}
					}
				}
			}
			if (found_errors) {
				var rules = tzi.GetAdjustmentRules ();
				for (int i = 0; i < rules.Length; ++i) {
					var rule = rules [i];
					Console.WriteLine ("# AdjustmentRules[{0}]: DaylightDelta={1}; DateStart={2}; DateEnd={3}; DaylightTransitionStart={4}/{5} @ {6}; DaylightTransitionEnd={7}/{8} @ {9}",
						i,
						rule.DaylightDelta,
						rule.DateStart.ToString ("yyyy-MM-dd"), rule.DateEnd.ToString ("yyyy-MM-dd"),
						rule.DaylightTransitionStart.Month, rule.DaylightTransitionStart.Day,
						rule.DaylightTransitionStart.TimeOfDay.ToString ("hh:mm:ss"),
						rule.DaylightTransitionEnd.Month, rule.DaylightTransitionEnd.Day,
						rule.DaylightTransitionEnd.TimeOfDay.ToString ("hh:mm:ss"));
				}
			}
			Assert.IsFalse (found_errors, "TimeZoneData MISMATCH! See logcat output for details.");
		}

		static Java.Util.Date ToDate (DateTime time)
		{
			return new Java.Util.Date (time.Year - 1900, time.Month - 1, time.Day, time.Hour, time.Minute, time.Second);
		}

		[Test]
		public void Transitions ()
		{
			var hasDst  = TimeZoneInfo.Local.SupportsDaylightSavingTime;
			var tz      = TimeZone.CurrentTimeZone;
			var tzi     = TimeZoneInfo.Local;
			var changes = tz.GetDaylightChanges (2014);
			using (var jtz = Java.Util.TimeZone.Default) {
				var start = changes.Start;
				if (tzi.IsInvalidTime (changes.Start)) {
					// The time is invalid because *it does not exist*.
					// For example, if DST starts at 2AM, the time transition is *actually* ..., 01:58AM, 01:59AM, 03:00AM, 03:01AM, ...
					// There is no 2AM.
					start = start + changes.Delta;
				}
				using (var d = ToDate (start)) {
					Assert.AreEqual (jtz.InDaylightTime (d), tz.IsDaylightSavingTime (start),
							string.Format ("Within DST Start time mismatch: Java({0}) != .NET({1})!", d.ToLocaleString (), start));
				}
				if (tz.IsDaylightSavingTime (start)) {
					var mPreStart = changes.Start - changes.Delta;
					using (var preStart = ToDate (mPreStart)) {
						Assert.AreEqual (jtz.InDaylightTime (preStart), tz.IsDaylightSavingTime (mPreStart),
								string.Format ("DST-1h in-DST mismatch: Java({0}) != .NET({1})", preStart.ToLocaleString (), mPreStart));
					}
					var mPostStart = changes.Start + changes.Delta;
					using (var postStart = ToDate (mPostStart)) {
						Assert.AreEqual (jtz.InDaylightTime (postStart), tz.IsDaylightSavingTime (mPostStart),
								string.Format ("DST+1h in-DST mismatch: Java({0}) != .NET({1})", postStart.ToLocaleString (), mPostStart));
					}
				}
				using (var d = ToDate (changes.End)) {
					Assert.AreEqual (jtz.InDaylightTime (d), tz.IsDaylightSavingTime (changes.End));
					if (tz.IsDaylightSavingTime (changes.Start)) {
						// At end-of-DST, we "gain" an hour, so the previous hour is *repeated*
						// e.g. 12 AM, 1 AM, 1 AM, 2 AM, ...
						// Check *2* hours prior to avoid the repeated hour.
						var mPreEnd = changes.End.AddHours (-2);
						using (var preEnd = ToDate (mPreEnd)) {
							Assert.AreEqual (jtz.InDaylightTime (preEnd), tz.IsDaylightSavingTime (mPreEnd),
									string.Format ("ST-1h in-DST mismatch: Java({0}) != .NET({1})", preEnd.ToLocaleString (), mPreEnd));
						}
						var mPostEnd = changes.End.AddHours (1);
						using (var postEnd = ToDate (mPostEnd)) {
							Assert.AreEqual (jtz.InDaylightTime (postEnd), tz.IsDaylightSavingTime (mPostEnd),
									string.Format ("ST+1h out-DST mismatch: Java({0}) != .NET({1})", postEnd.ToLocaleString (), mPostEnd));
						}
					}
				}
			}
		}

		// https://bugzilla.xamarin.com/show_bug.cgi?id=22955
		[Test]
		public void DateTimeConversions_ShouldNotThrow ()
		{
			Assert.DoesNotThrow (() => {
				DateTime baseTime = new DateTime (2014, 9, 11, 0, 0, 0, 0);
				DateTime epochTime = new DateTime (1970, 1, 1, 0, 0, 0, 0);
				TimeSpan span = baseTime.ToLocalTime () - epochTime.ToLocalTime ();
				Assert.IsNotNull (span);
			}, "Regression test for #22955 failed.");
		}

		static string[] TimeStrings = { @"1969-12-31 18:59:59.999", @"1942-8-25 6:32:52",
			@"2039-02-08T14:42:52.510+06:00", @"2044-08-13T21:07:12.510+02:00", @"2089-11-25T01:07:37-04:00" };
		// https://bugzilla.xamarin.com/show_bug.cgi?id=22955#c2
		// https://bugzilla.xamarin.com/show_bug.cgi?id=22955#c30
		[Test, TestCaseSource (nameof (TimeStrings))]
		public void DateTimeConversions_ShouldNotThrow2 (string timeString)
		{
			Assert.DoesNotThrow (() => {
				var tz = TimeZone.CurrentTimeZone.GetUtcOffset (Convert.ToDateTime (timeString));
				Assert.IsNotNull (tz);
			}, "Regression test for #22955 c2 failed.");
		}

		static int[] Years = { 1962, 1970, 1992, 2014, 2020, 2038, 2062 };
		// https://bugzilla.xamarin.com/show_bug.cgi?id=22955#c17
		// https://bugzilla.xamarin.com/show_bug.cgi?id=22955#c30
		[Test, TestCaseSource (nameof (Years))]
		public void DaylightChangesEndMinusStart_ShouldNotThrow (int year)
		{
			Assert.DoesNotThrow (() => {
				var dst = TimeZone.CurrentTimeZone.GetDaylightChanges (year);
				Assert.IsNotNull (dst);
			}, "Regression test for #22955 c17 failed.");
		}

		// https://bugzilla.xamarin.com/show_bug.cgi?id=22955#c35
		[Test]
		public void DateTimeConversions_ShouldNotThrow3 ()
		{
			Assert.DoesNotThrow (() => {
				var dt = DateTime.MinValue;
				var dt1 = dt.ToLocalTime ();
				Assert.IsNotNull (dt1);
			}, "Regression test for #22955 c35 failed.");
		}

		// https://bugzilla.xamarin.com/show_bug.cgi?id=23405#c23
		[Test]
		public void DifferentDateTimeInit_ShouldHaveSameValue ()
		{
			var utcNow = DateTime.UtcNow;
			DateTime dtFromId = TimeZoneInfo.ConvertTimeFromUtc (utcNow, TimeZoneInfo.FindSystemTimeZoneById (TimeZoneInfo.Local.Id));
			DateTime dtFromLocal = TimeZoneInfo.ConvertTimeFromUtc (utcNow, TimeZoneInfo.Local);
			Assert.AreEqual (dtFromLocal, dtFromId, "Regression test for #23405 c23 failed.");
		}
	}
}
