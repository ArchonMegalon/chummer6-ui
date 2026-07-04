# Maintainer: Chummer release automation <release@chummer.run>
pkgname=chummer6-bin
pkgver=20260704.150719
pkgrel=1
pkgdesc='Shadowrun character and campaign companion desktop build'
arch=('x86_64')
url='https://chummer.run'
license=('custom')
depends=('fontconfig' 'gtk3' 'libx11' 'libxcursor' 'libxext' 'libxfixes' 'libxi' 'libxinerama' 'libxrandr' 'libxrender' 'libglvnd' 'zlib')
source_x86_64=('chummer-avalonia-linux-x64-installer.deb::https://chummer.run/downloads/files/chummer-avalonia-linux-x64-installer.deb')
sha256sums_x86_64=('6630c109979f47815b622d784ba704c07b8ec99d7170a43e66559f6e5c156f7f')
options=('!strip')

package() {
  bsdtar -xf "$srcdir/chummer-avalonia-linux-x64-installer.deb" -C "$srcdir"
  local data_tar
  data_tar="$(find "$srcdir" -maxdepth 1 -type f -name 'data.tar*' | sort | head -n 1)"
  if [[ -z "$data_tar" ]]; then
    echo "Chummer .deb payload is missing data.tar.*" >&2
    return 1
  fi

  bsdtar -xf "$data_tar" -C "$pkgdir"
}
