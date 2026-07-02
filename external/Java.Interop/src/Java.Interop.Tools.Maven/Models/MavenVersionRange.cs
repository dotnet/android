using System.Collections.Generic;
using Java.Interop.Tools.Maven.Extensions;

namespace Java.Interop.Tools.Maven.Models;

public class MavenVersionRange
{
	public string? MinVersion { get; private set; }
	public string? MaxVersion { get; private set; }
	public bool IsMinInclusive { get; private set; } = true;
	public bool IsMaxInclusive { get; private set; }
	public bool HasLowerBound { get; private set; }
	public bool HasUpperBound { get; private set; }

	// Adapted from https://github.com/Redth/MavenNet/blob/master/MavenNet/MavenVersionRange.cs
	// Original version uses NuGetVersion, which doesn't cover all "valid" Maven version cases
	public static IEnumerable<MavenVersionRange> Parse (string range)
	{
		if (!range.HasValue ())
			yield break;

		// Do a pass over the range string to parse out version groups
		// eg: (1.0],(1.1,]
		var in_group = false;
		var current_group = string.Empty;

		foreach (var c in range) {
			if (c == '(' || c == '[') {
				current_group += c;
				in_group = true;
			} else if (c == ')' || c == ']' || (!in_group && c == ',')) {
				// Don't add the , separating groups
				if (in_group)
					current_group += c;

				in_group = false;

				if (current_group.HasValue ())
					yield return ParseSingle (current_group);

				current_group = string.Empty;
			} else {
				current_group += c;
			}
		}

		if (!string.IsNullOrEmpty (current_group))
			yield return ParseSingle (current_group);
	}

	static MavenVersionRange ParseSingle (string range)
	{
		var mv = new MavenVersionRange ();

		// Check for opening ( or [
		if (range [0] == '(') {
			mv.IsMinInclusive = false;
			range = range.Substring (1);
		} else if (range [0] == '[') {
			range = range.Substring (1);
		}

		var last = range.Length - 1;

		// Check for closing ) or ]
		if (range [last] == ')') {
			mv.IsMaxInclusive = false;
			range = range.Substring (0, last);
		} else if (range [last] == ']') {
			mv.IsMaxInclusive = true;
			range = range.Substring (0, last);
		}

		// Look for a single value
		if (!range.Contains (",")) {
			mv.MinVersion = range;
			mv.HasLowerBound = true;

			// Special case [1.0]
			if (mv.IsMinInclusive && mv.IsMaxInclusive) {
				mv.MaxVersion = range;
				mv.HasUpperBound = true;
			}

			return mv;
		}

		// Split the 2 values (note either can be empty)
		var lower = range.FirstSubset (',').Trim ();
		var upper = range.LastSubset (',').Trim ();

		if (lower.HasValue ()) {
			mv.MinVersion = lower;
			mv.HasLowerBound = true;
		}

		if (upper.HasValue ()) {
			mv.MaxVersion = upper;
			mv.HasUpperBound = true;
		}

		return mv;
	}

	public bool ContainsVersion (MavenVersion version)
	{
		if (HasLowerBound) {
			var min_version = MavenVersion.Parse (MinVersion!);

			if (IsMinInclusive && version.CompareTo (min_version) < 0)
				return false;
			else if (!IsMinInclusive && version.CompareTo (min_version) <= 0)
				return false;
		}

		if (HasUpperBound) {
			var max_version = MavenVersion.Parse (MaxVersion!);

			if (IsMaxInclusive && version.CompareTo (max_version) > 0)
				return false;
			else if (!IsMaxInclusive && version.CompareTo (max_version) >= 0)
				return false;
		}

		return true;
	}
}
