using Spherewright.Bridge.Core.Safety;
using UnityEngine;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private GameData? _constructionStandstillWorld;
    private Player? _constructionStandstillPlayer;
    private Vector3 _constructionStandstillPosition;
    private long _constructionStandstillTick=-1;
    private int _constructionStandstillFrames;

    // Observe only at the real501 callback. A broad foot sphere can touch
    // terrain while the rigid body is still settling by a quarter metre.
    private void ObserveConstructionStandstill()
    {
        var p=GameMain.mainPlayer;var tick=GameMain.gameTick;
        if(p is null || !_sessions.IsCurrentSessionOwned || GameMain.isPaused || !ConstructionPlayerIdle(p)
            || !ConstructionGroundSettled(p,out _,out _) || p.controller.rigid.velocity.sqrMagnitude!=0f
            || p.controller.velocity.sqrMagnitude!=0f || p.controller.actionDrift.rtsVelocity.sqrMagnitude!=0f)
        {_constructionStandstillFrames=0;_constructionStandstillTick=tick;return;}
        if(tick==_constructionStandstillTick)return;
        var retained=ReferenceEquals(GameMain.data,_constructionStandstillWorld) && ReferenceEquals(p,_constructionStandstillPlayer)
            && tick==_constructionStandstillTick+1 && Vector3.Distance(p.position,_constructionStandstillPosition)<=.001f;
        _constructionStandstillFrames=retained?Math.Min(64,_constructionStandstillFrames+1):1;
        _constructionStandstillWorld=GameMain.data;_constructionStandstillPlayer=p;
        _constructionStandstillPosition=p.position;_constructionStandstillTick=tick;
    }

    private bool ConstructionStandstillProved(Player p)=>ReferenceEquals(GameMain.data,_constructionStandstillWorld)
        && ReferenceEquals(p,_constructionStandstillPlayer)
        && NativeConstructionStandstillPolicy.StableProved(_constructionStandstillFrames,GameMain.gameTick-_constructionStandstillTick,
            Vector3.Distance(p.position,_constructionStandstillPosition))
        && ConstructionGroundSettled(p,out _,out _);

    private static bool ConstructionGroundSettled(Player p,out double? height,out double? normal)
    {
        height=null;normal=null;var scene=CaptureNativeLocalWalkScene();if(scene is null || !scene.MatchesCurrent())return false;
        var up=p.position.normalized;
        if(!Physics.Raycast(new Ray(p.position+up*.5f,-up),out var hit,1f,15873,QueryTriggerInteraction.Ignore))return false;
        var own=scene.Surface.Count(c=>ReferenceEquals(c,hit.collider))==1
            || GameStateReader.CurrentOwnedContactOwnerBlocker(p,scene.Surface,hit.collider) is null;
        height=Vector3.Dot(hit.point-p.position,up);normal=Vector3.Dot(hit.normal,up);
        return NativeConstructionStandstillPolicy.SupportProved(own,height,normal);
    }
}
