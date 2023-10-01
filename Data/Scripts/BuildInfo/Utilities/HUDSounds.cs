using Sandbox.Game.Entities;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features
{
    public class HUDSounds : ModComponent
    {
        const float VolumeMultiplier = 0.3f; // HACK: earlobe'd it match the game's HUD sounds

        MyEntity3DSoundEmitter SoundEmitter;
        int SoundTimeout = 0;

        readonly MySoundPair SoundClick = new MySoundPair("HudClick");
        //readonly MySoundPair SoundUnable = new MySoundPair("HudUnable");
        //readonly MySoundPair SoundMouseClick = new MySoundPair("HudMouseClick");
        //readonly MySoundPair SoundColor = new MySoundPair("HudColorBlock");
        //readonly MySoundPair SoundItem = new MySoundPair("HudItem");

        public HUDSounds(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            SoundEmitter?.Cleanup();
            SoundEmitter = null;
        }

        public void PlayClick()
        {
            PlayHudSound(SoundClick, 0.25f);
        }

        void PlayHudSound(MySoundPair soundPair, float volume, int timeout = 0)
        {
            if(timeout > 0)
            {
                if(SoundTimeout > Main.Tick)
                    return;

                SoundTimeout = Main.Tick + timeout;
            }

            if(SoundEmitter == null)
            {
                SoundEmitter = new MyEntity3DSoundEmitter(null);

                // remove all effects and conditions from this emitter; must not clear the dictionary itself!
                SoundEmitter.EmitterMethods[(int)MyEntity3DSoundEmitter.MethodsEnum.CanHear].ClearImmediate();
                SoundEmitter.EmitterMethods[(int)MyEntity3DSoundEmitter.MethodsEnum.ShouldPlay2D].ClearImmediate();
                SoundEmitter.EmitterMethods[(int)MyEntity3DSoundEmitter.MethodsEnum.CueType].ClearImmediate();
                SoundEmitter.EmitterMethods[(int)MyEntity3DSoundEmitter.MethodsEnum.ImplicitEffect].ClearImmediate();
            }

            SoundEmitter.SetPosition(MyAPIGateway.Session.Camera.WorldMatrix.Translation);
            SoundEmitter.CustomVolume = volume * VolumeMultiplier;
            SoundEmitter.PlaySound(soundPair, stopPrevious: false, alwaysHearOnRealistic: true, force2D: true);
        }
    }
}