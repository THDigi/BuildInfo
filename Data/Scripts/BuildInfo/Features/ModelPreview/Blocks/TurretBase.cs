﻿using Digi.BuildInfo.Features.LiveData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game.Entity;
using VRageMath;

namespace Digi.BuildInfo.Features.ModelPreview.Blocks
{
    public class TurretBase : MultiSubpartBase
    {
        protected override bool Initialized()
        {
            if(!base.Initialized())
                return false;

            MyLargeTurretBaseDefinition turretDef = (MyLargeTurretBaseDefinition)BlockDef;

            foreach(PreviewEntityWrapper part in Parts)
            {
                // HACK: hardcoded turret subpart pairing

                if(BlockDef.Id.TypeId == typeof(MyObjectBuilder_InteriorTurret) && part.Source == "subpart_InteriorTurretBase1")
                {
                    Fix(part, "InteriorTurretBase2");
                    break;
                }

                if(BlockDef.Id.TypeId == typeof(MyObjectBuilder_LargeMissileTurret) && part.Source == "subpart_MissileTurretBase1")
                {
                    Fix(part, "MissileTurretBarrels");
                    break;
                }

                if(BlockDef.Id.TypeId == typeof(MyObjectBuilder_LargeGatlingTurret))
                {
                    if(turretDef?.SubpartPairing != null)
                    {
                        string partSourceNoPrefix = part.Source.Substring("subpart_".Length);

                        string base1name;
                        if(turretDef.SubpartPairing.Dictionary.TryGetValue("Base1", out base1name))
                        {
                            bool gotPart1 = false;
                            foreach(string name in base1name.Split(BData_GatlingTurret.TurretSubpartSeparator))
                            {
                                if(partSourceNoPrefix == name)
                                {
                                    gotPart1 = true;
                                    break;
                                }
                            }

                            string base2name;
                            if(gotPart1 && turretDef.SubpartPairing.Dictionary.TryGetValue("Base2", out base2name))
                            {
                                foreach(string name in base2name.Split(BData_GatlingTurret.TurretSubpartSeparator))
                                {
                                    MyEntitySubpart subpart;
                                    if(part.Entity.TryGetSubpart(name, out subpart))
                                    {
                                        Fix(part, name);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    else if(part.Source == "subpart_GatlingTurretBase1")
                    {
                        Fix(part, "GatlingTurretBase2");
                    }
                    break;
                }
            }

            return true;
        }

        void Fix(PreviewEntityWrapper part, string subpartId)
        {
            if(!part.LocalMatrix.HasValue)
                return;

            // part seems already aligned well, let's not touch it
            if(Vector3.Dot(part.LocalMatrix.Value.Forward, Vector3.Forward) >= 0.99f
            && Vector3.Dot(part.LocalMatrix.Value.Right, Vector3.Right) >= 0.99f)
                return;

            part.LocalMatrix = Matrix.CreateTranslation(part.LocalMatrix.Value.Translation);

            // allow the layer-1 model to be seen duplicated because we're fixing its orientation here
            part.BaseModelVisible = true;

            MyEntitySubpart subpart;
            if(part.Entity.TryGetSubpart(subpartId, out subpart))
            {
                BData_Turret.UnrotateSubparts(Matrix.Identity, part.Entity, subpart);
            }
        }
    }
}