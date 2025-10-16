using System.Text;

namespace ApplicationUtility;

[AspectReporter (typeof (NativeAotSharedLibrary))]
class SharedLibraryReporter : BaseReporter
{
	readonly SharedLibrary library;

	protected override string AspectName => SharedLibrary.AspectName;
	protected virtual string LibraryKind => "Shared library";
	protected override string ShortDescription => library.Name;

	public SharedLibraryReporter (SharedLibrary library)
	{
		this.library = library;
	}

	protected override void DoReport ()
	{
		WriteAspectDesc (LibraryKind);
		WriteNativeArch (library.TargetArchitecture);

		if (library.HasSoname) {
			WriteItem ("Soname", ValueOrNone (library.Soname));
		}
		WriteItem ("Build ID", ValueOrNone (library.BuildID));
		WriteDebugInfoDesc ();

		if (library.HasAndroidIdent) {
			WriteItem ("Android ident", ValueOrNone (library.AndroidIdent));
		}
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

		sb.Clear ();
		sb.Append ($"0x{library.Alignment:x} (");
		if (!library.AlignmentCompatibleWith16k) {
			sb.Append ("NOT ");
		}
		sb.Append ("compatible with Android 16k library alignment requirement)");
		WriteLabel ("Alignment");
		WriteLine (library.AlignmentCompatibleWith16k ? ValidValueColor : InvalidValueColor, sb.ToString ());
	}
}
