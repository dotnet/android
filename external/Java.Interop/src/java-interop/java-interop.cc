#include <stdlib.h>
#include <string.h>

#include "java-interop.h"

#ifdef _WINDOWS
// Warning C4996: 'strdup': The POSIX name for this item is deprecated.
#define strdup _strdup
#endif   // ndef _WINDOWS

char*
java_interop_strdup (const char* value)
{
	return strdup (value);
}

void
java_interop_free   (void *p)
{
	free (p);
}

