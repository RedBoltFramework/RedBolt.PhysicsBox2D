# Copyright (c) 2026 RedBolt and contributors.
# This work is licensed under the terms of the MIT license.
# For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

cmake_minimum_required(VERSION 3.28)

project(RedBoltPhysicsBox2D VERSION 0.1.0 LANGUAGES C CXX)

set(BUILD_SHARED_LIBS OFF CACHE BOOL "Build dependencies statically" FORCE)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)
set(CMAKE_CXX_SCAN_FOR_MODULES OFF)

if(EMSCRIPTEN)
    add_compile_options(-mbulk-memory)
endif()
if(ANDROID)
    add_compile_options(-fsigned-char)
    add_link_options("-Wl,-z,max-page-size=16384")
endif()

# LLVM-MinGW's COFF linker supports whole-archive, but not the GNU ld
# push-state/pop-state pair emitted by CMake's default feature definition.
if(MINGW)
    set(CMAKE_CXX_LINK_LIBRARY_USING_WHOLE_ARCHIVE
        "LINKER:--whole-archive"
        "<LINK_ITEM>"
        "LINKER:--no-whole-archive")
    set(CMAKE_CXX_LINK_LIBRARY_USING_WHOLE_ARCHIVE_SUPPORTED TRUE)
endif()

