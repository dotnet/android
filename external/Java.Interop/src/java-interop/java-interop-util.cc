#ifdef _WINDOWS
#include <assert.h>
#include <direct.h>
#include <windows.h>

char*
utf16_to_utf8 (const wchar_t *widestr)
{
	int required_size = WideCharToMultiByte (CP_UTF8, 0, widestr, -1, NULL, 0, NULL, NULL);
	if (required_size <= 0) {
		return nullptr;
	}

	char *mbstr = static_cast<char*> (calloc (required_size, sizeof (char)));
	if (mbstr == nullptr) {
		return nullptr;
	}

	int converted_size = WideCharToMultiByte (CP_UTF8, 0, widestr, -1, mbstr, required_size, NULL, NULL);
	assert (converted_size == required_size);
	if (required_size != converted_size) {
		free (mbstr);
		return nullptr;
	}

	return mbstr;
}

wchar_t*
utf8_to_utf16 (const char *mbstr)
{
	int required_chars = MultiByteToWideChar (CP_UTF8, 0, mbstr, -1, NULL, 0);
	if (required_chars <= 0) {
		return nullptr;
	}

	wchar_t *widestr = static_cast<wchar_t*> (calloc (required_chars, sizeof (wchar_t)));
	if (widestr == nullptr) {
		return nullptr;
	}

	int converted_chars = MultiByteToWideChar (CP_UTF8, 0, mbstr, -1, widestr, required_chars);
	assert (converted_chars == required_chars);
	if (required_chars != converted_chars) {
		free (widestr);
		return nullptr;
	}

	return widestr;
}
#endif // def _WINDOWS
