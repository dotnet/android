namespace Microsoft.Android.AppTools;

enum ELFPayloadError
{
	None,
	NotELF,
	LoadFailed,
	NotSharedLibrary,
	NotLittleEndian,
	NoPayloadSection,
}
