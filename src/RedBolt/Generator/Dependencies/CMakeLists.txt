# Copyright (c) 2026 RedBolt and contributors.
# This work is licensed under the terms of the MIT license.
# For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

if(NOT REDBOLT_DEPENDENCIES_GENERATOR_DLL)
    set(REDBOLT_DEPENDENCIES_GENERATOR_DLL
        "${PROJECT_SOURCE_DIR}/build/bin/$<CONFIG>/host/tools/RedBolt.PhysicsBox2D.Generator.Dependencies.dll"
        CACHE FILEPATH "RedBolt.PhysicsBox2D dependency generator assembly")
endif()

file(GLOB_RECURSE REDBOLT_DEPENDENCIES_GENERATOR_INPUTS CONFIGURE_DEPENDS
    "${CMAKE_CURRENT_SOURCE_DIR}/*.cs"
    "${CMAKE_CURRENT_SOURCE_DIR}/*.csproj"
    "${REDBOLT_ROOT}/src/RedBolt/Generator/Dependencies/BindingTargetGenerator.cs"
    "${REDBOLT_ROOT}/src/RedBolt/Generator/Dependencies/FixedWidthAliasNormalizer.cs"
    "${REDBOLT_ROOT}/src/RedBolt/Generator/Dependencies/GeneratorOptions.cs")

set(REDBOLT_DEPENDENCIES_GENERATOR_INPUTS "${REDBOLT_DEPENDENCIES_GENERATOR_INPUTS}" CACHE INTERNAL "RedBolt.PhysicsBox2D generator inputs")

