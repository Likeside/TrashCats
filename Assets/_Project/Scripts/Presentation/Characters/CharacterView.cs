using DG.Tweening;
using TrashCats.Domain;
using TrashCats.Presentation.Animation;
using TrashCats.Presentation.UI;
using UnityEngine;

namespace TrashCats.Presentation.Characters
{
    /// <summary>
    /// A fighter in the arena: flipbook art when available, otherwise a coloured placeholder animated with tweens.
    /// Root is mirrored for enemies so attached parts (tail) mirror too.
    /// </summary>
    public sealed class CharacterView
    {
        private readonly CharacterArt _art;
        private readonly SpriteFlipbook _body;
        private readonly SpriteRenderer _renderer;
        private readonly Transform _visual;
        private readonly bool _enemy;
        private readonly Color _baseColor;

        public Transform Root { get; }

        public CharacterView(Transform root, Transform visual, SpriteRenderer renderer, SpriteFlipbook body, CharacterArt art, bool enemy)
        {
            Root = root;
            _visual = visual;
            _renderer = renderer;
            _body = body;
            _art = art;
            _enemy = enemy;
            _baseColor = renderer.color;
            PlayIdle();
        }

        private float Facing => _enemy ? -1f : 1f;

        public void PlayIdle()
        {
            if (_art != null && _art.HasBattleArt) _body.Play(_art.idle);
        }

        /// <summary>Seconds (at ×1) from the start of the attack to the frame where the blow lands (drawn frame 4).</summary>
        public float StrikeDelay(SkillDefinition skill)
        {
            var clip = skill.IsHeavyAttack ? _art?.heavyAttack : _art?.attack;
            return clip != null && clip.keys != null && clip.keys.Length > 3 ? clip.KeyTime(3) : 0.3f;
        }

        public void PlayAttack(SkillDefinition skill, int speed)
        {
            var clip = skill.IsHeavyAttack ? _art?.heavyAttack : _art?.attack;
            if (clip != null && clip.frames != null && clip.frames.Length > 0)
            {
                _body.Play(clip);
                DOVirtual.DelayedCall(clip.Duration / speed, PlayIdle).SetLink(Root.gameObject);
            }

            if (skill.Effect == SkillEffectKind.Damage)
                _visual.DOPunchPosition(new Vector3(0.9f * Facing, 0, 0), 0.3f / speed, 2, 0.2f).SetLink(Root.gameObject);
            else
                _visual.DOPunchScale(new Vector3(0.15f, 0.15f, 0), 0.3f / speed, 4).SetLink(Root.gameObject);
        }

        public void PlayHit(int speed)
        {
            if (_art?.hit?.frames != null && _art.hit.frames.Length > 0)
            {
                _body.Play(_art.hit);
                DOVirtual.DelayedCall(_art.hit.Duration / speed, PlayIdle).SetLink(Root.gameObject);
            }

            _visual.DOShakePosition(0.2f / speed, new Vector3(0.25f, 0.1f, 0), 20).SetLink(Root.gameObject);
            _renderer.DOKill();
            _renderer.color = _baseColor;
            _renderer.DOColor(Color.Lerp(_baseColor, new Color(1f, 0.3f, 0.3f), 0.6f), 0.06f / speed).SetLoops(2, LoopType.Yoyo).SetLink(Root.gameObject);
        }

        public void PlayDodge(bool leftSide, int speed) =>
            _visual.DOPunchPosition(new Vector3(leftSide ? -0.7f : 0.7f, 0.3f, 0), 0.3f / speed, 1, 0.5f).SetLink(Root.gameObject);

        public void PlayKnockOut(bool leftSide, int speed)
        {
            if (_art?.knockOut?.frames != null && _art.knockOut.frames.Length > 0)
            {
                var ko = _art.knockOut;
                ko.loop = false;
                _body.Play(ko);
            }
            else
            {
                _visual.DORotate(new Vector3(0, 0, leftSide ? 80 : -80), 0.4f / speed).SetLink(Root.gameObject);
            }

            _renderer.DOFade(0.35f, 0.6f / speed).SetLink(Root.gameObject);
        }

        public void Destroy()
        {
            if (Root != null)
            {
                Root.DOKill(true);
                Object.Destroy(Root.gameObject);
            }
        }
    }
}
