using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X64;
using Reloaded.Memory.Sources;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace p3ppc.multipleweapons
{
    internal unsafe class FieldAttackMotion5Fix
    {
        private const int PlayerPadProcRva = 0x226310;

        private const int TaskArgsOffset = 0x48;
        private const int PlayerModelOffset = 0x60;
        private const int ReturnToIdleFlagOffset = 0x78;

        // model handle:
        //   +0x00 -> underlying GMO/model object
        //
        // underlying object:
        //   +0x2A0 -> motion pointer table
        //   +0x2B8 -> motion count
        //   +0x2BC -> current motion id
        private const int MotionTableOffset = 0x2A0;
        private const int MotionCountOffset = 0x2B8;
        private const int CurrentMotionOffset = 0x2BC;

        // motion entry:
        //   +0x48 -> FrameLoop start
        //   +0x4C -> FrameLoop end
        //   +0x54 -> end-frame mode used by vanilla completion code
        //   +0x58 -> current frame
        private const int MotionStartOffset = 0x48;
        private const int MotionEndOffset = 0x4C;
        private const int MotionEndModeOffset = 0x54;
        private const int MotionCurrentFrameOffset = 0x58;

        private const int NormalPlayerState = 1;
        private const int FieldAttackMotion = 5;

        private IHook<PlayerPadProcDelegate> _playerPadProcHook = null!;

        private int _lastLoggedFrame = -9999;
        private bool _loggedMotionStart;

        internal void Hook(IReloadedHooks hooks, IMemory memory)
        {
            nint playerPadProc = Utils.BaseAddress + PlayerPadProcRva;

            _playerPadProcHook =
                hooks.CreateHook<PlayerPadProcDelegate>(
                    OnPlayerPadProc,
                    playerPadProc)
                .Activate();

            Utils.LogDebug(
                $"FieldAttackMotion5Fix: hooked player pad proc at " +
                $"0x{(nuint)playerPadProc:X}");
        }

        private void OnPlayerPadProc(
            nint task,
            nint param2,
            nint param3)
        {
            if (task != 0)
            {
                nint state = *(nint*)(task + TaskArgsOffset);

                if (state != 0 &&
                    *(int*)state == NormalPlayerState)
                {
                    nint model = *(nint*)(state + PlayerModelOffset);

                    if (model != 0)
                    {
                        nint modelImpl = *(nint*)model;

                        if (modelImpl != 0)
                        {
                            int rawMotion =
                                *(int*)(modelImpl + CurrentMotionOffset);

                            int motion = rawMotion & 0xFFF;

                            if (motion == FieldAttackMotion)
                            {
                                int motionCount =
                                    *(int*)(modelImpl + MotionCountOffset);

                                nint motionTable =
                                    *(nint*)(modelImpl + MotionTableOffset);

                                if (motionTable != 0 &&
                                    motion >= 0 &&
                                    motion < motionCount)
                                {
                                    nint motionEntry =
                                        *(nint*)(motionTable + motion * 8);

                                    if (motionEntry != 0)
                                    {
                                        float start =
                                            *(float*)(motionEntry + MotionStartOffset);

                                        float end =
                                            *(float*)(motionEntry + MotionEndOffset);

                                        float frame =
                                            *(float*)(motionEntry + MotionCurrentFrameOffset);

                                        int endMode =
                                            *(int*)(motionEntry + MotionEndModeOffset);

                                        float duration = end - start;

                                        // Mirror FUN_1403AB2C0:
                                        // it adds one frame for endMode == 1
                                        // before comparing against duration.
                                        float completionFrame =
                                            frame + (endMode == 1 ? 1.0f : 0.0f);

                                        if (!_loggedMotionStart)
                                        {
                                            Utils.LogDebug(
                                                $"FieldAttackMotion5Fix: motion 5 active; " +
                                                $"frame={frame:F2}, duration={duration:F2}, " +
                                                $"start={start:F2}, end={end:F2}, mode={endMode}");

                                            _loggedMotionStart = true;
                                        }

                                        int wholeFrame = (int)frame;
                                        if (wholeFrame != _lastLoggedFrame)
                                        {
                                            _lastLoggedFrame = wholeFrame;

                                            Utils.LogDebug(
                                                $"FieldAttackMotion5Fix: motion 5 " +
                                                $"frame {frame:F2}/{duration:F2}");
                                        }

                                        if (duration > 0.0f &&
                                            completionFrame >= duration - 0.001f)
                                        {
                                            // This is the exact flag that vanilla
                                            // player-pad code consumes to switch
                                            // back to the normal idle/run motion.
                                            *(int*)(state + ReturnToIdleFlagOffset) = 1;

                                            Utils.LogDebug(
                                                $"FieldAttackMotion5Fix: motion 5 reached " +
                                                $"its real GMO end ({frame:F2}/{duration:F2}); " +
                                                $"forcing vanilla return-to-idle path.");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                _loggedMotionStart = false;
                                _lastLoggedFrame = -9999;
                            }
                        }
                    }
                }
            }

            _playerPadProcHook.OriginalFunction(
                task,
                param2,
                param3);
        }

        [Function(CallingConventions.Microsoft)]
        private delegate void PlayerPadProcDelegate(
            nint task,
            nint param2,
            nint param3);
    }
}
