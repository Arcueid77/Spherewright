using Spherewright.Bridge.Core.Factory;
using Spherewright.Bridge.Core.Safety;
using Spherewright.Contracts.Actions;
using Spherewright.Contracts.Factory;
using Spherewright.Contracts.Logistics;
using Spherewright.Plugin.Transport;

namespace Spherewright.Plugin.Game;

internal sealed partial class NormalGameActionCoordinator
{
    private static PrepareConfigureBuildingRequest StationDeliveryRequest(NormalActionPlanPayload p)=>new()
    {
        PlanetId=p.PlanetId,EntityId=p.EntityId,Mode=BuildingConfigurationModes.LogisticsStationDelivery,
        ExpectedStationConfigurationStateHash=p.StationConfigurationStateHash,
        StationVesselDeliveryMinimumPercent=p.ConfigureStationVesselDeliveryMinimumPercent,
    };

    private void ApplyStationDeliveryOnMainThread(NormalActionPlanPayload plan)
    {
        var request=StationDeliveryRequest(plan);
        var query=new InspectFactoryEntityRequest {PlanetId=plan.PlanetId,ObjectId=plan.EntityId};
        var before=_reader.InspectFactoryEntityOnMainThread(plan.SessionId,query);
        var observed=before.Value?.LogisticsStation;
        if(!before.Success || !LogisticsStationDeliveryPolicy.CanApply(request,observed))
            throw new InvalidOperationException("The exact idle station delivery setting changed before its Native UI assignment.");
        var expected=PluginJson.Deserialize<LogisticsStationSnapshot>(PluginJson.Serialize(observed!))!;
        expected.VesselDeliverySetting=plan.ConfigureStationVesselDeliveryMinimumPercent;
        var expectedFull=CanonicalStateHash.LogisticsStation(expected);
        var package=CapturePlayerPackageState(GameMain.mainPlayer);
        var factory=GameMain.localPlanet!.factory;
        var station=factory.transport.GetStationComponent(factory.entityPool[plan.EntityId].stationId)
            ??throw new InvalidOperationException("The exact station disappeared.");
        // Native29151 UIStationWindow.OnMinDeliverVesselValueChange clamps
        // the percentage to1..100 and assigns this same configuration field.
        // No cargo, energy, fleet, order or simulation field is assigned.
        station.deliveryShips=plan.ConfigureStationVesselDeliveryMinimumPercent;
        var after=_reader.InspectFactoryEntityOnMainThread(plan.SessionId,query);
        if(!after.Success || after.Value?.LogisticsStation is not { } actual
            || station.deliveryShips!=plan.ConfigureStationVesselDeliveryMinimumPercent
            || CanonicalStateHash.LogisticsStation(actual)!=expectedFull
            || CapturePlayerPackageState(GameMain.mainPlayer)!=package)
            throw new InvalidOperationException("Native vessel delivery assignment did not preserve the complete station and player inventory state.");
    }
}
