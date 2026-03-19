using System;

namespace ApplicationUtility;

/// <summary>
/// Generates a report for an ELF shared library, including name, architecture, size,
/// alignment, debug info, build ID, and Android identification.
/// </summary>
[AspectReporter (typeof (SharedLibrary))]
class SharedLibraryReporter : BaseReporter
{
	public const string AlignmentLabel = "Alignment";
	public const string AndroidIdentLabel = "Android ident";
	public const string BuildIdLabel = "Build ID";
	public const string DebugInfoLabel = "Debug info";
	public const string DebugLinkLabel = "External debug info file name";
	public const string DotNetWrapperLabel = ".NET for Android data wrapper";
	public const string MonoAotLabel = "Mono AOT image";
	public const string NativeAotLabel = "NativeAOT";
	public const string SizeLabel = "Size";
	public const string SonameLabel = "Soname";
	public const string XamarinAppLabel = "Application specific data and code";

	protected SharedLibrary Library { get; }

	protected override string AspectName => Library.AspectName;
	protected virtual string LibraryKind => "Shared library";
	protected override string ShortDescription => Library.Name;

	public SharedLibraryReporter (SharedLibrary library, MarkdownDocument doc)
		: base (doc)
	{
		Library = library;
	}

	protected override void DoReport (ReportForm form, uint sectionLevel)
	{
		switch (form) {
			case ReportForm.Standalone:
				DoStandaloneReport ();
				break;

			case ReportForm.SimpleList:
				DoListReport ();
				break;

			default:
				throw new NotSupportedException ($"Unsupported report form '{form}'");
		}
	}

	protected virtual void DoStandaloneReport ()
	{
		AddAspectDesc (LibraryKind);

		AddSection ("Generic ELF shared library info");
		ReportDoc.BeginList (appendLine: false);
		AddCommonItems ();
		ReportDoc.EndList ().EndListItem (appendLine: false);
	}

	protected virtual void DoListReport (bool startWithNewLine = true)
	{
		ReportDoc.BeginList (appendLine: startWithNewLine);
		AddCommonItems ();
		ReportDoc.EndList ().EndListItem (appendLine: false);
	}

	void AddCommonItems ()
	{
		AddNativeArchListItem (Library.TargetArchitecture);
		AddSoname ();
		AddBuildId ();
		AddSize ();
		AddAlignment ();
		AddDebugInfo ();
		AddDebugInfoLink ();
		AddMonoAot ();
		AddNativeAot ();
		AddXamarinApp ();
		AddDotNetWrapper ();
		AddAndroidIdent ();
	}

	void AddSoname ()
	{
		if (!Library.HasSoname) {
			return;
		}

		ReportDoc.AddLabeledListItem (SonameLabel, ValueOrNone (Library.Soname));
	}

	void AddBuildId (bool appendLine = true)
	{
		ReportDoc.AddLabeledListItem (BuildIdLabel, ValueOrNone (Library.BuildID), appendLine: appendLine);
	}

	void AddAlignment (bool appendLine = true)
	{
		ReportDoc.AddLabeledListItem (AlignmentLabel, $"{Utilities.SizeToString (Library.Alignment)}", appendLine: appendLine);
	}

	void AddDebugInfo (bool appendLine = true)
	{
		ReportDoc.AddLabeledListItem (DebugInfoLabel, $"{YesNo (Library.HasDebugInfo)}", appendLine: appendLine);
	}

	void AddDebugInfoLink (bool appendLine = true)
	{
		if (!Library.HasDebugLink) {
			return;
		}

		ReportDoc.AddLabeledListItem (DebugLinkLabel, ValueOrNone (Library.DebugLink), appendLine: appendLine);
	}

	void AddSize (bool appendLine = true)
	{
		ReportDoc.AddLabeledListItem (SizeLabel, $"{Utilities.SizeToString (Library.Size)}", appendLine: appendLine);
	}

	void AddDotNetWrapper (bool appendLine = true)
	{
		var lib = Library as DotNetAndroidWrapperSharedLibrary;
		if (lib == null) {
			return;
		}

		ReportDoc.AddLabeledListItem (DotNetWrapperLabel, $"{YesNo (true)}; Payload size: {Utilities.SizeToString (lib.PayloadSize)}", appendLine: appendLine);
	}

	void AddXamarinApp (bool appendLine = true)
	{
		var lib = Library as XamarinAppSharedLibrary;
		if (lib == null) {
			return;
		}

		ReportDoc.AddLabeledListItem (XamarinAppLabel, $"{YesNo (true)}; Format tag: 0x{lib.FormatTag:x}", appendLine: appendLine);
	}

	void AddNativeAot (bool appendLine = true)
	{
		if (Library is not NativeAotSharedLibrary) {
			return;
		}

		ReportDoc.AddLabeledListItem (NativeAotLabel, YesNo (true), appendLine: appendLine);
	}

	void AddMonoAot (bool appendLine = true)
	{
		if (Library is not MonoAotSharedLibrary) {
			return;
		}

		ReportDoc.AddLabeledListItem (MonoAotLabel, YesNo (true), appendLine: appendLine);
	}

	void AddAndroidIdent (bool appendLine = true)
	{
		if (!Library.HasAndroidIdent) {
			return;
		}

		ReportDoc.AddLabeledListItem (AndroidIdentLabel, ValueOrNone (Library.AndroidIdent));
	}
}
