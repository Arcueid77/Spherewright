using Spherewright.Contracts.Actions;

namespace Spherewright.Bridge.Core.Safety;

// The game adapter supplies only the normal Save, complete-file proof and the
// native Esc-menu EndGame path. There is deliberately no process-exit callback.
public sealed class ProtectedMainMenuReturn
{
    private DateTimeOffset _deadline;
    public MainMenuReturnReadback Readback { get; } = new MainMenuReturnReadback();
    public bool Terminal { get; private set; }
    public bool Succeeded => Terminal && Readback.MainMenuVerified;

    public void Begin(Func<long> save, Func<long, bool> verify, Action endGame, DateTimeOffset now)
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
            _deadline = now.AddSeconds(10);
            endGame();
        }
        catch { Fail(predicate); }
    }

    public void Observe(Func<bool> mainMenuReady, DateTimeOffset now)
    {
        if (Terminal || !Readback.NativeEndRequested) return;
        if (now >= _deadline) { Fail("main_menu_readback_timeout"); return; }
        try
        {
            if (mainMenuReady())
            {
                Readback.MainMenuVerified = true;
                Terminal = true;
                return;
            }
        }
        catch { Fail("main_menu_readback"); return; }
    }

    private void Fail(string predicate) { Readback.FailedPredicate = predicate; Terminal = true; }
}
