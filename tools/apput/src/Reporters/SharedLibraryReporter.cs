using System.Text;

namespace ApplicationUtility;

[AspectReporter (typeof (NativeAotSharedLibrary))]
class SharedLibraryReporter : BaseReporter
{
	readonly SharedLibrary library;
	protected virtual string LibraryKind => "Shared library";

	public SharedLibraryReporter (SharedLibrary library)
	{
		this.library = library;
	}

	public override void Report ()
	{
		WriteAspectDesc (LibraryKind);
		WriteNativeArch (library.TargetArchitecture);
		WriteItem ("Build ID", library.HasBuildID ? library.BuildID! : "none");
		WriteDebugInfoDesc ();

		if (library.HasAndroidIdent) {
			WriteItem ("Android ident", library.AndroidIdent!);
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

		bool compatibleWith16k = library.Alignment >= 0x4000 && (library.Alignment % 0x4000 == 0);
		sb.Clear ();
		sb.Append ($"0x{library.Alignment:x} (");
		if (!compatibleWith16k) {
			sb.Append ("NOT ");
		}
		sb.Append ("compatible with Android 16k library alignment)");
		WriteLabel ("Alignment");
		WriteLine (compatibleWith16k ? ValidValueColor : InvalidValueColor, sb.ToString ());
	}
}
