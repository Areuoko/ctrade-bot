using cAlgo.API;

namespace CleanPullM15Pro.Host;

/// <summary>
/// Minimal cBot lifecycle adapter. No trading logic implemented.
/// </summary>
[Robot(AccessRights = AccessRights.None)]
public class CleanPullM15ProBot : Robot
{
    /// <summary>Called once when the robot starts.</summary>
    protected override void OnStart()
    {
        // Phase 2 — wire Application orchestration here.
    }

    /// <summary>Called on each closed bar.</summary>
    protected override void OnBar()
    {
        // Phase 3+ — evaluate on each closed M15 bar.
    }

    /// <summary>Called once when the robot stops.</summary>
    protected override void OnStop()
    {
        // Cleanup only.
    }
}