set(REDBOLT_BOX2D_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/box2d" CACHE INTERNAL "Box2D binding output")
set(REDBOLT_BOX2D_BINDINGS_STEM "RedBolt.Bindings.Box2D" CACHE INTERNAL "Box2D binding stem")
set(REDBOLT_BOX2D_BINDINGS_CS "${REDBOLT_BOX2D_GENERATED_DIR}/${REDBOLT_BOX2D_BINDINGS_STEM}.Bindings.g.cs" CACHE INTERNAL "Box2D generated C# binding")
set(REDBOLT_BOX2D_BINDINGS_CPP "${REDBOLT_BOX2D_GENERATED_DIR}/${REDBOLT_BOX2D_BINDINGS_STEM}.Bindings.g.cpp" CACHE INTERNAL "Box2D generated native binding")
set(REDBOLT_BOX2D_BINDINGS_JSON "${REDBOLT_BOX2D_GENERATED_DIR}/${REDBOLT_BOX2D_BINDINGS_STEM}.json" CACHE INTERNAL "Box2D binding metadata")
set(REDBOLT_BOX2D_PARSE_HEADER "${REDBOLT_BOX2D_GENERATED_DIR}/Box2DAll.h" CACHE INTERNAL "Box2D parse header")
file(MAKE_DIRECTORY "${REDBOLT_BOX2D_GENERATED_DIR}")

file(CONFIGURE OUTPUT "${REDBOLT_BOX2D_PARSE_HEADER}" CONTENT [=[
#pragma once
#include <stdlib.h>
#include <box2d/box2d.h>

typedef struct redbolt_b2RayCastContext
{
    int targetIndex;
    int currentIndex;
    b2RayResult result;
} redbolt_b2RayCastContext;

static float redbolt_b2RayCastCallback(
    b2ShapeId shapeId, b2Vec2 point, b2Vec2 normal, float fraction, void* context)
{
    redbolt_b2RayCastContext* data = (redbolt_b2RayCastContext*)context;
    if (data->currentIndex == data->targetIndex)
    {
        data->result.shapeId = shapeId;
        data->result.point = point;
        data->result.normal = normal;
        data->result.fraction = fraction;
        data->result.hit = true;
    }
    data->currentIndex += 1;
    return 1.0f;
}

static inline int redbolt_b2WorldCastRayCount(
    b2WorldId worldId, b2Vec2 origin, b2Vec2 translation, b2QueryFilter filter)
{
    redbolt_b2RayCastContext context = {-1, 0, {0}};
    b2World_CastRay(worldId, origin, translation, filter, redbolt_b2RayCastCallback, &context);
    return context.currentIndex;
}

static inline b2RayResult redbolt_b2WorldCastRayAt(
    b2WorldId worldId, b2Vec2 origin, b2Vec2 translation, b2QueryFilter filter, int index)
{
    redbolt_b2RayCastContext context = {index, 0, {0}};
    b2World_CastRay(worldId, origin, translation, filter, redbolt_b2RayCastCallback, &context);
    return context.result;
}

static inline b2ContactBeginTouchEvent redbolt_b2ContactBeginEventAt(b2WorldId worldId, int index)
{
    b2ContactEvents events = b2World_GetContactEvents(worldId);
    b2ContactBeginTouchEvent empty = {0};
    return index >= 0 && index < events.beginCount ? events.beginEvents[index] : empty;
}

static inline b2ContactEndTouchEvent redbolt_b2ContactEndEventAt(b2WorldId worldId, int index)
{
    b2ContactEvents events = b2World_GetContactEvents(worldId);
    b2ContactEndTouchEvent empty = {0};
    return index >= 0 && index < events.endCount ? events.endEvents[index] : empty;
}

static inline b2SensorBeginTouchEvent redbolt_b2SensorBeginEventAt(b2WorldId worldId, int index)
{
    b2SensorEvents events = b2World_GetSensorEvents(worldId);
    b2SensorBeginTouchEvent empty = {0};
    return index >= 0 && index < events.beginCount ? events.beginEvents[index] : empty;
}

static inline b2SensorEndTouchEvent redbolt_b2SensorEndEventAt(b2WorldId worldId, int index)
{
    b2SensorEvents events = b2World_GetSensorEvents(worldId);
    b2SensorEndTouchEvent empty = {0};
    return index >= 0 && index < events.endCount ? events.endEvents[index] : empty;
}

typedef struct redbolt_b2PreSolveData
{
    int shapeAIndex;
    unsigned short shapeAWorld;
    unsigned short shapeAGeneration;
    int shapeBIndex;
    unsigned short shapeBWorld;
    unsigned short shapeBGeneration;
    float normalX;
    float normalY;
    int pointCount;
    float point0X;
    float point0Y;
    float separation0;
    float point1X;
    float point1Y;
    float separation1;
} redbolt_b2PreSolveData;

typedef int (*redbolt_b2PreSolveCallback)(const redbolt_b2PreSolveData* contact, void* context);

typedef struct redbolt_b2PreSolveContext
{
    redbolt_b2PreSolveCallback callback;
    void* context;
} redbolt_b2PreSolveContext;

static bool redbolt_b2PreSolveThunk(
    b2ShapeId shapeA, b2ShapeId shapeB, b2Manifold* manifold, void* context)
{
    redbolt_b2PreSolveContext* data = (redbolt_b2PreSolveContext*)context;
    const int pointCount = manifold != NULL ? manifold->pointCount : 0;
    const b2Vec2 normal = manifold != NULL ? manifold->normal : (b2Vec2){0};
    const b2Vec2 point0 = pointCount > 0 ? manifold->points[0].point : (b2Vec2){0};
    const b2Vec2 point1 = pointCount > 1 ? manifold->points[1].point : (b2Vec2){0};
    const float separation0 = pointCount > 0 ? manifold->points[0].separation : 0.0f;
    const float separation1 = pointCount > 1 ? manifold->points[1].separation : 0.0f;
    const redbolt_b2PreSolveData contact = {
        shapeA.index1, shapeA.world0, shapeA.generation,
        shapeB.index1, shapeB.world0, shapeB.generation,
        normal.x, normal.y, pointCount,
        point0.x, point0.y, separation0,
        point1.x, point1.y, separation1};
    return data->callback(&contact, data->context) != 0;
}

static inline void* redbolt_b2WorldSetManagedPreSolveCallback(
    b2WorldId worldId, void* callback, void* context)
{
    redbolt_b2PreSolveContext* data = (redbolt_b2PreSolveContext*)malloc(sizeof(redbolt_b2PreSolveContext));
    if (data == NULL)
        return NULL;
    data->callback = (redbolt_b2PreSolveCallback)callback;
    data->context = context;
    b2World_SetPreSolveCallback(worldId, redbolt_b2PreSolveThunk, data);
    return data;
}

static inline void redbolt_b2WorldClearManagedPreSolveCallback(b2WorldId worldId, void* callbackContext)
{
    b2World_SetPreSolveCallback(worldId, NULL, NULL);
    free(callbackContext);
}
]=] @ONLY)
