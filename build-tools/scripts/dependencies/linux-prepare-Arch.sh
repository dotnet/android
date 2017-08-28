ARCH_DEPS="autoconf
	automake
	binutils
	bison
	curl
	fakeroot
	file
	findutils
	flex
	gawk
	gcc
	gettext
	git
	grep
	groff
	gtk-sharp-2
	gzip
	jdk8-openjdk
	libtool
	libzip
	m4
	make
	nuget
	patch
	pkg-config
	pkg-config
	referenceassemblies-pcl
	sed
	texinfo
	unzip
	which
	zip
	"
if [ $NO_SUDO = "false" ]; then
	sudo pacman -S --noconfirm --needed $ARCH_DEPS
else
	echo "Sudo is required!"
fi
