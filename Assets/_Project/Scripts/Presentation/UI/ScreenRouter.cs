using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TrashCats.Presentation.UI
{
    public enum ScreenId
    {
        Hideout,
        Map,
        BattlePrep,
        Combat,
    }

    /// <summary>
    /// One active screen at a time plus the shared bottom navigation (Hideout / Map), shown only on main screens
    /// (Docs/GDD/UI_UX.md §3).
    /// </summary>
    public sealed class ScreenRouter
    {
        private readonly Dictionary<ScreenId, (GameObject Root, bool WithNav, Action OnShow)> _screens = new Dictionary<ScreenId, (GameObject, bool, Action)>();
        private readonly GameObject _nav;
        private readonly UiButton _hideoutTab;
        private readonly UiButton _mapTab;

        public ScreenId Current { get; private set; }
        public event Action<ScreenId> Changed;

        /// <summary>Inside of the tin panel's rim: left, bottom, right, top.</summary>
        private static readonly Vector4 Rim = new Vector4(23, 27, 23, 18);

        public ScreenRouter(RectTransform navRoot)
        {
            _nav = navRoot.gameObject;
            Ui.Card(navRoot, "NavBack", CardStyle.Tin).rectTransform.Fill();
            _hideoutTab = Ui.Button(navRoot, "Tab_Hideout", Loc.T("screen_router.01"), Palette.Primary, () => Show(ScreenId.Hideout), 36);
            // the tabs fill the tin panel's dark inside, up to its rim (hud_a: the rim is 44 px at the sides, 35 px on top,
            // 53 px at the bottom, drawn at half size)
            _hideoutTab.Rect.Place(Vector2.zero, new Vector2(0.5f, 1), new Vector2(Rim.x, Rim.y), new Vector2(-6, -Rim.w));
            _mapTab = Ui.Button(navRoot, "Tab_Map", Loc.T("screen_router.02"), Palette.Disabled, () => Show(ScreenId.Map), 36);
            _mapTab.Rect.Place(new Vector2(0.5f, 0), Vector2.one, new Vector2(6, Rim.y), new Vector2(-Rim.z, -Rim.w));
            AddIcon(_hideoutTab, "home");
            AddIcon(_mapTab, "map");
        }

        public void Register(ScreenId id, GameObject root, bool withNav, Action onShow = null)
        {
            _screens[id] = (root, withNav, onShow);
            root.SetActive(false);
        }

        public void Show(ScreenId id)
        {
            foreach (var pair in _screens)
                pair.Value.Root.SetActive(pair.Key == id);
            var screen = _screens[id];
            _nav.SetActive(screen.WithNav);
            _hideoutTab.SetColor(id == ScreenId.Hideout ? Palette.Primary : Palette.Disabled);
            _mapTab.SetColor(id == ScreenId.Map ? Palette.Primary : Palette.Disabled);
            Current = id;
            screen.OnShow?.Invoke();
            Changed?.Invoke(id);
        }

        /// <summary>Icon on the left of a tab, as on the concept's big home and map buttons.</summary>
        private static void AddIcon(UiButton tab, string key)
        {
            var icon = Ui.Icon(tab.Rect, key);
            if (icon == null)
                return;
            icon.rectTransform.Place(new Vector2(0.06f, 0.15f), new Vector2(0.3f, 0.85f));
            tab.Text.rectTransform.anchorMin = new Vector2(0.3f, tab.Text.rectTransform.anchorMin.y);
        }

        /// <summary>Marks the Map tab when something new is waiting there.</summary>
        public void SetMapBadge(bool on) => _mapTab.Text.text = on ? Loc.T("screen_router.03") : Loc.T("screen_router.02");
    }
}
