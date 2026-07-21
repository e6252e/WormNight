using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class SegmentProjectileRuntime
    {
        private void PlayProjectileHitSfx(Vector3 position)
        {
            GameplaySfxEmitter.TryPlayAt(transform, GameplaySfxCue.Hit, position, true);
        }

        private void PlayProjectileExplosionSfx(Vector3 position)
        {
            GameplaySfxEmitter.TryPlayAt(transform, GameplaySfxCue.Explosion, position, true);
        }

        private void PlayProjectileRollHitSfx(Vector3 position)
        {
            GameplaySfxEmitter.TryPlayAt(transform, GameplaySfxCue.RollHit, position, true);
        }
    }
}
