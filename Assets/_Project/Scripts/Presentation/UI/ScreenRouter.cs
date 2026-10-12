using System;
using System.Collections.Generic;
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

        public ScreenRouter(RectTransform navRoot)
        {
            _nav = navRoot.gameObject;
            Ui.Panel(navRoot, "NavBack", Palette.PanelDark).rectTransform.Fill();
            _hideoutTab = Ui.Button(navRoot, "Tab_Hideout", "Убежище", Palette.Primary, () => Show(ScreenId.Hideout), 36);
            _hideoutTab.Rect.Place(Vector2.zero, new Vector2(0.5f, 1), new Vector2(20, 15), new Vector2(-10, -15));
            _mapTab = Ui.Button(navRoot, "Tab_Map", "Карта", Palette.Disabled, () => Show(ScreenId.Map), 36);
            _mapTab.Rect.Place(new Vector2(0.5f, 0), Vector2.one, new Vector2(10, 15), new Vector2(-20, -15));
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
            _hideoutTab.Image.color = id == ScreenId.Hideout ? Palette.Primary : Palette.Disabled;
            _mapTab.Image.color = id == ScreenId.Map ? Palette.Primary : Palette.Disabled;
            Current = id;
            screen.OnShow?.Invoke();
            Changed?.Invoke(id);
        }

        /// <summary>Marks the Map tab when something new is waiting there.</summary>
        public void SetMapBadge(bool on) => _mapTab.Text.text = on ? "Карта (!)" : "Карта";
    }
}
