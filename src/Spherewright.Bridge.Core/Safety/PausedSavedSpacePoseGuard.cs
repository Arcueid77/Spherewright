using Spherewright.Contracts.Players;

namespace Spherewright.Bridge.Core.Safety;

/// <summary>Frozen saved Sail data, not a claim of zero inertial velocity or grounded idle.</summary>
public sealed class PausedSavedSpacePoseGuard
{
    private readonly object _world,_player,_controller;
    private readonly long _tick;
    private bool _invalidated;
    public string Fingerprint {get;}
    private PausedSavedSpacePoseGuard(object world,object player,object controller,long tick,string pose)
    {_world=world;_player=player;_controller=controller;_tick=tick;Fingerprint=pose;}

    public static PausedSavedSpacePoseGuard? Capture(object? world,object? player,object? controller,
        long savedTick,long currentTick,string movement,NativeSpaceTransitSnapshot? pose)
    {
        if(world is null || player is null || controller is null || savedTick<0 || savedTick!=currentTick
            || !Valid(movement,pose))return null;
        return new(world,player,controller,savedTick,Hash(pose!));
    }
    public bool Matches(object? world,object? player,object? controller,long tick,string movement,NativeSpaceTransitSnapshot? pose)=>
        !_invalidated && ReferenceEquals(world,_world) && ReferenceEquals(player,_player) && ReferenceEquals(controller,_controller)
        && tick==_tick && Valid(movement,pose) && Fingerprint==Hash(pose!);
    public void Invalidate()=>_invalidated=true;

    private static bool Valid(string movement,NativeSpaceTransitSnapshot? p)
    {
        if(p is null || movement!="Sail" || !p.NativePaused || p.Warping || p.WarpCommand || p.WarpState!=0
            || new[]{p.UniverseX,p.UniverseY,p.UniverseZ,p.VelocityX,p.VelocityY,p.VelocityZ,p.Speed}
                .Any(v=>double.IsNaN(v)||double.IsInfinity(v)) || p.Speed<0)return false;
        var norm=Math.Sqrt(p.VelocityX*p.VelocityX+p.VelocityY*p.VelocityY+p.VelocityZ*p.VelocityZ);
        return !double.IsInfinity(norm) && Math.Abs(norm-p.Speed)<=Math.Max(1e-8,norm*1e-10);
    }
    private static string Hash(NativeSpaceTransitSnapshot p)=>CanonicalStateHash.Combine("paused-saved-sail-pose-v1",
        p.StarId,p.UniverseX,p.UniverseY,p.UniverseZ,p.VelocityX,p.VelocityY,p.VelocityZ,p.Speed,
        p.NativePaused,p.WarpState,p.WarpCommand,p.Warping);
}
