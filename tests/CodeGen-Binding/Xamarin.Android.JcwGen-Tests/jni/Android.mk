LOCAL_PATH := $(call my-dir)

include $(CLEAR_VARS)

LOCAL_CFLAGS    := -g -DPLATFORM_ANDROID=1
LOCAL_LDLIBS    := -llog
LOCAL_MODULE    := timing
LOCAL_SRC_FILES := timing.c

include $(BUILD_SHARED_LIBRARY)

