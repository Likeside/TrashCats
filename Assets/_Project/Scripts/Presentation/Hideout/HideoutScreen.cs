using System;
using System.Collections.Generic;
using TrashCats.App;
using TrashCats.Domain;
using TrashCats.Presentation.Bootstrap;
using TrashCats.Presentation.UI;
using UnityEngine;

namespace TrashCats.Presentation.Hideout
{
    /// <summary>
    /// Main hideout screen (Docs/GDD/UI_UX.md §4): HUD, scene, cat panel, bottom navigation and the overlays opened
    /// from them. Subscribes to domain events and refreshes the affected widgets.
    /// </summary>
    public sealed class HideoutScreen : IDisposable
    {
        private const float HudHeight = 110f;
        private const float NavHeight = 130f;
        private const float PanelHeight = 340f;

        private readonly GameSession _session;
        private readonly HudView _hud;
        private readonly HideoutSceneView _scene;
        private readonly CatPanelView _panel;
        private readonly ActivitySheet _sheet;
        private readonly OfflineSummaryModal _offline;
        private readonly ToastView _toast;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        public OverlayStack Overlays { get; } = new OverlayStack();

        public HideoutScreen(GameServices services, RectTransform root, bool debugTools, Action resetProgress)
        {
            _session = services.Session;
            var bus = services.Bus;

            var hudRect = Ui.Rect(root, "Hud").Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -HudHeight), Vector2.zero);
            var sceneRect = Ui.Rect(root, "Scene").Place(Vector2.zero, Vector2.one, new Vector2(0, NavHeight + PanelHeight), new Vector2(0, -HudHeight));
            var panelRect = Ui.Rect(root, "CatPanel").Place(Vector2.zero, new Vector2(1, 0), new Vector2(0, NavHeight), new Vector2(0, NavHeight + PanelHeight));
            var navRect = Ui.Rect(root, "Nav").Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, NavHeight));
            var overlayRect = Ui.Rect(root, "Overlays").Fill();

            _sheet = new ActivitySheet(_session, overlayRect, Overlays, ShowResult);
            _offline = new OfflineSummaryModal(overlayRect, Overlays);
            DebugPanel debugPanel = null;
            if (debugTools)
                debugPanel = new DebugPanel(overlayRect, Overlays, _session, services.Debug, resetProgress);

            _hud = new HudView(_session, hudRect, debugPanel != null ? debugPanel.Open : (Action)null);
            _scene = new HideoutSceneView(_session, bus, sceneRect, OpenSheet);
            _panel = new CatPanelView(_session, panelRect, OpenSheet, ShowResult);
            _toast = new ToastView(sceneRect);
            BuildNavigation(navRect);

            _subscriptions.Add(bus.Subscribe<DomainEvent>(_ => RefreshTexts()));
            _subscriptions.Add(bus.Subscribe<OfflineProgressApplied>(e => _offline.Show(e.Summary)));
        }

        public void Tick()
        {
            _scene.Tick();
            _panel.Tick();
        }

        public void Dispose()
        {
            foreach (var s in _subscriptions)
                s.Dispose();
            _scene.Dispose();
        }

        private void RefreshTexts()
        {
            _hud.Refresh();
            _panel.Refresh();
            if (_sheet.IsOpen)
                _sheet.Refresh();
        }

        private void OpenSheet(CatId catId) => _sheet.Open(catId);

        private void ShowResult(CommandResult result)
        {
            if (!result.IsOk)
                _toast.Show(Texts.Error(result.Error));
        }

        private static void BuildNavigation(RectTransform root)
        {
            Ui.Panel(root, "NavBack", Palette.PanelDark).rectTransform.Fill();
            var hideout = Ui.Button(root, "Hideout", "Убежище", Palette.Primary, null, 36);
            hideout.Rect.Place(Vector2.zero, new Vector2(0.5f, 1), new Vector2(20, 15), new Vector2(-10, -15));
            var map = Ui.Button(root, "Map", "Карта (скоро)", Palette.Disabled, null, 36);
            map.Rect.Place(new Vector2(0.5f, 0), Vector2.one, new Vector2(10, 15), new Vector2(-20, -15));
            map.Interactable = false;
        }
    }
}
