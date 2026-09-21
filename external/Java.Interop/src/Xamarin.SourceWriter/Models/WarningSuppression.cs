using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.SourceWriter
{
	// A single compiler diagnostic that has to be suppressed around one generated member,
	// together with the reason it cannot be avoided. Suppressions are deliberately scoped to
	// the member that needs them so that the same diagnostic elsewhere still breaks the build.
	public class WarningSuppression
	{
		public string Code { get; }
		public string Reason { get; }

		public WarningSuppression (string code, string reason)
		{
			Code = code ?? throw new ArgumentNullException (nameof (code));
			Reason = reason ?? throw new ArgumentNullException (nameof (reason));
		}
	}

	// Implemented by the writers that can emit a member-scoped `#pragma warning disable`.
	public interface ISuppressWarnings
	{
		List<WarningSuppression> SuppressWarnings { get; }
	}

	// A standalone set of suppressions, for the generated constructs that are written
	// directly to a `CodeWriter` instead of through a writer that implements
	// `ISuppressWarnings`.
	public class WarningSuppressionScope : ISuppressWarnings
	{
		public List<WarningSuppression> SuppressWarnings { get; } = new List<WarningSuppression> ();
	}

	public static class WarningSuppressionExtensions
	{
		public static void WriteSuppressWarningsStart (this ISuppressWarnings self, CodeWriter writer)
		{
			foreach (var suppression in Ordered (self)) {
				writer.WriteLine ($"// {suppression.Reason}");
				writer.WriteLine ($"#pragma warning disable {suppression.Code}");
			}
		}

		public static void WriteSuppressWarningsEnd (this ISuppressWarnings self, CodeWriter writer)
		{
			foreach (var suppression in Ordered (self).Reverse ())
				writer.WriteLine ($"#pragma warning restore {suppression.Code}");
		}

		// The same diagnostic can be reported for more than one reason on a single member,
		// and a nested `disable`/`restore` pair for the same code would end the suppression
		// early, so each code is emitted once.
		static IList<WarningSuppression> Ordered (ISuppressWarnings self) =>
			self.SuppressWarnings
				.GroupBy (s => s.Code)
				.OrderBy (g => g.Key, StringComparer.Ordinal)
				.Select (g => g.Count () == 1
					? g.First ()
					: new WarningSuppression (g.Key, string.Join (" ", g.Select (s => s.Reason).Distinct ())))
				.ToList ();
	}
}
