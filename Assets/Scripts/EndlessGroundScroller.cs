using UnityEngine;

/// <summary>
/// Tiles a sprite endlessly to the left with zero drift and no visible seam.
/// Positions are derived each frame from a single accumulated offset — no floating-point
/// build-up from repeated Translate() calls.
/// </summary>
public class EndlessGroundScroller : MonoBehaviour
{
    [Header("Tile Source")]
    [Tooltip("Assign the single background tile to repeat.")]
    [SerializeField] SpriteRenderer sourceTile;

    [Header("Scroll Settings")]
    [SerializeField] float scrollSpeed = 2f;

    [Header("Advanced")]
    [Tooltip("Extra tiles beyond screen fill. 1 is usually enough.")]
    [SerializeField] int extraTiles = 1;
    [Tooltip("Tiny world-unit overlap so sub-pixel rounding never leaves a gap.")]
    [SerializeField] float overlapBias = 0.01f;

    Transform[] _tiles;
    float _tileW;
    float _scrolledX;
    float _startX;
    float _baseY;
    float _baseZ;
    Camera _cam;

    void Start()
    {
        _cam = Camera.main;

        if (sourceTile == null)
        {
            Debug.LogError("[EndlessGroundScroller] No source tile assigned!");
            enabled = false;
            return;
        }

        _tileW = sourceTile.bounds.size.x;
        if (_tileW <= 0f)
        {
            Debug.LogError("[EndlessGroundScroller] Source tile has zero width — check its sprite.");
            enabled = false;
            return;
        }

        BuildTiles();
    }

    void BuildTiles()
    {
        float camHalfW = _cam ? _cam.orthographicSize * _cam.aspect : 10f;
        float camX     = _cam ? _cam.transform.position.x : 0f;

        // One extra tile each side so no gap is ever visible during camera drift
        int count = Mathf.CeilToInt(camHalfW * 2f / _tileW) + extraTiles + 2;

        _baseY  = sourceTile.transform.position.y;
        _baseZ  = sourceTile.transform.position.z;
        _startX = camX - camHalfW - _tileW;   // one tile off-screen left as buffer

        sourceTile.transform.SetParent(transform);
        _tiles    = new Transform[count];
        _tiles[0] = sourceTile.transform;

        for (int i = 1; i < count; i++)
        {
            var clone = Instantiate(sourceTile, transform);
            clone.name = sourceTile.name + "_tile" + i;
            _tiles[i] = clone.transform;
        }

        _scrolledX = 0f;
        PlaceTiles();
    }

    void Update()
    {
        _scrolledX += scrollSpeed * Time.deltaTime;
        PlaceTiles();
    }

    void PlaceTiles()
    {
        // Effective tile pitch (overlap closes sub-pixel cracks)
        float pitch = _tileW - overlapBias;

        // How far into the current tile cycle are we?
        float fraction = _scrolledX % pitch;
        // Which "generation" of tiles have fully passed? Rotates tile assignment.
        int   shift    = Mathf.FloorToInt(_scrolledX / pitch) % _tiles.Length;

        for (int i = 0; i < _tiles.Length; i++)
        {
            // Rotate tile GameObjects so the one scrolling off-screen left reappears right
            int tileIdx = (i + shift) % _tiles.Length;
            float x = _startX + i * pitch - fraction;
            _tiles[tileIdx].position = new Vector3(x, _baseY, _baseZ);
        }
    }
}
