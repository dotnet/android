LOCAL_PATH := $(call my-dir)

include $(CLEAR_VARS)

LOCAL_CFLAGS    := -g
LOCAL_LDLIBS    := -llog
LOCAL_MODULE    := simple
LOCAL_SRC_FILES := simple-lib.c

include $(BUILD_SHARED_LIBRARY)

