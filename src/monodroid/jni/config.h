#ifndef INC_MONODROID_CONFIG_H
#define INC_MONODROID_CONFIG_H

#include <monodroid.h>
#include <stdint.h>

MONO_API  int CreateNLSocket (void);
MONO_API  int ReadEvents (void *sock, void *buffer, int32_t count, int32_t size);
MONO_API  int CloseNLSocket (void *sock);

#endif  /* !def INC_MONODROID_CONFIG_H */
