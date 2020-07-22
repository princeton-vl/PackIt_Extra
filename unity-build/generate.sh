#!/bin/bash

VERSION="packing"
DATASET="tr"
NUM_CHROMO_GEN=100
NUM_GEN_BREAK=100
NUM_GEN=1000
NUM_PACKS=10
USE_EMPTY_SPACE="false"
INIT_LARGEST_SHAPE="false"
RUN_ABLATION="false"
RESOLUTION=0.0125
RUN=0
SEED=$(($RANDOM*100 + $RANDOM))

POSITIONAL=()
while [[ $# -gt 0 ]]
do
key="$1"
case $key in
    -v|--version)
    VERSION="$2"
    shift # past argument
    shift # past value
    ;;
    -d|--dataset)
    DATASET="$2"
    shift # past argument
    shift # past value
    ;;
    -ncg|--num_chromo_gen)
    NUM_CHROMO_GEN="$2"
    shift # past argument
    shift # past value
    ;;
    -ngb|--num_gen_break)
    NUM_GEN_BREAK="$2"
    shift # past argument
    shift # past value
    ;;
    -ng|--num_gen)
    NUM_GEN="$2"
    shift # past argument
    shift # past value
    ;;
    -np|--num_packs)
    NUM_PACKS="$2"
    shift # past argument
    shift # past value
    ;;
    -ues|--use_empty_space)
    USE_EMPTY_SPACE="$2"
    shift # past argument
    shift # past value
    ;;
    -ils|--init_largest_shape)
    INIT_LARGEST_SHAPE="$2"
    shift # past argument
    shift # past value
    ;;
    -ra|--run_ablation)
    RUN_ABLATION="$2"
    shift # past argument
    shift # past value
    ;;
    -r|--resolution)
    RESOLUTION="$2"
    shift # past argument
    shift # past value
    ;;
    -ru|--run)
    RUN="$2"
    shift # past argument
    shift # past value
    ;;
    *)
    echo "unknow argument $1" # unknown argument
    ;;
esac
done

FILE_NAME="pack_""$DATASET"/"$RUN"_"$DATASET"
SHAPENET="./Resources/Shapes_""$DATASET"

COMMAND="./""$VERSION"".x86_64 -nographics -fileName $FILE_NAME -shapeNet \
$SHAPENET -numPacks $NUM_PACKS -numChromoGen $NUM_CHROMO_GEN -numGen $NUM_GEN \
-numGenBreak $NUM_GEN_BREAK -useEmptySpace $USE_EMPTY_SPACE -initLargestShape \
$INIT_LARGEST_SHAPE -resolution $RESOLUTION -seed $SEED -runAblation $RUN_ABLATION"
echo $COMMAND
$COMMAND

COMMAND="./precompute.x86_64 -nographics -packFolder pack_""$DATASET"" -packFile  ""$RUN"_"$DATASET"
echo $COMMAND
$COMMAND
