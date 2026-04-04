using DynamicDungeon.Core.Models;

namespace DynamicDungeon.Core.Algorithms
{
    public interface IMapAlgorithm
    {
        TileMap Generate(GenerationParameters parameters);
    }
}
