# Copyright (c) 2026 RedBolt and contributors.
# This work is licensed under the terms of the MIT license.
# For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

redbolt_add_native_binding_generation(
    TARGET redbolt-generate-box2d-bindings
    SURFACE box2d
    HEADER "${REDBOLT_BOX2D_PARSE_HEADER}"
    HEADER_INCLUDE_PATH "Box2DAll.h"
    FILE_STEM "${REDBOLT_BOX2D_BINDINGS_STEM}"
    OUTPUT_DIR "${REDBOLT_BOX2D_GENERATED_DIR}"
    OUTPUT_METADATA "${REDBOLT_BOX2D_BINDINGS_JSON}"
    OUTPUT_CS "${REDBOLT_BOX2D_BINDINGS_CS}"
    OUTPUT_CPP "${REDBOLT_BOX2D_BINDINGS_CPP}"
    NATIVE_LIBRARY_NAME "RedBolt.Native.Box2D"
    INCLUDE_DIRS "${REDBOLT_BOX2D_INCLUDE_DIR}"
    OWNED_HEADERS "${REDBOLT_BOX2D_PARSE_HEADER}" "${REDBOLT_BOX2D_INCLUDE_DIR}/box2d")

add_dependencies(redbolt-generate-box2d-bindings box2d)
