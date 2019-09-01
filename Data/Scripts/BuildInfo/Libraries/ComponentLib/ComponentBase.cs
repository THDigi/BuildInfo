using System.Collections.Generic;

namespace Digi.ComponentLib
{
    public abstract class ComponentBase<T> : IComponent where T : ModBase
    {
        public readonly T Mod;

        private UpdateFlags _backingFlags = UpdateFlags.NONE;
        private UpdateFlags _setNewFlags = UpdateFlags.INVALID;
        public UpdateFlags Flags
        {
            get { return _backingFlags; }
            set
            {
                if(_setNewFlags == value)
                    return;

                if(!Mod.ComponentRefreshFlags.Contains(this))
                    Mod.ComponentRefreshFlags.Add(this);

                _setNewFlags = value;
            }
        }

        public void SetFlag(UpdateFlags flag, bool set)
        {
            if(set)
                Flags |= flag;
            else
                Flags &= ~flag;
        }

        public ComponentBase(T mod)
        {
            Mod = mod;
            mod.Components.Add(this);
        }

        public virtual void RegisterComponent()
        {
        }

        public virtual void UnregisterComponent()
        {
        }

        public virtual void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
        }

        public virtual void UpdateBeforeSim(int tick)
        {
        }

        public virtual void UpdateAfterSim(int tick)
        {
        }

        public virtual void UpdateDraw()
        {
        }

        public void RefreshFlags()
        {
            UpdateList(Mod.ComponentUpdateInput, _setNewFlags, UpdateFlags.UPDATE_INPUT);
            //UpdateList(Mod.ComponentUpdateBeforeSim, _setNewFlags, UpdateFlags.UPDATE_BEFORE_SIM);
            UpdateList(Mod.ComponentUpdateAfterSim, _setNewFlags, UpdateFlags.UPDATE_AFTER_SIM);
            UpdateList(Mod.ComponentUpdateDraw, _setNewFlags, UpdateFlags.UPDATE_DRAW);
            _backingFlags = _setNewFlags;
            _setNewFlags = UpdateFlags.INVALID;
        }

        private void UpdateList(List<IComponent> list, UpdateFlags setFlags, UpdateFlags checkFlag)
        {
            if(_backingFlags.HasFlag(checkFlag) && !setFlags.HasFlag(checkFlag))
            {
                list.Remove(this);
            }
            else if(!_backingFlags.HasFlag(checkFlag) && setFlags.HasFlag(checkFlag))
            {
                list.Add(this);
            }
        }
    }
}
