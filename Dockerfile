FROM ubuntu:16.04

MAINTAINER Atsushi Eno <atsushieno@gmail.com>

RUN apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
RUN echo "deb http://download.mono-project.com/repo/debian wheezy main" | tee /etc/apt/sources.list.d/mono-xamarin.list
RUN echo "deb http://download.mono-project.com/repo/debian alpha main" | tee /etc/apt/sources.list.d/mono-xamarin-alpha.list
RUN apt-get update

RUN echo y | apt install curl openjdk-8-jdk git make automake autoconf libtool unzip vim-common clang nuget mono-xbuild referenceassemblies-pcl lib32stdc++6 lib32z1 libzip4

RUN mkdir /sources
RUN cd /sources && git clone https://github.com/xamarin/xamarin-android.git
RUN cd /sources/xamarin-android && git submodule init
RUN cd /sources/xamarin-android && git submodule update external/mono
RUN cd /sources/xamarin-android/external/mono && git submodule init
RUN cd /sources/xamarin-android/external/mono && git submodule update external/referencesource
RUN cd /sources/xamarin-android/external/mono && git submodule update --init --recursive
RUN cd /sources/xamarin-android && make prepare
RUN cd /sources/xamarin-android && make


