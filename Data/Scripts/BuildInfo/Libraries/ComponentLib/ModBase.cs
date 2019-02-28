using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;

namespace Digi.ComponentLib
{
    public abstract class ModBase
    {
        public int Tick;

        public readonly List<IComponent> Components = new List<IComponent>();
        public readonly List<IComponent> ComponentRefreshFlags = new List<IComponent>();
        public readonly List<IComponent> ComponentUpdateInput = new List<IComponent>();
        public readonly List<IComponent> ComponentUpdateBeforeSim = new List<IComponent>();
        public readonly List<IComponent> ComponentUpdateAfterSim = new List<IComponent>();
        public readonly List<IComponent> ComponentUpdateDraw = new List<IComponent>();

        public virtual void WorldLoading()
        {
        }

        public virtual void WorldStart()
        {
            for(int i = 0; i < Components.Count; ++i)
            {
                Components[i].RegisterComponent();
            }
        }

        public virtual void WorldExit()
        {
            try
            {
                for(int i = 0; i < Components.Count; ++i)
                {
                    Components[i].UnregisterComponent();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public virtual void UpdateInput()
        {
            try
            {
                bool paused = MyParticlesManager.Paused;

                if(!paused)
                {
                    // global ticker; placed here because before or after sim are optional
                    unchecked
                    {
                        ++Tick;
                    }
                }

                if(ComponentRefreshFlags.Count > 0)
                {
                    foreach(var comp in ComponentRefreshFlags)
                    {
                        comp.RefreshFlags();
                    }

                    ComponentRefreshFlags.Clear();
                }

                int comps = ComponentUpdateInput.Count;

                if(comps > 0)
                {
                    bool inMenu = MyAPIGateway.Gui.IsCursorVisible || MyAPIGateway.Gui.ChatEntryVisible;
                    bool anyKeyOrMouse = MyAPIGateway.Input.IsAnyKeyPress() || MyAPIGateway.Input.IsAnyMousePressed();

                    for(int i = 0; i < comps; ++i)
                    {
                        ComponentUpdateInput[i].UpdateInput(anyKeyOrMouse, inMenu, paused);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public virtual void UpdateBeforeSim()
        {
            try
            {
                for(int i = 0; i < ComponentUpdateBeforeSim.Count; ++i)
                {
                    ComponentUpdateBeforeSim[i].UpdateBeforeSim(Tick);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public virtual void UpdateAfterSim()
        {
            try
            {
                for(int i = 0; i < ComponentUpdateAfterSim.Count; ++i)
                {
                    ComponentUpdateAfterSim[i].UpdateAfterSim(Tick);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public virtual void UpdateDraw()
        {
            try
            {
                for(int i = 0; i < ComponentUpdateDraw.Count; ++i)
                {
                    ComponentUpdateDraw[i].UpdateDraw();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
