using System;
using VRage.Game;
using VRage.Game.Components;

namespace Digi.ComponentLib
{
    /// <summary>
    /// Component to tie component logic to game API.
    /// Intended as partial for easy configuration without modifying the lib.
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public partial class BuildInfo_GameSession : MySessionComponentBase
    {
        IModBase ModBase;

        public override void LoadData()
        {
            try
            {
                LoadMod();
                ModBase?.FinishProfilingConstructors();
            }
            catch(Exception e)
            {
                Log.Error(e);
                UnloadData();
                throw new Exception("Error in mod loading, see above exceptions.");
            }
        }

        public override void BeforeStart()
        {
            try
            {
                ModBase?.WorldStart();
            }
            catch(Exception e)
            {
                Log.Error(e);
                UnloadData();
                throw new Exception("Error in mod loading, see above exceptions.");
            }
        }

        protected override void UnloadData()
        {
            try
            {
                UnloadMod();
                ModBase?.WorldExit();
            }
            catch(Exception e)
            {
                Log.Error(e);
                throw new Exception("Error in mod unloading, see above exceptions.");
            }

            Log.Close();
        }

        public override void HandleInput()
        {
            ModBase?.UpdateInput();
        }

        public override void UpdateBeforeSimulation()
        {
            ModBase?.UpdateBeforeSim();
        }

        public override void UpdateAfterSimulation()
        {
            ModBase?.UpdateAfterSim();
        }

        public override void Draw()
        {
            ModBase?.UpdateDraw();
        }

        public override MyObjectBuilder_SessionComponent GetObjectBuilder()
        {
            ModBase?.WorldSave();
            return base.GetObjectBuilder();
        }

        public override void UpdatingStopped()
        {
            ModBase?.UpdateStopped();
        }
    }
}