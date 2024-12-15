
wgetgdrive(){
  # $1 = file ID
  # $2 = file name

  URL="https://docs.google.com/uc?export=download&id=$1"

  wget --load-cookies /tmp/cookies.txt "https://docs.google.com/uc?export=download&confirm=$(wget --quiet --save-cookies /tmp/cookies.txt --keep-session-cookies --no-check-certificate $URL -O- | sed -rn 's/.*confirm=([0-9A-Za-z_]+).*/\1\n/p')&id=$1" -O $2 && rm -rf /tmp/cookies.txt
}

mkdir tmp
wgetgdrive 1T1P_GLgvPp_mF-tXGe_Yq_0FdARLF1R1 tmp/data.zip
wgetgdrive 1sfnY_LdIK4S8CnxyBpZtF-yN5mGak2el tmp/precompute.zip
wgetgdrive 1N1QTrSEa_x27dsfc5spU9iHp6PWCLb0H tmp/packing.zip
wgetgdrive 1HBURFlt3tUACDSgTo4NdM4BSnZ-lM2e5 tmp/resources.zip

unzip tmp/data.zip -d unity-build
unzip tmp/precompute.zip -d unity-build
ln -s ../StreamingAssets unity-build/precompute_Data/StreamingAssets
unzip tmp/packing.zip -d unity-build
ln -s ../StreamingAssets unity-build/packing_Data/StreamingAssets
unzip tmp/resources.zip -d unity-build

rm -r tmp
