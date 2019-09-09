namespace Digi.BuildInfo.Features
{
    public class BuilderAdditions : ClientComponent
    {
        //private IMyHudNotification unsupportedGridSizeNotification;

        public BuilderAdditions(Client mod) : base(mod)
        {
        }

        public override void RegisterComponent()
        {
            //EquipmentMonitor.UpdateControlled += EquipmentMonitor_UpdateControlled;
        }

        public override void UnregisterComponent()
        {
            //EquipmentMonitor.UpdateControlled -= EquipmentMonitor_UpdateControlled;
        }

        //private void EquipmentMonitor_UpdateControlled(IMyCharacter character, IMyShipController shipController, VRage.Game.ModAPI.Interfaces.IMyControllableEntity controlled, int tick)
        //{
        //    if(!EquipmentMonitor.IsCubeBuilder)
        //        return;
        //
        //    var def = EquipmentMonitor.BlockDef;
        //
        //    if(def == null)
        //        return;
        //
        //    var hit = MyCubeBuilder.Static.HitInfo as IHitInfo;
        //    var grid = hit?.HitEntity as IMyCubeGrid;
        //
        //    if(grid != null && grid.GridSizeEnum != def.CubeSize)
        //    {
        //        if(unsupportedGridSizeNotification == null)
        //            unsupportedGridSizeNotification = MyAPIGateway.Utilities.CreateNotification("", 100, MyFontEnum.Red);
        //
        //        unsupportedGridSizeNotification.Text = $"({def.CubeSize}Ship) {def.DisplayNameText} can't be built on {grid.GridSizeEnum}Ship size.";
        //        unsupportedGridSizeNotification.Show();
        //    }
        //}
    }
}
