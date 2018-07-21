#!/bin/bash -e

function Help()
{
	echo "$0: usage: $0 -r ROOT_DIR -o OUT_DIR [FILE]+"
	exit 1
}

function FullPath()
{
	local p="$1"
	if [ -d "$p" ]; then
		echo "$(cd "$p" && pwd)"
	else
		d="`dirname "$p"`"
		echo "$(cd "$d" && pwd)/$(basename "$p")"
	fi
}

function SubdirPath()
{
	local root path subdir
	root="$1"
	path="$2"

	if [ -d "$path" ]; then
		path="$(cd "$path" && pwd)"
		subdir="${path#$root}"
		echo "${subdir:-.}"
	else
		local b d
		d="$(dirname "$path")"
		b="$(basename "$path")"
		path="$(cd $d && pwd)"
		subdir="${path#$root}"
		echo "${subdir:-.}/$b"
	fi
}

while getopts "o:r:" option ; do
	case "$option" in
		o)
			TARGET_DIR="$OPTARG"
			;;
		r)
			ROOT_DIR="$OPTARG"
			;;
	esac
done

shift $(($OPTIND-1))

if [ -z "$TARGET_DIR" -o -z "$ROOT_DIR" ]; then
	echo "$0: missing required argument -o or -r" >&2
	Help
fi

if [ ! -d "$ROOT_DIR" ]; then
	echo "$0: \"-r '$ROOT_DIR'\" must refer to a valid directory" 2>&2
	Help
fi

mkdir -p "$TARGET_DIR"
TARGET_DIR="$(cd "$TARGET_DIR" && pwd)"
ROOT_DIR="$(cd "$ROOT_DIR" && pwd)"

for p in "$@" ; do
	if [ ! -e "$p" ]; then
		echo "$0: Skipping non-existent file '$p'" >&2
		continue
	fi
	full_path="$(FullPath "$p")"
	sub_path="$(SubdirPath "$ROOT_DIR" "$full_path")"
	target_sub="$TARGET_DIR/$(dirname "$sub_path")"
	target_path="$target_sub/$(basename "$p")"
	mkdir -p "$target_sub"
	if [ -f "$target_path" ]; then
		rm "$target_path"
	fi
	ln -s "$full_path" "$target_path"
done
