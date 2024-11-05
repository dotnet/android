using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace jittimes {
	public record struct Timestamp(long nanoseconds) : IComparable {
		static readonly Regex regex = new Regex ("^([0-9]+)s:([0-9]+)::([0-9]+)$");

		public static Timestamp Parse (string time)
		{
			var match = regex.Match (time);
			if (!match.Success || match.Groups.Count <= 3)
				return default;

			var s = Convert.ToInt64 (match.Groups [1].Value);
			var ms = Convert.ToInt32 (match.Groups [2].Value);
			var ns = Convert.ToInt32 (match.Groups [3].Value);
			return new Timestamp(1000_000_000*s + 1000_000*ms + ns);
		}

		static public Timestamp operator - (Timestamp ts1, Timestamp ts2)
			=> new Timestamp(ts1.nanoseconds - ts2.nanoseconds);

		static public Timestamp operator + (Timestamp ts1, Timestamp ts2)
			=> new Timestamp(ts1.nanoseconds + ts2.nanoseconds);

		public override string ToString ()
		{
			var remainder = Math.Abs(nanoseconds);
			var s = remainder / 1000_000_000;
			remainder -= 1000_000_000*s;
			var ms = remainder / 1000_000;
			var ns = remainder - 1000_000*ms;
			var sign = nanoseconds < 0 ? "-" : "";
			var sec = s != 0 ? $"{s}(s):" : "";
			return $"{sign}{sec}{ms}::{ns}";
		}

		public Timestamp Positive ()
			=> new Timestamp(Math.Max(0L, nanoseconds));

		public double Milliseconds ()
			=> nanoseconds / 1000_000;

		public int CompareTo(object o)
		{
			if (!(o is Timestamp other))
				throw new ArgumentException ("Object is not a Timestamp");

			return Comparer<long>.Default.Compare(this.nanoseconds, other.nanoseconds);
		}
	}
}
