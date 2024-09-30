namespace Xamarin.Android.AssemblyStore;

enum ELFPayloadError
{
	None,
	NotELF,
	LoadFailed,
	NotSharedLibrary,
	NotLittleEndian,
	NoPayloadSection,
}
