using System;
using TrashCats.App;
using TrashCats.Domain;
using TrashCats.Presentation.Bootstrap;
using TrashCats.Presentation.UI;
using UnityEngine;

namespace TrashCats.Presentation.Hideout
{
    /// <summary>
    /// Main hideout screen (Docs/GDD/UI_UX.md §4): HUD, scene and cat panel. Its sheets and modals live in the shared
    /// overlay layer so they cover the bottom navigation too.
    /// </summary>
    public sealed class HideoutScreen : IDisposable
    {
        public const float HudHeight = 110f;
        public const float NavHeight = 160f;
        private const float PanelHeight = 430f + CatPanelView.ExtraHeight;

        private readonly HudView _hud;
        private readonly HideoutSceneView _scene;
        private readonly CatPanelView _panel;
        private readonly ActivitySheet _sheet;
        private readonly ToastView _toast;

        public GameObject Root { get; }

        public HideoutScreen(GameServices services, RectTransform parent, RectTransform overlayRoot, OverlayStack overlays,
            CatDetailsSheet details, UpgradePanel upgrades, DebugPanel debugPanel, Action<CommandResult> showResult,
            Characters.CharacterViewFactory art, Action onSettings = null)
        {
            var session = services.Session;
            var root = Ui.Rect(parent, "HideoutScreen").Fill();
            Root = root.gameObject;

            // the scene runs up under the floating top bar (concept)
            var sceneRect = Ui.Rect(root, "Scene").Place(Vector2.zero, Vector2.one, new Vector2(0, NavHeight + PanelHeight), Vector2.zero);
            var hudRect = Ui.Rect(root, "Hud").Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -HudHeight), Vector2.zero);
            var panelRect = Ui.Rect(root, "CatPanel").Place(Vector2.zero, new Vector2(1, 0), new Vector2(0, NavHeight), new Vector2(0, NavHeight + PanelHeight));

            _sheet = new ActivitySheet(session, overlayRoot, overlays, showResult, details.Open, art);
            _hud = new HudView(session, hudRect, debugPanel != null ? debugPanel.Open : (Action)null, onSettings);
            _scene = new HideoutSceneView(session, services.Bus, sceneRect, _sheet.Open, upgrades.Open, art);
            _scene.Loot += _hud.Collect;
            _panel = new CatPanelView(session, panelRect, _sheet.Open, showResult, art);
            _toast = new ToastView(sceneRect);
        }

        public void Tick()
        {
            _scene.Tick();
            _panel.Tick();
            if (_sheet.IsOpen)
                _sheet.Tick();
        }

        public void RefreshTexts()
        {
            _hud.Refresh();
            _panel.Refresh();
            _scene.RefreshUpgrades();
            if (_sheet.IsOpen)
                _sheet.Refresh();
        }

        public void RebuildCats() => _scene.SyncCats();

        public void Toast(string message, string icon = null) => _toast.Show(message, icon);

        public void Dispose() => _scene.Dispose();
    }
}
