using TrashCats.Presentation.Animation;
using TrashCats.Presentation.Hideout;
using TrashCats.Presentation.UI;
using UnityEngine;

namespace TrashCats.Presentation.Characters
{
    /// <summary>Creates arena fighters and backgrounds from the art registry, with graybox fallbacks.</summary>
    public sealed class CharacterViewFactory
    {
        private readonly CharacterArtRegistry _registry;
        private readonly Sprite _square;
        private readonly Sprite _arenaBackground;

        public CharacterViewFactory(CharacterArtRegistry registry, Sprite arenaBackground)
        {
            _registry = registry;
            _arenaBackground = arenaBackground;
            var texture = Texture2D.whiteTexture;
            _square = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0f), texture.width);
        }

        public CharacterArt Find(string viewKey) => _registry != null ? _registry.Find(viewKey) : null;

        public Sprite Portrait(string viewKey) => Find(viewKey)?.portrait;

        public Sprite Card(string viewKey) => Find(viewKey)?.card;

        public Sprite GetSprite(string key) => _registry != null ? _registry.GetSprite(key) : null;

        private SpriteRenderer _arenaRenderer;
        private GameObject _arenaFallback;

        /// <summary>Shows the arena picture for a territory, or the graybox floor when there is none.</summary>
        public void SetArena(string territoryViewKey)
        {
            var sprite = GetSprite("arena_" + territoryViewKey) ?? _arenaBackground;
            _arenaRenderer.sprite = sprite;
            _arenaRenderer.color = Color.white;
            _arenaRenderer.enabled = sprite != null;
            _arenaFallback.SetActive(sprite == null);
            if (sprite == null) return;
            var size = sprite.bounds.size;
            var scale = Mathf.Max(12f / size.x, 19.4f / size.y);
            _arenaRenderer.transform.localScale = Vector3.one * scale;
            // pictures have their pivot at the bottom centre: align the bottom of the picture with the bottom of the screen
            _arenaRenderer.transform.localPosition = new Vector3(0, -9.7f - sprite.bounds.min.y * scale, 0);
        }

        public void BuildArenaBackground(Transform arena)
        {
            var art = new GameObject("ArenaArt").AddComponent<SpriteRenderer>();
            art.transform.SetParent(arena, false);
            art.sortingOrder = -101;
            _arenaRenderer = art;
            _arenaFallback = new GameObject("Fallback");
            _arenaFallback.transform.SetParent(arena, false);
            var back = new GameObject("Background").AddComponent<SpriteRenderer>();
            back.transform.SetParent(_arenaFallback.transform, false);
            back.sortingOrder = -100;
            {
                back.sprite = _square;
                back.color = Palette.Hex("#2B2A2E");
                back.transform.localScale = new Vector3(12f, 20f, 1f);
                back.transform.localPosition = new Vector3(0, -10f, 0);
                var ground = new GameObject("Ground").AddComponent<SpriteRenderer>();
                ground.transform.SetParent(_arenaFallback.transform, false);
                ground.sprite = _square;
                ground.color = Palette.Hex("#13171E");
                ground.sortingOrder = -99;
                ground.transform.localScale = new Vector3(12f, 7f, 1f);
                ground.transform.localPosition = new Vector3(0, -10.2f, 0);
            }
        }

        public CharacterView CreateFighter(Transform parent, string viewKey, bool enemy)
        {
            var root = new GameObject("Fighter_" + viewKey).transform;
            root.SetParent(parent, false);
            root.localScale = new Vector3(enemy ? -1f : 1f, 1f, 1f);

            var art = Find(viewKey);
            var hasArt = art != null && art.HasBattleArt;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);
            // scale first: SpriteFlipbook captures its base scale (for breathing) when it is added
            visual.localScale = hasArt ? Vector3.one * art.battleScale : new Vector3(2.2f, 3.6f, 1f);
            var renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            var flipbook = visual.gameObject.AddComponent<SpriteFlipbook>();

            if (hasArt)
            {
                renderer.sprite = art.idle.frames[0];
                renderer.color = art.tint;
                if (art.tail?.frames != null && art.tail.frames.Length > 0)
                {
                    var tail = new GameObject("Tail");
                    tail.transform.SetParent(visual, false);
                    tail.AddComponent<SpriteRenderer>().sortingOrder = -1;
                    var tailFlipbook = tail.AddComponent<SpriteFlipbook>();
                    tail.AddComponent<FlipbookAttachment>().Bind(flipbook);
                    tailFlipbook.Play(art.tail);
                }
            }
            else
            {
                renderer.sprite = _square;
                renderer.color = enemy ? Palette.Hex("#7A4A4A") : CatColors.For(new Domain.CatId(viewKey.StartsWith("cat_") ? viewKey : "cat_" + viewKey));
            }

            return new CharacterView(root, visual, renderer, flipbook, art, enemy);
        }
    }
}
