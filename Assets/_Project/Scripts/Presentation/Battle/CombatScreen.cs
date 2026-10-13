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
        private readonly CanvasGroup _bannerGroup;
        private readonly TMP_Text[] _queues = new TMP_Text[2];
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

            _title = Ui.Label(root, "", 40, TextAnchor.MiddleCenter, Palette.Paper);
            _title.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -80), Vector2.zero);
            _title.Outlined();
            for (var side = 0; side < 2; side++)
            {
                _queues[side] = Ui.Label(root, "", 28, side == 0 ? TextAnchor.UpperLeft : TextAnchor.UpperRight, Palette.Paper);
                _queues[side].rectTransform.Place(new Vector2(side * 0.5f, 1), new Vector2(side * 0.5f + 0.5f, 1), new Vector2(30, -200), new Vector2(-30, -90));
                _queues[side].Outlined();
                _huds[side] = new FighterHud(root, side);
            }

            // skill banner: a tilted yellow comic ribbon (Art Bible §6), or outlined yellow text without the skin
            _bannerRoot = Ui.Rect(root, "Banner").Place(new Vector2(0.05f, 0.71f), new Vector2(0.95f, 0.81f));
            _bannerGroup = _bannerRoot.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;
            _bannerGroup.blocksRaycasts = false;
            var ribbon = Ui.Skin?.ribbon;
            if (ribbon != null)
                Ui.Picture(_bannerRoot, "Ribbon", ribbon).preserveAspect = false;
            _banner = Ui.Label(_bannerRoot, "", 60, TextAnchor.MiddleCenter, ribbon != null ? Palette.Ink : Palette.Primary, heading: true);
            if (ribbon != null)
                _banner.rectTransform.localRotation = Quaternion.Euler(0, 0, 4f);
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
                    }

                    break;
                }
                case StatusChanged status:
                    _huds[(int)status.Side].SetStatus(status.Status, status.Value);
                    break;
                case SuddenDeathStarted _:
                    if (animate) ShowBanner("Ярость!");
                    break;
                case FighterKnockedOut ko:
                {
                    var side = (int)ko.Side;
                    if (animate) _views[side]?.PlayKnockOut(side == 0, _speedFactor);
                    _huds[side].KnockedOut();
                    break;
                }
                case BattleEnded ended:
                    ShowBanner(ended.Outcome == BattleOutcome.Victory ? "Победа!" : "Поражение");
                    foreach (var view in _views)
                        view?.PlayIdle();
                    _endDelay = animate ? 1.2f : 0.01f;
                    break;
            }
        }

        private void RefreshQueues()
        {
            if (_battle == null) return;
            var log = _log.Take(_next).ToList();
            for (var side = 0; side < 2; side++)
            {
                var queue = side == 0 ? _battle.Setup.PlayerQueue : _battle.Setup.EnemyQueue;
                var knocked = new HashSet<string>(log.OfType<FighterKnockedOut>().Where(k => (int)k.Side == side).Select(k => k.FighterId));
                _queues[side].text = string.Join("\n", queue.Select(f =>
                    knocked.Contains(f.Id) ? $"<color=#888888>× {NameOf(f.Id, f.Side)}</color>" : NameOf(f.Id, f.Side)));
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

        private void FloatAt(Vector3 world, string text, Color color, int size)
        {
            var camera = Camera.main;
            if (camera == null) return;
            var label = Ui.Label(_uiRoot, text, size, TextAnchor.MiddleCenter, color);
            label.Outlined();
            var rt = label.rectTransform;
            var canvas = _uiRoot.GetComponentInParent<Canvas>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_uiRoot, camera.WorldToScreenPoint(world),
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : camera, out var local);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(300, 90);
            rt.anchoredPosition = local + _uiRoot.rect.center * 0f;
            DOTween.Sequence()
                .Append(rt.DOAnchorPosY(local.y + 110, 0.8f / _speedFactor))
                .Join(label.DOFade(0f, 0.8f / _speedFactor).SetEase(Ease.InQuad))
                .OnComplete(() => UnityEngine.Object.Destroy(label.gameObject))
                .SetLink(label.gameObject);
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
                _hp.Rect.Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -108), new Vector2(0, -56));
                _grit = Ui.Bar(box, "Grit", Palette.Hex("#3F7FBF"), "grit");
                _grit.Rect.Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -156), new Vector2(0, -112));
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
