namespace ApplicationUtility;

/// <summary>
/// Form in which the object report should be generated.
/// </summary>
enum ReportForm
{
	/// <summary>
	/// Standalone format means the reporter can generate the document in
	/// an shape or form it wants as it's not part of a larger (outer)
	/// report.
	/// </summary>
	Standalone,

	/// <summary>
	/// Report should be rendered as a simple list, one bit of information
	/// per list item. It is meant to be part of a larger (outer)
	/// report.
	/// </summary>
	SimpleList,

	/// <summary>
	/// Report is in a subsection of another report.
	/// </summary>
	Subsection,
}
