ifeq ($(OS),Linux)
NO_SUDO ?= false
ARCH_DEPS			= \
	autoconf \
	automake \
	binutils \
	bison \
	fakeroot \
	file \
	findutils \
	flex \
	gawk \
	gettext \
	grep \
	groff \
	gzip \
	libtool \
	m4 \
	make \
	patch \
	pkg-config \
	sed \
	texinfo \
	which \
	git \
	curl \
	unzip \
	jdk8-openjdk \
	xxd \
	pkg-config
ARCH_DEPS_GCC		= \
	gcc
UBUNTU_DEPS          = \
	autoconf \
	autotools-dev \
	automake \
	clang \
	curl \
	g++-mingw-w64 \
	gcc-mingw-w64 \
	git \
	libtool \
	libz-mingw-w64-dev \
	libzip4 \
	linux-libc-dev \
	make \
	openjdk-8-jdk \
	unzip \
	vim-common

ifeq ($(OS_ARCH),x86_64)
UBUNTU_DEPS          += \
	lib32stdc++6 \
	lib32z1 \
	libx32tinfo-dev \
	linux-libc-dev:i386 \
	zlib1g-dev:i386
ARCH_DEPS_GCC		= \
	gcc-multilib
endif
LINUX_DISTRO         := $(shell lsb_release -i -s || true)
LINUX_DISTRO_RELEASE := $(shell lsb_release -r -s || true)
BINFMT_MISC_TROUBLE  := cli win
ifeq ($(NO_SUDO),false)
linux-prepare-message::
	@echo
	@echo Installing build depedencies for $(LINUX_DISTRO)
	@echo Will use sudo, please provide your password as needed
	@echo
linux-prepare-Arch:: linux-prepare-message
	sudo pacman -S --noconfirm $(ARCH_DEPS) $(ARCH_DEPS_GCC)
linux-prepare-Ubuntu:: linux-prepare-message
	sudo apt-get -f -u install $(UBUNTU_DEPS)
else
linux-prepare-Ubuntu::
	@echo
	@echo sudo is disabled, cannot install dependencies
	@echo Listing status of all the dependencies
	@PACKAGES_MISSING=no ; \
	for p in $(UBUNTU_DEPS); do \
		if dpkg -l $$p > /dev/null 2>&1 ; then \
			echo "[INSTALLED] $$p" ; \
		else \
			echo "[ MISSING ] $$p" ; \
			PACKAGES_MISSING=yes ; \
		fi ; \
	done ; \
	echo ; \
	if [ "x$$PACKAGES_MISSING" = "xyes" ]; then \
		echo Some packages are missing, cannot continue ; \
		echo ; \
		false ; \
	fi
endif

linux-prepare-$(LINUX_DISTRO)::

linux-prepare-$(LINUX_DISTRO)-$(LINUX_DISTRO_RELEASE)::
endif
