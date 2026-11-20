using Spherewright.Contracts.Actions;

namespace Spherewright.Bridge.Core.Safety;

// The game adapter supplies only the normal Save, complete-file proof and the
// native Esc-menu EndGame path. There is deliberately no process-exit callback.
public sealed class ProtectedMainMenuReturn
{
    private DateTimeOffset? _deadline;
    private const int ReadbackWaitSeconds = 120;
    public MainMenuReturnReadback Readback { get; } = new MainMenuReturnReadback();
    public bool Terminal { get; private set; }
    public bool Succeeded => Terminal && Readback.MainMenuVerified;

    // A paused preservation exit serializes the current state before EndGame;
    // the actor and drones need not first become physically idle. A running
    // world still needs the ordinary idle check.
    public static string? ReadinessBlocker(bool otherActionActive, bool nativeGameReady,
        bool pausedPreservation, bool nativePaused, bool physicallyIdle)
    {
        if (otherActionActive) return "Wait for every accepted action to become terminal before returning to the main menu.";
        if (!nativeGameReady) return "The loaded game, menu interface or loader is not ready for a normal return.";
        if (pausedPreservation)
            return nativePaused ? null : "Paused preservation return requires the current world to remain paused.";
        return physicallyIdle ? null : "Returning to the main menu requires the grounded, order-free player and physically idle construction drones.";
    }

    public void Begin(Func<long> save, Func<long, bool> verify, Action endGame)
    {
        if (Readback.NativeEndRequested || Terminal) return;
        var predicate = "normal_owned_save";
        try
        {
            var tick = save();
            Readback.NormalSaveConfirmed = true;
            Readback.SavedGameTick = tick;
            predicate = "complete_current_owned_save";
            if (tick < 0 || !verify(tick)) { Fail(predicate); return; }
            Readback.CompleteSaveVerified = true;
            predicate = "native_end_game";
            Readback.NativeEndRequested = true;
            endGame();
        }
        catch { Fail(predicate); }
    }

    public void Observe(Func<bool> mainMenuReady, DateTimeOffset now)
    {
        if (Terminal || !Readback.NativeEndRequested) return;
        try
        {
            // Save and EndGame have already returned. A verified menu remains
            // success even when the first available poll arrives late.
            if (mainMenuReady())
            {
                Readback.MainMenuVerified = true;
                Terminal = true;
                return;
            }
            if (_deadline is null) _deadline = now.AddSeconds(ReadbackWaitSeconds);
            else if (now >= _deadline.Value) Fail("main_menu_readback_timeout");
        }
        catch { Fail("main_menu_readback"); return; }
    }

    private void Fail(string predicate) { Readback.FailedPredicate = predicate; Terminal = true; }
}
