#include "java-interop.h"
#include "java-interop-dlfcn.h"
#include "java-interop-util.h"

#ifdef _WINDOWS
#include <windows.h>
#else
#include <dlfcn.h>
#include <string.h>
#endif

namespace microsoft::java_interop {

static char *
_get_last_dlerror ()
{
#ifdef _WINDOWS

	DWORD error = GetLastError ();
	if (error == ERROR_SUCCESS /* 0 */) {
		return nullptr;
	}

	wchar_t *buf = nullptr;

	DWORD size = FormatMessageW (
			/* dwFlags */       FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
			/* lpSource */      NULL,
			/* dwMessageId */   error,
			/* dwLanguageId */  MAKELANGID (LANG_NEUTRAL, SUBLANG_DEFAULT),
			/* lpBuffer */      (LPWSTR) &buf,
			/* nSize */         0,
			/* Arguments */     NULL
	);
	if (size == 0)
		return nullptr;

	char *message = utf16_to_utf8 (buf);
	LocalFree (buf);

	return message;

#else   // ndef _WINDOWS

	return java_interop_strdup (dlerror ());

#endif  // ndef _WINDOWS
}

static void
_free_error (char **error)
{
	if (error == nullptr)
		return;
	java_interop_free (*error);
	*error = nullptr;
}

static void
_set_error (char **error, const char *message)
{
	if (error == nullptr)
		return;
	*error = java_interop_strdup (message);
}

static void
_set_error_to_last_error (char **error)
{
	if (error == nullptr)
		return;
	*error = _get_last_dlerror ();
}

void*
java_interop_lib_load (const char *path, [[maybe_unused]] unsigned int flags, char **error)
{
	_free_error (error);
	if (path == nullptr) {
		_set_error (error, "path=nullptr is not supported");
		return nullptr;
	}

	void *handle    = nullptr;

#ifdef _WINDOWS

	wchar_t *wpath   = utf8_to_utf16 (path);
	if (wpath == nullptr) {
		_set_error (error, "could not convert path to UTF-16");
		return nullptr;
	}
	HMODULE module  = LoadLibraryExW (
			/* lpLibFileName */ wpath,
			/* hFile */         nullptr,
			/* dwFlags */       LOAD_LIBRARY_SEARCH_APPLICATION_DIR | LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_USER_DIRS | LOAD_LIBRARY_SEARCH_SYSTEM32
	);
	java_interop_free (wpath);

	handle = reinterpret_cast<void*>(module);

#else   // ndef _WINDOWS

	int mode = 0;
	if ((flags & JAVA_INTEROP_LIB_LOAD_GLOBALLY) == JAVA_INTEROP_LIB_LOAD_GLOBALLY) {
		mode = RTLD_GLOBAL;
	}
	if ((flags & JAVA_INTEROP_LIB_LOAD_LOCALLY) == JAVA_INTEROP_LIB_LOAD_LOCALLY) {
		mode = RTLD_LOCAL;
	}

	if (mode == 0) {
		mode = RTLD_LOCAL;
	}
	mode |= RTLD_NOW;

	handle  = dlopen (path, mode);

#endif  // ndef _WINDOWS

	if (handle == nullptr) {
		_set_error_to_last_error (error);
	}

	return handle;
}

void*
java_interop_lib_symbol (void *library, const char *symbol, char **error)
{
	_free_error (error);

	if (library == nullptr) {
		_set_error (error, "library=nullptr");
		return nullptr;
	}
	if (symbol == nullptr) {
		_set_error (error, "symbol=nullptr");
		return nullptr;
	}

	void *address   = nullptr;

#ifdef _WINDOWS

	HMODULE module  = reinterpret_cast<HMODULE>(library);
	FARPROC a       = GetProcAddress (module, symbol);
	address	        = reinterpret_cast<void*>(a);

#else   // ndef _WINDOWS

	address         = dlsym (library, symbol);

#endif  // ndef _WINDOWS

	if (address == nullptr) {
		_set_error_to_last_error (error);
	}

	return address;
}

int
java_interop_lib_close (void* library, char **error)
{
	_free_error (error);
	if (library == nullptr) {
		_set_error (error, "library=nullptr");
		return JAVA_INTEROP_LIB_INVALID_PARAM;
	}

	int r   = 0;

#ifdef _WINDOWS
	HMODULE h   = reinterpret_cast<HMODULE>(library);
	BOOL    v   = FreeLibrary (h);
	if (!v) {
		r   = JAVA_INTEROP_LIB_CLOSE_FAILED;
	}
#else   // ndef _WINDOWS
	r           = dlclose (library);
	if (r != 0) {
		r   = JAVA_INTEROP_LIB_CLOSE_FAILED;
	}
#endif  // ndef _WINDOWS

	if (r != 0) {
		_set_error_to_last_error (error);
	}

	return r;
}

} // namespace microsoft::java_interop
