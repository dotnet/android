topdir = ../..

API_LEVEL = 28
API_LEVEL_NAME = 28

CSCOMPILE = csc -debug:portable

CONFIGURATION = Debug

CONST_MAPPER_SOURCES = \
	generate-const-mapping.cs \
	$(topdir)/src/Mono.Android/obj/$(CONFIGURATION)/android-$(API_LEVEL)/mcw/__NamespaceMapping__.cs \
	$(topdir)/src/Mono.Android/Android.Runtime/NamespaceMappingAttribute.cs

all:: map.ext.csv remaining-int-consts.txt remaining-int-methods-filtered.txt

generate-const-list-2.exe: generate-const-list-2.cs
	$(CSCOMPILE) generate-const-list-2.cs

const-list-$(API_LEVEL).xml: generate-const-list-2.exe ../../bin/Build$(CONFIGURATION)/api/*.xml.in
	mono --debug generate-const-list-2.exe ../../bin/Build$(CONFIGURATION)/api > const-list-$(API_LEVEL).xml || rm const-list-$(API_LEVEL).xml

generate-const-mapping.exe: $(CONST_MAPPER_SOURCES)
	$(CSCOMPILE) $(CONST_MAPPER_SOURCES)

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

remaining-int-methods-filtered.txt: remaining-int-methods.txt reduction_rules.txt
	vi remaining-int-methods.txt -s reduction_rules.txt
	grep "int[\[ ]" remaining-int-methods.txt > remaining-int-methods-filtered.txt

remaining-int-methods.txt: $(topdir)/src/Mono.Android/obj/$(CONFIGURATION)/android-$(API_LEVEL)/mcw/*.cs
	grep "public" $(topdir)/src/Mono.Android/obj/$(CONFIGURATION)/android-$(API_LEVEL)/mcw/*.cs | grep -v "const" | grep "int" | grep "[({]" | grep -v GLES | grep -v Android.R. | grep -v Khronos | sort | sed 's/android-[0-9]*/android-XXX/' > remaining-int-methods.txt

clean::
	-rm -rf tmp.xml const-list-$(API_LEVEL).xml \
		generate-const-list-2.exe generate-const-list-2.exe.mdb \
		generate-const-mapping.exe generate-const-mapping.exe.mdb \
		map.ext.csv intermediate-enum-candidates.xml remaining-int-consts.txt \
