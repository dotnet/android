LOCAL_PATH := $(call my-dir)

include $(CLEAR_VARS)

ifeq ($(CONFIGURATION),Debug)
COMMON_CFLAGS ?= -ggdb3 -O0 -fno-omit-frame-pointer
else
COMMON_CFLAGS ?= -g -O2 -DRELEASE=1
endif

LOCAL_CFLAGS =	$(COMMON_CFLAGS) \
	-std=c99 \
	-DHAVE_LINUX_NETLINK_H=1 -DHAVE_LINUX_RTNETLINK_H=1 \
	-DSGEN_BRIDGE_VERSION=$(SGEN_BRIDGE_VERSION) \
	-D_REENTRANT -DPLATFORM_ANDROID -DANDROID -DLINUX -Dlinux -D__linux_ \
	-DHAVE_CONFIG_H -DJI_DLL_EXPORT -DMONO_DLL_EXPORT \
	-I$(topdir)/libmonodroid/zip -I$(BUILDDIR)/include -I$(BUILDDIR)/include/eglib \
	-mandroid \
	-fno-strict-aliasing \
	-ffunction-sections \
	-fomit-frame-pointer \
	-funswitch-loops \
	-finline-limit=300 \
	-fvisibility=hidden \
	-fstack-protector \
	-Wa,--noexecstack \
	-Wformat -Werror=format-security \
	$(if $(TIMING),-DMONODROID_TIMING=1,) \
	$(if $(NODEBUG),,-DDEBUG=1)

ifneq ($(wildcard /app/.),)
LOCAL_CFLAGS = $(LOCAL_CFLAGS) -DLINUX_FLATPAK
endif

LOCAL_LDFLAGS   += \
	-Wall \
	-Wl,--export-dynamic \
	-Wl,-z,now \
	-Wl,-z,relro \
	-Wl,-z,noexecstack \
	-Wl,--no-undefined \

LOCAL_C_INCLUDES	:= \
	$(LOCAL_PATH)/../../../bin/$(CONFIGURATION)/include/$(TARGET_ARCH_ABI)/eglib \
	$(LOCAL_PATH)/../../../external/mono/eglib/src \
	$(LOCAL_PATH)/zip

LOCAL_LDLIBS    := -llog -lz -lstdc++

LOCAL_MODULE    := monodroid

LOCAL_SRC_FILES := \
	../../../external/mono/support/nl.c \
	../../../external/mono/support/zlib-helper.c \
	dylib-mono.c \
	embedded-assemblies.c \
	jni.c \
	monodroid-glue.c \
	util.c \
	logger.c \
	debug.c \
	timezones.c \
	zip/ioapi.c \
	zip/unzip.c \
	xamarin_getifaddrs.c \
	cpu-arch-detect.c \
	monodroid-networkinfo.c

jni/monodroid-glue.c: jni/config.include jni/machine.config.include

$(LOCAL_PATH)/machine.config.include: $(LOCAL_PATH)/../machine.config.xml
	(cat $< ; dd if=/dev/zero bs=1 count=1 2>/dev/null) > monodroid.machine.config
	xxd -i monodroid.machine.config | sed 's/^unsigned /static const unsigned /g' > jni/machine.config.include
	rm monodroid.machine.config

$(LOCAL_PATH)/config.include: $(LOCAL_PATH)/../config.xml
	(cat $< ; dd if=/dev/zero bs=1 count=1 2>/dev/null) > monodroid.config
	xxd -i monodroid.config | sed 's/^unsigned /static const unsigned /g' > jni/config.include
	rm monodroid.config


include $(BUILD_SHARED_LIBRARY)


