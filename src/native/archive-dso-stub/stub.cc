//
// This is here to clearly indicate that a `lib/{ABI}/lib*.so` file is really a
// data file for .NET for Android and not a "regular" library.  Might be useful
// when analyzing APK/AABs.
//
[[gnu::visibility("default")]]
bool dotnet_for_android_data_payload = true;
