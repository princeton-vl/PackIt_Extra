
wgetgdrive(){
  # $1 = file ID
  # $2 = file name

  URL="https://docs.google.com/uc?export=download&id=$1"

  wget --load-cookies /tmp/cookies.txt "https://docs.google.com/uc?export=download&confirm=$(wget --quiet --save-cookies /tmp/cookies.txt --keep-session-cookies --no-check-certificate $URL -O- | sed -rn 's/.*confirm=([0-9A-Za-z_]+).*/\1\n/p')&id=$1" -O $2 && rm -rf /tmp/cookies.txt
}

mkdir tmp
wgetgdrive 1VpwiAx0i1sanmqHLf1TIe9re5LBVaPQW tmp/data.zip
wgetgdrive 12SNYDxsruXCeQasOzVgdlbBrHzAkOlDO tmp/precompute.zip
wgetgdrive 1z46AEKQJtSgoA48bXkqfNVLYW8vmjq7X tmp/packing.zip
wgetgdrive 1BGv3V0k7fyTlkhpAtC-GFVUopV19nkbY tmp/resources.zip

unzip tmp/data.zip -d unity-build
unzip tmp/precompute.zip -d unity-build
ln -s ../StreamingAssets unity-build/precompute_Data/StreamingAssets
unzip tmp/packing.zip -d unity-build
ln -s ../StreamingAssets unity-build/packing_Data/StreamingAssets
unzip tmp/resources.zip -d unity-build

rm -r tmp
