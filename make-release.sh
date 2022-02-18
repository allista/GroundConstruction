#!/bin/bash

CWD=$(dirname "$0")
cd "${CWD}" || exit

make_mod_release \
    -e '*/config.xml' '*.user' '*.orig' '*.mdb' '*.pdb' \
    '*/System.*.dll' '*/Mono.*.dll' '*/Unity*.dll' \
    'GameData/000_AT_Utils/Plugins/AnimatedConverters.dll' \
    'GameData/000_AT_Utils/Plugins/SubmodelResizer.dll' \
    'GameData/000_AT_Utils/ResourceHack.cfg' \
    'GameData/ConfigurableContainers/Parts/*' \
    -i '../AT_Utils/GameData' '../AT_Utils/ConfigurableContainers/GameData'
