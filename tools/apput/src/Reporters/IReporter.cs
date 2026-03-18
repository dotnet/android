namespace ApplicationUtility;

/// <summary>
/// Interface for aspect reporters that generate Markdown reports for a specific aspect type.
/// </summary>
interface IReporter
{
	/// <summary>
	/// Generates a report for the aspect.
	/// </summary>
	/// <param name="form">Whether the report is standalone or nested within a larger report.</param>
	/// <param name="sectionLevel">The Markdown heading level to start at.</param>
	void Report (ReportForm form = ReportForm.Standalone, uint sectionLevel = 1);
}
