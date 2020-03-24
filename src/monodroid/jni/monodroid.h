#ifndef __MONODROID_H
#define __MONODROID_H

#include <mono/utils/mono-publib.h>

// TODO: stopgap to be able to include mono-private-unstable.h, ultimately they should not reference glib types
typedef void *         gpointer;
typedef int32_t        gboolean;

#endif  /* defined __MONODROID_H */
