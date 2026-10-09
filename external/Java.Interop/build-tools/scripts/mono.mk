#
# Mono utility runtime
#
# Inputs:
#
#   $(OS): Optional; **uname**(1) value of the host operating system
#   $(CONFIGURATION): Build configuration name, e.g. Debug or Release
#   $(V): Output verbosity. If != 0, then `MONO_OPTIONS` is exported with --debug.
#
# Outputs:
#
#   $(RUNTIME):
#       The **mono**(1) program to use to execute managed code.

OS            ?= $(shell uname)
RUNTIME       := $(shell if [ -f "`which mono64`" ] ; then echo mono64 ; else echo mono; fi) --debug=casts

ifneq ($(V),0)
MONO_OPTIONS  += --debug
endif   # $(V) != 0

ifneq ($(MONO_OPTIONS),)
export MONO_OPTIONS
endif   # $(MONO_OPTIONS) != ''
