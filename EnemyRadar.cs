using System.Collections.Generic;
using UnityEngine;

namespace Sense;

/// <summary>
/// Client-side enemy radar. Keeps a red map marker on every spawned enemy within range of the
/// local player, adding and removing markers as enemies move in and out of range and tracking
/// each one as it moves.
///
/// Purely local: <c>Map.AddCustom</c> instantiates a marker under the local <see cref="Map"/>,
/// sends no RPC and changes no game state, so only this player sees the blips — nothing is pushed
/// to the host or other clients.
///
/// The game itself scaffolded enemy map markers — <c>Map.AddEnemy</c> and
/// <c>Map.EnemyPositionSet</c> exist but are empty — so we implement the same idea on the client
/// through the fully-working custom-marker path. <c>MapCustomEntity.Logic()</c> then tracks each
/// marker to its enemy at 10Hz and fades it on other floors, exactly like the game's own markers.
/// </summary>
internal static class EnemyRadar
{
    // How often we re-scan which enemies are in range. The markers track their own positions
    // (MapCustomEntity updates at 10Hz), so membership only needs checking a few times a second.
    private const float ScanInterval = 0.2f;
    private static float _nextScan;

    private struct Marker
    {
        public GameObject Src;          // tiny object riding the enemy; the blip tracks its position
        public MapCustomEntity Entity;  // the map blip itself
    }

    // Enemy -> the marker we placed for it.
    private static readonly Dictionary<EnemyParent, Marker> _markers = new();
    private static readonly List<EnemyParent> _remove = new();

    private static Sprite? _blip;

    /// <summary>
    /// Per-frame entry point (called from the plugin's Update). Cheap: most frames it just checks
    /// the throttle and returns. When disabled or out of a level it tears down any live markers.
    /// </summary>
    internal static void Tick()
    {
        if (!Sense.Enabled.Value || Map.Instance == null || !SemiFunc.RunIsLevel())
        {
            if (_markers.Count > 0)
                ClearAll();
            return;
        }

        if (Time.time < _nextScan)
            return;
        _nextScan = Time.time + ScanInterval;

        var player = PlayerController.instance;
        var director = EnemyDirector.instance;
        if (player == null || director == null)
            return;

        Vector3 origin = player.transform.position;
        float radius = Sense.Radius.Value;
        float sqrRadius = radius * radius;
        Color color = MarkerColor();

        // Add a marker for any in-range enemy that doesn't already have a live one.
        foreach (var enemy in director.enemiesSpawned)
        {
            if (!IsTrackable(enemy, origin, sqrRadius))
                continue;

            if (_markers.TryGetValue(enemy, out var existing))
            {
                if (existing.Entity != null && existing.Src != null)
                    continue;       // healthy marker already present
                DestroyMarker(enemy); // self-heal a marker that died under us, then recreate
            }

            if (TryCreateMarker(enemy, color, out var marker))
                _markers[enemy] = marker;
        }

        // Remove markers whose enemy has died, despawned or left range.
        _remove.Clear();
        foreach (var kv in _markers)
            if (!IsTrackable(kv.Key, origin, sqrRadius))
                _remove.Add(kv.Key!); // Unity-null keys are never true-null references

        foreach (var enemy in _remove)
            DestroyMarker(enemy);
    }

    private static bool IsTrackable(EnemyParent? enemy, Vector3 origin, float sqrRadius)
    {
        if (enemy == null || !enemy.Spawned)
            return false;

        var anchor = Anchor(enemy);
        if (anchor == null)
            return false;

        return (anchor.position - origin).sqrMagnitude <= sqrRadius;
    }

    // The centre of the enemy's body when available, else its root transform.
    private static Transform? Anchor(EnemyParent enemy)
    {
        var e = enemy.Enemy;
        if (e == null)
            return null;
        return e.CenterTransform != null ? e.CenterTransform : e.transform;
    }

    private static bool TryCreateMarker(EnemyParent enemy, Color color, out Marker marker)
    {
        marker = default;

        var anchor = Anchor(enemy);
        if (anchor == null)
            return false;

        // A tiny object parented to the enemy; the map marker tracks ITS transform, so it follows
        // the enemy automatically and is destroyed along with it.
        var src = new GameObject("SenseEnemyMarker");
        src.transform.SetParent(anchor, worldPositionStays: false);
        src.transform.localPosition = Vector3.zero;

        var custom = src.AddComponent<MapCustom>();
        custom.autoAdd = false; // we add it ourselves so we can pass our own colour
        custom.color = color;
        custom.sprite = Blip();

        // AddCustom wires up a MapCustomEntity that tracks src and self-destroys once src is gone.
        // It is a no-op outside real levels, but Tick already gates on RunIsLevel().
        Map.Instance.AddCustom(custom, custom.sprite, color);

        if (custom.mapCustomEntity == null)
        {
            Object.Destroy(src);
            return false;
        }

        marker = new Marker { Src = src, Entity = custom.mapCustomEntity };
        return true;
    }

    private static void DestroyMarker(EnemyParent enemy)
    {
        if (_markers.TryGetValue(enemy, out var marker))
        {
            if (marker.Entity != null)
                Object.Destroy(marker.Entity.gameObject);
            if (marker.Src != null)
                Object.Destroy(marker.Src);
        }
        _markers.Remove(enemy);
    }

    /// <summary>Tear down every marker we own (left the level, disabled, or unloading).</summary>
    internal static void ClearAll()
    {
        foreach (var kv in _markers)
        {
            if (kv.Value.Entity != null)
                Object.Destroy(kv.Value.Entity.gameObject);
            if (kv.Value.Src != null)
                Object.Destroy(kv.Value.Src);
        }
        _markers.Clear();
    }

    // The marker sprite: the game's own valuable blip art, tinted by the marker colour, so an
    // enemy reads as "a loot blip, but red". Falls back to a generated dot if the art is missing.
    private static Sprite Blip()
    {
        if (_blip != null)
            return _blip;

        var prefab = Map.Instance != null ? Map.Instance.ValuableObject : null;
        var mv = prefab != null ? prefab.GetComponent<MapValuable>() : null;
        _blip = (mv != null && mv.spriteSmall != null) ? mv.spriteSmall : MakeDot();
        return _blip;
    }

    private static Color MarkerColor()
    {
        var raw = Sense.MarkerColor.Value;
        if (!string.IsNullOrWhiteSpace(raw) &&
            ColorUtility.TryParseHtmlString("#" + raw.TrimStart('#'), out var c))
        {
            c.a = 1f;
            return c;
        }
        return Color.red;
    }

    // A soft white dot (tinted by the marker colour). Only used if the game's valuable sprite
    // can't be found for some reason.
    private static Sprite MakeDot()
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float r = size / 2f;
        var centre = new Vector2(r - 0.5f, r - 0.5f);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), centre);
                float a = Mathf.Clamp01(r - 1f - d);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
