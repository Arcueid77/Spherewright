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

The finite remote construction adapter admits item2208 through the existing paid drone workflow. Completion binds the entity's Native gamma generator and model prefab. Subsequent network membership, actual generation and sustained output are observed through the receiver data. Dyson design configuration uses the existing one-tick immediate remote-work budget with its original journey, energy and return reserves.

## Verification workflow

Use a declared game-tick interval with stable receiver identity, mode and network membership to validate sustained output. Record each sample's game tick, delivered electricity, buffer changes and topology. The observation endpoint supplies read-only data with `executable=false` and `commitAllowedNow=false`. Native design writes, production supply and save/recovery acceptance follow their own normal execution paths.

Source `ee810a699b71db44bfe11ae1e4e71b9370f79353` passed6389 source cases and exact CI. A coherent0.6 development installation completed normal cold continuation of the owned world, and the observer returned complete coverage of its initially empty local-star structures and receivers. Positive receiver electricity and nonempty design validation follow in Ascension. [Current-source Native record](https://github.com/Arcueid77/Spherewright-Lab/blob/e0023fabe84cb989f6498e99636cd83bfb64d03b/scenarios/v0.6-ascension/current-status.md)

## Bounded design and Native creation

The same29151 assembly defines `DysonSphereLayer.NewDysonNode(int, Vector3)`, `NewDysonFrame(int, int, int, bool)` and `NewDysonShell(int, List<int>)`. The ordinary editor brushes check technology latitude, node spacing, frame clearance/crossings and closed shell faces before these business operations. The adapter retains those checks on the main thread and requires a complete existing selected layer, current design hash, available render prototypes and a valid next allocator slot.

Native node collision uses normalized squared distance0.0051122503. Frame clearance uses0.0027562499 and maximum normalized chord length0.518. Stress admission compares rounded latitude degrees with rounded `GameHistoryData.dysonNodeLatitude`. `DysonFrame.segCount * 10` supplies the exact frame rocket requirement. Each new node requires30 structure points. A detached `DysonShell.GenerateGeometry()` supplies `vertexCount * cpPerVertex` for its sail requirement. The detached computation fills managed geometry and Native scratch maps.

`spherewright_get_dyson_state` exposes the current latitude and node/frame prototype counts. Its optional `designPreview` accepts1..64 ordered steps for one selected layer. Unique negative local IDs name proposed objects. Later steps reference earlier proposed nodes by their negative IDs or existing nodes by positive Native IDs. A detached graph copies the complete observed geometry and calculates each addition. `geometryComplete` governs nullable total rocket/sail demand. `technologyAllowsNow` reports whether current stress research admits the candidate. Geometry can be complete while that research remains required. Every preview is tokenless and read-only. Predicted IDs are observations of allocator state.

For execution use `spherewright_prepare_configure_building` with `mode=dyson-structure`, `entityId=0`, `recipeId=0`, `expectedFactoryStateHash=designStateHash` and one `dysonStructure`. Nodes carry their exact layer-local position at the existing radius. Frames carry two existing node IDs and an optional Euler curve. Shells carry3..32 unique nodes in boundary order and require existing intervening frames. The supported Native shell polygon has at most384 segment vertices. A `previewOnly=true` single-operation request returns costs without a token.

An executable plan binds the exact request, source session, planet/star, layer, static topology, current latitude, Native assets, object references and next allocation. Commit repeats these checks and calls one Native business operation. `dysonStructureReadback` records the planned/returned object IDs, whether the Native call began, original plan hash, before/after design hashes, exact added capacities and preservation of existing delivered/ordered progress and player package. A failure after invocation remains attached to the original action and quarantine. Multi-object execution fresh-prepares each next object using actual prior readbacks. Native manufacturing, launch, delivery and saved-world acceptance remain the subsequent validation stages.