set(REDBOLT_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../RedBolt" CACHE PATH "RedBolt source root")
set(REDBOLT_SHARPCPP_ROOT "${REDBOLT_ROOT}/extern/SharpCpp" CACHE PATH "SharpCpp source root")
set(REDBOLT_DEPENDENCY_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/extern" CACHE PATH "RedBolt.PhysicsBox2D dependency cache")
set(REDBOLT_GENERATED_ROOT "${CMAKE_BINARY_DIR}/generated")
set(REDBOLT_NATIVE_GENERATED_DIR "${REDBOLT_GENERATED_ROOT}/redbolt-physics-box2d-native")
set(REDBOLT_RUNTIME_OUTPUT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/build/bin/$<CONFIG>/linux-x64/extern/runtimes/linux-x64/native" CACHE PATH "Native runtime output")
set(REDBOLT_PDB_OUTPUT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/build/bin/$<CONFIG>/linux-x64/extern/pdb" CACHE PATH "Native PDB output")

find_program(DOTNET_EXECUTABLE dotnet REQUIRED)
function(redbolt_require_path path description)
    if(NOT EXISTS "${path}")
        message(FATAL_ERROR "${description} does not exist: ${path}")
    endif()
endfunction()

list(PREPEND CMAKE_MODULE_PATH "${REDBOLT_ROOT}/cmake/Modules")
set(CPM_DONT_UPDATE_MODULE_PATH ON CACHE BOOL "Use RedBolt CMake modules" FORCE)
set(CPM_SOURCE_CACHE "${REDBOLT_DEPENDENCY_ROOT}" CACHE PATH "CPM source cache" FORCE)
unset(CPM_DIRECTORY CACHE)
unset(CPM_VERSION CACHE)
include("${REDBOLT_ROOT}/cmake/CPM.cmake")
set(FETCHCONTENT_BASE_DIR "${CMAKE_BINARY_DIR}/deps")

include("${REDBOLT_ROOT}/cmake/RedBoltSharpCpp.cmake")

set(BOX2D_COMPILE_WARNING_AS_ERROR OFF CACHE BOOL "Disable Box2D warnings as errors" FORCE)
set(BOX2D_DISABLE_SIMD OFF CACHE BOOL "Use Box2D portable SIMD" FORCE)
set(BOX2D_AVX2 OFF CACHE BOOL "Disable Box2D AVX2" FORCE)
CPMAddPackage(
    NAME Box2D
    GIT_REPOSITORY https://github.com/erincatto/box2d.git
    GIT_TAG 8c661469c9507d3ad6fbd2fea3f1aa71669c2fe3
    CUSTOM_CACHE_KEY "."
    EXCLUDE_FROM_ALL YES)
set(REDBOLT_BOX2D_INCLUDE_DIR "${Box2D_SOURCE_DIR}/include")

if(ANDROID)
    if(ANDROID_ABI STREQUAL "x86_64")
        set(REDBOLT_BINDING_TARGET_TRIPLE "x86_64-linux-android24")
    elseif(ANDROID_ABI STREQUAL "arm64-v8a")
        set(REDBOLT_BINDING_TARGET_TRIPLE "aarch64-linux-android24")
    else()
        message(FATAL_ERROR "Unsupported Android ABI: ${ANDROID_ABI}")
    endif()
    get_filename_component(_redbolt_android_compiler_bin "${CMAKE_CXX_COMPILER}" DIRECTORY)
    get_filename_component(_redbolt_android_toolchain_root "${_redbolt_android_compiler_bin}" DIRECTORY)
    set(REDBOLT_BINDING_SYSROOT "${_redbolt_android_toolchain_root}/sysroot")
endif()
set(REDBOLT_BINDING_CXX_COMPILER "${CMAKE_CXX_COMPILER}")
set(_redbolt_2d_binding_defines)
if(CMAKE_CONFIGURATION_TYPES)
    list(APPEND _redbolt_2d_binding_defines "$<$<NOT:$<CONFIG:Debug>>:NDEBUG>")
elseif(CMAKE_BUILD_TYPE MATCHES "^(Release|Distribution|ReleaseASAN|ReleaseUBSAN|ReleaseTSAN|ReleaseCoverage)$")
    list(APPEND _redbolt_2d_binding_defines NDEBUG)
endif()
set(REDBOLT_BINDING_DEFINES ${_redbolt_2d_binding_defines})

add_subdirectory(src/RedBolt/Generator/Dependencies)
add_subdirectory(src/RedBolt/Native/Box2D)

if(EMSCRIPTEN)
    set(_redbolt_box2d_library_type STATIC)
else()
    set(_redbolt_box2d_library_type SHARED)
endif()
add_library(redbolt-physics-box2d-native-box2d ${_redbolt_box2d_library_type} "${REDBOLT_BOX2D_BINDINGS_CPP}")
add_dependencies(redbolt-physics-box2d-native-box2d redbolt-generate-box2d-bindings)
target_link_libraries(redbolt-physics-box2d-native-box2d PRIVATE SharpCpp::NativeSupport)
if(NOT EMSCRIPTEN)
    target_link_libraries(redbolt-physics-box2d-native-box2d PRIVATE "$<LINK_LIBRARY:WHOLE_ARCHIVE,box2d>")
endif()
target_include_directories(redbolt-physics-box2d-native-box2d PRIVATE "${REDBOLT_BOX2D_INCLUDE_DIR}")
target_compile_definitions(redbolt-physics-box2d-native-box2d PRIVATE REDBOLT_NATIVE_BUILD)
set_target_properties(redbolt-physics-box2d-native-box2d PROPERTIES
    OUTPUT_NAME "RedBolt.Native.Box2D"
    RUNTIME_OUTPUT_DIRECTORY "${REDBOLT_RUNTIME_OUTPUT_DIR}"
    LIBRARY_OUTPUT_DIRECTORY "${REDBOLT_RUNTIME_OUTPUT_DIR}"
    ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib/$<CONFIG>"
    WINDOWS_EXPORT_ALL_SYMBOLS ON
    C_VISIBILITY_PRESET default)
if(CMAKE_CONFIGURATION_TYPES)
    foreach(_redbolt_2d_configuration IN LISTS CMAKE_CONFIGURATION_TYPES)
        string(TOUPPER "${_redbolt_2d_configuration}" _redbolt_2d_configuration_upper)
        set_target_properties(redbolt-physics-box2d-native-box2d PROPERTIES
            RUNTIME_OUTPUT_DIRECTORY_${_redbolt_2d_configuration_upper} "${REDBOLT_RUNTIME_OUTPUT_DIR}"
            LIBRARY_OUTPUT_DIRECTORY_${_redbolt_2d_configuration_upper} "${REDBOLT_RUNTIME_OUTPUT_DIR}")
    endforeach()
endif()
if(WIN32)
    set_target_properties(redbolt-physics-box2d-native-box2d PROPERTIES PREFIX "")
endif()

if(NOT EMSCRIPTEN)
    file(GENERATE
        OUTPUT "${REDBOLT_GENERATED_ROOT}/native-modules/$<CONFIG>/BOX2D.json"
        CONTENT "{\n  \"module\": \"BOX2D\",\n  \"managedSelector\": \"RedBolt.Bindings.Box2D\",\n  \"nativeLibrary\": \"RedBolt.Native.Box2D\",\n  \"requires\": [],\n  \"requirementClosure\": [],\n  \"artifacts\": [\"$<TARGET_FILE_NAME:redbolt-physics-box2d-native-box2d>\"]\n}\n")
endif()

add_custom_target(redbolt-physics-box2d-native-all DEPENDS redbolt-physics-box2d-native-box2d)
add_custom_target(redbolt-physics-box2d-android-native DEPENDS redbolt-physics-box2d-native-box2d)

if(EMSCRIPTEN)
    set(REDBOLT_PHYSICS_BOX2D_WASM_ARCHIVE "${CMAKE_BINARY_DIR}/lib/Release/wasm/RedBolt.PhysicsBox2D.Native.Web.a")
    set(REDBOLT_PHYSICS_BOX2D_WASM_MRI "${CMAKE_BINARY_DIR}/redbolt-physics-box2d-wasm-archives.mri")
    file(GENERATE OUTPUT "${REDBOLT_PHYSICS_BOX2D_WASM_MRI}" CONTENT
        "create ${REDBOLT_PHYSICS_BOX2D_WASM_ARCHIVE}\naddlib $<TARGET_FILE:redbolt-physics-box2d-native-box2d>\naddlib $<TARGET_FILE:box2d>\nsave\nend\n")
    add_custom_command(
        OUTPUT "${REDBOLT_PHYSICS_BOX2D_WASM_ARCHIVE}"
        COMMAND "${CMAKE_COMMAND}"
            "-DREDBOLT_EMSCRIPTEN_ENV=${_redbolt_emscripten_environment}"
            "-DREDBOLT_ARCHIVER=${CMAKE_AR}"
            "-DREDBOLT_MRI_FILE=${REDBOLT_PHYSICS_BOX2D_WASM_MRI}"
            "-DREDBOLT_OUTPUT_ARCHIVE=${REDBOLT_PHYSICS_BOX2D_WASM_ARCHIVE}"
            -P "${REDBOLT_ROOT}/cmake/MergeStaticArchives.cmake"
        DEPENDS redbolt-physics-box2d-native-box2d box2d
        VERBATIM)
    add_custom_target(redbolt-physics-box2d-wasm-native DEPENDS "${REDBOLT_PHYSICS_BOX2D_WASM_ARCHIVE}")
endif()
