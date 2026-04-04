using System.Collections.Generic;

namespace DynamicDungeon.Core.Models
{
    public class TileMap
    {
        public int Width { get; }
        public int Height { get; }
        public Tile[,] Tiles { get; }
        public (int x, int y) SpawnPoint { get; set; }
        public (int x, int y) ExitPoint { get; set; }
        public List<(int x, int y)> ShortestPath { get; set; } = new List<(int x, int y)>();

        public TileMap(int width, int height)
        {
            Width = width;
            Height = height;
            Tiles = new Tile[width, height];
        }

        public Tile Get(int x, int y) => Tiles[x, y];
        public void Set(int x, int y, Tile tile) => Tiles[x, y] = tile;

        public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;
    }
}
