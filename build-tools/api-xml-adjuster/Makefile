CONFIGURATION=Debug

SHELL = /bin/bash

ANDROID_SDK_PATH=$(shell find ~/ -maxdepth 1 -name android-sdk-*)

MANDROID = ../../bin/$(CONFIGURATION)/lib/mandroid
BUILDBIN = ../../bin/Build$(CONFIGURATION)

CLASS_PARSE = $(MANDROID)/class-parse.exe
API_XML_TOOL = $(BUILDBIN)/api-xml-adjuster.exe
RUNTIME = mono
RUN_CLASS_PARSE = $(RUNTIME) $(CLASS_PARSE)
RUN_API_XML_TOOL = $(RUNTIME) $(API_XML_TOOL)
DOCS_DIR = .
API_LEVELS = 10 15 16 17 18 19 20 21 22 23 24 25

XML_OUTPUT_DIR = .

CLASS_PARSE_XML_DIR = $(XML_OUTPUT_DIR)/class-parse-xml
CLASS_PARSE_XML = $(CLASS_PARSE_XML_DIR)/api-$(LEVEL).xml.in
ANALYZED_XML_DIR = ../../src/Mono.Android/Profiles
ANALYZED_XML = $(ANALYZED_XML_DIR)/api-$(LEVEL).xml.in
ANDROID_JAR = $(ANDROID_SDK_PATH)/platforms/android-$(LEVEL)/android.jar

DOCS_DIR_CUR_LEVEL = $(DOCS_DIR)/docs-api-$(LEVEL)

XBUILD = JAVA_INTEROP_PATH=$(JAVA_INTEROP_PATH) xbuild

all:
	for level in $(API_LEVELS) ; do \
		make LEVEL=$$level api-xml || exit 1; \
	done

clean:
	for level in $(API_LEVELS) ; do \
		make LEVEL=$$level clean-api-xml || exit 1; \
	done
	rm -f $(CLASS_PARSE)
	cd $(JAVA_INTEROP_PATH)/tools/class-parse && $(XBUILD) /t:Clean
	cd $(JAVA_INTEROP_PATH)/src/Xamarin.Android.Tools.Bytecode && $(XBUILD) /t:Clean

api-xml: $(ANALYZED_XML)

clean-api-xml:
	rm -f $(CLASS_PARSE_XML) $(ANALYZED_XML)
	$(XBUILD) /t:Clean

$(CLASS_PARSE_XML): $(CLASS_PARSE) $(ANDROID_JAR) $(DOCS_DIR_CUR_LEVEL)
	mkdir -p $(CLASS_PARSE_XML_DIR)
	if [ $(LEVEL) -ge 24 ]; then \
		$(RUN_CLASS_PARSE) $(ANDROID_JAR) -docspath=$(DOCS_DIR_CUR_LEVEL)/reference --docstype=droiddoc2 > $(CLASS_PARSE_XML) || rm -f $(CLASS_PARSE_XML) ;\
	else \
		$(RUN_CLASS_PARSE) $(ANDROID_JAR) -docspath=$(DOCS_DIR_CUR_LEVEL)/reference --docstype=droiddoc > $(CLASS_PARSE_XML) || rm -f $(CLASS_PARSE_XML) ;\
	fi

$(ANALYZED_XML): $(API_XML_TOOL) $(CLASS_PARSE_XML)
	mkdir -p $(ANALYZED_XML_DIR)
	$(RUN_API_XML_TOOL) $(CLASS_PARSE_XML) $(ANALYZED_XML) || rm -f $(ANALYZED_XML)

$(CLASS_PARSE):
	cd $(JAVA_INTEROP_PATH)/tools/class-parse && $(XBUILD) /p:OutputPath=$(TOP)/out/lib/mandroid

$(API_XML_TOOL):
	$(XBUILD)

DOCS_DIR_CUR_LEVEL: docs-api-$(LEVEL)

# download and setup docs directory for each API profile

define extract-docs
	unzip $1 || exit 1
	mv $2 $@
endef

docs-%.zip:
	curl -L http://dl-ssl.google.com/android/repository/$@ > $@ || exit 1

docs-25_web.zip:
	curl -L https://dl.dropboxusercontent.com/u/493047/2016/docs-25-web.zip > $@ || exit 1

# API level 10 is Android v2.3.3; it's API level 9 (Android v2.3) with a few
# bugfixes which don't impact the documentation.
docs-api-10: docs-2.3_r01-linux.zip
	$(call extract-docs,$<,docs-2.3_r01-linux)

docs-api-15: docs-15_r02.zip
	$(call extract-docs,$<,docs)

docs-api-16: docs-16_r03.zip
	$(call extract-docs,$<,docs)

docs-api-17: docs-17_r02.zip
	$(call extract-docs,$<,docs)

docs-api-18: docs-18_r01.zip
	$(call extract-docs,$<,docs)

docs-api-19: docs-19_r01.zip
	$(call extract-docs,$<,docs)

# no 20 doc in final release either...
docs-api-20: docs-21_r01.zip
	$(call extract-docs,$<,docs)

docs-api-21: docs-21_r01.zip
	$(call extract-docs,$<,docs)

docs-api-22: docs-22_r01.zip
	$(call extract-docs,$<,docs)

docs-api-23: docs-23_r01.zip
	$(call extract-docs,$<,docs)

docs-api-24: docs-24_r01.zip
	$(call extract-docs,$<,docs)

docs-api-25: docs-25_web.zip	# Google hadn't published docs component yet!
	$(call extract-docs,$<,docs)

