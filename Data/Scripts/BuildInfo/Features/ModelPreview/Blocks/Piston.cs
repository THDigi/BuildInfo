using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.VanillaData;
using VRage.Game.Entity;
using VRageMath;

namespace Digi.BuildInfo.Features.ModelPreview.Blocks
{
    public class Piston : MultiSubpartBase
    {
        static readonly float? TopPartTransparency = Hardcoded.CubeBuilderTransparency * 2f;

        bool Valid;
        PreviewEntityWrapper TopPart;
        BData_Piston Data;

        protected override bool Initialized()
        {
            bool baseReturn = base.Initialized();
            Valid = false;

            Data = Main.LiveDataHandler.Get<BData_Piston>(BlockDef);
            if(Data == null || Data.PistonDef == null || Data.TopDef == null)
                return baseReturn;

            TopPart = new PreviewEntityWrapper(Data.TopDef.Model, null);
            Valid = (TopPart != null);
            return baseReturn || Valid;
        }

        protected override void Disposed()
        {
            base.Disposed();

            TopPart?.Close();
            TopPart = null;

            Data = null;
        }

        public override void Update(ref MatrixD drawMatrix)
        {
            base.Update(ref drawMatrix);

            if(!Valid)
                return;

            MatrixD topMatrix = Data.TopLocalMatrix * drawMatrix;

            TopPart.Update(ref topMatrix, TopPartTransparency);

            //ConstructionStack?.SetLocalMatrix(Data.TopLocalMatrix);
        }

        public override void SpawnConstructionModel(ConstructionModelPreview comp)
        {
            if(Valid)
            {
                // HACK: fixing orientation of piston subparts in main construction model
                // from MyPistonBase.UpdateAnimation() and LoadSubparts()
                #region
                if(comp.Stacks.Count > 0)
                {
                    float currentPos = 0;
                    float range = Data.PistonDef.Maximum - Data.PistonDef.Minimum;
                    float rangeDiv3 = range / 3f;

                    foreach(PreviewEntityWrapper model in comp.Stacks[0].Models)
                    {
                        MyEntitySubpart subpart1 = model?.Entity?.Subparts?.GetValueOrDefault("PistonSubpart1");
                        MyEntitySubpart subpart2 = subpart1?.Subparts?.GetValueOrDefault("PistonSubpart2");
                        MyEntitySubpart subpart3 = subpart2?.Subparts?.GetValueOrDefault("PistonSubpart3");

                        if(subpart1 != null)
                        {
                            float offset = MathHelper.Clamp(currentPos - 2f * rangeDiv3, 0, rangeDiv3);
                            Vector3 pos = subpart1.PositionComp.LocalMatrixRef.Translation;
                            Matrix m = Matrix.CreateTranslation(pos + Vector3.Up * offset);
                            subpart1.PositionComp.SetLocalMatrix(ref m);
                        }

                        if(subpart2 != null)
                        {
                            float offset = MathHelper.Clamp(currentPos - rangeDiv3, 0f, rangeDiv3);
                            Vector3 pos = subpart2.PositionComp.LocalMatrixRef.Translation;
                            Matrix m = Matrix.CreateTranslation(pos + Vector3.Up * offset);
                            subpart2.PositionComp.SetLocalMatrix(ref m);
                        }

                        if(subpart3 != null)
                        {
                            float offset = MathHelper.Clamp(currentPos, 0f, rangeDiv3);
                            Vector3 pos = subpart3.PositionComp.LocalMatrixRef.Translation;
                            Matrix m = Matrix.CreateTranslation(pos + Vector3.Up * offset);
                            subpart3.PositionComp.SetLocalMatrix(ref m);
                        }
                    }
                }
                #endregion

                Matrix lm = Data.TopLocalMatrix;
                lm.Translation -= BlockDef.ModelOffset; // needs to be relative to BB center
                ConstructionStack = ConstructionModelStack.CreateAndAdd(comp.Stacks, Data.TopDef, lm, TopPartTransparency);
            }
        }
    }
}
