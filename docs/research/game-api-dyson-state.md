# Native Dyson structure and ray-receiver observations

[English](game-api-dyson-state.md) · [简体中文](game-api-dyson-state.zh-CN.md)

The 0.6 Ascension observation slice targets DSP `0.10.35.29151`, Assembly-CSharp SHA-256 `EF314425DADE51F529F184F07BE136CD3C2660A6A81515603C7226FBE7AB1D8C`. Research uses the locally installed Native assembly and a full Native Plugin build.

## Structures and progress

`spherewright_get_dyson_state` binds the current owned local planet, its local star and an existing `GameData.dysonSpheres` entry. `layerId=0` selects all existing layers. IDs 1 through 10 select one layer. The reader copies scalar data on the game thread from the existing layer/node/frame/shell pools.

Node `sp` and `spMax` describe delivered and planned node structure points. A frame contributes `spA + spB` once and has its own `spMax`. Ordered structure points come from the node orders. Shell progress comes from the final `nodecps` accumulator, with maximum cells calculated from `vertexCount * cpPerVertex`. Boundary node/frame IDs are copied and checked as a closed cycle. Aggregation counts each frame and shell once.

The complete design hash binds the owned session, star, selected-layer scope, radii, orbit rotations, node positions/prototypes, frame endpoints and shell boundaries. Delivery progress and recent generation are sampled independently. The existing bounded silo rocket observer supplies matching rocket IDs and targets. Sail counters retain their Native allocation checks.

One observation returns at most 10 layers, 256 nodes, 512 frames, 128 shells, 4096 shell boundary nodes and 256 receivers. Pool scans are bounded. Incomplete or inconsistent coverage produces explicit issues and nullable aggregate totals. Selecting one layer provides a smaller observation scope for larger designs.

## Receiver power

Native `PowerSystem.GameTick` distributes useful electrical generation among connected generators and writes each generator's `generateCurrentTick`. That field is copied as `generatedEnergyPerTick`, with watts calculated at 60 Native ticks per second.

Gamma `capacityCurrentTick` follows the Native sunlight, warmup, catalyst and Dyson response calculations. Photon mode uses this capacity for product progress while its electrical `generateCurrentTick` is zero. The response therefore exposes capacity, actual recent electrical generation, product mode/buffer, catalyst count/inc and residual catalyst points as separate fields.

Each receiver binds its entity, Native gamma prefab, generator pool, star, planet and electrical network membership. Current-star coverage includes already-created resident factories. Factory detail inspection exposes the same data as optional `rayReceiver`.

## Verification workflow

Use a declared game-tick interval with stable receiver identity, mode and network membership to validate sustained output. Record each sample's game tick, delivered electricity, buffer changes and topology. The observation endpoint supplies read-only data with `executable=false` and `commitAllowedNow=false`. Native design writes, production supply and save/recovery acceptance follow their own normal execution paths.

This source slice has offline structure/power policy tests, MCP forwarding/schema checks and a Native build. Installed 0.6 runtime and gameplay evidence will be recorded through the Ascension scenario in Spherewright-Lab.
