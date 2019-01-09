DEBIAN_COMMON_DEPS="autoconf
	autotools-dev
	automake
	curl
	g++-mingw-w64
	gcc-mingw-w64
	git
	libncurses5
	libtool
	libz-mingw-w64-dev
	libzip4
	linux-libc-dev
	make
	openjdk-8-jdk
	unzip
	vim-common
	sqlite3
	zlib1g-dev
	"

if [ "$OS_ARCH" = "x86_64" ]; then
DEBIAN_COMMON_DEPS="$DEBIAN_COMMON_DEPS
	lib32stdc++6
	lib32z1
	"
fi

debian_install()
{
	if [ "$NO_SUDO" = "true" ]; then
		for p in $DISTRO_DEPS; do
			if dpkg -l $p > /dev/null 2>&1 ; then
				echo "[INSTALLED] $p"
			else
				echo "[ MISSING ] $p"
				PACKAGES_MISSING=yes
			fi
		done
		if [ "x$PACKAGES_MISSING" = "xyes" ]; then
			echo Some packages are missing, cannot continue
			echo
			exit 1
		fi
	else
		sudo apt-get -f -u -y install $DISTRO_DEPS
	fi
}
