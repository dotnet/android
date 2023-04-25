using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace jittimes {
	public struct Timestamp : IComparable {
		public Int64 seconds;
		public int milliseconds;
		public int nanoseconds;

		static readonly Regex regex = new Regex ("^([0-9]+)s:([0-9]+)::([0-9]+)$");

		public static Timestamp Parse (string time)
		{
			Timestamp ts = new Timestamp ();

			var match = regex.Match (time);
			if (!match.Success || match.Groups.Count <= 3) {
				ts.seconds = 0;
				ts.milliseconds = 0;
				ts.nanoseconds = 0;
				return ts;
			}

			var culture = CultureInfo.InvariantCulture;
			ts.seconds = Convert.ToInt64 (match.Groups [1].Value, culture);
			ts.milliseconds = Convert.ToInt32 (match.Groups [2].Value, culture);
			ts.nanoseconds = Convert.ToInt32 (match.Groups [3].Value, culture);

			return ts;
		}

		static public Timestamp operator - (Timestamp ts1, Timestamp ts2)
		{
			Timestamp result = new Timestamp ();

			if (ts1.nanoseconds >= ts2.nanoseconds)
				result.nanoseconds = ts1.nanoseconds - ts2.nanoseconds;
			else {
				result.nanoseconds = 1000000 + ts1.nanoseconds - ts2.nanoseconds;
				result.milliseconds--;
			}

			if (ts1.milliseconds >= ts2.milliseconds)
				result.milliseconds += ts1.milliseconds - ts2.milliseconds;
			else {
				result.milliseconds += 1000 + ts1.milliseconds - ts2.milliseconds;
				result.seconds--;
			}

			result.seconds += ts1.seconds - ts2.seconds;

			return result;
		}

		static public Timestamp operator + (Timestamp ts1, Timestamp ts2)
		{
			Timestamp result = new Timestamp {
				nanoseconds = ts1.nanoseconds + ts2.nanoseconds
			};

			if (result.nanoseconds > 1000000) {
				result.milliseconds += result.nanoseconds / 1000000;
				result.nanoseconds %= 1000000;
			}

			result.milliseconds += ts1.milliseconds + ts2.milliseconds;

			if (result.milliseconds > 1000) {
				result.seconds += result.milliseconds / 1000;
				result.milliseconds %= 1000;
			}

			return result;
		}

		public override string ToString ()
		{
			var sec = seconds != 0 ? $"{seconds}(s):" : "";

			return $"{sec}{milliseconds}::{nanoseconds}";
		}

		public double Milliseconds ()
		{
			return seconds * 1000.0 + (double)milliseconds + nanoseconds / 1000000.0;
		}

		public int CompareTo (object o)
		{
			if (!(o is Timestamp other))
				throw new ArgumentException ("Object is not a Timestamp");

			if (seconds > other.seconds)
				return 1;

			if (seconds < other.seconds)
				return -1;

			if (milliseconds > other.milliseconds)
				return 1;

			if (milliseconds < other.milliseconds)
				return -1;

			if (nanoseconds > other.nanoseconds)
				return 1;

			if (nanoseconds < other.nanoseconds)
				return -1;

			return 0;
		}
	}
}
