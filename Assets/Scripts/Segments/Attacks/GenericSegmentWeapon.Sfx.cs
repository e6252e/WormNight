using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class GenericSegmentWeapon
    {
        private void PlayFireSfx(Transform muzzle)
        {
            Transform root = muzzle != null ? muzzle : transform;
            if (AttackProfile != null && AttackProfile.MoveType == SegmentAttackMoveType.ExpandingFlameSphere)
            {
                if (GameplaySfxEmitter.TryPlay(root, GameplaySfxCue.FireStart))
                {
                    return;
                }
            }

            GameplaySfxEmitter.TryPlay(root, GameplaySfxCue.Fire);
        }

        private void PlayFireLoopSfx(Transform muzzle)
        {
            if (AttackProfile == null || AttackProfile.MoveType != SegmentAttackMoveType.ExpandingFlameSphere)
            {
                return;
            }

            Transform root = muzzle != null ? muzzle : transform;
            GameplaySfxEmitter.TryStartLoop(root, GameplaySfxCue.FireLoop);
        }

        private void StopFireLoopSfx()
        {
            GameplaySfxEmitter.TryStopLoop(transform, GameplaySfxCue.FireLoop);
        }

        private void PlayTrebuchetFireStartSfx(Transform muzzle)
        {
            Transform root = muzzle != null ? muzzle : transform;
            if (GameplaySfxEmitter.TryPlay(root, GameplaySfxCue.FireStart))
            {
                return;
            }

            GameplaySfxEmitter.TryPlay(root, GameplaySfxCue.Fire);
        }
    }
}
