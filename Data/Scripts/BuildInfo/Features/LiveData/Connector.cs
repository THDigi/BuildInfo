﻿using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.Definitions;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_Connector : BData_Base
    {
        /// <summary>
        /// True = connector, False = ejector
        /// </summary>
        public bool IsConnector = false;
        public bool IsSmallConnector = false;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();
            block.Model.GetDummies(dummies);

            foreach(string name in dummies.Keys)
            {
                // from MyShipConnector.LoadDummies()
                bool isConnector = name.ContainsIgnoreCase(Hardcoded.Connector_Connect_DummyName);
                bool isEjector = name.ContainsIgnoreCase(Hardcoded.Connector_Ejector_DummyName);
                if(isConnector || isEjector)
                {
                    if(isConnector)
                    {
                        IsSmallConnector = name.ContainsIgnoreCase(Hardcoded.Connector_SmallPort_DummyName);
                    }

                    IsConnector = isConnector;
                    break;
                }
            }

            dummies.Clear();
            return base.IsValid(block, def) || true;
        }
    }
}
