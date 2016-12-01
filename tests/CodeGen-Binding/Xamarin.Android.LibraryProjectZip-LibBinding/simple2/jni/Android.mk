LOCAL_PATH := $(call my-dir)

include $(CLEAR_VARS)

LOCAL_CFLAGS    := -g
LOCAL_LDLIBS    := -llog
LOCAL_MODULE    := simple2
LOCAL_SRC_FILES := simple2-lib.c

include $(BUILD_SHARED_LIBRARY)

