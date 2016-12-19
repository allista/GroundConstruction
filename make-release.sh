#!/bin/bash

cd $(dirname "$0")

../../PyKSPutils/make_mod_release \
-e '*/config.xml' '*.user' '*.orig' '*.mdb' \
'GameData/000_AT_Utils/Plugins/AnimatedConverters.dll' \
'GameData/ConfigurableContainers/Parts/*' \
-i '../AT_Utils/GameData' '../AT_Utils/ConfigurableContainers/GameData'
