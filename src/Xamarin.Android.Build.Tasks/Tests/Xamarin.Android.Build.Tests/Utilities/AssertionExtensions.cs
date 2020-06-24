using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	public static class AssertionExtensions
	{
		public static void AssertTargetIsSkipped (this BuildOutput output, string target, int? occurrence = null)
		{
			if (occurrence != null)
				Assert.IsTrue (output.IsTargetSkipped (target), $"The target {target} should have been skipped. ({occurrence})");
			else
				Assert.IsTrue (output.IsTargetSkipped (target), $"The target {target} should have been skipped.");
		}

		public static void AssertTargetIsNotSkipped (this BuildOutput output, string target, int? occurrence = null)
		{
			if (occurrence != null)
				Assert.IsFalse (output.IsTargetSkipped (target), $"The target {target} should have *not* been skipped. ({occurrence})");
			else
				Assert.IsFalse (output.IsTargetSkipped (target), $"The target {target} should have *not* been skipped.");
		}

		public static void AssertTargetIsPartiallyBuilt (this BuildOutput output, string target, int? occurrence = null)
		{
			if (occurrence != null)
				Assert.IsTrue (output.IsTargetPartiallyBuilt (target), $"The target {target} should have been partially built. ({occurrence})");
			else
				Assert.IsTrue (output.IsTargetPartiallyBuilt (target), $"The target {target} should have been partially built.");
		}
	}
}
