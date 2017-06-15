topdir = ../..

API_LEVEL = 26
API_LEVEL_NAME = 26

CONFIGURATION = Debug
DOCS_DIR=~/android-toolchain/docs/docs-api-$(API_LEVEL_NAME)

CONST_MAPPER_SOURCES = \
	generate-const-mapping.cs \
	$(topdir)/src/Mono.Android/obj/$(CONFIGURATION)/android-$(API_LEVEL)/mcw/__NamespaceMapping__.cs \
	$(topdir)/src/Mono.Android/Android.Runtime/NamespaceMappingAttribute.cs

all:: map.ext.csv remaining-int-consts.txt remaining-int-methods-filtered.txt

#enum mappings

generate-const-list.exe: generate-const-list.cs
	mcs generate-const-list.cs -debug

const-list-$(API_LEVEL).xml: generate-const-list.exe
	mono --debug generate-const-list.exe $(DOCS_DIR)/reference/ -v > const-list-$(API_LEVEL).xml || rm const-list-$(API_LEVEL).xml

generate-intermediary-enum-candidates.exe : generate-intermediary-enum-candidates.cs
	mcs generate-intermediary-enum-candidates.cs -debug

#it is obsolete
intermediary-enum-candidates.xml: generate-intermediary-enum-candidates.exe
	mono --debug generate-intermediary-enum-candidates.exe $(topdir)/src/Mono.Android/api-$(API_LEVEL).xml.in > intermediary-enum-candidates.xml || rm intermediary-enum-candidates.xml

generate-const-mapping.exe: $(CONST_MAPPER_SOURCES)
	mcs $(CONST_MAPPER_SOURCES) -debug

map.ext.csv: generate-const-mapping.exe enum-conversion-mappings.xml const-list-$(API_LEVEL).xml
	mono --debug generate-const-mapping.exe enum-conversion-mappings.xml const-list-$(API_LEVEL).xml > map.ext.csv || rm map.ext.csv

remaining-int-consts.txt: ../../src/Mono.Android/obj/$(CONFIGURATION)/android-$(API_LEVEL)/mcw/*.cs
	grep "const int " ../../src/Mono.Android/obj/$(CONFIGURATION)/android-$(API_LEVEL)/mcw/*.cs \
		| grep -v Javax.Microedition \
		| grep -v Dalvik.Bytecode \
		| grep -v Android.Resource.cs \
		| grep -v BluetoothAssignedNumbers \
		| grep -v GLES \
		| grep -v ContentsFileDescriptor \
		| sed -e 's/android-[0-9]*/android-XXX/' \
		> remaining-int-consts.txt

# method mappings
generate-intermediary-doc-enum-mapping.exe: generate-intermediary-doc-enum-mapping.cs
	mcs generate-intermediary-doc-enum-mapping.cs -debug

generate-enumlist-to-query.exe : generate-enumlist-to-query.cs
	mcs generate-enumlist-to-query.cs -debug

generate-intermediary-method-mapping.exe: generate-intermediary-method-mapping.cs
	mcs generate-intermediary-method-mapping.cs -debug

intermediary-enum-list.txt: generate-enumlist-to-query.exe
	mono --debug generate-enumlist-to-query.exe const-list-$(API_LEVEL).xml > intermediary-enum-list.txt || rm intermediary-enum-list.txt

intermediary-doc-enum-mapping.tsv: generate-intermediary-doc-enum-mapping.exe intermediary-enum-list.txt
	mono --debug generate-intermediary-doc-enum-mapping.exe $(DOCS_DIR)/reference intermediary-enum-list.txt > intermediary-doc-enum-mapping.tsv || rm intermediary-doc-enum-mapping.tsv

intermediary-method-mapping.txt: generate-intermediary-method-mapping.exe
	mono --debug generate-intermediary-method-mapping.exe intermediary-doc-enum-mapping.tsv const-list-$(API_LEVEL).xml $(DOCS_DIR)/reference > intermediary-method-mapping.txt || rm intermediary-method-mapping.txt

intermediary-field-candidates.txt: intermediary-method-mapping.txt
	grep "#[a-z]" intermediary-method-mapping.txt  | grep -v '('  > intermediary-field-candidates.txt

remaining-int-methods-filtered.txt: remaining-int-methods.txt reduction_rules.txt
	vi remaining-int-methods.txt -s reduction_rules.txt
	grep "int[\[ ]" remaining-int-methods.txt > remaining-int-methods-filtered.txt

remaining-int-methods.txt: $(topdir)/src/Mono.Android/obj/$(CONFIGURATION)/android-$(API_LEVEL)/mcw/*.cs
	grep "public" $(topdir)/src/Mono.Android/obj/$(CONFIGURATION)/android-$(API_LEVEL)/mcw/*.cs | grep -v "const" | grep "int" | grep "[({]" | grep -v GLES | grep -v Android.R. | grep -v Khronos | sort | sed 's/android-[0-9]*/android-XXX/' > remaining-int-methods.txt

clean::
	-rm -rf tmp.xml const-list-$(API_LEVEL).xml \
		generate-const-list.exe generate-const-list.exe.mdb \
		generate-intermediary-enum-candidates.exe generate-intermediary-enum-candidates.exe.mdb \
		generate-const-mapping.exe generate-const-mapping.exe.mdb \
		map.ext.csv intermediate-enum-candidates.xml remaining-int-consts.txt \
		generate-intermediary-doc-enum-mapping.exe generate-intermediary-doc-enum-mapping.exe.mdb
