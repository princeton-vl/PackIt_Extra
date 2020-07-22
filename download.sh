
wgetgdrive(){
  # $1 = file ID
  # $2 = file name

  URL="https://docs.google.com/uc?export=download&id=$1"

  wget --load-cookies /tmp/cookies.txt "https://docs.google.com/uc?export=download&confirm=$(wget --quiet --save-cookies /tmp/cookies.txt --keep-session-cookies --no-check-certificate $URL -O- | sed -rn 's/.*confirm=([0-9A-Za-z_]+).*/\1\n/p')&id=$1" -O $2 && rm -rf /tmp/cookies.txt
}

mkdir tmp
wgetgdrive 1DD7bdRJSPbEIXe8qXzrD3lEz0nzfbGbU tmp/data.zip
wgetgdrive 1Y7xYBl2Nu8NlD-6rvmCf4iNJvxokS2PE tmp/precompute.zip
wgetgdrive 15fd9EcSdJPyG5x3AycWHRl8j1keKDnwh tmp/packing.zip
wgetgdrive 16vRzNlBRV3XNdzmOnqTqCnLaweeOfUSq tmp/resources.zip

unzip tmp/data.zip -d unity-build
unzip tmp/precompute.zip -d unity-build
ln -s ../StreamingAssets unity-build/precompute_Data/StreamingAssets
unzip tmp/packing.zip -d unity-build
ln -s ../StreamingAssets unity-build/packing_Data/StreamingAssets
unzip tmp/resources.zip -d unity-build

rm -r tmp