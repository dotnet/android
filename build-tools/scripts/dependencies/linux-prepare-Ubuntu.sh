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
sudo apt-get -f -u install $UBUNTU_DEPS
