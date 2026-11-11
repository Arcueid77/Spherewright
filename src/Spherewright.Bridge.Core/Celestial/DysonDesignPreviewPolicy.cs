using Spherewright.Contracts.Celestial;

namespace Spherewright.Bridge.Core.Celestial;

public static class DysonDesignPreviewPolicy
{
    public const int MaximumSteps = 64;

    public static bool ExistingReferencesBound(DysonDesignPreviewRequest request, IEnumerable<int> existingNodeIds)
    {
        var existing = new HashSet<int>(existingNodeIds);
        return request.Steps.SelectMany(step => new[] { step.Structure.NodeAId, step.Structure.NodeBId }
                .Concat(step.Structure.BoundaryNodeIds))
            .Where(id => id > 0).All(existing.Contains);
    }

    public static string? ValidateRequest(int layerId, DysonDesignPreviewRequest request)
    {
        if (layerId < 1 || layerId > DysonObservationPolicy.MaximumLayers
            || string.IsNullOrEmpty(request.ExpectedDesignStateHash) || request.Steps is null
            || request.Steps.Count < 1 || request.Steps.Count > MaximumSteps)
            return "dyson_design_preview_requires_one_layer_hash_and_1_to_64_steps";
        var keys = new HashSet<int>();
        var nodeKeys = new Dictionary<int, int>();
        foreach (var step in request.Steps)
        {
            if (step is null || step.LocalId >= 0 || !keys.Add(step.LocalId) || step.Structure is null
                || step.Structure.LayerId != layerId || step.Structure.BoundaryNodeIds is null)
                return "dyson_design_preview_requires_unique_negative_step_ids_and_one_layer";
            var structure = step.Structure.Copy();
            var references = new[] { structure.NodeAId, structure.NodeBId }.Concat(structure.BoundaryNodeIds);
            if (references.Any(id => id < 0 && !nodeKeys.ContainsKey(id)))
                return "dyson_design_preview_references_must_select_prior_nodes";
            // Temporary positive identities validate the ordinary shape contract.
            // They stay outside the actual Native ID range and never reach a writer.
            structure.NodeAId = Resolve(structure.NodeAId, nodeKeys);
            structure.NodeBId = Resolve(structure.NodeBId, nodeKeys);
            structure.BoundaryNodeIds = structure.BoundaryNodeIds.Select(id => Resolve(id, nodeKeys)).ToList();
            if (DysonStructurePolicy.ValidateStructure(structure) is { } reason) return reason;
            if (structure.Kind == DysonStructureKinds.Node) nodeKeys[step.LocalId] = int.MaxValue - nodeKeys.Count;
        }
        return null;
    }

    private static int Resolve(int id, IReadOnlyDictionary<int, int> keys) => id < 0 ? keys[id] : id;
}
