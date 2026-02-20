namespace ApplicationUtility;

interface IReporter
{
	void Report (ReportForm form = ReportForm.Standalone, uint sectionLevel = 1);
}
