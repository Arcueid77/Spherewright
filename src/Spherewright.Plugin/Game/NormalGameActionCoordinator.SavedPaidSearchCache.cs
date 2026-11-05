using Spherewright.Bridge.Core.Factory;
using Spherewright.Contracts.Factory;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    // Native search candidates are not launched drones. Disabled Mecha.GameTick
    // retains this array. A builderModuleId=-1, however, can be dispatched by
    // DetermineLaunch even while the switch is disabled: it is never admitted.
    private bool TryBindSavedPaidSearchCache(Player p, PlanetFactory f, BlueprintBuildState b,
        out SavedPaidSearchCache? cache)
    {
        cache=null;
        if (!_sessions.IsCurrentSessionOwned || string.IsNullOrEmpty(_sessions.SessionId)
            || !ObserveSavedPaidSearchCache(p,f,b,out var targets)) return false;
        cache=new SavedPaidSearchCache(p,f,_sessions.SessionId!,b,targets);
        return true;
    }

    private static bool ObserveSavedPaidSearchCache(Player p,PlanetFactory f,BlueprintBuildState b,out HashSet<int> targets)
    {
        targets=new HashSet<int>();var m=p.mecha.constructionModule;
        if (!ReferenceEquals(p.factory,f) || m.droneEnabled || b.StructuredConstruction is null
            || b.Objects.Count<1 || b.Objects.Count>12 || b.Objects.Any(o=>o.State!=BlueprintObjectStates.PendingConstruction
                && o.State!=BlueprintObjectStates.Completed)
            || m.buildTargetTotalCount<0 || m.buildTargetTotalCount>b.Objects.Count
            || m.buildTargets is {Length:>256} || m.repairTargetTotalCount!=0 || m.autoReconstructTargetTotalCount!=0
            || m.repairTargets is {} repair && (repair.Length>256 || repair.Any(t=>t.objectId!=0))
            || m.autoReconstructTargets is {} auto && (auto.Length>256 || auto.Any(t=>t.objectId!=0))
            || !ConstructionOwnPrelaunchCacheClear(f,b)
            || b.Objects.Any(o=>o.State==BlueprintObjectStates.PendingConstruction
                && f.prebuildPool[o.PrebuildId!.Value].builderModuleId!=0)) return false;
        if(m.buildTargets is not null)
            foreach(var t in m.buildTargets)
            {
                if(t.objectId==0)continue;
                if(t.objectId<1 || !targets.Add(t.objectId) || float.IsNaN(t.value) || float.IsInfinity(t.value) || t.value<0f
                    || !b.Objects.Any(o=>o.State==BlueprintObjectStates.PendingConstruction && o.PrebuildId==t.objectId)
                    || t.objectId>=f.prebuildCursor || t.objectId>=f.prebuildPool.Length
                    || f.prebuildPool[t.objectId].id!=t.objectId || f.prebuildPool[t.objectId].isDestroyed) return false;
            }
        return m.buildTargetTotalCount==targets.Count;
    }

    private sealed class SavedPaidSearchCache
    {
        private readonly Player _player;private readonly PlanetFactory _factory;private readonly object _world;
        private readonly object _module;private readonly string _session,_binding,_submission;
        private readonly HashSet<int> _sourceTargets;
        internal SavedPaidSearchCache(Player p,PlanetFactory f,string session,BlueprintBuildState b,HashSet<int> targets)
        {_player=p;_factory=f;_world=GameMain.data!;_module=p.mecha.constructionModule;_session=session;
            _binding=b.StructuredConstruction!.ImmutableHash();_submission=SavedPaidConstructionStop.SubmissionHash(b);_sourceTargets=targets;}
        internal bool Matches(Player p,PlanetFactory f,string? session,BlueprintBuildState b)
            =>ReferenceEquals(p,_player) && ReferenceEquals(f,_factory) && ReferenceEquals(GameMain.data,_world)
                && ReferenceEquals(p.mecha.constructionModule,_module) && session==_session
                && b.StructuredConstruction?.ImmutableHash()==_binding && SavedPaidConstructionStop.SubmissionHash(b)==_submission
                && ObserveSavedPaidSearchCache(p,f,b,out var targets) && targets.All(_sourceTargets.Contains);
    }
}
