namespace Xamarin.Android.Application;

sealed class Constants
{
	// Correspond to the `FORMAT_TAG` constant in src/monodroid/xamarin-app.hh
	public const ulong FormatTag_V1 = 0x015E6972616D58;
	public const ulong FormatTag_V2 = 0x00026E69726D6158;

	// Symbols in libxamarin-app.so
	public const string ApplicationConfigSymbolName    = "application_config";
	public const string EnvironmentVariablesSymbolName = "app_environment_variables";
	public const string FormatTagSymbolName            = "format_tag";
	public const string MonoAotModeNameSymbolName      = "mono_aot_mode_name";
	public const string SystemPropertiesSymbolName     = "app_system_properties";

	public const string UnableToLoadDataForPointer = "[unable to load data a pointer indicates]";
}
