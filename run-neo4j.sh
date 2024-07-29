#! /usr/bin/env nix-shell
#! nix-shell -i bash -p docker
# shellcheck shell=bash
set -euo pipefail

[ -d .neo4j ] || mkdir .neo4j;

docker run \
    -d --name markovbot-neo4j \
    -p 7474:7474 -p 7687:7687 \
    -v "$(realpath .neo4j)":/data \
    --env NEO4J_PLUGINS='["apoc"]' \
    neo4j:5.22.0