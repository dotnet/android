# This file must be included from ../../Makefile

CSC               = csc
RUNTIME           = mono --debug

MONO_PATH         = $(call GetPath,MonoSource)
MONO_API_HTML_DIR = $(MONO_PATH)/mcs/tools/mono-api-html
MONO_API_HTML     = bin/Build$(CONFIGURATION)/mono-api-html.exe
MONO_API_INFO_DIR = $(MONO_PATH)/mcs/tools/corcompare
MONO_API_INFO     = bin/Build$(CONFIGURATION)/mono-api-info.exe
MONO_OPTIONS_SRC  = $(MONO_PATH)/mcs/class/Mono.Options/Mono.Options/Options.cs
FRAMEWORK_DIR     = bin/$(CONFIGURATION)/lib/xamarin.android/xbuild-frameworks/MonoAndroid


run-api-compatibility-tests: $(MONO_API_HTML) $(MONO_API_INFO)
	mkdir -p bin/Build$(CONFIGURATION)/compatibility
	make -C external/xamarin-android-api-compatibility check \
		STABLE_FRAMEWORKS="$(STABLE_FRAMEWORKS)" \
		MONO_API_HTML="$(RUNTIME) $(abspath $(MONO_API_HTML))" \
		MONO_API_INFO="$(RUNTIME) $(abspath $(MONO_API_INFO))" \
		HTML_OUTPUT_DIR="$(abspath bin/Build$(CONFIGURATION)/compatibility)" \
		XA_FRAMEWORK_DIR="$(abspath $(FRAMEWORK_DIR))"

$(MONO_API_HTML): $(wildcard $(MONO_API_HTML_DIR)/*.cs) $(MONO_OPTIONS_SRC)
	$(CSC) -out:$@ $(wildcard $(MONO_API_HTML_DIR)/*.cs) \
		$(MONO_OPTIONS_SRC) \
		-r:System.Xml.dll -r:System.Xml.Linq.dll

MONO_API_INFO_REFS  = \
  bin/$(CONFIGURATION)/lib/xamarin.android/xbuild/Xamarin/Android/Xamarin.Android.Cecil.dll

$(MONO_API_INFO): $(wildcard $(MONO_API_INFO_DIR)/*.cs) $(MONO_OPTIONS_SRC)
	$(CSC) -out:$@ $^ \
		$(MONO_API_INFO_REFS:%=-r:%)
