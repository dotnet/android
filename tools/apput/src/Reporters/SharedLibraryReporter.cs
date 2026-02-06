using System;
using System.Text;

namespace ApplicationUtility;

[AspectReporter (typeof (NativeAotSharedLibrary))]
class SharedLibraryReporter : BaseReporter
{
	readonly SharedLibrary library;

	protected override string AspectName => SharedLibrary.AspectName;
	protected virtual string LibraryKind => "Shared library";
	protected override string ShortDescription => library.Name;

	public SharedLibraryReporter (SharedLibrary library, MarkdownDocument doc)
		: base (doc)
	{
		this.library = library;
	}

	protected override void DoReport (ReportForm form)
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
	void DoStandaloneReport ()
	{
		WriteAspectDesc (LibraryKind);

		WriteSubsectionBanner ("Generic ELF shared library info");
		WriteNativeArch (library.TargetArchitecture);

		if (library.HasSoname) {
			WriteItem ("Soname", ValueOrNone (library.Soname));
		}
		WriteItem ("Build ID", ValueOrNone (library.BuildID));
		WriteDebugInfoDesc ();
		WriteAlignmentInfo ();

		if (library.HasAndroidIdent) {
			WriteSubsectionBanner ("Android-specific ELF shared library info");
			WriteItem ("Android ident", ValueOrNone (library.AndroidIdent));
		}
	}

	void DoListReport ()
	{
		ReportDoc.BeginList ();
		AddNativeArchListItem (library.TargetArchitecture);
		if (library.HasSoname) {
			ReportDoc.AddLabeledListItem ("Soname", ValueOrNone (library.Soname));
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
		if (!library.HasSoname) {
			return;
		}

		ReportDoc.AddLabeledListItem ("Soname", ValueOrNone (library.Soname));
	}

	void AddBuildId (bool appendLine = true)
	{
		ReportDoc.AddLabeledListItem ("Build ID", ValueOrNone (library.BuildID), appendLine: appendLine);
	}

	void AddAlignment (bool appendLine = true)
	{
		ReportDoc.AddLabeledListItem ("Alignment", $"{library.Alignment}", appendLine: appendLine);
	}

	void AddDebugInfo (bool appendLine = true)
	{
		ReportDoc.AddLabeledListItem ("Debug info", $"{YesNo (library.HasDebugInfo)}", appendLine: appendLine);
	}

	void AddSize (bool appendLine = true)
	{
		ReportDoc.AddLabeledListItem ("Size", $"{library.Size}", appendLine: appendLine);
	}

	void AddAndroidIdent (bool appendLine = true)
	{
		if (!library.HasAndroidIdent) {
			return;
		}

		// TODO: fix output, currently produces gibberish
		ReportDoc.AddLabeledListItem ("Android ident", "FIXME" /* ValueOrNone (library.AndroidIdent) */);
	}

	protected void WriteDebugInfoDesc ()
	{
		WriteItem ("Has debug info", YesNo (library.HasDebugInfo));

		var sb = new StringBuilder (YesNo (library.HasDebugLink));
		if (library.HasDebugLink) {
			sb.Append (" ('");
			sb.Append (library.DebugLink);
			sb.Append ("')");
		}
		WriteItem ("Has debug link", sb.ToString ());
	}

	protected void WriteAlignmentInfo ()
	{
		var sb = new StringBuilder ();
		sb.Append ($"0x{library.Alignment:x} (");
		if (!library.AlignmentCompatibleWith16k) {
			sb.Append ("NOT ");
		}
		sb.Append ("compatible with Android 16k library alignment requirement)");
		WriteLabel ("Alignment");
		WriteLine (library.AlignmentCompatibleWith16k ? ValidValueColor : InvalidValueColor, sb.ToString ());
	}
}
