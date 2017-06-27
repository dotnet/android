UBUNTU_DEPS="autoconf 
	autotools-dev 
	automake 
	clang 
	curl 
	g++-mingw-w64 
	gcc-mingw-w64 
	git 
	libtool 
	libz-mingw-w64-dev 
	libzip4 
	linux-libc-dev 
	make 
	openjdk-8-jdk 
	unzip 
	vim-common
	"

if [ $OS_ARCH = "x86_64" ]; then
UBUNTU_DEPS="$UBUNTU_DEPS 
	lib32stdc++6 \
	lib32z1 \
	libx32tinfo-dev \
	linux-libc-dev:i386 \
	zlib1g-dev:i386 "
fi 
if [ $NO_SUDO = "true" ]; then
	for p in $UBUNTU_DEPS; do 
		if dpkg -l $$p > /dev/null 2>&1 ; then 
			echo "[INSTALLED] $$p" 
		else 
			echo "[ MISSING ] $$p" 
			PACKAGES_MISSING=yes 
		fi 
	done 
	if [ "x$$PACKAGES_MISSING" = "xyes" ]; then 
		echo Some packages are missing, cannot continue 
		echo 
		false 
	fi
else
sudo apt-get -f -u install $UBUNTU_DEPS
fi
