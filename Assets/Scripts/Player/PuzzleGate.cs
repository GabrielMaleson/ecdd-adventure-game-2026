using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Tilemaps;

// "Solve the puzzle(s) and THIS part of the wall opens."
//
// One gate = one opening. It watches a list of Puzzle Ids (the same ids typed on the
// CrateTargets) and opens the moment ALL of them are solved. Nothing is hard-coded to
// the crypt: put a second gate in the scene, point it at a different puzzle id, and a
// different slab of wall opens — which is exactly the "more puzzles open more of the
// wall later" case.
//
// It can open the wall in either of two ways; use whichever matches how the wall is
// built (both may be filled in at once):
//
//   1. GameObject swap — Hide the closed wall piece, Show an open/doorway piece.
//      Same vocabulary as Pickup. Use this if the wall portion is its own object
//      (sprite, child tilemap, whatever) that can be switched off.
//
//   2. Tilemap slab — drag in the wall's Tilemap and an Opening Area box; every tile
//      inside that box is erased when the gate opens. Use this if the wall is painted
//      into one shared tilemap and can't be switched off as an object. No cell
//      coordinates are ever typed by hand: the box IS the region. Its TilemapCollider
//      updates itself, so the erased strip stops blocking too.
//
// Authoring the Opening Area: empty GameObject + BoxCollider2D with Is Trigger ON,
// dragged/resized over the part of the wall that should disappear. Triggers block
// nothing, and the gate reads its bounds once on Awake.
[DisallowMultipleComponent]
public class PuzzleGate : MonoBehaviour
{
    [Header("What has to be solved")]
    [Tooltip("Puzzle Ids (as typed on the CrateTargets) that must ALL be solved before this opens. One empty entry = the default/unnamed puzzle. Add ids here later to make this opening wait on more puzzles.")]
    [SerializeField] List<string> requiredPuzzleIds = new List<string> { "" };

    [Header("Open by swapping objects")]
    [Tooltip("Switched OFF when it opens — the closed wall piece.")]
    [SerializeField] GameObject[] hide;

    [Tooltip("Switched ON when it opens — an open doorway sprite, a passage trigger, stairs.")]
    [SerializeField] GameObject[] show;

    [Header("Or: erase a slab of a wall Tilemap")]
    [Tooltip("The tilemap the wall is painted into. Leave empty if the wall is a GameObject you're switching off above.")]
    [SerializeField] Tilemap wallTilemap;

    [Tooltip("Box marking WHICH tiles vanish. Every tile whose cell centre is inside these bounds is erased. Use a trigger BoxCollider2D on an empty object, sized over the opening.")]
    [SerializeField] Collider2D openingArea;

    [Header("Behaviour")]
    [Tooltip("On = once open it stays open, even if a crate is later pulled off the rug (or a push is undone with Z). Off = the wall closes again whenever the puzzle stops being solved — handy while testing.")]
    [SerializeField] bool stayOpenOnceOpened = true;

    [Tooltip("Fires when the wall opens — stone-grinding SFX, dust, camera pan to the new passage.")]
    public UnityEvent onOpened;

    [Tooltip("Fires if the wall closes again (only possible with Stay Open Once Opened off).")]
    public UnityEvent onClosed;

    public bool IsOpen { get; private set; }

    Bounds openingBounds;
    bool hasOpeningBounds;

    // Tiles this gate erased, so it can put them back if it ever closes again.
    readonly List<Vector3Int> erasedCells = new List<Vector3Int>();
    readonly List<TileBase>   erasedTiles = new List<TileBase>();

    void Awake()
    {
        // Read the area once, up front: a disabled or destroyed marker later would
        // report empty bounds and silently erase nothing.
        if (openingArea != null)
        {
            openingBounds = openingArea.bounds;
            hasOpeningBounds = true;
        }
    }

    void OnEnable() => CrateTarget.PuzzleStateChanged += OnPuzzleStateChanged;
    void OnDisable() => CrateTarget.PuzzleStateChanged -= OnPuzzleStateChanged;

    // Covers a puzzle that was already solved before this gate started listening (a
    // board authored solved, or a scene where CrateTarget evaluated first).
    void Start()
    {
        if (AllSolved()) Open();
    }

    void OnPuzzleStateChanged(string id, bool solved)
    {
        // Other puzzles in the scene are none of this gate's business.
        if (!requiredPuzzleIds.Contains(id ?? "")) return;

        if (AllSolved()) Open();
        else if (!stayOpenOnceOpened) Close();
    }

    bool AllSolved()
    {
        if (requiredPuzzleIds.Count == 0) return false;
        foreach (var id in requiredPuzzleIds)
            if (!CrateTarget.IsSolved(id ?? "")) return false;
        return true;
    }

    void Open()
    {
        if (IsOpen) return;
        IsOpen = true;

        foreach (var go in hide) if (go != null) go.SetActive(false);
        foreach (var go in show) if (go != null) go.SetActive(true);

        EraseSlab();
        onOpened?.Invoke();
    }

    void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;

        foreach (var go in hide) if (go != null) go.SetActive(true);
        foreach (var go in show) if (go != null) go.SetActive(false);

        RestoreSlab();
        onClosed?.Invoke();
    }

    // Walks the cell rectangle covering the marker box and clears whatever is painted
    // there, remembering each tile so Close can paint it back.
    void EraseSlab()
    {
        if (wallTilemap == null) return;
        if (!hasOpeningBounds)
        {
            Debug.LogWarning($"{name}: a Wall Tilemap is set but no Opening Area box — nothing to erase. Drag in a trigger BoxCollider2D covering the part of the wall that should open.", this);
            return;
        }

        Vector3Int min = wallTilemap.WorldToCell(openingBounds.min);
        Vector3Int max = wallTilemap.WorldToCell(openingBounds.max);

        for (int x = min.x; x <= max.x; x++)
        for (int y = min.y; y <= max.y; y++)
        {
            var cell = new Vector3Int(x, y, min.z);

            // Cell centre, not corner: a box grazing a tile's edge shouldn't take it.
            if (!openingBounds.Contains(Plane(wallTilemap.GetCellCenterWorld(cell)))) continue;

            var tile = wallTilemap.GetTile(cell);
            if (tile == null) continue;

            erasedCells.Add(cell);
            erasedTiles.Add(tile);
            wallTilemap.SetTile(cell, null);
        }
    }

    void RestoreSlab()
    {
        if (wallTilemap == null) return;
        for (int i = 0; i < erasedCells.Count; i++)
            wallTilemap.SetTile(erasedCells[i], erasedTiles[i]);
        erasedCells.Clear();
        erasedTiles.Clear();
    }

    // A 2D box marker is paper-thin in Z; flattening the point onto it keeps Contains
    // from rejecting every tile over a depth difference nobody authored.
    Vector3 Plane(Vector3 p) => new Vector3(p.x, p.y, openingBounds.center.z);
}
