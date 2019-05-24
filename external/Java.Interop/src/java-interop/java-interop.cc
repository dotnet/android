#include <stdlib.h>
#include <string.h>

#include "java-interop.h"

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

