using System;
using System.Text;

namespace ApplicationUtility;

[AspectReporter (typeof (SharedLibrary))]
class SharedLibraryReporter : BaseReporter
{
	protected SharedLibrary Library { get; }

	protected override string AspectName => SharedLibrary.AspectName;
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

	// TODO: migrate to Markdown
	protected virtual void DoStandaloneReport ()
	{
		WriteAspectDesc (LibraryKind);

		WriteSubsectionBanner ("Generic ELF shared library info");
		WriteNativeArch (Library.TargetArchitecture);

		if (Library.HasSoname) {
			WriteItem ("Soname", ValueOrNone (Library.Soname));
		}
		WriteItem ("Build ID", ValueOrNone (Library.BuildID));
		WriteDebugInfoDesc ();
		WriteAlignmentInfo ();

		if (Library.HasAndroidIdent) {
			WriteSubsectionBanner ("Android-specific ELF shared library info");
			WriteItem ("Android ident", ValueOrNone (Library.AndroidIdent));
		}
	}

	protected virtual void DoListReport ()
	{
		ReportDoc.BeginList ();
		AddNativeArchListItem (Library.TargetArchitecture);
		if (Library.HasSoname) {
			ReportDoc.AddLabeledListItem ("Soname", ValueOrNone (Library.Soname));
		}
		AddBuildId ();
		AddSize ();
		AddAlignment ();
		AddDebugInfo ();
		AddAndroidIdent (appendLine: false);

		ReportDoc.EndList ().EndListItem ();
	}

	void AddSoname ()
	{
		if (!Library.HasSoname) {
			return;
		}

		ReportDoc.AddLabeledListItem ("Soname", ValueOrNone (Library.Soname));
	}

	void AddBuildId (bool appendLine = true)
	{
		ReportDoc.AddLabeledListItem ("Build ID", ValueOrNone (Library.BuildID), appendLine: appendLine);
	}

	void AddAlignment (bool appendLine = true)
	{
		ReportDoc.AddLabeledListItem ("Alignment", $"{Library.Alignment}", appendLine: appendLine);
	}

	void AddDebugInfo (bool appendLine = true)
	{
		ReportDoc.AddLabeledListItem ("Debug info", $"{YesNo (Library.HasDebugInfo)}", appendLine: appendLine);
	}

	void AddSize (bool appendLine = true)
	{
		ReportDoc.AddLabeledListItem ("Size", $"{Library.Size}", appendLine: appendLine);
	}

	void AddAndroidIdent (bool appendLine = true)
	{
		if (!Library.HasAndroidIdent) {
			return;
		}

		// TODO: fix output, currently produces gibberish
		ReportDoc.AddLabeledListItem ("Android ident", "FIXME" /* ValueOrNone (library.AndroidIdent) */);
	}

	protected void WriteDebugInfoDesc ()
	{
		WriteItem ("Has debug info", YesNo (Library.HasDebugInfo));

		var sb = new StringBuilder (YesNo (Library.HasDebugLink));
		if (Library.HasDebugLink) {
			sb.Append (" ('");
			sb.Append (Library.DebugLink);
			sb.Append ("')");
		}
		WriteItem ("Has debug link", sb.ToString ());
	}

	protected void WriteAlignmentInfo ()
	{
		var sb = new StringBuilder ();
		sb.Append ($"0x{Library.Alignment:x} (");
		if (!Library.AlignmentCompatibleWith16k) {
			sb.Append ("NOT ");
		}
		sb.Append ("compatible with Android 16k library alignment requirement)");
		WriteLabel ("Alignment");
		WriteLine (Library.AlignmentCompatibleWith16k ? ValidValueColor : InvalidValueColor, sb.ToString ());
	}
}
