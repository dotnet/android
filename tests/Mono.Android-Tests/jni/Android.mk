LOCAL_PATH := $(call my-dir)

include $(CLEAR_VARS)

LOCAL_LDLIBS    := -llog
LOCAL_MODULE    := reuse-threads
LOCAL_SRC_FILES := reuse-threads.c

include $(BUILD_SHARED_LIBRARY)

