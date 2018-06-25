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
all_installed=yes
for pkg in $ARCH_DEPS
do
	if ! pacman -Qq "$pkg" > /dev/null 2>&1 
	then
		all_installed=no 
		missing_pkgs+=("$pkg")
	fi
done
if [ "$NO_SUDO" = "false" ]
then
	[ "$all_installed" = "yes" ] && exit
	if ! sudo pacman -S --noconfirm --needed $ARCH_DEPS
	then
		echo "Failed to install required packages"
		exit 1
	fi
else
	if [ "$all_installed" = "no" ]
	then
		echo "Missing package(s): '${missing_pkgs[@]}'"
		exit 1
	fi
fi
