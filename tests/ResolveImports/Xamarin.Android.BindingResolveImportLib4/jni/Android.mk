LOCAL_PATH := $(call my-dir)

include $(CLEAR_VARS)

LOCAL_CFLAGS    := -g
LOCAL_LDLIBS    := -llog
LOCAL_MODULE    := timing4
LOCAL_SRC_FILES := timing.c

include $(BUILD_SHARED_LIBRARY)

