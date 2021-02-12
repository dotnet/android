CONFIGURATION     = Debug

all: bin/$(CONFIGURATION)/net472/invocation-overheads.exe

bin/$(CONFIGURATION)/net472/invocation-overheads.exe:
	msbuild /restore

clean:
	msbuild /t:Clean

run:
	msbuild /t:Run /nologo /v:m
