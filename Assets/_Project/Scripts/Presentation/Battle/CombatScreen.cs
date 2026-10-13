using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TrashCats.App;
using TrashCats.Domain;
using TrashCats.Presentation.Characters;
using TrashCats.Presentation.Hideout;
using TrashCats.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrashCats.Presentation.Battle
{
    /// <summary>
    /// Replays the pre-simulated battle log (Docs/GDD/COMBAT_DESIGN.md §11): fighters in a world-space arena,
    /// HP/Grit bars, queues, damage numbers and skill banners in the UI. The log is never waited on by the simulation.
    /// </summary>
    public sealed class CombatScreen
    {
        private const float SecondsPerTick = 0.1f;

        private readonly GameSession _session;
        private readonly CharacterViewFactory _characters;
        private readonly ResultsModal _results;
        private readonly Transform _arena;
        private readonly TMP_Text _title;
        private readonly TMP_Text _banner;
        private readonly RectTransform _bannerRoot;
        private readonly CanvasGroup _rage;
        private readonly CanvasGroup _bannerGroup;
        private readonly RectTransform[] _queues = new RectTransform[2];
        private readonly List<(string Id, RoundPicture Picture)>[] _queuePictures = { new List<(string, RoundPicture)>(), new List<(string, RoundPicture)>() };
        private readonly FighterHud[] _huds = new FighterHud[2];
        private readonly CharacterView[] _views = new CharacterView[2];
        private readonly UiButton _speed;
        private readonly UiButton _skip;
        private readonly RectTransform _uiRoot;

        private PendingBattle _battle;
        private IReadOnlyList<BattleEvent> _log;
        private int _next;
        private float _time;
        private float _endDelay = -1f;
        private int _speedFactor = 1;

        public GameObject Root { get; }

        private static readonly Vector3[] Spots = { new Vector3(-2.4f, -3.2f, 0), new Vector3(2.4f, -3.2f, 0) };

        public CombatScreen(GameSession session, RectTransform parent, Transform arenaParent, CharacterViewFactory characters, ResultsModal results)
        {
            _session = session;
            _characters = characters;
            _results = results;

            _arena = new GameObject("Arena").transform;
            _arena.SetParent(arenaParent, false);
            _characters.BuildArenaBackground(_arena);
            _arena.gameObject.SetActive(false);

            var root = Ui.Rect(parent, "CombatScreen").Fill();
            _uiRoot = root;
            Root = root.gameObject;

            _title = Ui.Label(root, "", 34, TextAnchor.MiddleCenter, Palette.Paper);
            _title.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -64), new Vector2(0, -8));
            _title.Outlined();
            for (var side = 0; side < 2; side++)
            {
                // fighter queue as round portraits (combat concept): ours on the left, theirs on the right
                _queues[side] = Ui.Rect(root, side == 0 ? "PlayerQueue" : "EnemyQueue")
                    .Place(new Vector2(side * 0.55f, 1), new Vector2(side * 0.55f + 0.45f, 1), new Vector2(20, -210), new Vector2(-20, -70));
                _huds[side] = new FighterHud(root, side);
            }

            // red halftone vignette while "Ярость!" lasts
            var rage = Ui.Panel(root, "Rage", Ui.Skin?.rageVignette != null ? Color.white : new Color(0.8f, 0.1f, 0.1f, 0.25f));
            rage.sprite = Ui.Skin?.rageVignette;
            rage.rectTransform.Fill();
            rage.transform.SetAsFirstSibling();
            _rage = rage.gameObject.AddComponent<CanvasGroup>();
            _rage.alpha = 0f;
            _rage.blocksRaycasts = false;

            // skill banner: a tilted yellow comic ribbon (Art Bible §6), or outlined yellow text without the skin
            _bannerRoot = Ui.Rect(root, "Banner").Place(new Vector2(0.05f, 0.71f), new Vector2(0.95f, 0.81f));
            _bannerGroup = _bannerRoot.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;
            _bannerGroup.blocksRaycasts = false;
            // a jagged comic burst behind the skill name, like "WILD BITE!" in the combat concept
            var ribbon = Ui.Skin?.burst;
            if (ribbon != null)
                Ui.Picture(_bannerRoot, "Burst", ribbon).preserveAspect = false;
            _banner = Ui.Label(_bannerRoot, "", 60, TextAnchor.MiddleCenter, ribbon != null ? Palette.Ink : Palette.Primary, heading: true);
            if (ribbon != null)
                _bannerRoot.localRotation = Quaternion.Euler(0, 0, 5f);
            else
                _banner.Outlined();

            _speed = Ui.Button(root, "Combat_Speed", "×1", Palette.Primary, ToggleSpeed, 44);
            _speed.Rect.Place(Vector2.zero, new Vector2(0.45f, 0.09f), new Vector2(30, 30), new Vector2(-15, 0));
            _skip = Ui.Button(root, "Combat_Skip", "Пропустить", Palette.Secondary, Skip, 40);
            _skip.Rect.Place(new Vector2(0.55f, 0), new Vector2(1, 0.09f), new Vector2(15, 30), new Vector2(-30, 0));
        }

        public void SetSpeed(int factor)
        {
            _speedFactor = Mathf.Clamp(factor, 1, 2);
            _speed.Text.text = $"×{_speedFactor}";
        }

        public event Action<int> SpeedChanged;

        /// <summary>Battle sounds; null plays nothing.</summary>
        public Audio.GameAudio Audio { get; set; }

        private void Sound(string key, float volume = 1f) => Audio?.Play(key, volume);

        /// <summary>Starts replaying the current pending battle. With <paramref name="resultsOnly"/> jumps straight to the results.</summary>
        public void Play(bool resultsOnly)
        {
            _battle = _session.World.PendingBattle;
            if (_battle == null)
                return;

            _arena.gameObject.SetActive(true);
            _characters.SetArena(_session.Config.GetTerritory(_battle.Territory).ViewKey);
            _log = _battle.Result.Log;
            _next = 0;
            _time = 0f;
            _endDelay = -1f;
            _banner.text = "";
            _bannerGroup.alpha = 0f;
            _title.text = Texts.TerritoryName(_battle.Territory);
            foreach (var view in _views) view?.Destroy();
            Array.Clear(_views, 0, 2);
            foreach (var hud in _huds) hud.Reset();

            var repeat = _session.World.Territories.Get(_battle.Territory).Status == TerritoryStatus.Captured
                         || _session.World.Territories.Get(_battle.Territory).Attempts > 1;
            _skip.Interactable = repeat || Debug.isDebugBuild;
            BuildQueues();
            _rage.alpha = 0f;
            _rage.DOKill();
            if (!resultsOnly) Sound("cmb_bell", 0.7f);
            if (resultsOnly)
                Skip();
            RefreshQueues();
        }

        public void Hide()
        {
            _arena.gameObject.SetActive(false);
            foreach (var view in _views) view?.Destroy();
            Array.Clear(_views, 0, 2);
            _battle = null;
        }

        public void Tick(float deltaTime)
        {
            if (_battle == null)
                return;

            if (_endDelay >= 0f)
            {
                _endDelay -= deltaTime;
                if (_endDelay < 0f)
                    _results.Show(_battle);
                return;
            }

            _time += deltaTime * _speedFactor;
            var tick = (int)(_time / SecondsPerTick);
            while (_next < _log.Count && _log[_next].Tick <= tick)
                Present(_log[_next++], animate: true);
        }

        private void Skip()
        {
            if (_battle == null || _endDelay >= 0f)
                return;
            while (_next < _log.Count)
                Present(_log[_next++], animate: false);
            _endDelay = 0.01f;
        }

        private void ToggleSpeed()
        {
            SetSpeed(_speedFactor == 1 ? 2 : 1);
            SpeedChanged?.Invoke(_speedFactor);
        }

        private void Present(BattleEvent e, bool animate)
        {
            switch (e)
            {
                case FighterEntered entered:
                {
                    var side = (int)entered.Side;
                    _views[side]?.Destroy();
                    var queue = entered.Side == Side.Player ? _battle.Setup.PlayerQueue : _battle.Setup.EnemyQueue;
                    var viewKey = queue.FirstOrDefault(f => f.Id == entered.FighterId)?.ViewKey ?? entered.FighterId;
                    var view = _characters.CreateFighter(_arena, viewKey, entered.Side == Side.Enemy);
                    view.Root.position = Spots[side];
                    if (animate)
                    {
                        view.Root.position = Spots[side] + new Vector3(side == 0 ? -6 : 6, 0, 0);
                        view.Root.DOMove(Spots[side], 0.35f / _speedFactor).SetEase(Ease.OutCubic);
                    }

                    _views[side] = view;
                    if (animate && entered.Side == Side.Player) Sound("cmb_hiss", 0.6f);
                    _huds[side].Enter(NameOf(entered.FighterId, entered.Side), entered.Hp, entered.MaxHp, entered.Grit, entered.MaxGrit);
                    RefreshQueues();
                    break;
                }
                case ActionPerformed action:
                {
                    var side = (int)action.Side;
                    _huds[side].SetGrit(action.GritAfter);
                    if (animate)
                    {
                        _views[side]?.PlayAttack(_session.Config.GetSkill(action.SkillId), _speedFactor);
                        Sound("cmb_swing");
                        if (!_session.Config.GetSkill(action.SkillId).IsInnate)
                            ShowBanner(Texts.SkillName(action.SkillId) + "!");
                    }

                    break;
                }
                case HitResolved hit:
                {
                    var target = 1 - (int)hit.AttackerSide;
                    _huds[target].SetHp(hit.TargetHpAfter);
                    if (animate)
                    {
                        var text = hit.Hit ? $"-{hit.Damage}" : "Мимо!";
                        var color = hit.Hit ? (hit.Reduced ? Palette.Disabled : Color.white) : Palette.Secondary;
                        FloatAt(Spots[target] + new Vector3(0, 4.6f + hit.HitIndex * 0.6f, 0), text, color, hit.Hit && hit.Damage >= 20 ? 64 : 52);
                        if (hit.Hit) _views[target]?.PlayHit(_speedFactor);
                        else _views[target]?.PlayDodge(target == 0, _speedFactor);
                        var heavy = hit.Hit && hit.Damage >= 20;
                        Sound(!hit.Hit ? "cmb_miss" : heavy ? "cmb_hit_heavy" : "cmb_hit");
                        if (hit.Hit)
                        {
                            // comic impact: a star on heavy hits (with a shake), claw marks on the others
                            Effect(Spots[target] + new Vector3(0, 2.4f + hit.HitIndex * 0.4f, 0), heavy ? Ui.Skin?.fxStar : Ui.Skin?.fxScratch, heavy ? 340 : 240);
                            if (heavy) Shake(0.25f);
                        }
                    }

                    break;
                }
                case StatusChanged status:
                    _huds[(int)status.Side].SetStatus(status.Status, status.Value);
                    break;
                case SuddenDeathStarted _:
                    if (animate)
                    {
                        ShowBanner("Ярость!");
                        Sound("cmb_bell");
                        _rage.DOKill();
                        _rage.alpha = 0.5f;
                        _rage.DOFade(1f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetLink(_rage.gameObject);
                    }
                    break;
                case FighterKnockedOut ko:
                {
                    var side = (int)ko.Side;
                    if (animate)
                    {
                        _views[side]?.PlayKnockOut(side == 0, _speedFactor);
                        Sound("cmb_ko");
                        Effect(Spots[side] + new Vector3(0, 0.6f, 0), Ui.Skin?.fxDust, 380);
                        Shake(0.35f);
                    }
                    _huds[side].KnockedOut();
                    break;
                }
                case BattleEnded ended:
                    ShowBanner(ended.Outcome == BattleOutcome.Victory ? "Победа!" : "Поражение");
                    Sound(ended.Outcome == BattleOutcome.Victory ? "cmb_win" : "cmb_lose");
                    _rage.DOKill();
                    _rage.DOFade(0f, 0.3f).SetLink(_rage.gameObject);
                    foreach (var view in _views)
                        view?.PlayIdle();
                    _endDelay = animate ? 1.2f : 0.01f;
                    break;
            }
        }

        private void BuildQueues()
        {
            for (var side = 0; side < 2; side++)
            {
                foreach (var (_, picture) in _queuePictures[side])
                    UnityEngine.Object.Destroy(picture.Root.gameObject);
                _queuePictures[side].Clear();
                var queue = side == 0 ? _battle.Setup.PlayerQueue : _battle.Setup.EnemyQueue;
                for (var i = 0; i < queue.Count; i++)
                {
                    var fighter = queue[i];
                    var picture = new RoundPicture(_queues[side], "Fighter_" + fighter.Id, side == 0 ? Palette.Hex("#3F7FBF") : Palette.Hex("#B5402B"), 0.75f);
                    // the next fighter stands nearest to the middle
                    var x = side == 0 ? 1f - (i + 0.5f) / 3f : (i + 0.5f) / 3f;
                    picture.Root.At(new Vector2(x, 0.5f), new Vector2(130, 130));
                    picture.SetPicture(_characters.Portrait(fighter.ViewKey));
                    _queuePictures[side].Add((fighter.Id, picture));
                }
            }
        }

        private void RefreshQueues()
        {
            if (_battle == null) return;
            var log = _log.Take(_next).ToList();
            for (var side = 0; side < 2; side++)
            {
                var knocked = new HashSet<string>(log.OfType<FighterKnockedOut>().Where(k => (int)k.Side == side).Select(k => k.FighterId));
                var current = _queuePictures[side].FirstOrDefault(p => !knocked.Contains(p.Id)).Id;
                foreach (var (id, picture) in _queuePictures[side])
                {
                    var out_ = knocked.Contains(id);
                    picture.Picture.color = out_ ? new Color(0.35f, 0.35f, 0.38f) : Color.white;
                    picture.Backdrop.color = out_ ? Palette.PanelDark : side == 0 ? Palette.Hex("#3F7FBF") : Palette.Hex("#B5402B");
                    picture.SetGold(id == current);
                    picture.Root.localScale = Vector3.one * (id == current ? 1.15f : 0.9f);
                }
            }
        }

        private void ShowBanner(string text)
        {
            _bannerGroup.DOKill();
            _bannerRoot.DOKill();
            _banner.text = text;
            _bannerGroup.alpha = 1f;
            _bannerRoot.localScale = Vector3.one * 1.3f;
            _bannerRoot.DOScale(1f, 0.2f).SetLink(_bannerRoot.gameObject);
            _bannerGroup.DOFade(0f, 0.5f).SetDelay(0.7f / _speedFactor).SetLink(_bannerRoot.gameObject);
        }

        /// <summary>A comic effect sprite popping at a world point and fading out.</summary>
        private void Effect(Vector3 world, Sprite sprite, float size)
        {
            var camera = Camera.main;
            if (sprite == null || camera == null) return;
            var image = Ui.Panel(_uiRoot, "Fx", Color.white);
            image.sprite = sprite;
            image.preserveAspect = true;
            var rt = image.rectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_uiRoot, camera.WorldToScreenPoint(world), null, out var local);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = local;
            rt.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(-20f, 20f));
            rt.localScale = Vector3.one * 0.3f;
            var duration = 0.35f / _speedFactor;
            DOTween.Sequence()
                .Append(rt.DOScale(1f, duration * 0.4f).SetEase(Ease.OutBack))
                .Append(image.DOFade(0f, duration * 0.6f))
                .OnComplete(() => UnityEngine.Object.Destroy(image.gameObject))
                .SetLink(image.gameObject);
        }

        private void Shake(float strength)
        {
            var camera = Camera.main;
            if (camera == null) return;
            camera.transform.DOComplete();
            camera.transform.DOShakePosition(0.25f / _speedFactor, strength, 18).SetLink(camera.gameObject);
        }

        private void FloatAt(Vector3 world, string text, Color color, int size)
        {
            var camera = Camera.main;
            if (camera == null) return;
            // big hits land on a comic burst (Art Bible §6: damage numbers, the heavy ones with an explosion behind)
            var burst = size >= 64 ? Ui.Skin?.burst : null;
            RectTransform rt;
            TMP_Text label;
            if (burst != null)
            {
                var back = Ui.Panel(_uiRoot, "Burst", Color.white);
                back.sprite = burst;
                rt = back.rectTransform;
                label = Ui.Label(rt, text, size, TextAnchor.MiddleCenter, Palette.Ink, heading: true);
                label.rectTransform.Fill();
                back.transform.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(-10f, 10f));
            }
            else
            {
                label = Ui.Label(_uiRoot, text, size, TextAnchor.MiddleCenter, color);
                label.Outlined();
                rt = label.rectTransform;
            }
            var canvas = _uiRoot.GetComponentInParent<Canvas>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_uiRoot, camera.WorldToScreenPoint(world),
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : camera, out var local);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = burst != null ? new Vector2(250, 160) : new Vector2(300, 90);
            rt.anchoredPosition = local;
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            if (burst != null)
            {
                rt.localScale = Vector3.one * 0.4f;
                rt.DOScale(1f, 0.15f / _speedFactor).SetEase(Ease.OutBack).SetLink(rt.gameObject);
            }
            DOTween.Sequence()
                .Append(rt.DOAnchorPosY(local.y + 110, 0.8f / _speedFactor))
                .Join(group.DOFade(0f, 0.8f / _speedFactor).SetEase(Ease.InQuad))
                .OnComplete(() => UnityEngine.Object.Destroy(rt.gameObject))
                .SetLink(rt.gameObject);
        }

        private static string NameOf(string id, Side side) => side == Side.Player ? Texts.CatName(new CatId(id)) : Texts.EnemyName(id);

        /// <summary>Name, HP and Grit bars and status line for one side.</summary>
        private sealed class FighterHud
        {
            private readonly TMP_Text _name;
            private readonly UiBar _hp;
            private readonly UiBar _grit;
            private readonly TMP_Text _status;
            private readonly Dictionary<CombatStatus, int> _statuses = new Dictionary<CombatStatus, int>();
            private int _maxHp = 1, _maxGrit = 1;

            public FighterHud(RectTransform root, int side)
            {
                var x0 = side == 0 ? 0.04f : 0.54f;
                var box = Ui.Rect(root, side == 0 ? "PlayerHud" : "EnemyHud").Place(new Vector2(x0, 0.12f), new Vector2(x0 + 0.42f, 0.27f));
                _name = Ui.Label(box, "", 34, TextAnchor.UpperCenter, Palette.Paper);
                _name.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -50), Vector2.zero);
                _name.Outlined();
                _hp = Ui.Bar(box, "Hp", Palette.Hex("#D9402B"), "hp");
                _hp.Rect.Place(new Vector2(0, 1), Vector2.one, new Vector2(46, -100), new Vector2(0, -62));
                _grit = Ui.Bar(box, "Grit", Palette.Hex("#3F7FBF"), "grit");
                _grit.Rect.Place(new Vector2(0, 1), Vector2.one, new Vector2(46, -144), new Vector2(0, -110));
                _status = Ui.Label(box, "", 26, TextAnchor.UpperCenter, Palette.Primary);
                _status.Outlined();
                _status.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -200), new Vector2(0, -160));
            }

            public void Reset()
            {
                _name.text = "";
                _statuses.Clear();
                _status.text = "";
                _hp.Set(0, "");
                _grit.Set(0, "");
            }

            public void Enter(string name, int hp, int maxHp, int grit, int maxGrit)
            {
                _name.text = name;
                _maxHp = Math.Max(1, maxHp);
                _maxGrit = Math.Max(1, maxGrit);
                _statuses.Clear();
                _status.text = "";
                SetHp(hp);
                SetGrit(grit);
            }

            public void SetHp(int hp) => _hp.Set((float)hp / _maxHp, $"HP {hp}/{_maxHp}");
            public void SetGrit(int grit) => _grit.Set((float)grit / _maxGrit, $"Задор {grit}");

            public void SetStatus(CombatStatus status, int value)
            {
                _statuses[status] = value;
                _status.text = string.Join("  ", _statuses.Where(p => p.Value > 0).Select(p => p.Key switch
                {
                    CombatStatus.Fur => $"Шерсть ×{p.Value}",
                    CombatStatus.Evade => "Начеку",
                    _ => $"Ослеплён {p.Value}",
                }));
            }

            public void KnockedOut()
            {
                _name.text += " — нокаут";
                _statuses.Clear();
                _status.text = "";
            }
        }
    }
}
